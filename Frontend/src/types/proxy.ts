/**
 * 代理管理类型定义
 * 对应后端DockerPanel.API.Models.Proxy命名空间下的所有类型
 * 基于YARP (Yet Another Reverse Proxy) 的完整代理管理系统
 */

// ==================== 基础代理类型 ====================

/**
 * 代理配置信息
 */
export interface ProxyConfig {
  id: string
  name: string
  description: string
  enabled: boolean
  createdAt: string
  updatedAt: string
  routes: ProxyRoutes
  clusters: ProxyClusters
  settings: ProxySettings
  tags: Record<string, string>
  version: string
  status: string
  healthStatus: ProxyHealthStatus
}

/**
 * 代理路由集合
 */
export interface ProxyRoutes {
  routes: ProxyRoute[]
  catchAll: ProxyRoute | null
}

/**
 * 代理路由
 */
export interface ProxyRoute {
  routeId: string
  clusterId: string
  match: ProxyRouteMatch
  path: string
  host: string
  method: string
  header: Record<string, string>
  transforms: ProxyTransform[]
  metadata: Record<string, any>
  priority: number
  enabled: boolean
  order: number
  authPolicy: string | null
  corsPolicy: string | null
  timeout: ProxyTimeout
}

/**
 * 路由匹配条件
 */
export interface ProxyRouteMatch {
  path: string
  host: string[]
  methods: string[]
  headers: Record<string, string[]>
  queryParameters: Record<string, string[]>
}

/**
 * 代理转换
 */
export interface ProxyTransform {
  pathPattern: string
  pathRewrite: string
  requestHeaders: Record<string, string>
  responseHeaders: Record<string, string>
  queryParameters: Record<string, string>
  responseTrailers: Record<string, string>
}

/**
 * 代理超时设置
 */
export interface ProxyTimeout {
  timeout: string
  activityTimeout: string
}

/**
 * 代理集群集合
 */
export interface ProxyClusters {
  clusters: ProxyCluster[]
}

/**
 * 代理集群
 */
export interface ProxyCluster {
  clusterId: string
  loadBalancingPolicy: string
  sessionAffinity: ProxySessionAffinity
  healthCheck: ProxyHealthCheck
  httpRequest: ProxyHttpRequest
  destinations: ProxyDestination[]
  metadata: Record<string, any>
  enabled: boolean
  version: string
}

/**
 * 会话亲和性
 */
export interface ProxySessionAffinity {
  enabled: boolean
  mode: string // Cookie, CustomHeader
  failurePolicy: string // Return503Error, Redistribute
  cookie: ProxySessionAffinityCookie
  customHeader: ProxySessionAffinityCustomHeader
}

/**
 * 会话亲和性Cookie配置
 */
export interface ProxySessionAffinityCookie {
  name: string
  path: string
  sameSite: string
  httpOnly: boolean
  secure: boolean
  domain: string
  maxAge: string
  expiration: string
  isEssential: boolean
}

/**
 * 会话亲和性自定义头配置
 */
export interface ProxySessionAffinityCustomHeader {
  name: string
}

/**
 * 健康检查
 */
export interface ProxyHealthCheck {
  enabled: boolean
  interval: string
  timeout: string
  port: number
  path: string
  expectedStatusCodes: number[]
  expectedHttpMethod: string
  headers: Record<string, string>
}

/**
 * HTTP请求配置
 */
export interface ProxyHttpRequest {
  version: string // Http1, Http2, Http3
  versionPolicy: string // RequestVersionOrLower, RequestVersionOrHigher
  activityTimeout: string
  versionUpgrade: ProxyVersionUpgrade
  headerEncoding: ProxyHeaderEncoding
}

/**
 * 版本升级配置
 */
export interface ProxyVersionUpgrade {
  enabled: boolean
  version: string
}

/**
 * 头编码配置
 */
export interface ProxyHeaderEncoding {
  method: string // Default, All
  allowed: string[]
}

/**
 * 代理目标
 */
export interface ProxyDestination {
  address: string
  health: ProxyDestinationHealth
  metadata: Record<string, any>
  weight: number
  priority: number
}

/**
 * 目标健康状态
 */
export interface ProxyDestinationHealth {
  active: boolean
  reason: string
  lastKnownActive: string
}

/**
 * 代理设置
 */
export interface ProxySettings {
  rateLimit: ProxyRateLimit
  circuitBreaker: ProxyCircuitBreaker
  retry: ProxyRetry
  timeout: ProxyTimeout
  compression: ProxyCompression
  authentication: ProxyAuthentication
  cors: ProxyCors
  forwarders: ProxyForwarders
  loadBalancing: ProxyLoadBalancing
  security: ProxySecurity
}

