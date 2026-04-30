// ==================== 网络管理相关类型定义 ====================

import { BaseConfig, Status, Priority, PaginationParams, PaginationResponse } from './common'

// 网络基础信息
export interface Network extends BaseConfig {
  name: string
  driver: string
  scope: string
  internal: boolean
  attachable: boolean
  ingress: boolean
  configFrom?: NetworkConfigFrom
  configOnly: boolean
  containers: Record<string, NetworkContainer>
  options: Record<string, string>
  labels: Record<string, string>
  enableIPv6: boolean
  ipam: IPAM
  driverOpts: Record<string, string>
  nodeId?: string
  nodeName?: string
  created: string
  scope?: 'local' | 'swarm' | 'global'
  subnets?: NetworkSubnet[]
  services?: Record<string, NetworkService>
  peerLinks?: NetworkPeerLink[]
  encrypted: boolean
  checkDuplicate: boolean
  composable: boolean
  createdTime?: string
  modifiedTime?: string
}

// 网络配置来源
export interface NetworkConfigFrom {
  network: string
}

// 网络容器信息
export interface NetworkContainer {
  name: string
  endpointID: string
  macAddress: string
  ipv4Address: string
  ipv6Address: string
  ipamConfig?: {
    IPv4Address?: string
    IPv6Address?: string
    LinkLocalIPs?: string[]
  }
  networks?: Record<string, NetworkEndpointInfo>
  aliases?: string[]
  dnsNames?: string[]
  driverOpts?: Record<string, string>
}

// 网络端点信息
export interface NetworkEndpointInfo {
  ipamConfig?: {
    IPv4Address?: string
    IPv6Address?: string
    LinkLocalIPs?: string[]
  }
  links: string[]
  aliases: string[]
  networkID: string
  endpointID: string
  gateway: string
  ipAddress: string
  ipPrefixLen: number
  ipv6Gateway: string
  globalIPv6Address: string
  globalIPv6PrefixLen: number
  macAddress: string
  driverOpts: Record<string, string>
}

// IPAM (IP地址管理)
export interface IPAM {
  driver: string
  config: IPAMConfig[]
  options: Record<string, string>
  defaultAddressPools?: AddressPool[]
  defaultAddressPoolOpts?: AddressPoolOpts
}

export interface IPAMConfig {
  subnet: string
  ipRange?: string
  gateway?: string
  auxAddresses?: Record<string, string>
  networkID?: string
  subnetPool?: string
}

export interface AddressPool {
  base: string
  size: number
}

export interface AddressPoolOpts {
  datacenter?: string
  iprange?: string
  subnet?: string
  gateway?: string
  auxaddresses?: Record<string, string>
}

// 网络子网
export interface NetworkSubnet {
  subnet: string
  gateway: string
  ipRange?: string
  auxAddresses?: Record<string, string>
  networkID?: string
}

// 网络服务
export interface NetworkService {
  name: string
  id: string
  endpointId: string
  virtualIP: string
  ports: NetworkPort[]
  tasks: NetworkTask[]
  spec: NetworkServiceSpec
  endpoint: NetworkServiceEndpoint
  updateStatus?: NetworkServiceUpdateStatus
  previousSpec?: NetworkServiceSpec
  currentSpec?: NetworkServiceSpec
}

export interface NetworkPort {
  targetPort: number
  publishedPort: number
  protocol: 'tcp' | 'udp' | 'sctp'
  publishMode: 'ingress' | 'host'
  name?: string
}

export interface NetworkTask {
  id: string
  serviceId: string
  slot?: number
  nodeId: string
  status: NetworkTaskStatus
  desiredState: NetworkTaskState
  containers?: NetworkContainer[]
  assignedGenericResources?: NetworkGenericResource[]
  name?: string
  image?: string
  networks?: Record<string, NetworkAttachment>
  endpoint?: NetworkTaskEndpoint
  forceUpdate?: number
  logDriver?: NetworkLogDriver
  runtime?: NetworkRuntime
  accessDetails?: NetworkAccessDetails
  restart?: NetworkRestartPolicy
  placement?: NetworkPlacement
  resources?: NetworkResourceRequirements
  healthcheck?: NetworkHealthCheck
  networksAttachments?: NetworkAttachment[]
}

export interface NetworkTaskStatus {
  timestamp: string
  state: NetworkTaskState
  message?: string
  err?: string
  containerStatus?: NetworkContainerStatus
}

export interface NetworkContainerStatus {
  containerID: string
  pid: number
  exitCode: number
}

