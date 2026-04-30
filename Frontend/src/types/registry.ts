// 仓库类型枚举
export enum RegistryType {
  Private = 0,  // 私有仓库
  Mirror = 1,   // 镜像加速器
  DockerHub = 2 // Docker Hub
}

// 仓库状态枚举
export enum RegistryStatus {
  Unknown = 'Unknown',
  Online = 'Online',
  Offline = 'Offline',
  Error = 'Error',
  Connecting = 'Connecting',
  Authenticating = 'Authenticating'
}

// 认证配置
export interface AuthConfig {
  type: string
  username?: string
  password?: string
  token?: string
  refreshToken?: string
  tokenExpiresAt?: string
  clientId?: string
  clientSecret?: string
  scope?: string
  authUrl?: string
  tokenUrl?: string
  headers?: Record<string, string>
  parameters?: Record<string, string>
  autoRefresh?: boolean
}

// 仓库配置
export interface RegistryConfig {
  apiVersion: string
  namespace?: string
  timeout: number
  maxRetries: number
  allowInsecure: boolean
  plainHttp: boolean
  mirrorOf?: string
  mirrors: string[]
  customHeaders: Record<string, string>
  features: Record<string, any>
  rateLimit?: RateLimitConfig
  proxy?: ProxyConfig
}

// 速率限制配置
export interface RateLimitConfig {
  requestsPerSecond: number
  burstSize: number
  enabled: boolean
}

// 代理配置
export interface ProxyConfig {
  httpProxy?: string
  httpsProxy?: string
  noProxy?: string
  proxyUsername?: string
  proxyPassword?: string
}

// 镜像仓库信息
export interface ImageRegistry {
  id: string
  name: string
  description: string
  /** 仓库域名，如 registry-1.docker.io */
  domain: string
  /** 完整 URL（自动根据 isSecure 生成） */
  url: string
  /** 仓库类型：Private=私有仓库，Mirror=镜像加速器，DockerHub=Docker Hub */
  registryType: RegistryType
  isDefault: boolean
  isPublic: boolean
  /** 是否使用 HTTPS */
  isSecure: boolean
  username: string
  sslVerify?: boolean
  authConfig?: AuthConfig
  config?: RegistryConfig
  status: RegistryStatus
  createdAt: string
  updatedAt: string
  lastConnected?: string
  createdBy: string
  updatedBy: string
  metadata: Record<string, any>
  credentials?: RegistryCredential[]
  statistics?: RegistryStatistics
}

// 仓库凭证
export interface RegistryCredential {
  id: string
  registryId: string
  username: string
  email?: string
  password: string
  token?: string
  expiresAt?: string
  isActive: boolean
  createdAt: string
  updatedAt: string
  createdBy: string
  permissions: Record<string, string>
}

// 创建仓库请求
export interface CreateRegistryRequest {
  name: string
  description?: string
  /** 仓库域名，如 registry-1.docker.io，不要包含 http:// 或 https:// */
  domain: string
  username?: string
  password?: string
  email?: string
  isPublic?: boolean
  /** 是否使用 HTTPS，默认 true */
  isSecure?: boolean
  isDefault?: boolean
  /** 仓库类型：Private=私有仓库，Mirror=镜像加速器 */
  registryType?: RegistryType
}

// 更新仓库请求
export interface UpdateRegistryRequest {
  name?: string
  description?: string
  /** 仓库域名，如 registry-1.docker.io，不要包含 http:// 或 https:// */
  domain?: string
  username?: string
  password?: string
  isPublic?: boolean
  /** 是否使用 HTTPS */
  isSecure?: boolean
  isDefault?: boolean
  registryType?: RegistryType
}

// 仓库连接测试结果
export interface RegistryTestResult {
  isConnected: boolean
  message: string
  registryUrl: string
  testTime: string
  responseTimeMs: number
  details: Record<string, any>
}

// 仓库认证结果
export interface RegistryAuthResult {
  isValid: boolean
  message: string
  authType: string
  testTime: string
  token?: string
  expiresAt?: string
}

// 仓库登录请求
export interface RegistryLoginRequest {
  registryId: string
  username?: string
  password?: string
  token?: string
  saveCredentials?: boolean
  additionalParams?: Record<string, string>
}