/**
 * 限流设置
 */
export interface ProxyRateLimit {
  enabled: boolean
  policy: string
  requestsPerSecond: number
  burst: number
  slidingWindow: ProxySlidingWindow
}

/**
 * 滑动窗口
 */
export interface ProxySlidingWindow {
  enabled: boolean
  window: string
  samples: number
}

/**
 * 熔断器设置
 */
export interface ProxyCircuitBreaker {
  enabled: boolean
  failureThreshold: number
  samplingDuration: string
  minimumThroughput: number
  durationOfBreak: string
}

/**
 * 重试设置
 */
export interface ProxyRetry {
  enabled: boolean
  maxRetries: number
  mode: string // Fixed, Exponential
  backoff: ProxyRetryBackoff
}

/**
 * 重试退避策略
 */
export interface ProxyRetryBackoff {
  mode: string // Fixed, Exponential
  delay: string
  maxDelay: string
  exponentialDelay: string
}

/**
 * 压缩设置
 */
export interface ProxyCompression {
  enabled: boolean
  responseCompressionEnabled: boolean
  providers: string[]
  mimeTypes: string[]
  enableForHttps: boolean
}

/**
 * 认证设置
 */
export interface ProxyAuthentication {
  enabled: boolean
  type: string // Basic, Bearer, ApiKey
  schemes: ProxyAuthenticationScheme[]
}

/**
 * 认证方案
 */
export interface ProxyAuthenticationScheme {
  name: string
  type: string
  parameters: Record<string, any>
}

/**
 * CORS设置
 */
export interface ProxyCors {
  enabled: boolean
  allowCredentials: boolean
  allowedHeaders: string[]
  allowedMethods: string[]
  allowedOrigins: string[]
  exposedHeaders: string[]
  maxAge: string
  preflightMaxAge: string
}

/**
 * 转发器设置
 */
export interface ProxyForwarders {
  enabled: boolean
  headers: string[]
}

/**
 * 负载均衡设置
 */
export interface ProxyLoadBalancing {
  algorithm: string // RoundRobin, LeastRequests, Random, PowerOfTwoChoices
  destinations: ProxyLoadBalancingDestination[]
}

/**
 * 负载均衡目标
 */
export interface ProxyLoadBalancingDestination {
  address: string
  weight: number
  health: ProxyDestinationHealth
}

/**
 * 安全设置
 */
export interface ProxySecurity {
  allowedHosts: string[]
  httpsRedirection: boolean
  hsts: ProxyHsts
  requestHeaders: Record<string, string>
  responseHeaders: Record<string, string>
}

/**
 * HSTS设置
 */
export interface ProxyHsts {
  enabled: boolean
  maxAge: string
  includeSubDomains: boolean
  preload: boolean
}

/**
 * 代理健康状态
 */
export interface ProxyHealthStatus {
  status: string // Healthy, Unhealthy, Degraded
  lastCheck: string
  message: string
  details: Record<string, any>
}

// ==================== SSL证书相关类型 ====================

/**
 * SSL证书信息
 */
export interface ProxySslCertificate {
  id: string
  name: string
  subject: string
  issuer: string
  notBefore: string
  notAfter: string
  thumbprint: string
  certificatePem: string
  privateKeyPem: string
  enabled: boolean
  status: string
  daysUntilExpiry: number
  isExpiringSoon: boolean
  isExpired: boolean
  certificateInfo: ProxyCertificateInfo
  validationStatus: ProxyCertificateValidation
  acmeProvider: string
  certificateSource: string // acme, upload
  createdAt: string
  updatedAt: string
}

/**
 * 证书信息
 */
export interface ProxyCertificateInfo {
  subject: string
  issuer: string
  serialNumber: string
  notBefore: string
  notAfter: string
  thumbprint: string
  signatureAlgorithm: string
  publicKey: string
  version: number
  extensions: ProxyCertificateExtension[]
}

/**
 * 证书扩展
 */
export interface ProxyCertificateExtension {
  oid: string
  critical: boolean
  value: string
}

/**
 * 证书验证状态
 */
export interface ProxyCertificateValidation {
  isValid: boolean
  status: string
  errorMessage?: string
  warnings: string[]
  validatedAt: string
}

// ==================== 统计和监控相关类型 ====================

/**
 * 代理统计信息
 */
