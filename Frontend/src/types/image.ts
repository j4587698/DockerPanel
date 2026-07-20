// ==================== 镜像管理相关类型定义 ====================

import { BaseConfig, Status, Priority, ResourceLimits, EnvironmentVariable, PaginationParams, PaginationResponse, OperationResult } from './common'

// 镜像基本信息 - 与后端 ImageInfo 对齐
export interface ImageInfo {
  id: string
  repository: string
  tag: string
  digest: string
  size: number
  createdAt: string
  created: string
  createdBy: string
  repoTags: string[]
  tags: string[]
  labels: Record<string, string>
  nodeId: string
  nodeName: string
  architecture: string
  os: string
}

// 镜像详细信息 - 与后端 ImageDetailInfo 对齐
export interface ImageDetailInfo extends ImageInfo {
  architecture: string
  os: string
  variant: string
  author: string
  comment: string
  config: string
  parent: string
  rootFS: string[]
  virtualSize: number
  sizeDelta: number
  // 镜像配置信息 - 用于创建容器时预填充
  exposedPorts: string[]
  volumes: string[]
  env: string[]
  workingDir?: string
  entrypoint?: string[]
  cmd?: string[]
  user?: string
}

// 镜像基础信息
export interface Image extends BaseConfig {
  repository: string
  tag: string
  digest: string
  size: number
  created: string
  createdBy: string
  repoTags: string[]
  labels: Record<string, string>
  nodeId: string
  nodeName: string
  architecture?: string
  os?: string
  variant?: string
  author?: string
  comment?: string
  config?: string
  parent?: string
  rootFS?: string[]
  virtualSize?: number
  sizeDelta?: number
  status?: 'available' | 'downloading' | 'uploading' | 'error'
  downloadProgress?: number
  errorMessage?: string
  layers: ImageLayer[]
  history: ImageHistoryEntry[]
  metadata: ImageMetadata
}

// 镜像详细信息
export interface ImageDetail extends Image {
  architecture: string
  os: string
  variant: string
  author: string
  comment: string
  config: ImageConfig
  parent: string
  rootFS: RootFS
  virtualSize: number
  sizeDelta: number
  container: string
  containerConfig: ContainerConfig
  dockerVersion: string
  graphDriver: GraphDriver
  osVersion: string
  size: number
  manifest: string
  platform: ImagePlatform
}

// 镜像配置
export interface ImageConfig {
  hostname?: string
  domainname?: string
  user?: string
  attachStdin: boolean
  attachStdout: boolean
  attachStderr: boolean
  exposedPorts?: Record<string, any>
  tty: boolean
  openStdin: boolean
  stdinOnce: boolean
  env?: string[]
  cmd?: string[]
  image?: string
  volumes?: Record<string, any>
  workingDir?: string
  entrypoint?: string[]
  networkDisabled?: boolean
  macAddress?: string
  onBuild?: string[]
  labels?: Record<string, string>
  stopSignal?: string
  stopTimeout?: number
  shell?: string[]
  argsEscaped?: boolean
  healthcheck?: ImageHealthConfig
}

// 容器配置（镜像内部）
export interface ContainerConfig {
  hostname?: string
  domainname?: string
  user?: string
  attachStdin: boolean
  attachStdout: boolean
  attachStderr: boolean
  exposedPorts?: Record<string, any>
  tty: boolean
  openStdin: boolean
  stdinOnce: boolean
  env?: string[]
  cmd?: string[]
  image?: string
  volumes?: Record<string, any>
  workingDir?: string
  entrypoint?: string[]
  networkDisabled?: boolean
  macAddress?: string
  onBuild?: string[]
  labels?: Record<string, string>
  stopSignal?: string
  stopTimeout?: number
  shell?: string[]
}

// 根文件系统
export interface RootFS {
  type: string
  layers: string[]
}

// 图形驱动信息
export interface GraphDriver {
  name: string
  data: Record<string, string>
}

// 镜像平台信息
export interface ImagePlatform {
  architecture: string
  os: string
  variant?: string
  osVersion?: string
  osFeatures?: string[]
  features?: string[]
}

// 镜像健康检查配置
export interface ImageHealthConfig {
  test: string[]
  interval: number
  timeout: number
  retries: number
  startPeriod: number
}

// 镜像层级信息
export interface ImageLayer {
  id: string
  digest: string
  size: number
  createdAt: string
  createdBy: string
  tags: string[]
  comment: string
  parentId?: string
  emptyLayer: boolean
  command: string
  author?: string
}