// 镜像推送请求
export interface PushImageRequest {
  imageName: string
  tag?: string
  registryId: string
  targetRepository?: string
  targetTag?: string
  force?: boolean
  labels?: Record<string, string>
  nodeId?: string
}

// 镜像拉取请求
export interface PullImageRequest {
  imageName: string
  tag?: string
  registryId: string
  localName?: string
  localTag?: string
  force?: boolean
  platform?: string
  nodeId?: string
}

// 仓库搜索请求
export interface SearchRegistryRequest {
  registryId: string
  query: string
  limit?: number
  offset?: number
  includeOfficial?: boolean
  includeAutomated?: boolean
  sortBy?: string
  sortOrder?: string
}

// 仓库搜索结果
export interface RegistrySearchResult {
  registryId: string
  registryName: string
  query: string
  totalCount: number
  returnedCount: number
  offset: number
  results: RegistryImage[]
  facets: Record<string, any>
  searchTime: string
  duration: number
}

// 仓库镜像信息
export interface RegistryImage {
  id: string
  name: string
  repository: string
  tag: string
  digest: string
  size: number
  createdAt: string
  lastUpdated: string
  registryId: string
  registryName: string
  isOfficial: boolean
  starCount: number
  description: string
  tags: string[]
}

// 镜像标签信息
export interface RegistryImageTag {
  name: string
  digest: string
  size: number
  createdAt: string
  lastUpdated: string
  isLatest: boolean
  platforms: string[]
  metadata: Record<string, any>
}

// 镜像详情
export interface RegistryImageDetail {
  id: string
  name: string
  repository: string
  description: string
  tags: RegistryImageTag[]
  totalSize: number
  createdAt: string
  lastUpdated: string
  registryId: string
  registryName: string
  isOfficial: boolean
  isAutomated: boolean
  starCount: number
  downloadCount: number
  license?: string
  platforms: string[]
  labels: Record<string, string>
  architectures: string[]
  layers: RegistryImageLayer[]
}

// 镜像层信息
export interface RegistryImageLayer {
  digest: string
  size: number
  mediaType?: string
  createdAt: string
  command?: string
}

// 仓库操作结果
export interface RegistryOperationResult {
  registryId: string
  operation: string
  success: boolean
  message: string
  startTime: string
  endTime: string
  duration: number
  details: Record<string, any>
  errors: string[]
  warnings: string[]
}

// 仓库健康检查结果
export interface RegistryHealthCheckResult {
  registryId: string
  isHealthy: boolean
  status: string
  message: string
  checkTime: string
  responseTimeMs: number
  version: string
  availableApis: string[]
  metrics: Record<string, any>
  issues: HealthCheckIssue[]
}

// 健康检查问题
export interface HealthCheckIssue {
  type: string
  code: string
  message: string
  component?: string
  details: Record<string, any>
}

// 镜像扫描结果
export interface RegistryImageScanResult {
  registryId: string
  imageName: string
  tag: string
  scanTime: string
  status: ScanStatus
  criticalVulnerabilities: number
  highVulnerabilities: number
  mediumVulnerabilities: number
  lowVulnerabilities: number
  vulnerabilities: SecurityVulnerability[]
  metadata: Record<string, any>
}

// 扫描状态
export enum ScanStatus {
  Pending = 'Pending',
  Scanning = 'Scanning',
  Completed = 'Completed',
  Failed = 'Failed',
  Skipped = 'Skipped'
}

// 安全漏洞
export interface SecurityVulnerability {
  id: string
  title: string
  description: string
  severity: string
  package: string
  version: string
  fixedVersion?: string
  cves: string[]
  references?: string
  publishedAt: string
  updatedAt: string
}

// 仓库访问日志
export interface RegistryAccessLog {
  id: string
  registryId: string
  action: string
  imageName: string
  tag?: string
  userId?: string
  username?: string
  ipAddress?: string
  userAgent?: string
  success: boolean
  errorMessage?: string
  responseTimeMs: number
  transferBytes: number
  timestamp: string
  metadata: Record<string, any>
}

// 仓库同步结果
export interface RegistrySyncResult {
  registryId: string
  registryName: string
  totalImages: number
  syncedImages: number
  newImages: number
  updatedImages: number
  skippedImages: number
  errors: SyncError[]
  syncTime: string
  syncDuration: number
  isSuccess: boolean
  summary: string
}