export interface ProxyStatistics {
  totalRequests: number
  totalConnections: number
  activeConnections: number
  requestsPerSecond: number
  averageResponseTime: number
  errorRate: number
  statusCodes: Record<string, number>
  topRoutes: ProxyRouteStatistics[]
  topClusters: ProxyClusterStatistics[]
  bandwidth: ProxyBandwidthStatistics
  timeRange: string
  lastUpdated: string
}

/**
 * 路由统计
 */
export interface ProxyRouteStatistics {
  routeId: string
  path: string
  requests: number
  averageResponseTime: number
  errorRate: number
  statusCodes: Record<string, number>
}

/**
 * 集群统计
 */
export interface ProxyClusterStatistics {
  clusterId: string
  requests: number
  averageResponseTime: number
  errorRate: number
  activeDestinations: number
  totalDestinations: number
  destinationHealth: Record<string, ProxyDestinationHealth>
}

/**
 * 带宽统计
 */
export interface ProxyBandwidthStatistics {
  inboundBytes: number
  outboundBytes: number
  totalBytes: number
  averageBytesPerRequest: number
}

/**
 * 代理日志条目
 */
export interface ProxyLogEntry {
  id: string
  timestamp: string
  level: string // Debug, Info, Warning, Error, Critical
  message: string
  category: string
  routeId?: string
  clusterId?: string
  destinationAddress?: string
  statusCode?: number
  responseTime?: number
  userAgent?: string
  clientIp?: string
  method?: string
  path?: string
  queryString?: string
  referer?: string
  correlationId?: string
  metadata: Record<string, any>
}

/**
 * 代理健康检查结果
 */
export interface ProxyHealthCheckResult {
  status: string
  lastCheck: string
  responseTime: number
  details: Record<string, any>
  routes: Record<string, ProxyRouteHealth>
  clusters: Record<string, ProxyClusterHealth>
}

/**
 * 路由健康状态
 */
export interface ProxyRouteHealth {
  status: string
  lastCheck: string
  message: string
}

/**
 * 集群健康状态
 */
export interface ProxyClusterHealth {
  status: string
  lastCheck: string
  message: string
  destinations: Record<string, ProxyDestinationHealth>
}

// ==================== 请求/响应类型 ====================

/**
 * 创建代理配置请求
 */
export interface CreateProxyConfigRequest {
  name: string
  description: string
  routes: ProxyRoutes
  clusters: ProxyClusters
  settings: ProxySettings
  tags: Record<string, string>
}

/**
 * 更新代理配置请求
 */
export interface UpdateProxyConfigRequest {
  name: string
  description: string
  routes: ProxyRoutes
  clusters: ProxyClusters
  settings: ProxySettings
  tags: Record<string, string>
  version: string
}

/**
 * 代理测试请求
 */
export interface ProxyTestRequest {
  configId: string
  testUrl: string
  testMethod: string
  testHeaders: Record<string, string>
  testBody?: string
  timeout: number
}

/**
 * 代理测试结果
 */
export interface ProxyTestResult {
  success: boolean
  responseTime: number
  statusCode: number
  statusText: string
  responseHeaders: Record<string, string>
  responseBody: string
  error?: string
  testDetails: {
    url: string
    method: string
    startTime: string
    endTime: string
    duration: string
  }
}

/**
 * 上传SSL证书请求
 */
export interface UploadSslCertificateRequest {
  name: string
  certificateData: string
  privateKeyData: string
  password?: string
  enabled: boolean
  tags: Record<string, string>
}

/**
 * 更新SSL证书请求
 */
export interface UpdateSslCertificateRequest {
  name: string
  certificateData?: string
  privateKeyData?: string
  password?: string
  enabled: boolean
  tags: Record<string, string>
}

/**
 * SSL证书测试结果
 */
export interface ProxySslCertificateTestResult {
  certificateValidation: ProxyCertificateValidation
  connectionTest: ProxySslConnectionTest
  certificateChain: string[]
  warnings: string[]
  errors: string[]
  isValid: boolean
  testDetails: {
    host: string
    port: number
    startTime: string
    endTime: string
    duration: string
  }
}

/**
 * SSL连接测试
 */
export interface ProxySslConnectionTest {
  success: boolean
  error?: string
  protocol: string
  cipherSuite: string
  keyExchange: string
  serverKeySize: number
  clientKeySize: number
}

/**
 * 导出代理配置请求
 */
export interface ExportProxyConfigRequest {
  configId: string
  format: string // json, yaml, xml
  includeSecrets: boolean
  includeStatistics: boolean
}

/**
 * 导入代理配置请求
 */