export type NetworkTaskState = 'new' | 'pending' | 'assigned' | 'accepted' | 'preparing' | 'ready' | 'starting' | 'running' | 'complete' | 'shutdown' | 'failed' | 'rejected' | 'remove' | 'orphaned'

export interface NetworkGenericResource {
  kind: string
  value: string
}

export interface NetworkAttachment {
  network: NetworkAttachmentConfig
  addresses?: string[]
  aliases?: string[]
  driverOpts?: Record<string, string>
  dnsNames?: string[]
  domainName?: string
  endpointID?: string
  gateway?: string
  globalIPv6Address?: string
  globalIPv6PrefixLen?: number
  ipAddress?: string
  ipPrefixLen?: number
  ipv6Gateway?: string
  links?: string[]
  macAddress?: string
}

export interface NetworkAttachmentConfig {
  target: string
  aliases?: string[]
  driverOpts?: Record<string, string>
}

export interface NetworkTaskEndpoint {
  spec: NetworkServiceEndpointSpec
  virtualIPs: NetworkVirtualIP[]
  ports?: NetworkPort[]
  publishedPorts?: NetworkPublishedPort[]
}

export interface NetworkVirtualIP {
  networkID: string
  addr: string
}

export interface NetworkPublishedPort {
  targetPort: number
  publishedPort: number
  protocol: 'tcp' | 'udp' | 'sctp'
  publishMode: 'ingress' | 'host'
}

export interface NetworkLogDriver {
  name: string
  options: Record<string, string>
}

export interface NetworkRuntime {
  name: string
  options: Record<string, string>
}

export interface NetworkAccessDetails {
  token?: string
  address?: string
  ports?: NetworkPort[]
}

export interface NetworkRestartPolicy {
  condition: 'none' | 'on-failure' | 'any'
  delay?: string
  maxAttempts?: number
  window?: string
}

export interface NetworkPlacement {
  constraints?: string[]
  preferences?: NetworkPlacementPreference[]
  maxReplicas?: number
  platforms?: NetworkPlatform[]
  spread?: NetworkSpread[]
}

export interface NetworkPlacementPreference {
  spread: NetworkSpread
}

export interface NetworkSpread {
  spreadDescriptor: string
}

export interface NetworkPlatform {
  architecture: string
  os: string
}

export interface NetworkResourceRequirements {
  limits?: NetworkResources
  reservations?: NetworkResources
}

export interface NetworkResources {
  nanoCPUs?: number
  memoryBytes?: number
  genericResources?: NetworkGenericResource[]
}

export interface NetworkHealthCheck {
  test?: string[]
  interval?: number
  timeout?: number
  retries?: number
  startPeriod?: number
  startInterval?: number
}

// 网络服务规范
export interface NetworkServiceSpec {
  name: string
  labels: Record<string, string>
  mode: NetworkServiceMode
  updateConfig?: NetworkUpdateConfig
  rollbackConfig?: NetworkUpdateConfig
  taskTemplate: NetworkTaskSpec
  networks?: NetworkAttachmentConfig[]
  endpointSpec: NetworkServiceEndpointSpec
}

export interface NetworkServiceMode {
  replicated?: {
    replicas: number
  }
  global?: {}
  replicatedJob?: {
    maxConcurrent?: number
    totalCompletions?: number
  }
  globalJob?: {}
}

export interface NetworkUpdateConfig {
  parallelism: number
  delay: string
  failureAction: 'pause' | 'continue' | 'rollback'
  monitor: string
  maxFailureRatio: number
  order: 'stop-first' | 'start-first'
}

export interface NetworkTaskSpec {
  containerSpec: NetworkContainerSpec
  resources?: NetworkResourceRequirements
  restart?: NetworkRestartPolicy
  placement?: NetworkPlacement
  networks?: NetworkAttachmentConfig[]
  runtime?: NetworkRuntime
  logDriver?: NetworkLogDriver
  forceUpdate?: number
}

export interface NetworkContainerSpec {
  image: string
  command?: string[]
  args?: string[]
  env?: string[]
  hostname?: string
  domainname?: string
  user?: string
  groups?: string[]
  workingDir?: string
  tty?: boolean
  openStdin?: boolean
  readOnly?: boolean
  stopSignal?: string
  stopGracePeriod?: number
  healthcheck?: NetworkHealthCheck
  hosts?: string[]
  dnsConfig?: NetworkDNSConfig
  secrets?: NetworkSecret[]
  configs?: NetworkConfigReference[]
  isolation?: string
  privileged?: boolean
  init?: boolean
  initPath?: string
  sysctls?: Record<string, string>
  capAdd?: string[]
  capDrop?: string[]
  ulimits?: NetworkUlimit[]
  mounts?: NetworkMount[]
  labels?: Record<string, string>
  file?: NetworkFileReference
}