// 镜像历史记录
export interface ImageHistoryEntry {
  id: string
  created: string
  createdBy: string
  tags: string[]
  size: number
  comment: string
  layer?: string
  author?: string
}

// 镜像元数据
export interface ImageMetadata {
  manifest: ImageManifest
  config: ImageConfig
  layers: ImageLayerInfo[]
  annotations?: Record<string, string>
  version?: string
  schemaVersion?: number
  mediaType?: string
}

// 镜像清单
export interface ImageManifest {
  schemaVersion: number
  mediaType: string
  config: {
    mediaType: string
    size: number
    digest: string
  }
  layers: Array<{
    mediaType: string
    size: number
    digest: string
    urls?: string[]
    annotations?: Record<string, string>
  }>
  annotations?: Record<string, string>
  platform?: ImagePlatform
}

// 镜像层级详细信息
export interface ImageLayerInfo {
  digest: string
  size: number
  command: string
  created: string
  parentId?: string
  comment?: string
  author?: string
  emptyLayer: boolean
  diffId?: string
  mediaType?: string
  blobSum?: string
}

// 镜像搜索结果
export interface ImageSearchResult {
  name: string
  description: string
  stars: number
  official: boolean
  automated: boolean
  isOfficial: boolean
  isAutomated: boolean
  isPrivate: boolean
  pullCount?: number
  starCount?: number
  summary?: string
  tags?: string[]
  architectures?: string[]
  os?: string[]
  size?: number
  lastUpdated?: string
}

// 镜像构建请求
export interface BuildImageRequest {
  dockerfileName?: string
  contextPath?: string
  repository?: string
  tag?: string
  buildArgs: Record<string, string>
  labels: Record<string, string>
  noCache: boolean
  removeIntermediateContainers: boolean
  nodeId: string
  dockerfile?: string
  context?: string
  tags?: string[]
  remove?: boolean
  forceRemove?: boolean
  pull?: boolean
  networkMode?: string
  platform?: string
  target?: string
  outputs?: ImageBuildOutput[]
  buildContext?: string
  remote?: string
  authConfigs?: Record<string, ImageAuthConfig>
  secrets?: ImageBuildSecret[]
  ssh?: ImageBuildSSH[]
  ulimits?: ImageUlimit[]
  memory?: number
  memorySwap?: number
  cpuShares?: number
  cpuPeriod?: number
  cpuQuota?: number
  cpusetCpus?: string
  cpusetMems?: string
  cgroupParent?: string
  isolation?: string
  shmsize?: number
  squash?: boolean
  labels?: Record<string, string>
  cacheFrom?: string[]
  securityOpt?: string[]
  extraHosts?: string[]
  session?: string
  version?: string
}

// 镜像构建输出
export interface ImageBuildOutput {
  type: string
  dest?: string
}

// 镜像认证配置
export interface ImageAuthConfig {
  username?: string
  password?: string
  auth?: string
  email?: string
  serveraddress?: string
  identitytoken?: string
  registrytoken?: string
}

// 镜像构建密钥
export interface ImageBuildSecret {
  id: string
  src?: string
  target?: string
  env?: string
}

// 镜像构建SSH
export interface ImageBuildSSH {
  id: string
  paths?: string[]
  passphrase?: string
}

// 镜像资源限制
export interface ImageUlimit {
  name: string
  soft: number
  hard: number
}

// 镜像构建结果
export interface ImageBuildResult {
  imageId: string
  buildLog: string[]
  warnings: string[]
  errors: string[]
  buildTime: number
  size: number
  tags: string[]
  intermediateImages: string[]
  cacheHits: number
  cacheMisses: number
  buildSteps: ImageBuildStep[]
}

// 镜像构建步骤
export interface ImageBuildStep {
  step: number
  command: string
  output: string[]
  duration: number
  size: number
  intermediateContainerId?: string
  cacheHit: boolean
}

// 镜像导入请求
export interface ImportImageRequest {
  source: string
  sourceType: 'file' | 'url' | 'tar'
  repository?: string
  tag?: string
  changes?: string[]
  message?: string
  platform?: string
  from?: string
  fromSrc?: string
  repo?: string
  tag?: string
}