export interface ImportProxyConfigRequest {
  configData: string
  format: string
  overwriteExisting: boolean
  validateOnly: boolean
}

/**
 * 导入代理配置结果
 */
export interface ImportProxyConfigResult {
  success: boolean
  configId?: string
  configName?: string
  warnings: string[]
  errors: string[]
  importedRoutes: number
  importedClusters: number
  importedSettings: boolean
  validationResults: ProxyConfigValidationResult[]
}

/**
 * 代理配置验证结果
 */
export interface ProxyConfigValidationResult {
  isValid: boolean
  category: string
  message: string
  details: Record<string, any>
  severity: string // Info, Warning, Error
}

/**
 * SSL配置
 */
export interface ProxySslConfig {
  enabled: boolean
  certificateId?: string
  protocols: string[]
  cipherSuites: string[]
  clientCertificates: string[]
  requireClientCertificate: boolean
  verifyCertificate: boolean
  verifyCertificateChain: boolean
  verifyCertificateRevocation: boolean
  checkCertificateRevocation: boolean
  renegotiation: string
}

/**
 * SSL证书到期提醒
 */
export interface ProxySslCertificateExpiryAlert {
  certificateId: string
  certificateName: string
  subject: string
  issuer: string
  notAfter: string
  daysUntilExpiry: number
  alertLevel: string // Info, Warning, Critical
  message: string
  recommendedAction: string
}

// ==================== 集群和路由相关类型 ====================

/**
 * 代理集群信息
 */
export interface ProxyClusterInfo {
  clusterId: string
  loadBalancingPolicy: string
  destinationCount: number
  activeDestinations: number
  healthStatus: string
  totalRequests: number
  averageResponseTime: number
  errorRate: number
  lastUpdated: string
}

/**
 * 代理路由信息
 */
export interface ProxyRouteInfo {
  routeId: string
  clusterId: string
  path: string
  host: string[]
  methods: string[]
  enabled: boolean
  priority: number
  totalRequests: number
  averageResponseTime: number
  errorRate: number
  statusCodes: Record<string, number>
  lastUpdated: string
}

// ==================== 原�有代理类型（保持向后兼容） ====================

/**
 * 代理基础信息（向后兼容）
 */
export interface Proxy {
  id: string
  name: string
  description: string
  enabled: boolean
  protocol: 'http' | 'https' | 'tcp' | 'udp'
  mode: 'reverse' | 'forward' | 'transparent'
  status: string
  config: ProxyConfig
  routes: ProxyRoute[]
  clusters: ProxyCluster[]
  transforms: ProxyTransform[]
  security: ProxySecurity
  monitoring: ProxyMonitoring
  performance: ProxyPerformance
  ssl: ProxySSL
  healthCheck: ProxyHealthCheck
  loadBalancing: ProxyLoadBalancing
  rateLimit: ProxyRateLimit
  caching: ProxyCaching
  compression: ProxyCompression
  logging: ProxyLogging
  metadata: ProxyMetadata
  usage: ProxyUsage
  nodeId?: string
  nodeName?: string
  createdAt: string
  updatedAt: string
}

/**
 * 代理状态（向后兼容）
 */
export interface ProxyStatus {
  status: 'active' | 'inactive' | 'error' | 'maintenance' | 'starting' | 'stopping'
  uptime: number
  lastStarted?: string
  lastStopped?: string
  errorMessage?: string
  health: 'healthy' | 'unhealthy' | 'degraded'
  connections: {
    active: number
    total: number
    failed: number
  }
  throughput: {
    requestsPerSecond: number
    bytesPerSecond: number
  }
}

/**
 * 代理监控（向后兼容）
 */
export interface ProxyMonitoring {
  enabled: boolean
  metrics: ProxyMetrics
  alerts: ProxyAlert[]
  logging: ProxyLogging
  tracing: ProxyTracing
  profiling: ProxyProfiling
}

/**
 * 代理性能（向后兼容）
 */
export interface ProxyPerformance {
  tuning: ProxyPerformanceTuning
  optimization: ProxyOptimization
  benchmarks: ProxyBenchmark[]
  recommendations: ProxyRecommendation[]
}

/**
 * 代理SSL（向后兼容）
 */
export interface ProxySSL {
  enabled: boolean
  certificates: ProxySSLCertificate[]
  protocols: string[]
  cipherSuites: string[]
  clientAuth: ProxySSLClientAuth
  ocsp: ProxySSLOCSP
  stapling: ProxySSLStapling
  session: ProxySSLSession
}

/**
 * 代理元数据（向后兼容）
 */