export interface NetworkDNSConfig {
  nameservers?: string[]
  search?: string[]
  options?: string[]
}

export interface NetworkSecret {
  file?: NetworkMount
  secretID: string
  secretName: string
}

export interface NetworkConfigReference {
  file?: NetworkMount
  configID: string
  configName: string
}

export interface NetworkUlimit {
  name: string
  soft: number
  hard: number
}

export interface NetworkMount {
  type: 'bind' | 'volume' | 'tmpfs' | 'npipe'
  source?: string
  target?: string
  readonly?: boolean
  consistency?: string
  bindOptions?: NetworkBindOptions
  volumeOptions?: NetworkVolumeOptions
  tmpfsOptions?: NetworkTmpfsOptions
}

export interface NetworkBindOptions {
  propagation: string
  nonRecursive: boolean
  createMountpoint: boolean
}

export interface NetworkVolumeOptions {
  noCopy: boolean
  labels?: Record<string, string>
  subpath?: string
  driverConfig?: NetworkDriverConfig
  size?: number
}

export interface NetworkDriverConfig {
  name: string
  options: Record<string, string>
}

export interface NetworkTmpfsOptions {
  sizeBytes?: number
  mode?: number
}

export interface NetworkFileReference {
  name: string
  uid?: number
  gid?: number
  mode?: number
}

export interface NetworkServiceEndpointSpec {
  mode: 'vip' | 'dnsrr'
  ports?: NetworkPort[]
}

export interface NetworkServiceEndpoint {
  spec: NetworkServiceEndpointSpec
  ports?: NetworkPort[]
  virtualIPs: NetworkVirtualIP[]
}

export interface NetworkServiceUpdateStatus {
  state: 'updating' | 'completed' | 'rollback_started' | 'rollback_paused' | 'rollback_completed' | 'paused' | 'unknown'
  startedAt?: string
  completedAt?: string
  message?: string
}

// 网络对等连接
export interface NetworkPeerLink {
  name: string
  id: string
  endpoints: NetworkPeerEndpoint[]
  state: NetworkPeerState
  created: string
  modified: string
}

export interface NetworkPeerEndpoint {
  name: string
  id: string
  ipAddress: string
  macAddress: string
  networkID: string
}

export type NetworkPeerState = 'pending' | 'active' | 'inactive' | 'failed'

// 网络创建请求
export interface CreateNetworkRequest {
  name: string
  checkDuplicate?: boolean
  driver?: string
  internal?: boolean
  attachable?: boolean
  ingress?: boolean
  enableIPv6?: boolean
  configOnly?: boolean
  configFrom?: NetworkConfigFrom
  ipam?: IPAM
  options?: Record<string, string>
  labels?: Record<string, string>
  scope?: 'local' | 'swarm' | 'global'
  driverOpts?: Record<string, string>
  subnets?: IPAMConfig[]
  nodeId?: string
}

// 网络连接请求
export interface ConnectContainerRequest {
  container: string
  endpointConfig?: NetworkEndpointConfig
}

export interface NetworkEndpointConfig {
  ipamConfig?: {
    IPv4Address?: string
    IPv6Address?: string
    LinkLocalIPs?: string[]
  }
  links?: string[]
  aliases?: string[]
  networkID?: string
  driverOpts?: Record<string, string>
}

// 网络断开请求
export interface DisconnectContainerRequest {
  container: string
  force?: boolean
}

// 网络统计信息
export interface NetworkStatistics {
  totalNetworks: number
  networksByDriver: Record<string, number>
  networksByScope: Record<string, number>
  totalContainers: number
  connectedContainers: number
  totalIPs: number
  usedIPs: number
  availableIPs: number
  networksByNode: Record<string, number>
  largestNetwork?: NetworkInfo
  oldestNetwork?: NetworkInfo
  newestNetwork?: NetworkInfo
  lastUpdated: string
}

export interface NetworkInfo {
  id: string
  name: string
  driver: string
  scope: string
  internal: boolean
  enableIPv6: boolean
  attachable: boolean
  ingress: boolean
  labels: Record<string, string>
  options: Record<string, string>
  createdAt: string
  created: string
  nodeId: string
  containers: Array<any>
  containersCount: number
  size?: number
  isDefault?: boolean  // 是否为默认网络（不可删除）
}

// 网络使用情况
export interface NetworkUsage {
  networkId: string
  networkName: string
  containersCount: number
  totalIPs: number
  usedIPs: number
  availableIPs: number
  usagePercentage: number
  lastUsed?: string
  bandwidthUsage?: NetworkBandwidthUsage
}