// 镜像导出请求
export interface ExportImageRequest {
  images: string[]
  format?: 'tar' | 'tar.gz'
  output?: string
  compress?: boolean
  includeManifest?: boolean
  includeConfig?: boolean
  platform?: string
}

// 镜像标签请求
export interface TagImageRequest {
  sourceImage: string
  targetRepository: string
  targetTag?: string
  force?: boolean
}

// 镜像推送请求
export interface PushImageRequest {
  image: string
  tag?: string
  registry?: string
  credentials?: {
    username: string
    password: string
    serverAddress?: string
    identitytoken?: string
    registrytoken?: string
  }
  platform?: string
  all?: boolean
}

// 镜像拉取请求
export interface PullImageRequest {
  imageName: string
  tag?: string
  registry?: string
  nodeId?: string
  connectionId?: string // 添加 SignalR 连接 ID
  credentials?: {
    username: string
    password: string
    serverAddress?: string
    identitytoken?: string
    registrytoken?: string
  }
  platform?: string
  all?: boolean
  policy?: 'always' | 'missing' | 'never'
}

// 镜像拉取进度条目
export interface ImagePullProgress {
  id: string
  status: string
  progressDetail: string
  current: number
  total: number
}

// 单个镜像层拉取进度
export interface PullLayer {
  layerId: string
  status: string
  current: number
  total: number
  progress: number
}

// 镜像清理选项
export interface PruneOptions {
  dangling: boolean
  all: boolean
  filter?: string
  keepUntil: boolean
  keepUntilDuration?: string
}

export interface ImagePruneOptions {
  dangling?: boolean
  all?: boolean
  filters?: Record<string, string[]>
}

// 镜像清理结果
export interface PruneResult {
  imagesDeleted: number
  spaceReclaimed: number
  deletedImageIds: string[]
  errors: string[]
}

export interface ImagePruneResult {
  imagesDeleted: Array<{
    id: string
    untagged?: string[]
    deleted?: string[]
  }>
  spaceReclaimed: number
  deletedImageIds: string[]
  errors: string[]
}

// 批量操作相关
export interface BatchRemoveImagesRequest {
  imageIds: string[]
  force: boolean
  nodeId: string
  pruneChildren?: boolean
}

export interface BatchOperationResult {
  totalCount: number
  successCount: number
  failureCount: number
  errors: OperationError[]
}

export interface OperationError {
  id: string
  message: string
  errorType: string
}

// 镜像统计信息
export interface ImageStatistics {
  totalImages: number
  totalSize: number
  imagesWithTags: number
  danglingImages: number
  imagesByRepository: Record<string, number>
  sizeByRepository: Record<string, number>
  lastUpdated: string
  nodeId: string
  totalLayers?: number
  averageSize?: number
  largestImage?: Image
  smallestImage?: Image
  imagesByTag?: Record<string, number>
  imagesByArchitecture?: Record<string, number>
  imagesByOS?: Record<string, number>
  imagesByAge?: Record<string, number>
  storageUsage?: ImageStorageUsage
}

// 镜像存储使用情况
export interface ImageStorageUsage {
  total: number
  active: number
  reclaimable: number
  reclaimableSize: number
  layers: number
  layerSize: number
  buildCache: number
  buildCacheSize: number
}

// 镜像仓库相关
export interface ImageRegistry extends BaseConfig {
  url: string
  username: string
  isDefault: boolean
  isSecure: boolean
  isPrivate: boolean
  isOfficial: boolean
  supportsMirroring: boolean
  supportsAuthentication: boolean
  supportsSearch: boolean
  tags?: string[]
  description?: string
  documentationUrl?: string
  contact?: string
  location?: string
  official?: boolean
  stars?: number
  pulls?: number
  lastChecked?: string
  status: 'active' | 'inactive' | 'error'
  errorMessage?: string
  credentials?: RegistryCredentials
}

export interface RegistryCredentials {
  username: string
  password: string
  email?: string
  auth?: string
  identityToken?: string
  registryToken?: string
}

export interface CreateRegistryRequest {
  name: string
  url: string
  username: string
  password: string
  isSecure: boolean
  isDefault: boolean
  description?: string
  tags?: string[]
}

export interface UpdateRegistryRequest {
  name?: string
  url?: string
  username?: string
  password?: string
  isSecure?: boolean
  isDefault?: boolean
  description?: string
  tags?: string[]
}