export interface ProxyMetadata {
  version: string
  tags: string[]
  categories: string[]
  description?: string
  documentation?: string
  contact?: ProxyContact
  license?: string
  changelog?: string
}

/**
 * 代理使用情况（向后兼容）
 */
export interface ProxyUsage {
  requests: ProxyUsageRequests
  bandwidth: ProxyUsageBandwidth
  connections: ProxyUsageConnections
  errors: ProxyUsageErrors
  performance: ProxyUsagePerformance
  lastUpdated: string
}

/**
 * 代理创建请求（向后兼容）
 */
export interface CreateProxyRequest {
  name: string
  domain: string
  targetHost: string
  targetPort: number
  listenPort: number
  protocol: 'http' | 'https' | 'tcp' | 'udp'
  mode: 'reverse' | 'forward' | 'transparent'
  enabled?: boolean
  config?: ProxyConfig
  routes?: ProxyRoute[]
  clusters?: ProxyCluster[]
  security?: ProxySecurity
  monitoring?: ProxyMonitoring
  ssl?: ProxySSL
  nodeId?: string
  description?: string
  labels?: Record<string, string>
}

/**
 * 代理更新请求（向后兼容）
 */
export interface UpdateProxyRequest {
  name?: string
  domain?: string
  targetHost?: string
  targetPort?: number
  listenPort?: number
  protocol?: 'http' | 'https' | 'tcp' | 'udp'
  mode?: 'reverse' | 'forward' | 'transparent'
  enabled?: boolean
  config?: ProxyConfig
  routes?: ProxyRoute[]
  clusters?: ProxyCluster[]
  security?: ProxySecurity
  monitoring?: ProxyMonitoring
  ssl?: ProxySSL
  description?: string
  labels?: Record<string, string>
}

/**
 * 代理统计信息（向后兼容）
 */
export interface ProxyStatisticsOld {
  totalProxies: number
  activeProxies: number
  inactiveProxies: number
  errorProxies: number
  proxiesByProtocol: Record<string, number>
  proxiesByMode: Record<string, number>
  proxiesByNode: Record<string, number>
  totalRequests: number
  totalBandwidth: number
  averageResponseTime: number
  errorRate: number
  uptime: number
  lastUpdated: string
  topProxies: ProxyUsageInfo[]
}

/**
 * 代理使用信息（向后兼容）
 */
export interface ProxyUsageInfo {
  proxyId: string
  proxyName: string
  requests: number
  bandwidth: number
  responseTime: number
  errorRate: number
}

/**
 * 代理事件（向后兼容）
 */
export interface ProxyEvent {
  id: string
  type: 'created' | 'updated' | 'deleted' | 'started' | 'stopped' | 'error' | 'health_check'
  proxyId: string
  proxyName: string
  timestamp: string
  message: string
  details?: Record<string, any>
  source: string
  nodeId?: string
  nodeName?: string
}

/**
 * 代理搜索和过滤（向后兼容）
 */
export interface ProxySearchParams {
  name?: string
  domain?: string
  protocol?: string
  mode?: string
  status?: string
  enabled?: boolean
  nodeId?: string
  targetHost?: string
  targetPort?: number
  listenPort?: number
  label?: string
  filters?: Record<string, string[]>
  sort?: 'created' | 'name' | 'domain' | 'status' | 'requests'
  order?: 'asc' | 'desc'
  page?: number
  pageSize?: number
}

/**
 * 代理搜索响应（向后兼容）
 */
export interface ProxySearchResponse {
  proxies: Proxy[]
  total: number
  filtered: number
  page: number
  pageSize: number
  totalPages?: number
  hasNext?: boolean
  hasPrev?: boolean
}

/**
 * 代理模板（向后兼容）
 */
export interface ProxyTemplate {
  id: string
  name: string
  domain: string
  targetHost: string
  targetPort: number
  listenPort: number
  protocol: 'http' | 'https' | 'tcp' | 'udp'
  mode: 'reverse' | 'forward' | 'transparent'
  config: ProxyConfig
  routes: ProxyRoute[]
  clusters: ProxyCluster[]
  security: ProxySecurity
  monitoring: ProxyMonitoring
  ssl: ProxySSL
  category: string
  tags: string[]
  description: string
  readme?: string
  parameters: ProxyTemplateParameter[]
  dependencies: ProxyTemplateDependency[]
  createdAt: string
  updatedAt: string
}

/**
 * 代理模板参数（向后兼容）
 */
