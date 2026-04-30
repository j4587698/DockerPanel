// Compose 状态枚举
export enum ComposeStatus {
  Unknown = 'Unknown',
  Created = 'Created',
  Running = 'Running',
  Stopped = 'Stopped',
  PartiallyRunning = 'PartiallyRunning',
  Error = 'Error',
  Deploying = 'Deploying',
  Removing = 'Removing'
}

// Compose 服务详情（用于可视化编辑器联动）
export interface ComposeServiceDetail {
  name: string
  image: string
  ports: string[]
  environment: Record<string, string | null>
  volumes: string[]
  
  // 构建配置
  build?: string
  dockerfile?: string
  context?: string
  
  // 运行配置
  containerName?: string
  command?: string
  entrypoint?: string
  workingDir?: string
  user?: string
  hostname?: string
  
  // 重启策略
  restart?: string
  
  // 依赖
  dependsOn: string[]
  
  // 网络配置
  networks: string[]
  networkMode?: string
  
  // 标签
  labels: Record<string, string>
  
  // 环境文件
  envFile: string[]
  
  // 健康检查
  healthCheck?: ComposeHealthCheck
  
  // 资源限制
  memLimit?: number
  memReservation?: number
  cpuCount?: number
  cpuShares?: number
  
  // 其他
  privileged?: boolean
  capAdd: string[]
  capDrop: string[]
  extraHosts: string[]
  pid?: string
  ipc?: string
}

// 健康检查配置
export interface ComposeHealthCheck {
  test?: string[]
  interval?: number
  timeout?: number
  retries?: number
  startPeriod?: number
  disable?: boolean
}

// Compose 文件接口
export interface ComposeFile {
  id: string
  name: string
  description: string
  content: string
  path: string
  nodeId?: string
  nodeName: string
  version: string
  services: string[]
  networks: string[]
  volumes: string[]
  metadata: Record<string, any>
  createdAt: string
  updatedAt: string
  createdBy: string
  updatedBy: string
  fileSize: number
  hash: string
  isActive: boolean
  status: ComposeStatus
  serviceDetails?: ComposeServiceDetail[]
}

// Compose 项目信息
export interface ComposeProject {
  id: string
  name: string
  filePath?: string
  nodeId?: string
  nodeName: string
  status: ComposeStatus
  services: ComposeServiceInfo[]
  networks: ComposeNetworkInfo[]
  volumes: ComposeVolumeInfo[]
  createdAt: string
  updatedAt: string
  config: Record<string, any>
}

// Compose 服务信息
export interface ComposeServiceInfo {
  name: string
  image: string
  status: string
  containerId?: string
  ports: string[]
  networks: string[]
  environment: Record<string, string>
  createdAt?: string
  startedAt?: string
  isRunning: boolean
  health: number
  healthStatus?: string
}

// Compose 网络信息
export interface ComposeNetworkInfo {
  name: string
  networkId?: string
  driver: string
  external: boolean
  labels: Record<string, string>
  connectedServices: string[]
}

// Compose 卷信息
export interface ComposeVolumeInfo {
  name: string
  volumeId?: string
  driver: string
  external: boolean
  options: Record<string, string>
  labels: Record<string, string>
  usedByServices: string[]
}

// 创建 Compose 文件请求
export interface CreateComposeFileRequest {
  name: string
  description?: string
  content: string
  path?: string
  nodeId?: string
  metadata?: Record<string, any>
}

// 更新 Compose 文件请求
export interface UpdateComposeFileRequest {
  name?: string
  description?: string
  content?: string
  path?: string
  metadata?: Record<string, any>
}

// 部署 Compose 请求
export interface DeployComposeRequest {
  composeFileId: string
  nodeId?: string
  detach?: boolean
  removeOrphans?: boolean
  forceRecreate?: boolean
  noRecreate?: boolean
  noBuild?: boolean
  noDeps?: boolean
  services?: string[]
  environment?: Record<string, string>
  labels?: Record<string, string>
  timeout?: number
}

// Compose 操作请求
export interface ComposeOperationRequest {
  composeFileId: string
  nodeId?: string
  services?: string[]
  timeout?: number
  force?: boolean
  parameters?: Record<string, any>
}

// Compose 日志请求
export interface ComposeLogsRequest {
  composeFileId: string
  nodeId?: string
  services?: string[]
  follow?: boolean
  timestamps?: boolean
  since?: string
  until?: string
  tail?: number
}

// Compose 日志响应
export interface ComposeLogsResponse {
  composeFileId: string
  logs: ComposeLogEntry[]
  since?: string
  until?: string
  hasMore: boolean
}

// Compose 日志条目
export interface ComposeLogEntry {
  service: string
  container: string
  timestamp: string
  message: string
  stream: string
}

// Compose 操作结果
export interface ComposeOperationResult {
  composeFileId: string
  operation: string
  success: boolean
  message: string
  startTime: string
  endTime: string
  duration: number
  affectedServices: string[]
  details: Record<string, any>
  errors: string[]
  warnings: string[]
}

// Compose 项目统计
export interface ComposeProjectStats {
  composeFileId: string
  projectName: string
  totalServices: number
  runningServices: number
  stoppedServices: number
  unhealthyServices: number
  totalNetworks: number
  totalVolumes: number
  totalSize: number
  createdAt: string
  lastDeployed: string
  status: string
  healthPercentage: number
}

// Compose 模板
export interface ComposeTemplate {
  id: string
  name: string
  description: string
  category: string
  content: string
  tags: string[]
  variables: Record<string, any>
  icon?: string
  version: string
  author: string
  createdAt: string
  downloadCount: number
  rating: number
  isOfficial: boolean
  isPublic: boolean
}

// Compose 文件验证结果
export interface ComposeValidationResult {
  composeFileId: string
  isValid: boolean
  errors: ValidationError[]
  warnings: ValidationWarning[]
  infos: ValidationInfo[]
  validatedAt: string
  version: string
  serviceCount: number
  networkCount: number
  volumeCount: number
}

// 验证错误
export interface ValidationError {
  code: string
  message: string
  service?: string
  property?: string
  value?: string
  line: string
}

// 验证警告
export interface ValidationWarning {
  code: string
  message: string
  service?: string
  property?: string
  line: string
}

// 验证信息
export interface ValidationInfo {
  code: string
  message: string
  service?: string
  line: string
}

// Compose 文件版本
export interface ComposeFileVersion {
  id: string
  composeFileId: string
  version: string
  content: string
  changeDescription: string
  createdBy: string
  createdAt: string
  fileSize: number
  hash: string
  metadata: Record<string, any>
}

// Compose 依赖检查结果
export interface ComposeDependencyCheck {
  composeFileId: string
  allDependenciesSatisfied: boolean
  issues: DependencyIssue[]
  externalDependencies: ExternalDependency[]
  missingResources: MissingResource[]
  checkTime: string
}

// 依赖问题
export interface DependencyIssue {
  type: string
  code: string
  message: string
  service?: string
  resource?: string
  suggestion?: string
}

// 外部依赖
export interface ExternalDependency {
  name: string
  type: string
  source?: string
  isAvailable: boolean
  statusMessage?: string
}

// 缺失资源
export interface MissingResource {
  name: string
  type: string
  service?: string
  suggestion?: string
  canAutoCreate: boolean
}

// 克隆 Compose 文件请求
export interface CloneComposeFileRequest {
  newName: string
  description?: string
}

// 批量 Compose 操作请求
export interface BatchComposeOperationRequest {
  fileIds: string[]
  operation: string
  parameters?: Record<string, any>
}