/**
 * SSH管理类型定义
 * 对应后端DockerPanel.API.Controllers.SshController
 */

// ==================== 基础SSH类型 ====================

/**
 * SSH连接测试请求
 */
export interface SshConnectionTestRequest {
  host: string
  port: number
  username: string
  password?: string
  privateKeyPath?: string
}

/**
 * SSH连接信息
 */
export interface SshConnectionInfo {
  host: string
  port: number
  username: string
  password?: string
  privateKeyPath?: string
}

/**
 * SSH连接配置
 */
export interface SshConnectionConfig {
  id?: string
  name?: string
  description?: string
  host: string
  port: number
  username: string
  password?: string
  privateKeyPath?: string
  privateKeyPassphrase?: string
  connectionTimeout: number
  commandTimeout: number
  strictHostKeyChecking: boolean
  tags?: string
  status?: string
  createdAt?: string
  lastConnectedAt?: string
}

/**
 * SSH会话信息 (对应后端 SshSessionInfo)
 */
export interface SshSessionInfo {
  id: string
  host: string
  port: number
  username: string
  status: string
  connectedAt: string
  lastActivityAt?: string
  commandsExecuted: number
  bytesTransferred: number
}

/**
 * SSH操作日志 (对应后端 SshOperationLog)
 */
export interface SshOperationLog {
  id: string
  timestamp: string
  operation: string
  host: string
  port: number
  username: string
  status: string
  details?: string
  duration?: number
  errorMessage?: string
}

/**
 * SSH统计信息 (对应后端 SshStatistics)
 */
export interface SshStatistics {
  totalConnections: number
  activeConnections: number
  totalKeyPairs: number
  totalCommands: number
  totalFileTransfers: number
  totalBytesTransferred: number
}

/**
 * SSH设置 (对应后端 SshSettings)
 */
export interface SshSettings {
  defaultConnectionTimeout: number
  defaultCommandTimeout: number
  keepAliveInterval: number
  autoReconnect: boolean
  maxReconnectAttempts: number
  strictHostKeyChecking: boolean
  preferKeyAuth: boolean
  allowedCiphers: string[]
  defaultTerminalType: string
  terminalCols: number
  terminalRows: number
  terminalScrollback: number
  maxConcurrentTransfers: number
  chunkSize: number
  preserveTimestamps: boolean
  logOperations: boolean
  logRetentionDays: number
}

/**
 * 远程文件信息 (对应后端 RemoteFileInfo)
 */
export interface RemoteFileInfo {
  name: string
  path: string
  type: string
  size: number
  modifiedAt: string
  permissions: string
  owner: string
  group: string
}

/**
 * SSH密钥对
 */
export interface SshKeyPair {
  publicKey: string
  privateKey: string
  keyName: string
  createdAt: string
  fingerprint: string
}

/**
 * SSH命令执行结果
 */
export interface SshCommandResult {
  success: boolean
  output: string
  error: string
  exitCode: number
  executedAt: string
  executionDuration: string
}

// ==================== 请求类型 ====================

/**
 * 生成密钥对请求
 */
export interface GenerateKeyPairRequest {
  keyName: string
}

/**
 * 验证私钥请求
 */
export interface ValidatePrivateKeyRequest {
  privateKeyPath: string
  passphrase?: string
}

/**
 * 执行命令请求
 */
export interface ExecuteCommandRequest {
  host: string
  port: number
  username: string
  command: string
  password?: string
  privateKeyPath?: string
}

/**
 * 上传文件请求
 */
export interface UploadFileRequest {
  host: string
  port: number
  username: string
  localPath: string
  remotePath: string
  password?: string
  privateKeyPath?: string
}

/**
 * 下载文件请求
 */
export interface DownloadFileRequest {
  host: string
  port: number
  username: string
  remotePath: string
  localPath: string
  password?: string
  privateKeyPath?: string
}

/**
 * 批量SSH测试请求
 */
export interface BatchSshTestRequest {
  connections: SshConnectionInfo[]
}

/**
 * 批量SSH测试结果
 */
export interface BatchSshTestResult {
  results: SshConnectionTestResult[]
}

/**
 * SSH连接测试结果
 */
export interface SshConnectionTestResult {
  host: string
  port: number
  username: string
  success: boolean
  error?: string
  connectionTime?: string
}