export interface ProxyTemplateParameter {
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

/**
 * 代理模板依赖（向后兼容）
 */
export interface ProxyTemplateDependency {
  type: 'service' | 'certificate' | 'container' | 'network'
  name: string
  condition: string
  optional: boolean
}

/**
 * 批量代理操作（向后兼容）
 */
export interface BatchProxyOperation {
  proxyIds: string[]
  operation: 'start' | 'stop' | 'restart' | 'enable' | 'disable' | 'delete' | 'backup'
  options?: {
    force?: boolean
    backupLocation?: string
    includeConfig?: boolean
    includeSSL?: boolean
  }
}

/**
 * 批量代理结果（向后兼容）
 */
export interface BatchProxyResult {
  totalCount: number
  successCount: number
  failureCount: number
  results: Array<{
    proxyId: string
    success: boolean
    error?: string
    details?: any
  }>
}

// ==================== 通用类型定义（从common.ts导入） ====================

/**
 * 基础配置
 */
export interface BaseConfig {
  id: string
  name: string
  description: string
  enabled: boolean
  createdAt: string
  updatedAt: string
  tags: Record<string, string>
  metadata: Record<string, any>
}

/**
 * 状态
 */
export interface Status {
  status: string
  health: string
  lastCheck?: string
  message?: string
}

/**
 * 优先级
 */
export interface Priority {
  priority: number
  level: 'low' | 'medium' | 'high' | 'critical'
}

/**
 * 分页参数
 */
export interface PaginationParams {
  page?: number
  pageSize?: number
  sort?: string
  order?: 'asc' | 'desc'
  filter?: Record<string, any>
}

/**
 * 分页响应
 */
export interface PaginationResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages: number
  hasNext: boolean
  hasPrev: boolean
}

/**
 * 操作结果
 */
export interface OperationResult {
  success: boolean
  message?: string
  data?: any
  errors?: string[]
  warnings?: string[]
}

// ==================== 其他辅助类型 ====================

/**
 * 代理使用请求（向后兼容）
 */
export interface ProxyUsageRequests {
  total: number
  success: number
  failed: number
  rate: number
  averageResponseTime: number
  p95ResponseTime: number
  p99ResponseTime: number
}

/**
 * 代理使用带宽（向后兼容）
 */
export interface ProxyUsageBandwidth {
  inbound: number
  outbound: number
  total: number
  rate: number
}

/**
 * 代理使用连接（向后兼容）
 */
export interface ProxyUsageConnections {
  active: number
  total: number
  peak: number
  average: number
}

/**
 * 代理使用错误（向后兼容）
 */
export interface ProxyUsageErrors {
  total: number
  rate: number
  byType: Record<string, number>
  byCode: Record<string, number>
}

/**
 * 代理使用性能（向后兼容）
 */
export interface ProxyUsagePerformance {
  cpu: number
  memory: number
  disk: number
  network: number
}

/**
 * 代理性能调优（向后兼容）
 */
export interface ProxyPerformanceTuning {
  connectionPool: ProxyConnectionPool
  bufferSizes: ProxyBufferSize
  timeouts: ProxyTimeout
  concurrency: number
  cpuAffinity?: number[]
  memoryLimits?: ProxyMemoryLimits
}

/**
 * 内存限制（向后兼容）
 */
export interface ProxyMemoryLimits {
  maxHeapSize: number
  maxDirectMemory: number
  gcSettings: Record<string, any>
}

/**
 * 代理优化（向后兼容）
 */
export interface ProxyOptimization {
  caching: ProxyCaching
  compression: ProxyCompression
  keepAlive: ProxyKeepAlive
  http2: ProxyHTTP2
  websockets: ProxyWebSockets
}

/**
 * HTTP2设置（向后兼容）
 */
export interface ProxyHTTP2 {
  enabled: boolean
  maxConcurrentStreams: number
  initialWindowSize: number
  headerTableSize: number
}

/**
 * WebSocket设置（向后兼容）
 */
export interface ProxyWebSockets {
  enabled: boolean
  pingInterval: number
  maxFrameSize: number
  compressionEnabled: boolean
}

/**
 * 代理基准测试（向后兼容）
 */
export interface ProxyBenchmark {
  name: string
  type: 'throughput' | 'latency' | 'concurrency' | 'memory' | 'cpu'
  score: number
  unit: string
  timestamp: string
  details: Record<string, any>
}

/**
 * 代理建议（向后兼容）
 */
export interface ProxyRecommendation {
  id: string
  type: 'performance' | 'security' | 'reliability' | 'scalability'
  priority: 'low' | 'medium' | 'high'
  title: string
  description: string
  impact: string
  implementation: string
  estimatedGain: string
}

