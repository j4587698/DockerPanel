/**
 * SignalR 服务
 * 替代原生WebSocket实现，提供更稳定的实时通信
 */

import * as signalR from '@microsoft/signalr'
import api, { safeRefreshToken } from '@/api'

export interface SignalRMessage {
  type: string
  data: any
  timestamp: string
  id?: string
}

export interface ContainerUpdate {
  id: string
  name: string
  status: string
  state: string
  action: 'started' | 'stopped' | 'paused' | 'restarted' | 'removed' | 'created'
}

export interface SystemStatusUpdate {
  cpu: number
  memory: number
  disk: number
  network: {
    inbound: number
    outbound: number
  }
}

export interface LogEntry {
  container: string
  level: 'info' | 'warning' | 'error' | 'debug'
  message: string
  timestamp: string
}

export interface NotificationMessage {
  title: string
  message: string
  type: 'info' | 'warning' | 'error' | 'success'
  persistent?: boolean
  actions?: Array<{
    label: string
    action: string
  }>
}

class SignalRService {
  private connection: signalR.HubConnection | null = null
  private url: string = ''
  private reconnectAttempts = 0
  private maxReconnectAttempts = 10
  private isManualClose = false
  private messageQueue: SignalRMessage[] = []
  private connectPromise: Promise<void> | null = null

  // 手动重连相关
  private manualReconnectTimer: ReturnType<typeof setTimeout> | null = null
  private isReconnecting = false
  private heartbeatTimer: ReturnType<typeof setInterval> | null = null
  private readonly heartbeatInterval = 30000 // 30秒心跳

  // 活跃订阅跟踪（用于重连后恢复订阅）
  private activeSubscriptions: Set<string> = new Set()

  // 当前语言设置（用于重连后恢复）
  private currentLanguage: string = ''

  // 事件监听器
  private listeners: Map<string, ((message: SignalRMessage) => void)[]> = new Map()

  constructor() {    this.setupEventListeners()
  }

  private debugLog(...args: unknown[]): void {
    if (import.meta.env.DEV) {
      console.debug(...args)
    }
  }

  /**
   * 连接前确保登录态有效：access token(Cookie)过期时先用 refresh token 续期，
   * 否则 [Authorize] 的 Hub 在重连时会因 jwt_token 失效而拒绝连接。
   * 仅在 refresh token 也失效时才放弃，避免无谓的失败重试。
   */
  private async ensureRefreshed(): Promise<void> {
    try {
      await safeRefreshToken()
    } catch {
      // refresh 失败（如 refresh token 过期）时忽略，连接会因未授权而失败，
      // 由全局拦截器决定是否跳登录。safeRefreshToken 已保证单飞，无需额外去重。
    }
  }

  /**
   * 连接SignalR Hub
   */
  async connect(url?: string): Promise<void> {
    if (this.isConnected()) return
    
    if (this.connectPromise) {
      return this.connectPromise
    }

    this.connectPromise = this._doConnect(url)
    try {
      await this.connectPromise
    } finally {
      this.connectPromise = null
    }
  }

  private async _doConnect(url?: string): Promise<void> {
    try {
      this.isManualClose = false
      this.url = url || this.getDefaultUrl()

      // 重连前先确认登录态有效（续期 jwt_token Cookie）
      await this.ensureRefreshed()

      // 清理之前的连接
      if (this.connection) {
        try {
          await this.connection.stop()
        } catch (e) {
          // 忽略停止错误
        }
      }

      // 创建SignalR连接：依赖同域 HttpOnly Cookie(jwt_token) 鉴权，
      // 与 REST API 一致；不再从 localStorage 读取（已迁移到 Cookie）。
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(this.url)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: retryContext => {
            // 指数退避: 0s, 2s, 4s, 8s, 16s, 30s, 30s...
            if (retryContext.previousRetryCount === 0) {
              return 0
            }
            const delay = Math.min(30, Math.pow(2, retryContext.previousRetryCount - 1)) * 1000
            return delay
          }
        })
        .configureLogging(signalR.LogLevel.Warning)
        .build()

      // 设置连接事件
      this.connection.onreconnecting(error => {
        this.debugLog('SignalR正在重连:', error)
        this.isReconnecting = true
        this.emit('reconnecting', { 
          type: 'reconnecting', 
          data: { error, attempt: this.reconnectAttempts + 1 }, 
          timestamp: new Date().toISOString() 
        })
      })