export interface NetworkBandwidthUsage {
  rxBytes: number
  txBytes: number
  rxBps: number
  txBps: number
  timestamp: string
}

// 网络事件
export interface NetworkEvent {
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

// 网络搜索和过滤
export interface NetworkSearchParams extends PaginationParams {
  name?: string
  driver?: string
  scope?: string
  nodeId?: string
  label?: string
  type?: string
  internal?: boolean
  attachable?: boolean
  ingress?: boolean
  enableIPv6?: boolean
  filters?: Record<string, string[]>
  sort?: 'created' | 'name' | 'driver' | 'containers'
  order?: 'asc' | 'desc'
}

export interface NetworkSearchResponse {
  networks: Network[]
  total: number
  filtered: number
  page: number
  pageSize: number
  totalPages?: number
  hasNext?: boolean
  hasPrev?: boolean
}

// 网络模板
export interface NetworkTemplate extends BaseConfig {
  driver: string
  scope: string
  internal: boolean
  attachable: boolean
  ingress: boolean
  enableIPv6: boolean
  ipam: IPAM
  options: Record<string, string>
  labels: Record<string, string>
  driverOpts: Record<string, string>
  category: string
  tags: string[]
  description: string
  readme?: string
  parameters: NetworkTemplateParameter[]
  dependencies: NetworkTemplateDependency[]
  subnets: IPAMConfig[]
}

export interface NetworkTemplateParameter {
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

export interface NetworkTemplateDependency {
  type: 'network' | 'service' | 'volume'
  name: string
  condition: string
  optional: boolean
}

// 网络驱动信息
export interface NetworkDriver {
  name: string
  description: string
  documentation?: string
  options: NetworkDriverOption[]
  status: 'active' | 'inactive' | 'deprecated'
  version?: string
  capabilities: string[]
  supportedFeatures: string[]
  limitations: string[]
  configuration: NetworkDriverConfiguration
}

export interface NetworkDriverOption {
  name: string
  type: 'string' | 'number' | 'boolean' | 'array' | 'object'
  description: string
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

export interface NetworkDriverConfiguration {
  minVersion?: string
  maxVersion?: string
  kernelRequirements?: string[]
  systemRequirements?: string[]
  dependencies?: string[]
  conflicts?: string[]
  recommendedSettings?: Record<string, any>
  defaultOptions?: Record<string, any>
}

// 网络插件
export interface NetworkPlugin {
  name: string
  version: string
  description: string
  documentationUrl?: string
  vendor?: string
  license?: string
  status: 'active' | 'inactive' | 'error'
  enabled: boolean
  autoUpdate: boolean
  updateAvailable?: boolean
  installedAt: string
  lastUpdated: string
  size?: number
  checksum?: string
  signature?: string
  capabilities: string[]
  supportedDrivers: string[]
  configuration: NetworkPluginConfiguration
  metadata: NetworkPluginMetadata
}

export interface NetworkPluginConfiguration {
  configPath?: string
  dataPath?: string
  logPath?: string
  options: Record<string, any>
  environment: Record<string, string>
  resources: NetworkPluginResources
  permissions: NetworkPluginPermissions
}

export interface NetworkPluginResources {
  cpu?: number
  memory?: number
  disk?: number
  network?: number
  devices?: string[]
}

export interface NetworkPluginPermissions {
  capabilities?: string[]
  privileged?: boolean
  hostAccess?: boolean
  networkAccess?: boolean
  filesystemAccess?: boolean[]
}

export interface NetworkPluginMetadata {
  tags: string[]
  categories: string[]
  keywords: string[]
  homepage?: string
  repository?: string
  issues?: string
  changelog?: string
  releaseNotes?: string
  securityNotes?: string
  dependencies: string[]
  conflicts: string[]
  compatibility: NetworkPluginCompatibility
}

export interface NetworkPluginCompatibility {
  platforms: string[]
  architectures: string[]
  dockerVersions: string[]
  kernelVersions: string[]
  osVersions: string[]
}

// 网络策略
export interface NetworkPolicy {
  id: string
  name: string
  description: string
  namespace: string
  priority: number
  action: 'allow' | 'deny' | 'log'
  direction: 'ingress' | 'egress' | 'both'
  protocols: string[]
  ports: NetworkPolicyPort[]
  sources: NetworkPolicyPeer[]
  destinations: NetworkPolicyPeer[]
  enabled: boolean
  createdAt: string
  updatedAt: string
  version: number
  labels: Record<string, string>
  annotations: Record<string, string>
}

export interface NetworkPolicyPort {
  protocol: 'tcp' | 'udp' | 'sctp'
  port?: number
  endPort?: number
  portRange?: string
}

export interface NetworkPolicyPeer {
  ipBlock?: NetworkIPBlock
  namespaceSelector?: NetworkLabelSelector
  podSelector?: NetworkLabelSelector
  ports?: NetworkPolicyPort[]
}

export interface NetworkIPBlock {
  cidr: string
  except?: string[]
}

export interface NetworkLabelSelector {
  matchLabels: Record<string, string>
  matchExpressions: NetworkLabelSelectorRequirement[]
}

export interface NetworkLabelSelectorRequirement {
  key: string
  operator: 'In' | 'NotIn' | 'Exists' | 'DoesNotExist'
  values?: string[]
}

// 网络安全组
export interface NetworkSecurityGroup {
  id: string
  name: string
  description: string
  rules: NetworkSecurityRule[]
  enabled: boolean
  priority: number
  createdAt: string
  updatedAt: string
  labels: Record<string, string>
  annotations: Record<string, string>
  networks: string[]
  defaultDeny: boolean
  logging: boolean
}

export interface NetworkSecurityRule {
  id: string
  name: string
  description: string
  priority: number
  action: 'allow' | 'deny' | 'log'
  direction: 'ingress' | 'egress'
  protocol: 'tcp' | 'udp' | 'icmp' | 'all'
  sourcePorts?: string[]
  destinationPorts?: string[]
  sourceAddresses?: string[]
  destinationAddresses?: string[]
  enabled: boolean
  createdAt: string
  updatedAt: string
  hitCount: number
  lastHit?: string
}

// 网络监控
export interface NetworkMetrics {
  networkId: string
  networkName: string
  timestamp: string
  metrics: {
    bandwidth: NetworkBandwidthMetrics
    packets: NetworkPacketMetrics
    connections: NetworkConnectionMetrics
    errors: NetworkErrorMetrics
  }
  endpoints: NetworkEndpointMetrics[]
}

export interface NetworkBandwidthMetrics {
  rxBytes: number
  txBytes: number
  rxBps: number
  txBps: number
  averageBps: number
  peakBps: number
}

export interface NetworkPacketMetrics {
  rxPackets: number
  txPackets: number
  rxErrors: number
  txErrors: number
  rxDropped: number
  txDropped: number
}

export interface NetworkConnectionMetrics {
  activeConnections: number
  totalConnections: number
  connectionRate: number
  averageConnectionDuration: number
  failedConnections: number
}

export interface NetworkErrorMetrics {
  rxErrors: number
  txErrors: number
  droppedPackets: number
  overruns: number
  frameErrors: number
  carrierErrors: number
}

export interface NetworkEndpointMetrics {
  endpointId: string
  containerId: string
  ipAddress: string
  macAddress: string
  bandwidth: NetworkBandwidthMetrics
  packets: NetworkPacketMetrics
  connections: number
  lastActivity: string
}

// 网络诊断
export interface NetworkDiagnostics {
  networkId: string
  networkName: string
  timestamp: string
  status: 'healthy' | 'warning' | 'error'
  issues: NetworkDiagnosticIssue[]
  recommendations: NetworkDiagnosticRecommendation[]
  tests: NetworkDiagnosticTest[]
}

export interface NetworkDiagnosticIssue {
  severity: 'low' | 'medium' | 'high' | 'critical'
  category: 'connectivity' | 'performance' | 'security' | 'configuration'
  title: string
  description: string
  affectedEndpoints: string[]
  affectedContainers: string[]
  firstDetected: string
  lastDetected: string
  occurrences: number
}

export interface NetworkDiagnosticRecommendation {
  priority: 'low' | 'medium' | 'high'
  category: string
  title: string
  description: string
  actions: string[]
  estimatedImpact: string
  implementationComplexity: 'low' | 'medium' | 'high'
}

export interface NetworkDiagnosticTest {
  name: string
  type: 'connectivity' | 'bandwidth' | 'latency' | 'dns' | 'security'
  status: 'passed' | 'failed' | 'warning'
  result: string
  details: string
  duration: number
  timestamp: string
}

// 批量网络操作
export interface BatchNetworkOperation {
  networkIds: string[]
  operation: 'connect' | 'disconnect' | 'remove' | 'prune' | 'update'
  options?: {
    force?: boolean
    containers?: string[]
    config?: Partial<Network>
  }
}

export interface BatchNetworkResult {
  totalCount: number
  successCount: number
  failureCount: number
  results: Array<{
    networkId: string
    success: boolean
    error?: string
    details?: any
  }>
}