/**
 * 代理缓存（向后兼容）
 */
export interface ProxyCaching {
  enabled: boolean
  rules: ProxyCachingRule[]
  storage: ProxyCachingStorage
  eviction: ProxyCachingEviction
  compression: boolean
}

/**
 * 代理缓存规则（向后兼容）
 */
export interface ProxyCachingRule {
  id: string
  name: string
  pattern: string
  ttl: number
  maxSize: number
  vary: string[]
  condition?: string
  storage?: string
}

/**
 * 代理缓存存储（向后兼容）
 */
export interface ProxyCachingStorage {
  type: 'memory' | 'redis' | 'memcached' | 'disk' | 'custom'
  config: Record<string, any>
  maxSize: number
  maxItems: number
}

/**
 * 代理缓存驱逐（向后兼容）
 */
export interface ProxyCachingEviction {
  policy: 'lru' | 'lfu' | 'fifo' | 'random' | 'ttl'
  interval: number
}

/**
 * 代理日志记录（向后兼容）
 */
export interface ProxyLogging {
  enabled: boolean
  level: 'debug' | 'info' | 'warn' | 'error' | 'fatal'
  format: 'json' | 'text' | 'custom'
  outputs: ProxyLogOutput[]
  fields: ProxyLogField[]
}

/**
 * 代理日志输出（向后兼容）
 */
export interface ProxyLogOutput {
  type: 'file' | 'console' | 'syslog' | 'elasticsearch' | 'splunk' | 'custom'
  config: Record<string, any>
  enabled: boolean
}

/**
 * 代理日志字段（向后兼容）
 */
export interface ProxyLogField {
  name: string
  source: 'request' | 'response' | 'system' | 'custom'
  enabled: boolean
  transform?: string
}

/**
 * 代理追踪（向后兼容）
 */
export interface ProxyTracing {
  enabled: boolean
  sampling: number
  serviceName: string
  jaeger?: ProxyTracingJaeger
  zipkin?: ProxyTracingZipkin
  custom?: Record<string, any>
}

/**
 * 代理追踪Jaeger（向后兼容）
 */
export interface ProxyTracingJaeger {
  endpoint: string
  service: string
  tags: Record<string, string>
}

/**
 * 代理追踪Zipkin（向后兼容）
 */
export interface ProxyTracingZipkin {
  endpoint: string
  service: string
  sampleRate: number
}

/**
 * 代理性能分析（向后兼容）
 */
export interface ProxyProfiling {
  enabled: boolean
  interval: number
  duration: number
  outputs: string[]
  categories: string[]
}

/**
 * 代理连接池（向后兼容）
 */
export interface ProxyConnectionPool {
  maxConnections: number
  maxIdleConnections: number
  idleTimeout: number
  connectTimeout: number
  keepAlive: boolean
}

/**
 * 代理缓冲区大小（向后兼容）
 */
export interface ProxyBufferSize {
  send: number
  receive: number
  request: number
  response: number
}

/**
 * 代理超时（向后兼容）
 */
export interface ProxyTimeoutOld {
  connect: number
  send: number
  receive: number
  request: number
  response: number
  idle: number
}

/**
 * 代理保持连接（向后兼容）
 */
export interface ProxyKeepAlive {
  enabled: boolean
  timeout: number
  maxRequests: number
  maxIdle: number
}

/**
 * 代理头规则（向后兼容）
 */
export interface ProxyHeaders {
  request: ProxyHeaderRule[]
  response: ProxyHeaderRule[]
  remove: string[]
  add: Record<string, string>
}

/**
 * 代理头规则（向后兼容）
 */
export interface ProxyHeaderRule {
  name: string
  action: 'add' | 'remove' | 'replace' | 'append'
  value?: string
  condition?: string
}

/**
 * 代理重定向（向后兼容）
 */
export interface ProxyRedirects {
  enabled: boolean
  rules: ProxyRedirectRule[]
  defaultRedirect?: string
  preserveMethod: boolean
  preserveQuery: boolean
}

/**
 * 代理重定向规则（向后兼容）
 */
export interface ProxyRedirectRule {
  pattern: string
  destination: string
  permanent: boolean
  condition?: string
  statusCode?: number
}

/**
 * 代理重写（向后兼容）
 */
export interface ProxyRewrites {
  enabled: boolean
  rules: ProxyRewriteRule[]
  preserveHost: boolean
  preservePath: boolean
}

/**
 * 代理重写规则（向后兼容）
 */