// 镜像扫描结果
export interface ImageScanResult {
  imageId: string
  scanTime: string
  vulnerabilities: Vulnerability[]
  totalVulnerabilities: number
  severityCounts: Record<string, number>
  recommendations: string[]
  scanId?: string
  scanner?: string
  scannerVersion?: string
  policy?: ScanPolicy
  compliance?: ComplianceResult
}

export interface Vulnerability {
  id: string
  severity: 'low' | 'medium' | 'high' | 'critical'
  description: string
  package: string
  version: string
  fixedVersion?: string
  link?: string
  cvss?: CVSSScore
  cwe?: string[]
  publishedDate?: string
  lastModifiedDate?: string
  references?: string[]
  vendor?: string
  product?: string
}

export interface CVSSScore {
  baseScore: number
  impactScore: number
  exploitabilityScore: number
  severity: string
  vector: string
}

export interface ScanPolicy {
  name: string
  version: string
  rules: ScanRule[]
  failOn?: string[]
  onlyFixed?: boolean
}

export interface ScanRule {
  id: string
  name: string
  description: string
  severity: string
  condition: string
  action: 'allow' | 'deny' | 'warn'
}

export interface ComplianceResult {
  compliant: boolean
  score: number
  standards: ComplianceStandard[]
}

export interface ComplianceStandard {
  name: string
  version: string
  compliant: boolean
  score: number
  findings: ComplianceFinding[]
}

export interface ComplianceFinding {
  id: string
  title: string
  description: string
  severity: string
  recommendation: string
}

// 镜像验证结果
export interface ImageValidationResult {
  isValid: boolean
  errors: string[]
  warnings: string[]
  checksum?: string
  size?: number
  architecture?: string
  os?: string
  validationTime: string
  validator?: string
  version?: string
  signed?: boolean
  signature?: ImageSignature
  trust?: ImageTrust
}

export interface ImageSignature {
  keyId: string
  signature: string
  algorithm: string
  timestamp: string
  signer?: string
}

export interface ImageTrust {
  trusted: boolean
  trustLevel: 'low' | 'medium' | 'high'
  trustChain?: TrustChainItem[]
  notaryUrl?: string
}

export interface TrustChainItem {
  name: string
  publicKey: string
  role: string
  status: 'valid' | 'invalid' | 'unknown'
}

// 镜像备份相关
export interface ImageBackupRequest {
  imageId: string
  repository?: string
  tag?: string
  includeLayers?: boolean
  compression?: 'none' | 'gzip' | 'bzip2' | 'xz'
  outputFormat?: 'tar' | 'tar.gz' | 'tar.bz2' | 'tar.xz'
  includeConfig?: boolean
  includeManifest?: boolean
}

export interface ImageBackupResult {
  success: boolean
  imageId: string
  backupId: string
  backupPath?: string
  backupSize?: number
  backupFormat: string
  createdAt: string
  errorMessage?: string
  checksum?: string
  layers?: number
  compressionRatio?: number
}

export interface ImageRestoreRequest {
  backupPath?: string
  backupData?: string
  repository?: string
  tag?: string
  force?: boolean
  nodeId?: string
}

export interface ImageRestoreResult {
  success: boolean
  imageId?: string
  imageName?: string
  restoredAt: string
  errorMessage?: string
  warnings?: string[]
  checksum?: string
}

// 镜像安全扫描配置
export interface ImageScanConfig {
  enabled: boolean
  schedule: string
  severityThreshold: 'low' | 'medium' | 'high' | 'critical'
  scanners: string[]
  ignoreUnfixed: boolean
  notifications: {
    email?: string
    webhook?: string
    slack?: string
  }
  policies: ScanPolicy[]
  exclusions: ScanExclusion[]
}

export interface ScanExclusion {
  type: 'package' | 'cve' | 'path'
  value: string
  reason?: string
}

// 镜像优化建议
export interface ImageOptimizationResult {
  originalSize: number
  optimizedSize: number
  spaceSaved: number
  optimizationRatio: number
  recommendations: OptimizationRecommendation[]
  layers: LayerOptimization[]
  dockerfileOptimizations?: DockerfileOptimization[]
}

export interface OptimizationRecommendation {
  type: 'layer' | 'dependency' | 'base' | 'config' | 'security' | 'performance'
  impact: 'low' | 'medium' | 'high'
  description: string
  implementation: string
  estimatedSavings?: number
}