// ==================== 扩展SSH管理类型 ====================

/**
 * SSH会话信息
 */
export interface SshSession {
  id: string
  connectionInfo: SshConnectionInfo
  connectedAt: string
  lastActivityAt: string
  isActive: boolean
  sessionTimeout: number
  commandsExecuted: number
  filesTransferred: number
}

/**
 * SSH终端会话
 */
export interface SshTerminalSession {
  id: string
  connectionInfo: SshConnectionInfo
  terminalType: string
  rows: number
  cols: number
  createdAt: string
  isActive: boolean
  buffer: string[]
  cursorPosition: { row: number; col: number }
}

/**
 * SSH文件传输记录
 */
export interface SshFileTransfer {
  id: string
  transferType: 'upload' | 'download'
  connectionInfo: SshConnectionInfo
  sourcePath: string
  destinationPath: string
  fileSize: number
  bytesTransferred: number
  transferSpeed: number
  status: 'pending' | 'in-progress' | 'completed' | 'failed' | 'cancelled'
  startTime: string
  endTime?: string
  progress: number
  errorMessage?: string
}

/**
 * SSH命令历史
 */
export interface SshCommandHistory {
  id: string
  connectionInfo: SshConnectionInfo
  command: string
  result: SshCommandResult
  executedAt: string
  executionDuration: number
  userId?: string
  tags: string[]
  notes?: string
}

/**
 * SSH主机密钥
 */
export interface SshHostKey {
  host: string
  port: number
  keyType: string
  keyFingerprint: string
  publicKey: string
  algorithm: string
  keySize: number
  firstSeen: string
  lastSeen: string
  trusted: boolean
  notes?: string
}

/**
 * SSH配置模板
 */
export interface SshConfigTemplate {
  id: string
  name: string
  description: string
  category: string
  connectionConfig: Partial<SshConnectionConfig>
  predefinedCommands: SshPredefinedCommand[]
  tags: string[]
  isBuiltIn: boolean
  createdAt: string
  updatedAt: string
}

/**
 * SSH预定义命令
 */
export interface SshPredefinedCommand {
  id: string
  name: string
  command: string
  description: string
  category: string
  parameters?: SshCommandParameter[]
  requiresSudo: boolean
  timeout: number
  tags: string[]
}

/**
 * SSH命令参数
 */
export interface SshCommandParameter {
  name: string
  type: 'string' | 'number' | 'boolean' | 'select'
  description: string
  required: boolean
  defaultValue?: any
  options?: string[]
  validation?: {
    pattern?: string
    min?: number
    max?: number
  }
}

/**
 * SSH连接性能指标
 */
export interface SshConnectionMetrics {
  host: string
  port: number
  connectionTime: number
  responseTime: number
  bandwidth: {
    upload: number
    download: number
  }
  packetLoss: number
  lastTestTime: string
  status: 'excellent' | 'good' | 'poor' | 'unreachable'
}

/**
 * SSH安全设置
 */
export interface SshSecuritySettings {
  allowPasswordAuth: boolean
  allowPublicKeyAuth: boolean
  requirePrivateKeyAuth: boolean
  maxAuthAttempts: number
  connectionTimeout: number
  idleTimeout: number
  logLevel: 'none' | 'error' | 'warn' | 'info' | 'debug'
  auditCommands: boolean
  auditFileTransfers: boolean
}

/**
 * SSH用户偏好设置
 */
export interface SshUserPreferences {
  defaultTerminalType: string
  defaultTerminalRows: number
  defaultTerminalCols: number
  defaultConnectionTimeout: number
  defaultCommandTimeout: number
  autoSaveHistory: boolean
  showConnectionNotifications: boolean
  enableCommandAutocomplete: boolean
  preferredAuthMethod: 'password' | 'privatekey' | 'both'
  defaultKeyPath: string
}

/**
 * SSH连接池配置
 */
export interface SshConnectionPool {
  maxConnections: number
  maxConnectionsPerHost: number
  connectionIdleTimeout: number
  connectionHealthCheckInterval: number
  enableConnectionPooling: boolean
  reuseConnections: boolean
}

/**
 * SSH批量操作
 */
