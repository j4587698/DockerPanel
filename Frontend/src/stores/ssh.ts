/**
 * SSH管理状态管理
 * 对应后端DockerPanel.API.Controllers.SshController
 */

import { defineStore } from 'pinia'
import { ref, computed, readonly } from 'vue'
import { sshApi } from '@/api/ssh'
import type {
  SshConnectionTestRequest,
  SshConnectionInfo,
  SshKeyPair,
  SshCommandResult,
  SshConnectionTestResult,
  GenerateKeyPairRequest,
  ValidatePrivateKeyRequest,
  ExecuteCommandRequest,
  UploadFileRequest,
  DownloadFileRequest,
  BatchSshTestRequest,
  BatchSshTestResult,
  SshSession,
  SshTerminalSession,
  SshFileTransfer,
  SshCommandHistory,
  SshHostKey,
  SshConnectionMetrics,
  SshApiResponse,
  SshPaginatedResponse,
  SshConnectionConfig,
  SshUserPreferences,
  SshSecuritySettings,
  SshConnectionPool,
  SshBatchOperation,
  SshBatchOperationResult,
  SshNotificationSettings,
  SshGlobalConfig,
  SshConnectionStatus,
  SshAuthMethod
} from '@/types/ssh'

// ==================== SSH状态接口 ====================

interface SshState {
  // SSH连接测试
  connectionTestResult: SshConnectionTestResult | null
  batchTestResult: BatchSshTestResult | null
  connectionMetrics: SshConnectionMetrics | null

  // SSH密钥管理
  keyPairs: SshKeyPair[]
  selectedKeyPair: SshKeyPair | null
  keyPairValidation: { isValid: boolean; keyInfo?: any } | null

  // SSH命令执行
  commandResults: SshCommandResult[]
  selectedCommandResult: SshCommandResult | null
  commandHistory: SshCommandHistory[]
  selectedCommandHistory: SshCommandHistory | null

  // SSH文件传输
  fileTransfers: SshFileTransfer[]
  selectedFileTransfer: SshFileTransfer | null

  // SSH会话管理
  sessions: SshSession[]
  selectedSession: SshSession | null
  terminalSessions: SshTerminalSession[]
  selectedTerminalSession: SshTerminalSession | null

  // SSH主机密钥
  hostKeys: SshHostKey[]
  selectedHostKey: SshHostKey | null

  // SSH配置管理
  connectionConfigs: SshConnectionConfig[]
  selectedConnectionConfig: SshConnectionConfig | null

  // SSH批量操作
  batchOperations: SshBatchOperation[]
  selectedBatchOperation: SshBatchOperation | null

  // SSH设置
  userPreferences: SshUserPreferences | null
  securitySettings: SshSecuritySettings | null
  connectionPool: SshConnectionPool | null
  notificationSettings: SshNotificationSettings | null
  globalConfig: SshGlobalConfig | null

  // SSH统计和监控
  statistics: any
  healthStatus: any

  // SSH操作日志
  operationLogs: any[]
  selectedOperationLog: any | null

  // 通用状态
  loading: boolean
  error: string | null
  filter: {
    search: string
    host: string
    username: string
    status: string
    operation: string
    keyType: string
    transferType: string
    trusted: boolean
    isActive: boolean
    startDate: string
    endDate: string
  }
  pagination: {
    page: number
    pageSize: number
    total: number
  }
}

// ==================== SSH状态管理 ====================

