// ==================== 容器管理相关类型定义 ====================

import type { BaseConfig, Status, Priority, ResourceLimits, EnvironmentVariable, PortMapping, VolumeMapping, Metadata, HealthCheck, PaginationParams, PaginationResponse } from './common'

// 容器基础信息
export interface Container extends BaseConfig {
  image: string
  imageId: string
  command: string[]
  environment?: string[]
  status: 'created' | 'running' | 'paused' | 'restarting' | 'removing' | 'exited' | 'dead'
  state: ContainerState | string
  created: string
  started?: string
  finished?: string
  exitCode?: number
  error?: string
  ports: ContainerPort[]
  mounts: ContainerMount[]
  networkSettings: ContainerNetworkSettings
  logs: string[]
  size: ContainerSize
  restartPolicy: RestartPolicy
  labels: Record<string, string>
  annotations?: Record<string, string>
  nodeId: string
  nodeName: string
  health: ContainerHealth
  platform: string
  architecture: string
  os: string
  runtime: string
  isolation: string
  pidMode: string
  user: string
  workingDir: string
  hostname: string
  domainName: string
  macAddress: string
  ipAddresses: string[]
  dns: string[]
  dnsSearch: string[]
  extraHosts: string[]
  autoRemove: boolean
  privileged: boolean
  readonlyRootfs: boolean
  stdinOpen: boolean
  tty: boolean
  attachStdin: boolean
  attachStdout: boolean
  attachStderr: boolean
  oomKillDisable: boolean
  oomScoreAdj: number
  cpuShares: number
  cpuPeriod: number
  cpuQuota: number
  cpusetCpus: string
  cpusetMems: string
  memory: number
  memorySwap: number
  memoryReservation: number
  kernelMemory: number
  ulimits: ContainerUlimit[]
  logConfig: ContainerLogConfig
  securityOpts: string[]
  storageOpt: Record<string, string>
  sysctls: Record<string, string>
  tmpfs: Record<string, string>
  maskedPaths: string[]
  readonlyPaths: string[]
  init: boolean
  initPath: string
  volumeDriver: string
  deviceRequests: ContainerDeviceRequest[]
  deviceCgroupRules: string[]
  cgroupnsMode: string
  // 域名映射（反向代理）
  domainMappings?: DomainMappingInfo[]
}

// 容器域名映射信息
export interface DomainMappingInfo {
  id: string
  domain: string
  containerPort: number
  pathPrefix?: string
  enableSsl: boolean
  enabled: boolean
}

// 容器状态详细信息
export interface ContainerState {
  status: Status
  running: boolean
  paused: boolean
  restarting: boolean
  oomKilled: boolean
  dead: boolean
  pid: number
  exitCode: number
  error: string
  startedAt: string
  finishedAt: string
  health: ContainerHealthStatus
}

// 容器健康状态
export interface ContainerHealth {
  status: 'none' | 'starting' | 'healthy' | 'unhealthy' | 'unknown'
  failingStreak: number
  log: ContainerHealthLogEntry[]
}

export interface ContainerHealthStatus {
  status: 'none' | 'starting' | 'healthy' | 'unhealthy' | 'unknown'
  failingStreak: number
  log: ContainerHealthLogEntry[]
}

export interface ContainerHealthLogEntry {
  start: string
  end: string
  exitCode: number
  output: string
}

// 容器端口信息
export interface ContainerPort {
  ip: string
  privatePort: number
  publicPort: number
  type: 'tcp' | 'udp'
  containerPort: number
  hostIp?: string
  hostPort?: number
  protocol: string
}

// 容器挂载信息
export interface ContainerMount {
  type: 'bind' | 'volume' | 'tmpfs' | 'npipe'
  source: string
  destination: string
  mode: string
  rw: boolean
  propagation: string
  name?: string
  driver?: string
  labels?: Record<string, string>
}

// 容器网络设置
export interface ContainerNetworkSettings {
  bridge: string
  sandboxId: string
  hairpinMode: boolean
  linkLocalIPv6Address: string
  linkLocalIPv6PrefixLen: number
  ports: Record<string, Array<{
    hostIp: string
    hostPort: string
  }>>
  sandboxKey: string
  secondaryIPAddresses: ContainerNetworkAddress[]
  secondaryIPv6Addresses: ContainerNetworkAddress[]
  networks: Record<string, ContainerNetwork>
}

export interface ContainerNetworkAddress {
  addr: string
  prefixLen: number
}

export interface ContainerNetwork {
  IPAMConfig: Record<string, any>
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
  driverOpts: Record<string, any>
}

// 容器大小信息
export interface ContainerSize {
  rootFs: number
  rwFs: number
  sizeRw: number
  sizeRootFs: number
}

// 重启策略
export interface RestartPolicy {
  name: 'no' | 'on-failure' | 'always' | 'unless-stopped'
  maximumRetryCount: number
}