// 同步错误
export interface SyncError {
  imageName: string
  imageTag: string
  errorType: string
  message: string
  errorTime: string
  stackTrace?: string
}

// 仓库统计数据
export interface RegistryStatistics {
  registryId: string
  registryName: string
  totalImages: number
  totalSize: number
  repositories: number
  officialImages: number
  privateImages: number
  lastSync: string
  syncCount: number
  isHealthy: boolean
  syncSuccessRate: number
  topRepositories: string[]
  imageCountByTag: Record<string, number>
  lastUpdated: string
}

// 仓库配置模板
export interface RegistryConfigTemplate {
  registryType: string
  name: string
  description: string
  defaultConfig: RegistryConfig
  fields: TemplateField[]
  supportedAuthTypes: string[]
  examples: Record<string, string>
}

// 模板字段
export interface TemplateField {
  name: string
  label: string
  type: string
  required: boolean
  defaultValue?: string
  placeholder?: string
  description?: string
  options?: string[]
  validation?: Record<string, any>
}

// 批量仓库操作请求
export interface BatchRegistryOperationRequest {
  registryId: string
  operation: string
  images: BatchImageOperation[]
  parameters?: Record<string, any>
  continueOnError?: boolean
  maxConcurrency?: number
}

// 批量镜像操作
export interface BatchImageOperation {
  imageName: string
  tag?: string
  targetName?: string
  targetTag?: string
  parameters?: Record<string, any>
}

// 批量仓库操作结果
export interface BatchRegistryOperationResult {
  registryId: string
  operation: string
  startTime: string
  endTime: string
  duration: number
  totalImages: number
  successfulOperations: number
  failedOperations: number
  results: BatchOperationItem[]
  globalErrors: string[]
  summary: Record<string, any>
}

// 批量操作项
export interface BatchOperationItem {
  imageName: string
  tag?: string
  success: boolean
  message: string
  error?: string
  startTime: string
  endTime: string
  duration: number
  details: Record<string, any>
}

// 仓库使用统计
export interface RegistryUsageStats {
  registryId: string
  period: string
  startTime: string
  endTime: string
  totalPulls: number
  totalPushes: number
  totalBandwidth: number
  uniqueUsers: number
  totalImages: number
  totalSize: number
  dailyPulls: Record<string, number>
  dailyPushes: Record<string, number>
  topPulledImages: TopImage[]
  topPushedImages: TopImage[]
  topUsers: TopUser[]
}

// 热门镜像
export interface TopImage {
  name: string
  count: number
  size: number
  percentage: number
}

// 热门用户
export interface TopUser {
  username: string
  operationCount: number
  bandwidthUsage: number
}

// 仓库清理策略
export interface RegistryCleanupPolicy {
  retentionDays: number
  maxTagsPerImage: number
  protectedTags: string[]
  excludeImages: string[]
  deleteUntagged: boolean
  dryRun: boolean
  minSizeBytes: number
}

// 仓库清理结果
export interface RegistryCleanupResult {
  registryId: string
  startTime: string
  endTime: string
  duration: number
  totalImagesScanned: number
  tagsDeleted: number
  spaceFreed: number
  deletedItems: CleanupItem[]
  errors: string[]
  warnings: string[]
  isDryRun: boolean
}

// 清理项
export interface CleanupItem {
  imageName: string
  tag: string
  size: number
  createdAt: string
  reason: string
}

// 仓库同步配置
export interface RegistrySyncConfig {
  registryId: string
  enabled: boolean
  syncMode: string
  sourceRegistryId?: string
  includeImages: string[]
  excludeImages: string[]
  schedule: string
  syncTags: boolean
  syncMetadata: boolean
  maxConcurrency: number
  options: Record<string, any>
  createdAt: string
  updatedAt: string
  lastSync?: string
  lastSyncStatus?: string
}

// 仓库扫描配置
export interface RegistryScanConfig {
  registryId: string
  enabled: boolean
  scannerType: string
  schedule: string
  includeImages: string[]
  excludeImages: string[]
  severityLevels: string[]
  scanOnPush: boolean
  failOnCritical: boolean
  scannerOptions: Record<string, any>
  createdAt: string
  updatedAt: string
  lastScan?: string
  lastScanStatus?: string
}