      this.connection.onreconnected(connectionId => {
        this.debugLog('SignalR已重连:', connectionId)
        this.reconnectAttempts = 0
        this.isReconnecting = false
        this.startHeartbeat()
        this.emit('reconnected', {
          type: 'reconnected',
          data: { connectionId },
          timestamp: new Date().toISOString()
        })
        this.flushMessageQueue()
        // 重连后恢复订阅和语言设置
        this.restoreSubscriptions()
        this.restoreLanguage()
      })

      this.connection.onclose(error => {
        this.debugLog('SignalR连接已关闭:', error)
        this.stopHeartbeat()
        this.emit('disconnected', { 
          type: 'disconnected', 
          data: error, 
          timestamp: new Date().toISOString() 
        })
        
        // 如果不是手动关闭，启动手动重连
        if (!this.isManualClose) {
          this.startManualReconnect()
        }
      })

      // 设置Hub方法监听
      this.setupHubListeners()

      // 启动连接
      await this.connection.start()
      this.debugLog('SignalR连接已建立')
      
      // 重置重连计数
      this.reconnectAttempts = 0
      this.isReconnecting = false

      // 启动心跳
      this.startHeartbeat()

      // 发送连接状态
      this.emit('connected', { 
        type: 'connected', 
        data: { connected: true, connectionId: this.connection.connectionId }, 
        timestamp: new Date().toISOString() 
      })

    } catch (error) {
      console.error('SignalR连接失败:', error)
      
      // 如果不是手动关闭，尝试重连
      if (!this.isManualClose) {
        this.startManualReconnect()
      }
      
      throw new Error('SignalR连接失败')
    }
  }
  
  /**
   * 启动手心跳检测
   */
  private startHeartbeat(): void {
    this.stopHeartbeat()
    
    this.heartbeatTimer = setInterval(async () => {
      if (this.isConnected()) {
        try {
          await this.ping()
        } catch (error) {
          console.warn('心跳检测失败:', error)
        }
      }
    }, this.heartbeatInterval)
  }
  
  /**
   * 停止心跳检测
   */
  private stopHeartbeat(): void {
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer)
      this.heartbeatTimer = null
    }
  }
  
  /**
   * 启动手动重连
   */
  private startManualReconnect(): void {
    if (this.isManualClose || this.isReconnecting) {
      return
    }
    
    this.clearManualReconnectTimer()
    
    const attemptReconnect = async () => {
      if (this.isManualClose) {
        return
      }
      
      this.reconnectAttempts++
      
      if (this.reconnectAttempts > this.maxReconnectAttempts) {
        this.debugLog('已达到最大重连次数，停止重连')
        this.emit('reconnect-failed', {
          type: 'reconnect-failed',
          data: { attempts: this.reconnectAttempts },
          timestamp: new Date().toISOString()
        })
        return
      }
      
      // 指数退避延迟
      const delay = Math.min(30, Math.pow(2, this.reconnectAttempts - 1)) * 1000
      this.debugLog(`将在${delay}ms后尝试手动重连 (第${this.reconnectAttempts}次)`)
      
      this.manualReconnectTimer = setTimeout(async () => {
        if (this.isManualClose || this.isConnected()) {
          return
        }
        
        try {
          this.debugLog('开始手动重连...')
          await this.connect(this.url)
          this.debugLog('手动重连成功')
        } catch (error) {
          console.error('手动重连失败:', error)
          // 继续尝试
          attemptReconnect()
        }
      }, delay)
    }
    
    attemptReconnect()
  }
  
  /**
   * 清除手动重连定时器
   */
  private clearManualReconnectTimer(): void {
    if (this.manualReconnectTimer) {
      clearTimeout(this.manualReconnectTimer)
      this.manualReconnectTimer = null
    }
  }

  /**
   * 断开连接
   */
  disconnect(): void {
    this.isManualClose = true
    this.stopHeartbeat()
    this.clearManualReconnectTimer()
    this.reconnectAttempts = 0
    this.isReconnecting = false
    this.activeSubscriptions.clear() // 清理订阅跟踪

    if (this.connection) {
      this.connection.stop()
      this.connection = null
    }
  }

  /**
   * 发送消息到Hub方法
   */
  async invoke(methodName: string, ...args: any[]): Promise<any> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      console.warn('SignalR未连接，消息已加入队列')
      this.messageQueue.push({
        type: 'invoke',
        data: { methodName, args },
        timestamp: new Date().toISOString()
      } as SignalRMessage)
      return null
    }

    try {
      const result = await this.connection.invoke(methodName, ...args)
      return result
    } catch (error) {
      console.error('SignalR调用失败:', error)
      throw error
    }
  }

  /**
   * 订阅消息类型
   */
  subscribe(type: string, callback: (message: SignalRMessage) => void): () => void {
    if (!this.listeners.has(type)) {
      this.listeners.set(type, [])
    }

    this.listeners.get(type)!.push(callback)

    // 返回取消订阅函数
    return () => {
      const callbacks = this.listeners.get(type)
      if (callbacks) {
        const index = callbacks.indexOf(callback)
        if (index > -1) {
          callbacks.splice(index, 1)
        }
      }
    }
  }

  /**
   * 获取连接状态
   */
  getReadyState(): signalR.HubConnectionState {
    return this.connection?.state ?? signalR.HubConnectionState.Disconnected
  }

  /**
   * 是否已连接
   */
  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected
  }

  /**
   * 获取连接ID
   */
  getConnectionId(): string | undefined {
    return this.connection?.connectionId
  }

  /**
   * 设置Hub方法监听
   */
  private setupHubListeners(): void {
    if (!this.connection) return

    // 监听Welcome消息
    this.connection.on('Welcome', (data) => {
      this.emit('welcome', { type: 'welcome', data, timestamp: new Date().toISOString() })
    })

    // 监听容器更新
    this.connection.on('ContainersUpdated', (data) => {
      this.emit('container', { type: 'container', data, timestamp: new Date().toISOString() })
    })

    // 监听系统状态更新
    this.connection.on('SystemStatsUpdated', (data) => {
      this.emit('system', { type: 'system', data, timestamp: new Date().toISOString() })
    })

    // 监听Docker统计信息更新
    this.connection.on('DockerStatsUpdated', (data) => {
      this.emit('docker-stats', { type: 'docker-stats', data, timestamp: new Date().toISOString() })
    })

    // 监听容器统计信息更新
    this.connection.on('ContainerStatsUpdated', (data) => {
      this.emit('container-stats', { type: 'container-stats', data, timestamp: new Date().toISOString() })
    })

    // 监听日志更新
    this.connection.on('LogUpdated', (data) => {
      this.emit('logs', { type: 'logs', data, timestamp: new Date().toISOString() })
    })

    // 监听实时日志广播（用于实时日志页面）
    this.connection.on('logs', (data) => {
      this.emit('logs', { type: 'logs', data, timestamp: new Date().toISOString() })
    })

    // 监听通知
    this.connection.on('Notification', (data) => {
      this.emit('notification', { type: 'notification', data, timestamp: new Date().toISOString() })
    })

    // 监听心跳响应
    this.connection.on('Pong', (data) => {
      this.emit('pong', { type: 'pong', data, timestamp: new Date().toISOString() })
    })

    // 监听连接状态
    this.connection.on('ConnectionStatus', (data) => {
      this.emit('status', { type: 'status', data, timestamp: new Date().toISOString() })
    })

    // 监听日志订阅确认
    this.connection.on('LogsSubscribed', (data) => {
      this.emit('logs-subscribed', { type: 'logs-subscribed', data, timestamp: new Date().toISOString() })
    })

    // 监听 Compose 部署进度
    this.connection.on('ComposeDeployProgress', (data) => {
      this.emit('compose-deploy-progress', { type: 'compose-deploy-progress', data, timestamp: new Date().toISOString() })
    })

    // 监听 Compose 操作进度（启动/停止等）
    this.connection.on('ComposeOperationProgress', (data) => {
      this.emit('compose-operation-progress', { type: 'compose-operation-progress', data, timestamp: new Date().toISOString() })
    })

    // 监听卷打包进度
    this.connection.on('VolumeArchiveProgress', (data) => {
      this.emit('volume-archive-progress', { type: 'volume-archive-progress', data, timestamp: new Date().toISOString() })
    })

    // 监听镜像构建进度
    this.connection.on('ImageBuildProgress', (data) => {
      this.emit('image-build-progress', { type: 'image-build-progress', data, timestamp: new Date().toISOString() })
    })

    // 监听镜像拉取进度
    this.connection.on('ImagePullProgress', (data) => {
      this.emit('image-pull-progress', { type: 'image-pull-progress', data, timestamp: new Date().toISOString() })
    })

    // 监听镜像推送进度
    this.connection.on('ImagePushProgress', (data) => {
      this.emit('image-push-progress', { type: 'image-push-progress', data, timestamp: new Date().toISOString() })
    })

    // 监听证书进度更新
    this.connection.on('CertificateProgressUpdate', (data) => {
      this.emit('certificate-progress', { type: 'certificate-progress', data, timestamp: new Date().toISOString() })
    })

    // 监听错误消息
    this.connection.on('Error', (data) => {
      this.emit('error', { type: 'error', data, timestamp: new Date().toISOString() })
    })
  }

  /**
   * 触发事件
   */
  private emit(type: string, message: SignalRMessage): void {
    const callbacks = this.listeners.get(type)
    if (callbacks) {
      callbacks.forEach(callback => {
        try {
          callback(message)
        } catch (error) {
          console.error('执行SignalR回调失败:', error)
        }
      })
    }
  }

  /**
   * 刷新消息队列
   */
  private flushMessageQueue(): void {
    while (this.messageQueue.length > 0 && this.isConnected()) {
      const message = this.messageQueue.shift()
      if (message && message.type === 'invoke') {
        const { methodName, args } = message.data
        this.invoke(methodName, ...args).catch(error => {
          console.error('执行队列中的调用失败:', error)
        })
      }
    }
  }

  /**
   * 重连后恢复订阅
   */
  private restoreSubscriptions(): void {
    if (this.activeSubscriptions.size === 0) return

    this.debugLog('恢复订阅:', Array.from(this.activeSubscriptions))

    this.activeSubscriptions.forEach(sub => {
      this.invoke(sub).catch(error => {
        console.error(`恢复订阅 ${sub} 失败:`, error)
      })
    })
  }

  /**
   * 重连后恢复语言设置
   */
  private restoreLanguage(): void {
    if (this.currentLanguage) {
      this.setLanguage(this.currentLanguage).catch(error => {
        console.error('恢复语言设置失败:', error)
      })
    }
  }

  /**
   * 获取默认SignalR URL
   */
  private getDefaultUrl(): string {
    return '/dockerpanelHub'
  }

  /**
   * 设置浏览器事件监听器
   */
  private setupEventListeners(): void {
    // 页面可见性变化时处理连接
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) {
        // 页面隐藏时不需要特殊处理，SignalR会自动管理
      } else {
        // 页面显示时确保连接正常
        if (!this.isManualClose && !this.isConnected()) {
          this.debugLog('页面恢复可见，检查SignalR连接...')
          this.connect().catch(error => {
            console.error('恢复SignalR连接失败:', error)
          })
        } else if (this.isConnected()) {
          // 连接正常，发送心跳确认
          this.ping().catch(() => {})
        }
      }
    })

    // 网络状态变化时处理连接
    window.addEventListener('online', () => {
      this.debugLog('网络已恢复，尝试重新连接SignalR...')
      if (!this.isManualClose && !this.isConnected()) {
        this.connect().catch(error => {
          console.error('网络恢复后重连SignalR失败:', error)
        })
      }
    })

    // 页面卸载时关闭连接
    window.addEventListener('beforeunload', () => {
      this.disconnect()
    })
  }

  // Hub方法的便捷调用
  async subscribeToContainers() {
    this.activeSubscriptions.add('SubscribeToContainers')
    return await this.invoke('SubscribeToContainers')
  }

  async subscribeToSystemStats() {
    this.activeSubscriptions.add('SubscribeToSystemStats')
    return await this.invoke('SubscribeToSystemStats')
  }

  async unsubscribeFromSystemStats() {
    this.activeSubscriptions.delete('SubscribeToSystemStats')
    return await this.invoke('UnsubscribeFromSystemStats')
  }

  async subscribeToContainerStats() {
    this.activeSubscriptions.add('SubscribeToContainerStats')
    return await this.invoke('SubscribeToContainerStats')
  }

  async unsubscribeFromContainerStats() {
    this.activeSubscriptions.delete('SubscribeToContainerStats')
    return await this.invoke('UnsubscribeFromContainerStats')
  }

  async subscribeToStats() {
    // 后端使用 SubscribeToSystemStats 来标记连接活跃
    return await this.invoke('SubscribeToSystemStats');
  }

  async subscribeToLogs(containerId: string, tailLines: number = 100) {
    return await this.invoke('SubscribeToLogs', containerId, tailLines)
  }

  async unsubscribeFromLogs(containerId: string) {
    return await this.invoke('UnsubscribeFromLogs', containerId)
  }

  async ping() {
    return await this.invoke('Ping')
  }

  async setLanguage(language: string) {
    this.currentLanguage = language
    return await this.invoke('SetLanguage', language)
  }

  async getConnectionStatus() {
    return await this.invoke('GetConnectionStatus')
  }
}

// 导出单例实例
export const signalrService = new SignalRService()

// 导出类型和服务类
export { SignalRService as default }