// 容器资源限制
export interface ContainerUlimit {
  name: string
  soft: number
  hard: number
}

export interface ContainerLogConfig {
  type: string
  config: Record<string, string>
}

export interface ContainerDeviceRequest {
  driver: string
  count: number
  deviceIDs: string[]
  capabilities: string[][]
  options: Record<string, string>
}

// 容器创建请求
// 容器创建请求
export interface CreateContainerRequest {
  name?: string
  image: string
  ports?: Array<{ hostPort: string; containerPort: string; protocol: string }>
  volumes?: Array<{ hostPath: string; containerPath: string; readOnly: boolean }>
  environment?: Record<string, string>
  command?: string[]
  autoRemove?: boolean
  interactive?: boolean
  tty?: boolean
  workingDir?: string
  hostname?: string
  labels?: string[]
  resources?: {
    memoryLimit?: string
    cpuQuota?: string
    cpuPeriod?: string
    cpuShares?: string
  }
  restartPolicy?: {
    name: string
    maximumRetryCount?: number
  }
  network?: ContainerNetworkConfig
  healthCheck?: ContainerHealthCheck
  domainMapping?: DomainMappingConfig
  connectionId?: string
}

export interface ContainerNetworkConfig {
  networkId?: string
  aliases?: string[]
  ipAddress?: string
  additionalNetworks?: string[]
}

export interface DomainMappingConfig {
  domain?: string
  containerPort?: number
  enableSsl?: boolean
  certificateId?: string
  pathPrefix?: string
  autoRequestCertificate?: boolean
  yarpConfig?: Record<string, any>
}

// 容器主机配置
export interface ContainerHostConfig {
  binds: string[]
  portBindings: Record<string, Array<{
    hostIp: string
    hostPort: string
  }>>
  links: string[]
  publishAllPorts: boolean
  dns: string[]
  dnsSearch: string[]
  dnsOptions: string[]
  extraHosts: string[]
  volumesFrom: string[]
  networkMode: string
  ipcMode: string
  pidMode: string
  cgroup: string
  cgroupnsMode: string
  runtime: string
  readonlyRootfs: boolean
  securityOpt: string[]
  storageOpt: Record<string, string>
  ulimits: ContainerUlimit[]
  logConfig: ContainerLogConfig
  sysctls: Record<string, string>
  capAdd: string[]
  capDrop: string[]
  groupAdd: string[]
  deviceRequests: ContainerDeviceRequest[]
  diskQuota: number
  kernelMemory: number
  memory: number
  memoryReservation: number
  memorySwap: number
  memorySwappiness: number
  oomKillDisable: boolean
  oomScoreAdj: number
  pidsLimit: number
  cpuShares?: number
  cpuCount: number
  cpuPercent: number
  cpuPeriod: number
  cpuQuota: number
  cpuRealtimePeriod: number
  cpuRealtimeRuntime: number
  cpusetCpus: string
  cpusetMems: string
  devices: Array<{
    pathOnHost: string
    pathInContainer: string
    cgroupPermissions: string
  }>
  deviceCgroupRules: string[]
  blkioWeight: number
  blkioWeightDevice: Array<{
    path: string
    weight: number
  }>
  blkioDeviceReadBps: Array<{
    path: string
    rate: number
  }>
  blkioDeviceWriteBps: Array<{
    path: string
    rate: number
  }>
  blkioDeviceReadIOps: Array<{
    path: string
    rate: number
  }>
  blkioDeviceWriteIOps: Array<{
    path: string
    rate: number
  }>
  hugetlbLimit: number
  maskedPaths: string[]
  readonlyPaths: string[]
  tmpfs: Record<string, string>
  shmSize: number
  usernsMode: string
  usernsRemap: string
  isolation: string
  init: boolean
  initPath: string
  restartPolicy?: RestartPolicy
}

// 容器健康检查配置
export interface ContainerHealthCheck {
  test: string[]
  interval: number
  timeout: number
  retries: number
  startPeriod: number
  startInterval: number
}

// 容器网络配置
export interface ContainerNetworkingConfig {
  endpointsConfig: Record<string, ContainerEndpointConfig>
}

export interface ContainerEndpointConfig {
  ipAMConfig: Record<string, any>
  links: string[]
  aliases: string[]
  networkID: string
  gateway: string
  ipAddress: string
  ipPrefixLen: number
  ipv6Gateway: string
  globalIPv6Address: string
  globalIPv6PrefixLen: number
  macAddress: string
  driverOpts: Record<string, string>
}

// 容器操作请求
export interface ContainerActionRequest {
  containerId: string
  action: 'start' | 'stop' | 'restart' | 'pause' | 'unpause' | 'kill' | 'remove'
  force?: boolean
  timeout?: number
  signal?: string
}