export interface LayerOptimization {
  layerId: string
  originalSize: number
  optimizedSize: number
  savings: number
  recommendations: string[]
  canCache: boolean
  duplicateData: boolean
}

export interface DockerfileOptimization {
  line: number
  instruction: string
  current: string
  recommended: string
  reason: string
  impact: string
}

// 镜像使用情况
export interface ImageUsage {
  containersCount: number
  containers: ContainerUsage[]
  size: number
  lastUsed?: string
  usageFrequency: number
  popularityScore: number
}

export interface ContainerUsage {
  id: string
  name: string
  status: string
  created: string
  nodeId: string
  nodeName: string
}

// 镜像事件
export interface ImageEvent {
  id: string
  action: string
  actor: {
    id: string
    attributes: Record<string, string>
  }
  scope: string
  time: number
  timeNano: number
  type: string
}

// 镜像搜索和过滤
export interface ImageSearchParams extends PaginationParams {
  name?: string
  tag?: string
  repository?: string
  nodeId?: string
  label?: string
  before?: string
  since?: string
  dangling?: boolean
  filters?: Record<string, string[]>
  sort?: 'created' | 'size' | 'repository' | 'tag'
  order?: 'asc' | 'desc'
}

export interface ImageSearchResponse {
  images: Image[]
  total: number
  filtered: number
  page: number
  pageSize: number
  totalPages?: number
  hasNext?: boolean
  hasPrev?: boolean
}

// 镜像模板
export interface ImageTemplate extends BaseConfig {
  baseImage: string
  dockerfile: string
  buildArgs: Record<string, string>
  labels: Record<string, string>
  env: EnvironmentVariable[]
  exposedPorts: Record<string, string>
  volumes: Record<string, string>
  workdir: string
  user: string
  cmd: string[]
  entrypoint: string[]
  category: string
  tags: string[]
  description: string
  readme?: string
  parameters: ImageTemplateParameter[]
  dependencies: ImageTemplateDependency[]
  platforms: string[]
  architectures: string[]
}

export interface ImageTemplateParameter {
  name: string
  label: string
  type: 'string' | 'number' | 'boolean' | 'select' | 'multiselect'
  description?: string
  required: boolean
  defaultValue?: any
  options?: Array<{ label: string; value: any }>
  validation?: {
    min?: number
    max?: number
    pattern?: string
    message?: string
  }
}

export interface ImageTemplateDependency {
  type: 'image' | 'package' | 'service'
  name: string
  version?: string
  condition: string
  optional: boolean
}

// 镜像市场
export interface ImageMarketplace {
  name: string
  description: string
  url: string
  images: MarketplaceImage[]
  categories: MarketplaceCategory[]
  featured: MarketplaceImage[]
  popular: MarketplaceImage[]
  newest: MarketplaceImage[]
  updated: string
}

export interface MarketplaceImage {
  id: string
  name: string
  description: string
  shortDescription: string
  version: string
  tags: string[]
  category: string
  subcategory: string
  author: string
  publisher: string
  license: string
  downloads: number
  rating: number
  reviews: number
  size: number
  platforms: string[]
  architectures: string[]
  repository: string
  tags: string[]
  documentationUrl?: string
  sourceUrl?: string
  homepage?: string
  logo?: string
  screenshots: string[]
  changelog?: string
  requirements?: string[]
  dependencies?: string[]
  security?: ImageSecurityInfo
  pricing?: ImagePricingInfo
  support?: ImageSupportInfo
  lastUpdated: string
  createdAt: string
  isVerified: boolean
  isOfficial: boolean
  isRecommended: boolean
}

export interface MarketplaceCategory {
  id: string
  name: string
  description: string
  icon: string
  imageCount: number
  subcategories: MarketplaceCategory[]
}

export interface ImageSecurityInfo {
  scanned: boolean
  lastScanDate: string
  vulnerabilities: number
  criticalVulnerabilities: number
  highVulnerabilities: number
  mediumVulnerabilities: number
  lowVulnerabilities: number
  scanReportUrl?: string
}

export interface ImagePricingInfo {
  type: 'free' | 'paid' | 'freemium' | 'subscription'
  currency?: string
  price?: number
  billingPeriod?: string
  features?: string[]
  limitations?: string[]
}

export interface ImageSupportInfo {
  type: 'community' | 'commercial' | 'enterprise'
  level: 'basic' | 'standard' | 'premium'
  responseTime?: string
  channels?: string[]
  documentation?: string
  community?: string
}