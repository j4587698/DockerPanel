/**
 * WebSocket Store (基于SignalR)
 * 管理SignalR连接状态和实时消息
 */

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { ElMessage, ElNotification } from 'element-plus'
import {
  signalrService,
  type SignalRMessage,
} from '@/services/signalr'
import type { ContainerUpdate } from '@/services/signalr'
import type { SystemStatusUpdate } from '@/services/signalr'
import type { LogEntry } from '@/services/signalr'
import type { NotificationMessage } from '@/services/signalr'
import { useTasksStore } from '@/stores/tasks'

const debugLog = (...args: unknown[]) => {
  if (import.meta.env.DEV) {
    console.debug(...args)
  }
}

export const useWebSocketStore = defineStore('websocket', () => {
  // 状态
  const isConnected = ref(false)
  const connectionStatus = ref<'disconnected' | 'connecting' | 'connected' | 'error'>('disconnected')
  const lastMessage = ref<SignalRMessage | null>(null)
  const messages = ref<SignalRMessage[]>([])
  const unreadCount = ref(0)
  const connectionError = ref<string | null>(null)

  // 容器实时更新
  const containerUpdates = ref<ContainerUpdate[]>([])
  const systemStats = ref<SystemStatusUpdate | null>(null)
  const recentLogs = ref<LogEntry[]>([])
  const notifications = ref<NotificationMessage[]>([])

  // 计算属性
  const hasUnreadMessages = computed(() => unreadCount.value > 0)
  const connectionStatusText = computed(() => {
    switch (connectionStatus.value) {
      case 'disconnected': return '未连接'
      case 'connecting': return '连接中...'
      case 'connected': return '已连接'
      case 'error': return '连接错误'
      default: return '未知状态'
    }
  })

  const connectionStatusType = computed(() => {
    switch (connectionStatus.value) {
      case 'disconnected': return 'info'
      case 'connecting': return 'warning'
      case 'connected': return 'success'
      case 'error': return 'danger'
      default: return 'info'
    }
  })

  // 初始化SignalR连接
  const initWebSocket = async (url?: string) => {
    try {
      connectionStatus.value = 'connecting'
      connectionError.value = null

      await signalrService.connect(url)

      // 订阅SignalR消息
      subscribeToSignalrMessages()

      isConnected.value = true
      connectionStatus.value = 'connected'

      ElMessage.success('SignalR连接已建立')

    } catch (error) {
      console.error('SignalR连接失败:', error)
      connectionStatus.value = 'error'
      connectionError.value = error instanceof Error ? error.message : '连接失败'
      isConnected.value = false

      ElMessage.error('SignalR连接失败')
    }
  }

  // 断开SignalR连接
  const disconnectWebSocket = () => {
    signalrService.disconnect()
    isConnected.value = false
    connectionStatus.value = 'disconnected'
    connectionError.value = null

    // 清空数据
    messages.value = []
    containerUpdates.value = []
    recentLogs.value = []
    notifications.value = []
    unreadCount.value = 0

    ElMessage.info('SignalR连接已断开')
  }

  // 发送消息
  const sendMessage = (message: Partial<SignalRMessage>) => {
    return signalrService.invoke('SendMessage', message)
  }

  // 订阅SignalR消息事件
  const subscribeToSignalrMessages = () => {
    // 订阅连接状态变化
    signalrService.subscribe('connected', handleSignalrConnected)
    signalrService.subscribe('disconnected', handleSignalrDisconnected)
    signalrService.subscribe('reconnecting', handleSignalrReconnecting)
    signalrService.subscribe('reconnected', handleSignalrReconnected)
    signalrService.subscribe('reconnect-failed', handleSignalrReconnectFailed)

    // 订阅欢迎消息
    signalrService.subscribe('welcome', handleWelcomeMessage)

    // 订阅容器更新
    signalrService.subscribe('container', handleContainerUpdate)

    // 订阅系统状态更新
    signalrService.subscribe('system', handleSystemUpdate)

    // 订阅日志消息
    signalrService.subscribe('logs', handleLogMessage)

    // 订阅通知消息
    signalrService.subscribe('notification', handleNotificationMessage)

    // 订阅心跳响应
    signalrService.subscribe('pong', handlePong)

    // 订阅连接状态
    signalrService.subscribe('status', handleStatusUpdate)

    // 订阅错误消息
    signalrService.subscribe('error', handleError)

    // 订阅证书进度更新
    signalrService.subscribe('certificate-progress', handleCertificateProgress)
  }

  // 处理SignalR连接成功
  const handleSignalrConnected = (message: SignalRMessage) => {
    debugLog('SignalR连接成功:', message)
    isConnected.value = true
    connectionStatus.value = 'connected'
    connectionError.value = null
  }

  // 处理SignalR连接断开
  const handleSignalrDisconnected = (message: SignalRMessage) => {
    debugLog('SignalR连接断开:', message)
    isConnected.value = false
    connectionStatus.value = 'disconnected'
  }

  // 处理SignalR重连中
  const handleSignalrReconnecting = (message: SignalRMessage) => {
    debugLog('SignalR重连中:', message)
    isConnected.value = false
    connectionStatus.value = 'connecting'
    ElMessage.warning('SignalR连接断开，正在尝试重连...')
  }

  // 处理SignalR重连成功
  const handleSignalrReconnected = (message: SignalRMessage) => {
    debugLog('SignalR重连成功:', message)
    isConnected.value = true
    connectionStatus.value = 'connected'
    connectionError.value = null
    ElMessage.success('SignalR重连成功')
  }

  // 处理SignalR重连失败
  const handleSignalrReconnectFailed = (message: SignalRMessage) => {
    debugLog('SignalR重连失败:', message)
    isConnected.value = false
    connectionStatus.value = 'error'
    connectionError.value = '重连失败，请刷新页面重试'
    ElNotification({
      title: 'SignalR连接失败',
      message: '无法连接到服务器，请检查网络连接或刷新页面重试',
      type: 'error',
      duration: 0,
      showClose: true
    })
  }

  // 处理欢迎消息
  const handleWelcomeMessage = (message: SignalRMessage) => {
    // 收到欢迎消息，无需特殊处理
  }

  // 处理SignalR消息
  const handleSignalrMessage = (message: SignalRMessage) => {
    lastMessage.value = message
    messages.value.unshift(message)

    // 限制消息数量
    if (messages.value.length > 1000) {
      messages.value = messages.value.slice(0, 1000)
    }

    unreadCount.value++
  }

  // 处理容器更新
  const handleContainerUpdate = (message: SignalRMessage) => {
    const update = message.data as ContainerUpdate
    containerUpdates.value.unshift(update)

    // 限制更新数量
    if (containerUpdates.value.length > 100) {
      containerUpdates.value = containerUpdates.value.slice(0, 100)
    }

    // 显示容器状态变更通知
    showContainerNotification(update)
  }

  // 处理系统状态更新
  const handleSystemUpdate = (message: SignalRMessage) => {
    systemStats.value = message.data as SystemStatusUpdate
  }

  // 处理日志消息
  const handleLogMessage = (message: SignalRMessage) => {
    const logEntry = message.data as LogEntry
    recentLogs.value.unshift(logEntry)

    // 限制日志数量
    if (recentLogs.value.length > 500) {
      recentLogs.value = recentLogs.value.slice(0, 500)
    }

    // 重要日志显示通知
    if (logEntry.level === 'error') {
      ElNotification({
        title: '错误日志',
        message: `${logEntry.container}: ${logEntry.message}`,
        type: 'error',
        duration: 5000
      })
    }
  }

  // 处理通知消息
  const handleNotificationMessage = (message: SignalRMessage) => {
    const notification = message.data as NotificationMessage
    notifications.value.unshift(notification)

    // 限制通知数量
    if (notifications.value.length > 50) {
      notifications.value = notifications.value.slice(0, 50)
    }

    // 显示通知
    showNotification(notification)
  }

  // 处理心跳响应
  const handlePong = (message: SignalRMessage) => {
    // 心跳响应，无需特殊处理
  }

  // 处理状态更新
  const handleStatusUpdate = (message: SignalRMessage) => {
    // 连接状态更新
  }

  // 处理错误消息
  const handleError = (message: SignalRMessage) => {
    console.error('SignalR错误:', message)
    ElMessage.error(message.data?.message || '发生未知错误')
  }

  // 处理证书进度更新
  const handleCertificateProgress = (message: SignalRMessage) => {
    const data = message.data
    const tasksStore = useTasksStore()
    const taskId = `cert-${data.certificateId || data.progressId}`
    const status = data.status
    const progress = data.progressPercentage || 0

    if (status === 2 || status === 3) {
      tasksStore.updateTask(taskId, {
        status: status === 2 ? 'completed' : 'failed',
        progress: status === 2 ? 100 : progress,
        detail: status === 3 ? (data.errors?.[0] || '申请失败') : '申请完成'
      })
    } else if (status === 0 || progress === 0) {
      tasksStore.addTask({
        id: taskId,
        type: 'certificate',
        title: `证书申请 - ${data.certificateId || data.progressId}`,
        status: 'running',
        progress: 0,
        detail: data.currentStepDescription || data.message || '准备中...'
      })
    } else {
      tasksStore.updateTask(taskId, {
        status: 'running',
        progress,
        detail: data.currentStepDescription || data.message
      })
    }
  }

  // 显示容器状态变更通知
  const showContainerNotification = (update: ContainerUpdate) => {
    const actionMap: Record<string, { type: string; messageKey: string }> = {
      started: { type: 'success', messageKey: 'started' },
      stopped: { type: 'warning', messageKey: 'stopped' },
      paused: { type: 'info', messageKey: 'paused' },
      restarted: { type: 'info', messageKey: 'restarted' },
      removed: { type: 'danger', messageKey: 'removed' },
      created: { type: 'success', messageKey: 'created' }
    }

    const config = actionMap[update.action] || { type: 'info', messageKey: 'statusChanged' }
    
    // 简单翻译映射（store 中无法直接使用 i18n）
    const messageMap: Record<string, string> = {
      started: '已启动',
      stopped: '已停止', 
      paused: '已暂停',
      restarted: '已重启',
      removed: '已删除',
      created: '已创建',
      statusChanged: '状态变更'
    }

    ElNotification({
      title: '容器状态变更',
      message: `${update.name} ${messageMap[config.messageKey] || config.messageKey}`,
      type: config.type as any,
      duration: 3000
    })
  }

  // 显示通知
  const showNotification = (notification: NotificationMessage) => {
    ElNotification({
      title: notification.title,
      message: notification.message,
      type: notification.type as any,
      duration: notification.persistent ? 0 : 5000,
      showClose: true
    })
  }

  // 标记消息为已读
  const markMessagesAsRead = () => {
    unreadCount.value = 0
  }

  // 清空消息历史
  const clearMessages = () => {
    messages.value = []
    containerUpdates.value = []
    recentLogs.value = []
    notifications.value = []
    unreadCount.value = 0
  }

  // 获取特定类型的消息
  const getMessagesByType = (type: string) => {
    return messages.value.filter(msg => msg.type === type)
  }

  // 获取最近的容器更新
  const getRecentContainerUpdates = (limit = 10) => {
    return containerUpdates.value.slice(0, limit)
  }

  // 获取最近的日志
  const getRecentLogs = (limit = 50, level?: string) => {
    let logs = recentLogs.value
    if (level) {
      logs = logs.filter(log => log.level === level)
    }
    return logs.slice(0, limit)
  }

  // 获取未读通知
  const getUnreadNotifications = () => {
    return notifications.value.filter(n => !n.persistent)
  }

  return {
    // 状态
    isConnected,
    connectionStatus,
    connectionStatusText,
    connectionStatusType,
    lastMessage,
    messages,
    unreadCount,
    hasUnreadMessages,
    connectionError,

    // 数据
    containerUpdates,
    systemStats,
    recentLogs,
    notifications,

    // 方法
    initWebSocket,
    disconnectWebSocket,
    sendMessage,
    markMessagesAsRead,
    clearMessages,
    getMessagesByType,
    getRecentContainerUpdates,
    getRecentLogs,
    getUnreadNotifications
  }
})