// 容器统计信息
export interface ContainerStats {
  containerId: string
  name: string
  cpuUsage: number
  memoryUsage: number
  memoryLimit: number
  networkRx: number
  networkTx: number
  blockRead: number
  blockWrite: number
  timestamp: string
  cpuStats: ContainerCpuStats
  memoryStats: ContainerMemoryStats
  blkio: ContainerBlkioStats
  network: ContainerNetworkStats
  pids: ContainerPidsStats
  read: string
  preread: string
  numProcs: number
  storageStats: ContainerStorageStats
}

export interface ContainerCpuStats {
  cpu_usage: {
    total_usage: number
    percpu_usage: number[]
    usage_in_kernelmode: number
    usage_in_usermode: number
  }
  system_cpu_usage: number
  online_cpus: number
  throttling_data: {
    periods: number
    throttled_periods: number
    throttled_time: number
  }
  percentCpu: number
}

export interface ContainerMemoryStats {
  usage: number
  usagePercent: number
  max_usage: number
  stats: Record<string, number>
  limit: number
  commit: number
  commit_peak: number
  private_working_set: number
  mapped_file: number
  active_anon: number
  inactive_anon: number
  active_file: number
  inactive_file: number
  unevictable: number
}

export interface ContainerBlkioStats {
  io_service_bytes_recursive: Array<{
    major: number
    minor: number
    op: string
    value: number
  }>
  io_serviced_recursive: Array<{
    major: number
    minor: number
    op: string
    value: number
  }>
  io_queue_recursive: any[]
  io_service_time_recursive: any[]
  io_wait_time_recursive: any[]
  io_merged_recursive: any[]
  io_time_recursive: any[]
  sectors_recursive: any[]
}

export interface ContainerNetworkStats {
  rx_bytes: number
  rx_packets: number
  rx_errors: number
  rx_dropped: number
  tx_bytes: number
  tx_packets: number
  tx_errors: number
  tx_dropped: number
}

export interface ContainerPidsStats {
  current: number
  limit: number
}

export interface ContainerStorageStats {
  read_count: number
  read_size_bytes: number
  write_count: number
  write_size_bytes: number
}

// 容器日志配置
export interface ContainerLogsRequest {
  containerId: string
  follow?: boolean
  stdout?: boolean
  stderr?: boolean
  since?: string
  until?: string
  timestamps?: boolean
  tail?: string | number
  details?: boolean
}

export interface ContainerLogsResponse {
  logs: string[]
  totalLines: number
  hasMore: boolean
  from: string
  to: string
}

// 容器文件操作
export interface ContainerFileRequest {
  containerId: string
  path: string
  content?: string
  mode?: string
  uid?: number
  gid?: number
}

export interface ContainerFileResponse {
  content: string
  size: number
  mode: number
  uid: number
  gid: number
  mtime: string
  isDir: boolean
  isSymlink: boolean
  target?: string
}

export interface ContainerFileListResponse {
  entries: Array<{
    name: string
    size: number
    mode: number
    uid: number
    gid: number
    mtime: string
    isDir: boolean
    isSymlink: boolean
    target?: string
  }>
  total: number
}

// 容器进程信息
export interface ContainerProcess {
  pid: number
  ppid: number
  uid: number
  gid: number
  name: string
  tty: string
  time: string
  cmd: string
  status: string
  cpu: number
  mem: number
  vsz: number
  rss: number
  user: string
}

export interface ContainerTopResponse {
  processes: string[][]
  titles: string[]
}

// 容器变更信息
export interface ContainerChange {
  path: string
  kind: number
}

// 容器检查点
export interface ContainerCheckpoint {
  name: string
  checkpointId: string
  createdAt: string
  size: number
  exit: boolean
  tcpEstablished: boolean
  shell: boolean
  tcpConnections: boolean
  externalUnixSockets: boolean
  terminal: boolean
}

export interface CreateCheckpointRequest {
  checkpointId: string
  exit?: boolean
  tcpEstablished?: boolean
  shell?: boolean
  tcpConnections?: boolean
  externalUnixSockets?: boolean
  terminal?: boolean
}

// 容器更新请求
export interface UpdateContainerRequest {
  containerId: string
  cpuShares?: number
  memory?: number
  cgroupParent?: string
  blkioWeight?: number
  blkioWeightDevice?: Array<{
    path: string
    weight: number
  }>
  deviceReadBps?: Array<{
    path: string
    rate: number
  }>
  deviceWriteBps?: Array<{
    path: string
    rate: number
  }>
  kernelMemory?: number
  memoryReservation?: number
  memorySwap?: number
  memorySwappiness?: number
  nanoCpus?: number
  oomKillDisable?: boolean
  pidsLimit?: number
  ulimits?: ContainerUlimit[]
  cpuCount?: number
  cpuPercent?: number
  cpuPeriod?: number
  cpuRealtimePeriod?: number
  cpuRealtimeRuntime?: number
  cpuQuota?: number
  cpusetCpus?: string
  cpusetMems?: string
  restartPolicy?: RestartPolicy
}