export interface SshBatchOperation {
  id: string
  name: string
  description: string
  operation: 'command' | 'upload' | 'download' | 'test'
  targets: SshConnectionInfo[]
  command?: string
  fileOperation?: {
    sourcePath: string
    destinationPath: string
  }
  parallelExecution: boolean
  maxParallelTasks: number
  timeout: number
  retryAttempts: number
  continueOnError: boolean
  createdAt: string
  createdBy: string
  status: 'pending' | 'running' | 'completed' | 'failed' | 'cancelled'
  results?: SshBatchOperationResult[]
}

/**
 * SSH批量操作结果
 */
export interface SshBatchOperationResult {
  target: SshConnectionInfo
  success: boolean
  result?: any
  error?: string
  executionTime: string
  startTime: string
  endTime?: string
}

/**
 * SSH通知设置
 */
export interface SshNotificationSettings {
  connectionEvents: boolean
  commandExecutions: boolean
  fileTransfers: boolean
  errors: boolean
  securityEvents: boolean
  emailNotifications: boolean
  webhookUrl?: string
  notificationChannels: ('inapp' | 'email' | 'webhook' | 'slack')[]
}

// ==================== 实用工具类型 ====================

/**
 * SSH连接状态
 */
export type SshConnectionStatus =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'authenticating'
  | 'authenticated'
  | 'error'
  | 'timeout'

/**
 * SSH认证方式
 */
export type SshAuthMethod = 'password' | 'publickey' | 'keyboard' | 'hostbased'

/**
 * SSH文件传输方向
 */
export type SshTransferDirection = 'upload' | 'download'

/**
 * SSH命令执行状态
 */
export type SshCommandStatus =
  | 'pending'
  | 'running'
  | 'completed'
  | 'failed'
  | 'timeout'
  | 'cancelled'

/**
 * SSH终端类型
 */
export type SshTerminalType =
  | 'xterm'
  | 'xterm-256color'
  | 'vt100'
  | 'vt220'
  | 'screen'

/**
 * SSH日志级别
 */
export type SshLogLevel =
  | 'none'
  | 'error'
  | 'warn'
  | 'info'
  | 'debug'

/**
 * SSH操作类型
 */
export type SshOperationType =
  | 'connect'
  | 'disconnect'
  | 'execute'
  | 'upload'
  | 'download'
  | 'test'
  | 'generate-key'
  | 'validate-key'

// ==================== 响应包装类型 ====================

/**
 * SSH API响应包装
 */
export interface SshApiResponse<T = any> {
  success: boolean
  data?: T
  message?: string
  error?: string
  timestamp: string
  requestId?: string
}

/**
 * SSH分页响应
 */
export interface SshPaginatedResponse<T = any> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
  hasNext: boolean
  hasPrevious: boolean
}

/**
 * SSH批量操作响应
 */
export interface SshBatchResponse<T = any> {
  totalItems: number
  successfulItems: number
  failedItems: number
  results: T[]
  errors: string[]
  warnings: string[]
  executionTime: string
}

// ==================== 错误类型 ====================

/**
 * SSH错误类型
 */
export interface SshError {
  code: string
  message: string
  details?: string
  host?: string
  port?: number
  operation?: SshOperationType
  timestamp: string
  stackTrace?: string
}

/**
 * SSH验证错误
 */
export interface SshValidationError extends SshError {
  field?: string
  value?: any
  validationRule?: string
}

/**
 * SSH连接错误详情
 */
export interface SshConnectionErrorDetails {
  errorType: 'authentication' | 'network' | 'timeout' | 'hostkey' | 'permission' | 'unknown'
  sshErrorCode?: number
  errorMessage: string
  technicalDetails?: string
  suggestedActions?: string[]
}

// ==================== 配置和设置类型 ====================

/**
 * SSH全局配置
 */
export interface SshGlobalConfig {
  version: string
  defaultSettings: SshUserPreferences
  securitySettings: SshSecuritySettings
  connectionPool: SshConnectionPool
  notificationSettings: SshNotificationSettings
  features: {
    terminalSession: boolean
    fileTransfer: boolean
    batchOperations: boolean
    commandHistory: boolean
    connectionPooling: boolean
    metrics: boolean
    auditing: boolean
  }
  limits: {
    maxConcurrentConnections: number
    maxCommandHistory: number
    maxFileSize: number
    maxCommandTimeout: number
  }
}

// ==================== 导出所有类型 ====================
// All interfaces are already exported using 'export interface' syntax above