export interface ProxyRewriteRule {
  pattern: string
  replacement: string
  condition?: string
  flags: string[]
}

/**
 * SSL客户端认证（向后兼容）
 */
export interface ProxySSLClientAuth {
  enabled: boolean
  mode: 'none' | 'request' | 'require'
  certificates: ProxySSLCertificate[]
  caCertificates: string[]
}

/**
 * SSL OCSP（向后兼容）
 */
export interface ProxySSLOCSP {
  enabled: boolean
  url?: string
  cacheSize: number
  cacheTimeout: number
}

/**
 * SSL装订（向后兼容）
 */
export interface ProxySSLStapling {
  enabled: boolean
  cacheSize: number
  cacheTimeout: number
  refreshInterval: number
}

/**
 * SSL会话（向后兼容）
 */
export interface ProxySSLSession {
  enabled: boolean
  timeout: number
  cacheSize: number
  tickets: boolean
}

/**
 * 代理授权（向后兼容）
 */
export interface ProxyAuthorization {
  enabled: boolean
  policies: ProxyAuthPolicy[]
  defaultPolicy: 'allow' | 'deny'
}

/**
 * 代理授权策略（向后兼容）
 */
export interface ProxyAuthPolicy {
  name: string
  type: 'role' | 'permission' | 'attribute' | 'custom'
  rules: ProxyAuthRule[]
}

/**
 * 代理授权规则（向后兼容）
 */
export interface ProxyAuthRule {
  effect: 'allow' | 'deny'
  principal: string
  resource: string
  action: string
  condition?: string
}

/**
 * 代理CSRF保护（向后兼容）
 */
export interface ProxyCSRF {
  enabled: boolean
  headerName: string
  cookieName: string
  tokenLength: number
  expiration: number
}

/**
 * 代理IP过滤（向后兼容）
 */
export interface ProxyIPFiltering {
  enabled: boolean
  allowList: string[]
  denyList: string[]
  defaultPolicy: 'allow' | 'deny'
  geoIP: ProxyGeoIP
}

/**
 * 代理地理位置过滤（向后兼容）
 */
export interface ProxyGeoIP {
  enabled: boolean
  allowCountries: string[]
  denyCountries: string[]
  databasePath?: string
}

/**
 * 代理WAF（向后兼容）
 */
export interface ProxyWAF {
  enabled: boolean
  mode: 'detection' | 'prevention'
  rules: ProxyWAFFRule[]
  scoreThreshold: number
  action: 'block' | 'allow' | 'log'
}

/**
 * 代理WAF规则（向后兼容）
 */
export interface ProxyWAFFRule {
  id: string
  name: string
  type: 'sql_injection' | 'xss' | 'path_traversal' | 'command_injection' | 'custom'
  pattern: string
  action: 'block' | 'allow' | 'log'
  severity: 'low' | 'medium' | 'high' | 'critical'
  enabled: boolean
}

/**
 * 代理DDoS保护（向后兼容）
 */
export interface ProxyDDoSProtection {
  enabled: boolean
  threshold: ProxyDDoSThreshold
  action: 'block' | 'rate_limit' | 'challenge'
  whitelist: string[]
  blacklist: string[]
}

/**
 * 代理DDoS阈值（向后兼容）
 */
export interface ProxyDDoSThreshold {
  requestsPerSecond: number
  connectionsPerSecond: number
  bytesPerSecond: number
  burstSize: number
}

/**
 * 代理联系人（向后兼容）
 */
export interface ProxyContact {
  name?: string
  email?: string
  url?: string
}

/**
 * 代理度量（向后兼容）
 */
export interface ProxyMetrics {
  enabled: boolean
  interval: number
  retention: number
  categories: ProxyMetricsCategory[]
}

/**
 * 代理度量类别（向后兼容）
 */
export interface ProxyMetricsCategory {
  name: string
  enabled: boolean
  metrics: string[]
}

/**
 * 代理警报（向后兼容）
 */
export interface ProxyAlert {
  id: string
  name: string
  type: 'threshold' | 'anomaly' | 'health' | 'performance'
  severity: 'info' | 'warning' | 'error' | 'critical'
  condition: string
  threshold?: number
  enabled: boolean
  notifications: ProxyNotification[]
}

/**
 * 代理通知（向后兼容）
 */
export interface ProxyNotification {
  type: 'email' | 'webhook' | 'slack' | 'teams' | 'sms'
  destination: string
  template?: string
  enabled: boolean
}

/**
 * 代理自定义配置（向后兼容）
 */
export interface ProxyCustomConfig {
  [key: string]: any
}