// 创建SSH管理状态管理
export const useSshStore = defineStore('ssh', () => {
  // 状态
  const state = ref<SshState>({
    // SSH连接测试
    connectionTestResult: null,
    batchTestResult: null,
    connectionMetrics: null,

    // SSH密钥管理
    keyPairs: [],
    selectedKeyPair: null,
    keyPairValidation: null,

    // SSH命令执行
    commandResults: [],
    selectedCommandResult: null,
    commandHistory: [],
    selectedCommandHistory: null,

    // SSH文件传输
    fileTransfers: [],
    selectedFileTransfer: null,

    // SSH会话管理
    sessions: [],
    selectedSession: null,
    terminalSessions: [],
    selectedTerminalSession: null,

    // SSH主机密钥
    hostKeys: [],
    selectedHostKey: null,

    // SSH配置管理
    connectionConfigs: [],
    selectedConnectionConfig: null,

    // SSH批量操作
    batchOperations: [],
    selectedBatchOperation: null,

    // SSH设置
    userPreferences: null,
    securitySettings: null,
    connectionPool: null,
    notificationSettings: null,
    globalConfig: null,

    // SSH统计和监控
    statistics: null,
    healthStatus: null,

    // SSH操作日志
    operationLogs: [],
    selectedOperationLog: null,

    // 通用状态
    loading: false,
    error: null,
    filter: {
      search: '',
      host: '',
      username: '',
      status: '',
      operation: '',
      keyType: '',
      transferType: '',
      trusted: false,
      isActive: false,
      startDate: '',
      endDate: ''
    },
    pagination: {
      page: 1,
      pageSize: 20,
      total: 0
    }
  })

  // ==================== 计算属性 ====================

  // 过滤后的密钥对
  const filteredKeyPairs = computed(() => {
    let result = state.value.keyPairs

    if (state.value.filter.search) {
      result = result.filter(item =>
        item.keyName.toLowerCase().includes(state.value.filter.search.toLowerCase()) ||
        item.fingerprint.toLowerCase().includes(state.value.filter.search.toLowerCase())
      )
    }

    if (state.value.filter.keyType) {
      result = result.filter(item => item.keyName.includes(state.value.filter.keyType))
    }

    return result
  })

  // 过滤后的命令历史
  const filteredCommandHistory = computed(() => {
    let result = state.value.commandHistory

    if (state.value.filter.search) {
      result = result.filter(item =>
        item.command.toLowerCase().includes(state.value.filter.search.toLowerCase()) ||
        item.connectionInfo.host.toLowerCase().includes(state.value.filter.search.toLowerCase())
      )
    }

    if (state.value.filter.host) {
      result = result.filter(item => item.connectionInfo.host.includes(state.value.filter.host))
    }

    if (state.value.filter.username) {
      result = result.filter(item => item.connectionInfo.username.includes(state.value.filter.username))
    }

    return result
  })

  // 过滤后的文件传输
  const filteredFileTransfers = computed(() => {
    let result = state.value.fileTransfers

    if (state.value.filter.search) {
      result = result.filter(item =>
        item.sourcePath.toLowerCase().includes(state.value.filter.search.toLowerCase()) ||
        item.destinationPath.toLowerCase().includes(state.value.filter.search.toLowerCase())
      )
    }

    if (state.value.filter.host) {
      result = result.filter(item => item.connectionInfo.host.includes(state.value.filter.host))
    }

    if (state.value.filter.transferType) {
      result = result.filter(item => item.transferType === state.value.filter.transferType)
    }

    if (state.value.filter.status) {
      result = result.filter(item => item.status === state.value.filter.status)
    }

    return result
  })

  // 过滤后的会话
  const filteredSessions = computed(() => {
    let result = state.value.sessions

    if (state.value.filter.host) {
      result = result.filter(item => item.connectionInfo.host.includes(state.value.filter.host))
    }

    if (state.value.filter.username) {
      result = result.filter(item => item.connectionInfo.username.includes(state.value.filter.username))
    }

    if (state.value.filter.isActive !== undefined) {
      result = result.filter(item => item.isActive === state.value.filter.isActive)
    }

    return result
  })

  // 过滤后的主机密钥
  const filteredHostKeys = computed(() => {
    let result = state.value.hostKeys

    if (state.value.filter.host) {
      result = result.filter(item => item.host.includes(state.value.filter.host))
    }

    if (state.value.filter.trusted !== undefined) {
      result = result.filter(item => item.trusted === state.value.filter.trusted)
    }

    return result
  })

  // 过滤后的连接配置
  const filteredConnectionConfigs = computed(() => {
    let result = state.value.connectionConfigs

    if (state.value.filter.search) {
      result = result.filter(item =>
        item.host.toLowerCase().includes(state.value.filter.search.toLowerCase()) ||
        item.username.toLowerCase().includes(state.value.filter.search.toLowerCase())
      )
    }

    return result
  })

  // 过滤后的批量操作
  const filteredBatchOperations = computed(() => {
    let result = state.value.batchOperations

    if (state.value.filter.search) {
      result = result.filter(item =>
        item.name.toLowerCase().includes(state.value.filter.search.toLowerCase()) ||
        item.description.toLowerCase().includes(state.value.filter.search.toLowerCase())
      )
    }

    if (state.value.filter.operation) {
      result = result.filter(item => item.operation === state.value.filter.operation)
    }

    if (state.value.filter.status) {
      result = result.filter(item => item.status === state.value.filter.status)
    }

    return result
  })

  // 过滤后的操作日志
  const filteredOperationLogs = computed(() => {
    let result = state.value.operationLogs

    if (state.value.filter.search) {
      result = result.filter(item =>
        item.operation.toLowerCase().includes(state.value.filter.search.toLowerCase()) ||
        item.host.toLowerCase().includes(state.value.filter.search.toLowerCase())
      )
    }

    if (state.value.filter.operation) {
      result = result.filter(item => item.operation === state.value.filter.operation)
    }

    if (state.value.filter.host) {
      result = result.filter(item => item.host.includes(state.value.filter.host))
    }

    if (state.value.filter.username) {
      result = result.filter(item => item.username.includes(state.value.filter.username))
    }

    if (state.value.filter.status) {
      result = result.filter(item => item.status === state.value.filter.status)
    }

    return result
  })

  // ==================== SSH连接管理方法 ====================

  // 测试SSH连接
  const testConnection = async (data: SshConnectionTestRequest) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.testConnection(data)
      state.value.connectionTestResult = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '连接测试失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 批量测试SSH连接
  const batchTestConnection = async (data: BatchSshTestRequest) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.batchTestConnection(data)
      state.value.batchTestResult = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '批量连接测试失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取SSH连接性能指标
  const getConnectionMetrics = async (host: string, port: number) => {
    try {
      const response = await sshApi.getConnectionMetrics(host, port)
      state.value.connectionMetrics = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取连接指标失败'
      throw error
    }
  }

  // ==================== SSH密钥管理方法 ====================

  // 生成SSH密钥对
  const generateKeyPair = async (data: GenerateKeyPairRequest) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.generateKeyPair(data)
      state.value.keyPairs.push(response.data)
      state.value.selectedKeyPair = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '生成密钥对失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 验证SSH私钥
  const validatePrivateKey = async (data: ValidatePrivateKeyRequest) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.validatePrivateKey(data)
      state.value.keyPairValidation = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '验证私钥失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取SSH密钥对列表
  const fetchKeyPairs = async (params?: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.getKeyPairs(params) as any
      state.value.keyPairs = response?.items || []
      state.value.pagination.total = response?.total || 0
      return response
    } catch (error: any) {
      state.value.error = error.message || '获取密钥对列表失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 删除SSH密钥对
  const deleteKeyPair = async (keyId: string) => {
    state.value.loading = true
    state.value.error = null
    try {
      await sshApi.deleteKeyPair(keyId)
      state.value.keyPairs = state.value.keyPairs.filter(item => item.keyName !== keyId)
      if (state.value.selectedKeyPair?.keyName === keyId) {
        state.value.selectedKeyPair = null
      }
    } catch (error: any) {
      state.value.error = error.message || '删除密钥对失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== SSH命令执行方法 ====================

  // 执行SSH命令
  const executeCommand = async (data: ExecuteCommandRequest) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.executeCommand(data)
      state.value.commandResults.push(response.data)
      state.value.selectedCommandResult = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '执行命令失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取SSH命令历史
  const fetchCommandHistory = async (params?: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.getCommandHistory(params)
      state.value.commandHistory = response.data.items
      state.value.pagination.total = response.data.total
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取命令历史失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== SSH文件传输方法 ====================

  // 上传文件到SSH服务器
  const uploadFile = async (data: UploadFileRequest) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.uploadFile(data)
      state.value.fileTransfers.push(response.data)
      state.value.selectedFileTransfer = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '上传文件失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 从SSH服务器下载文件
  const downloadFile = async (data: DownloadFileRequest) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.downloadFile(data)
      state.value.fileTransfers.push(response.data)
      state.value.selectedFileTransfer = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '下载文件失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取SSH文件传输列表
  const fetchFileTransfers = async (params?: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.getFileTransfers(params)
      state.value.fileTransfers = response.data.items
      state.value.pagination.total = response.data.total
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取文件传输列表失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== SSH会话管理方法 ====================

  // 获取SSH会话列表
  const fetchSessions = async (params?: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.getSessions(params) as any
      state.value.sessions = response?.items || []
      state.value.pagination.total = response?.total || 0
      return response
    } catch (error: any) {
      state.value.error = error.message || '获取会话列表失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 创建SSH终端会话
  const createTerminalSession = async (data: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.createTerminalSession(data)
      state.value.terminalSessions.push(response.data)
      state.value.selectedTerminalSession = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '创建终端会话失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== SSH主机密钥管理方法 ====================

  // 获取SSH主机密钥列表
  const fetchHostKeys = async (params?: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.getHostKeys(params)
      state.value.hostKeys = response.data.items
      state.value.pagination.total = response.data.total
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取主机密钥列表失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 添加SSH主机密钥
  const addHostKey = async (data: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.addHostKey(data)
      state.value.hostKeys.push(response.data)
      state.value.selectedHostKey = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '添加主机密钥失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== SSH配置管理方法 ====================

  // 获取SSH连接配置列表
  const fetchConnectionConfigs = async (params?: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.getConnectionConfigs(params) as any
      // axios response interceptor已经返回了response.data, 所以response直接就是数据
      state.value.connectionConfigs = response?.items || []
      state.value.pagination.total = response?.total || 0
      return response
    } catch (error: any) {
      state.value.error = error.message || '获取连接配置列表失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 创建SSH连接配置
  const createConnectionConfig = async (data: SshConnectionConfig) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.createConnectionConfig(data)
      state.value.connectionConfigs.push(response.data)
      state.value.selectedConnectionConfig = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '创建连接配置失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== SSH设置管理方法 ====================

  // 获取SSH用户偏好设置
  const fetchUserPreferences = async () => {
    try {
      const response = await sshApi.getUserPreferences()
      state.value.userPreferences = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取用户偏好设置失败'
      throw error
    }
  }

  // 更新SSH用户偏好设置
  const updateUserPreferences = async (data: Partial<SshUserPreferences>) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.updateUserPreferences(data)
      state.value.userPreferences = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '更新用户偏好设置失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取SSH安全设置
  const fetchSecuritySettings = async () => {
    try {
      const response = await sshApi.getSecuritySettings()
      state.value.securitySettings = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取安全设置失败'
      throw error
    }
  }

  // 更新SSH安全设置
  const updateSecuritySettings = async (data: Partial<SshSecuritySettings>) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.updateSecuritySettings(data)
      state.value.securitySettings = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '更新安全设置失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取SSH全局配置
  const fetchGlobalConfig = async () => {
    try {
      const response = await sshApi.getGlobalConfig()
      state.value.globalConfig = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取全局配置失败'
      throw error
    }
  }

  // ==================== SSH统计和监控方法 ====================

  // 获取SSH统计信息
  const fetchStatistics = async () => {
    try {
      const response = await sshApi.getStatistics() as any
      state.value.statistics = response
      return response
    } catch (error: any) {
      state.value.error = error.message || '获取统计信息失败'
      throw error
    }
  }

  // 获取SSH健康状态
  const fetchHealthStatus = async () => {
    try {
      const response = await sshApi.getHealthStatus()
      state.value.healthStatus = response.data
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取健康状态失败'
      throw error
    }
  }

  // ==================== SSH操作日志方法 ====================

  // 获取SSH操作日志
  const fetchOperationLogs = async (params?: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.getOperationLogs(params) as any
      state.value.operationLogs = response?.items || []
      state.value.pagination.total = response?.total || 0
      return response
    } catch (error: any) {
      state.value.error = error.message || '获取操作日志失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== 新增方法（支持新组件） ====================

  // 导入SSH密钥对
  const importKeyPair = async (data: { name: string; publicKey: string; privateKey?: string; passphrase?: string }) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.importKeyPair(data)
      state.value.keyPairs.push(response.data)
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '导入密钥对失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 删除SSH连接配置
  const deleteConnectionConfig = async (configId: string) => {
    state.value.loading = true
    state.value.error = null
    try {
      await sshApi.deleteConnectionConfig(configId)
      state.value.connectionConfigs = state.value.connectionConfigs.filter(item => item.id !== configId)
      if (state.value.selectedConnectionConfig?.id === configId) {
        state.value.selectedConnectionConfig = null
      }
    } catch (error: any) {
      state.value.error = error.message || '删除连接配置失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 终止SSH会话
  const terminateSession = async (sessionId: string) => {
    state.value.loading = true
    state.value.error = null
    try {
      await sshApi.terminateSession(sessionId)
      state.value.sessions = state.value.sessions.filter(item => item.sessionId !== sessionId)
    } catch (error: any) {
      state.value.error = error.message || '终止会话失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 重连SSH会话
  const reconnectSession = async (sessionId: string) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.reconnectSession(sessionId)
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '重连会话失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 列出远程目录内容
  const listDirectory = async (data: { connectionId: string; path: string }) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.listDirectory(data)
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '列出目录失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 删除远程文件
  const deleteRemoteFile = async (data: { connectionId: string; path: string }) => {
    state.value.loading = true
    state.value.error = null
    try {
      await sshApi.deleteRemoteFile(data)
    } catch (error: any) {
      state.value.error = error.message || '删除文件失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取SSH设置
  const fetchSettings = async () => {
    try {
      const response = await sshApi.getSettings()
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取SSH设置失败'
      throw error
    }
  }

  // 更新SSH设置
  const updateSettings = async (settings: any) => {
    state.value.loading = true
    state.value.error = null
    try {
      const response = await sshApi.updateSettings(settings)
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '更新SSH设置失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== 通用方法 ====================

  // 设置过滤器
  const setFilter = (filter: Partial<SshState['filter']>) => {
    Object.assign(state.value.filter, filter)
  }

  // 重置过滤器
  const resetFilter = () => {
    state.value.filter = {
      search: '',
      host: '',
      username: '',
      status: '',
      operation: '',
      keyType: '',
      transferType: '',
      trusted: false,
      isActive: false,
      startDate: '',
      endDate: ''
    }
  }

  // 设置分页
  const setPagination = (pagination: Partial<SshState['pagination']>) => {
    Object.assign(state.value.pagination, pagination)
  }

  // 重置分页
  const resetPagination = () => {
    state.value.pagination = {
      page: 1,
      pageSize: 20,
      total: 0
    }
  }

  // 清除错误
  const clearError = () => {
    state.value.error = null
  }

  // 重置状态
  const resetState = () => {
    state.value.connectionTestResult = null
    state.value.batchTestResult = null
    state.value.connectionMetrics = null
    state.value.keyPairs = []
    state.value.selectedKeyPair = null
    state.value.keyPairValidation = null
    state.value.commandResults = []
    state.value.selectedCommandResult = null
    state.value.commandHistory = []
    state.value.selectedCommandHistory = null
    state.value.fileTransfers = []
    state.value.selectedFileTransfer = null
    state.value.sessions = []
    state.value.selectedSession = null
    state.value.terminalSessions = []
    state.value.selectedTerminalSession = null
    state.value.hostKeys = []
    state.value.selectedHostKey = null
    state.value.connectionConfigs = []
    state.value.selectedConnectionConfig = null
    state.value.batchOperations = []
    state.value.selectedBatchOperation = null
    state.value.userPreferences = null
    state.value.securitySettings = null
    state.value.connectionPool = null
    state.value.notificationSettings = null
    state.value.globalConfig = null
    state.value.statistics = null
    state.value.healthStatus = null
    state.value.operationLogs = []
    state.value.selectedOperationLog = null
    state.value.loading = false
    state.value.error = null
    resetFilter()
    resetPagination()
  }

  return {
    // 状态
    state: readonly(state),

    // 计算属性
    filteredKeyPairs,
    filteredCommandHistory,
    filteredFileTransfers,
    filteredSessions,
    filteredHostKeys,
    filteredConnectionConfigs,
    filteredBatchOperations,
    filteredOperationLogs,

    // SSH连接管理方法
    testConnection,
    batchTestConnection,
    getConnectionMetrics,

    // SSH密钥管理方法
    generateKeyPair,
    validatePrivateKey,
    fetchKeyPairs,
    deleteKeyPair,
    importKeyPair,

    // SSH命令执行方法
    executeCommand,
    fetchCommandHistory,

    // SSH文件传输方法
    uploadFile,
    downloadFile,
    fetchFileTransfers,
    listDirectory,
    deleteRemoteFile,

    // SSH会话管理方法
    fetchSessions,
    createTerminalSession,
    terminateSession,
    reconnectSession,

    // SSH主机密钥管理方法
    fetchHostKeys,
    addHostKey,

    // SSH配置管理方法
    fetchConnectionConfigs,
    createConnectionConfig,
    deleteConnectionConfig,

    // SSH设置管理方法
    fetchUserPreferences,
    updateUserPreferences,
    fetchSecuritySettings,
    updateSecuritySettings,
    fetchGlobalConfig,
    fetchSettings,
    updateSettings,

    // SSH统计和监控方法
    fetchStatistics,
    fetchHealthStatus,

    // SSH操作日志方法
    fetchOperationLogs,

    // 通用方法
    setFilter,
    resetFilter,
    setPagination,
    resetPagination,
    clearError,
    resetState
  }
})