// 容器备份相关
export interface ContainerBackupRequest {
  containerId: string
  name?: string
  includeVolumes?: boolean
  includeConfig?: boolean
  compression?: 'none' | 'gzip' | 'bzip2' | 'xz'
  outputFormat?: 'tar' | 'tar.gz' | 'tar.bz2' | 'tar.xz'
}

export interface ContainerBackupResult {
  success: boolean
  containerId: string
  backupId: string
  backupPath?: string
  backupSize?: number
  backupFormat: string
  createdAt: string
  errorMessage?: string
}

export interface ContainerRestoreRequest {
  backupPath?: string
  backupData?: string
  name?: string
  includeVolumes?: boolean
  includeConfig?: boolean
  force?: boolean
}

export interface ContainerRestoreResult {
  success: boolean
  containerId?: string
  containerName?: string
  restoredAt: string
  errorMessage?: string
}

// 容器模板
export interface ContainerTemplate extends BaseConfig {
  image: string
  command?: string[]
  env?: EnvironmentVariable[]
  ports: PortMapping[]
  volumes: VolumeMapping[]
  resources: ResourceLimits
  restartPolicy: RestartPolicy
  healthcheck?: ContainerHealthCheck
  networkMode: string
  labels: Record<string, string>
  category: string
  tags: string[]
  description: string
  readme?: string
  parameters: ContainerTemplateParameter[]
  dependencies: ContainerTemplateDependency[]
}

export interface ContainerTemplateParameter {
  name: string
  label: string
  type: 'string' | 'number' | 'boolean' | 'select' | 'multiselect' | 'file'
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

export interface ContainerTemplateDependency {
  type: 'service' | 'image' | 'volume' | 'network'
  name: string
  condition: string
  optional: boolean
}

// 容器统计
export interface ContainerStatistics {
  totalContainers: number
  runningContainers: number
  pausedContainers: number
  stoppedContainers: number
  containersByStatus: Record<string, number>
  containersByImage: Record<string, number>
  containersByNode: Record<string, number>
  totalCpuUsage: number
  totalMemoryUsage: number
  totalNetworkIO: ContainerNetworkStats
  totalDiskIO: ContainerBlkioStats
  averageCpuUsage: number
  averageMemoryUsage: number
  oldestContainer?: Container
  newestContainer?: Container
  largestContainer?: Container
  mostActiveContainer?: Container
  lastUpdated: string
}

// 容器事件
export interface ContainerEvent {
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

// 容器执行
export interface ContainerExecRequest {
  containerId: string
  command: string[]
  attachStdin?: boolean
  attachStdout?: boolean
  attachStderr?: boolean
  detach?: boolean
  tty?: boolean
  env?: string[]
  user?: string
  workingDir?: string
  privileged?: boolean
}

export interface ContainerExecResponse {
  id: string
  warnings: string[]
}

export interface ContainerExecStartRequest {
  execId: string
  detach?: boolean
  tty?: boolean
  consoleSize?: {
    height: number
    width: number
  }
}

export interface ContainerExecInspectResponse {
  canRemove: boolean
  containerID: string
  detachKeys: string
  exitCode: number
  id: string
  openStderr: boolean
  openStdin: boolean
  openStdout: boolean
  running: boolean
  pid: number
  processConfig: {
    arguments: string[]
    entrypoint: string
    privileged: boolean
    tty: boolean
    user: string
  }
}

// 容器搜索和过滤
export interface ContainerSearchParams extends PaginationParams {
  name?: string
  status?: Status
  image?: string
  nodeId?: string
  label?: string
  expose?: string
  publish?: string
  volume?: string
  network?: string
  health?: string
  isTask?: boolean
  before?: string
  since?: string
  limit?: number
  filters?: Record<string, string[]>
  sort?: 'created' | 'name' | 'status' | 'image'
  order?: 'asc' | 'desc'
}

export interface ContainerSearchResponse {
  containers: Container[]
  total: number
  filtered: number
  page: number
  pageSize: number
  totalPages?: number
  hasNext?: boolean
  hasPrev?: boolean
}

// 容器批量操作
export interface BatchContainerOperation {
  containerIds: string[]
  operation: 'start' | 'stop' | 'restart' | 'pause' | 'unpause' | 'remove' | 'kill'
  options?: {
    force?: boolean
    timeout?: number
    signal?: string
    removeVolumes?: boolean
    removeLinks?: boolean
  }
}

export interface BatchContainerResult {
  totalCount: number
  successCount: number
  failureCount: number
  results: Array<{
    containerId: string
    success: boolean
    error?: string
  }>
}