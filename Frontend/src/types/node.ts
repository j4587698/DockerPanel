// ==================== 节点管理相关类型定义 ====================

import { BaseConfig, Status, Priority, PaginationParams, PaginationResponse, OperationResult } from './common'

// 节点基础信息
export interface Node extends BaseConfig {
  name: string
  address: string
  port: number
  status: NodeStatus
  version: string
  apiVersion: string
  architecture: string
  os: string
  kernel: string
  resources: NodeResources
  docker: NodeDockerInfo
  network: NodeNetworkInfo
  storage: NodeStorageInfo
  security: NodeSecurity
  monitoring: NodeMonitoring
  labels: Record<string, string>
  annotations: Record<string, string>
  role: NodeRole
  availability: NodeAvailability
  conditions: NodeCondition[]
  metadata: NodeMetadata
  usage: NodeUsage
  lastHeartbeat: string
  heartbeatInterval: number
  connectionStatus: NodeConnectionStatus
}

// 节点状态
export interface NodeStatus {
  status: 'ready' | 'not_ready' | 'unknown' | 'maintenance' | 'cordoned' | 'draining'
  phase: 'running' | 'pending' | 'terminated' | 'unknown'
  message?: string
  reason?: string
  lastTransitionTime: string
  conditions: NodeCondition[]
}

export interface NodeCondition {
  type: 'ready' | 'memory_pressure' | 'disk_pressure' | 'pid_pressure' | 'network_unavailable' | 'kubelet_ready'
  status: 'true' | 'false' | 'unknown'
  lastHeartbeatTime: string
  lastTransitionTime: string
  reason?: string
  message?: string
}

// 节点角色
export interface NodeRole {
  master: boolean
  worker: boolean
  manager: boolean
  agent: boolean
  control_plane: boolean
  etcd: boolean
  ingress: boolean
  storage: boolean
  customRoles: string[]
}

// 节点可用性
export interface NodeAvailability {
  scheduling: boolean
  draining: boolean
  cordoned: boolean
  maintenance: boolean
  unschedulable: boolean
  reason?: string
  since?: string
}

// 节点资源
export interface NodeResources {
  capacity: NodeResourceCapacity
  allocatable: NodeResourceCapacity
  usage: NodeResourceUsage
  requests: NodeResourceRequests
  limits: NodeResourceLimits
  pressure: NodeResourcePressure
}

export interface NodeResourceCapacity {
  cpu: string
  memory: string
  ephemeral_storage: string
  hugepages_1Gi?: string
  hugepages_2Mi?: string
  pods: string
  nvidia_com_gpu?: string
  devices?: Record<string, string>
}

export interface NodeResourceUsage {
  cpu: NodeCPUUsage
  memory: NodeMemoryUsage
  storage: NodeStorageUsage
  network: NodeNetworkUsage
  pods: NodePodUsage
  devices: NodeDeviceUsage[]
}

export interface NodeCPUUsage {
  cores: number
  usage: number
  usage_percent: number
  load_average: {
    load1: number
    load5: number
    load15: number
  }
  context_switches: number
  interrupts: number
  processes: number
  threads: number
}

export interface NodeMemoryUsage {
  total: number
  used: number
  free: number
  available: number
  usage_percent: number
  swap: {
    total: number
    used: number
    free: number
  }
  cached: number
  buffers: number
  shared: number
  slab: number
}

export interface NodeStorageUsage {
  total: number
  used: number
  free: number
  usage_percent: number
  filesystems: NodeFilesystemUsage[]
  inodes: {
    total: number
    used: number
    free: number
    usage_percent: number
  }
}

export interface NodeFilesystemUsage {
  device: string
  mountpoint: string
  fstype: string
  total: number
  used: number
  free: number
  usage_percent: number
  inodes: {
    total: number
    used: number
    free: number
    usage_percent: number
  }
}

export interface NodeNetworkUsage {
  interfaces: NodeNetworkInterface[]
  connections: NodeNetworkConnection[]
  bandwidth: NodeNetworkBandwidth
  packets: NodeNetworkPackets
  errors: NodeNetworkErrors
}

export interface NodeNetworkInterface {
  name: string
  type: string
  speed: number
  mtu: number
  mac: string
  ipv4_addresses: string[]
  ipv6_addresses: string[]
  rx_bytes: number
  tx_bytes: number
  rx_packets: number
  tx_packets: number
  rx_errors: number
  tx_errors: number
  rx_dropped: number
  tx_dropped: number
  carrier: boolean
  duplex: string
  operstate: string
}

export interface NodeNetworkConnection {
  local_address: string
  local_port: number
  remote_address: string
  remote_port: number
  protocol: string
  state: string
  pid?: number
  process?: string
}

export interface NodeNetworkBandwidth {
  rx_bps: number
  tx_bps: number
  average_bps: number
  peak_bps: number
}

export interface NodeNetworkPackets {
  rx_pps: number
  tx_pps: number
  rx_dropped: number
  tx_dropped: number
}

export interface NodeNetworkErrors {
  rx_errors: number
  tx_errors: number
  rx_frame_errors: number
  rx_carrier_errors: number
  rx_fifo_errors: number
  tx_aborted_errors: number
}

export interface NodePodUsage {
  total: number
  running: number
  pending: number
  succeeded: number
  failed: number
  unknown: number
}

export interface NodeDeviceUsage {
  name: string
  type: string
  vendor: string
  model: string
  driver: string
  usage: NodeDeviceResourceUsage[]
}

export interface NodeDeviceResourceUsage {
  resource: string
  used: number
  total: number
  usage_percent: number
}

export interface NodeResourceRequests {
  cpu: number
  memory: number
  storage: number
  pods: number
  devices: Record<string, NodeDeviceResourceUsage[]>
}

export interface NodeResourceLimits {
  cpu: number
  memory: number
  storage: number
  pods: number
  devices: Record<string, NodeDeviceResourceUsage[]>
}

export interface NodeResourcePressure {
  memory: boolean
  disk: boolean
  pid: boolean
  network: boolean
  details: Record<string, any>
}

// 节点Docker信息
export interface NodeDockerInfo {
  version: NodeDockerVersion
  info: NodeDockerInfoData
  system: NodeDockerSystem
  registry: NodeDockerRegistry
  plugins: NodeDockerPlugins
  security: NodeDockerSecurity
}

export interface NodeDockerVersion {
  server: NodeDockerVersionInfo
  client: NodeDockerVersionInfo
  api: string
  min_api: string
  git_commit: string
  go_version: string
  os: string
  arch: string
  kernel_version: string
  build_time: string
  experimental: boolean
}

export interface NodeDockerVersionInfo {
  version: string
  api_version: string
  git_commit: string
  built: string
  os: string
  arch: string
}

export interface NodeDockerInfoData {
  id: string
  containers: number
  containers_running: number
  containers_paused: number
  containers_stopped: number
  images: number
  server_version: string
  memory_total: number
  debug: boolean
  experimental: boolean
  cgroup_driver: string
  cgroup_version: string
  n_events_listener: number
  kernel_version: string
  operating_system: string
  os_type: string
  architecture: string
  ncpu: number
  mem_total: number
  docker_root_dir: string
  http_proxy: string
  https_proxy: string
  no_proxy: string
  name: string
  labels: string[]
}

export interface NodeDockerSystem {
  df: NodeDockerSystemDf
  events: NodeDockerSystemEvent[]
  prune: NodeDockerSystemPrune
  version: NodeDockerVersion
}

export interface NodeDockerSystemDf {
  layers_size: number
  images: NodeDockerSystemDfImage[]
  containers: NodeDockerSystemDfContainer[]
  volumes: NodeDockerSystemDfVolume[]
  build_cache: NodeDockerSystemDfBuildCache
}

export interface NodeDockerSystemDfImage {
  repositories: string[]
  tag: string
  image_id: string
  created: string
  shared_size: number
  unique_size: number
  size: number
}

export interface NodeDockerSystemDfContainer {
  names: string[]
  container_id: string
  image: string
  created: string
  size: number
  shared_size: number
  unique_size: number
}

export interface NodeDockerSystemDfVolume {
  name: string
  links: string
  size: number
}

export interface NodeDockerSystemDfBuildCache {
  type: string
  size: number
  shared_size: number
  unique_size: number
}

export interface NodeDockerSystemEvent {
  status: string
  id: string
  from: string
  type: string
  action: string
  actor: NodeDockerEventActor
  scope: string
  time: number
  time_nano: number
}

export interface NodeDockerEventActor {
  id: string
  attributes: Record<string, string>
}

export interface NodeDockerSystemPrune {
  containers: number
  images: number
  volumes: number
  build_cache: number
  reclaimed_space: number
}

export interface NodeDockerRegistry {
  registries: NodeDockerRegistryInfo[]
  search: NodeDockerSearchResult[]
}

export interface NodeDockerRegistryInfo {
  name: string
  url: string
  insecure: boolean
  blocking: boolean
  mirrors: string[]
}

export interface NodeDockerSearchResult {
  name: string
  description: string
  stars: number
  official: boolean
  automated: boolean
}

export interface NodeDockerPlugins {
  volume: NodeDockerPlugin[]
  network: NodeDockerPlugin[]
  authorization: NodeDockerPlugin[]
  log: NodeDockerPlugin[]
  metrics: NodeDockerPlugin[]
}

export interface NodeDockerPlugin {
  name: string
  description: string
  enabled: boolean
  reference: string
  config: NodeDockerPluginConfig
}

export interface NodeDockerPluginConfig {
  args: string[]
  description: string
  documentation: string
  entrypoint: string
  env: string[]
  interfaces: NodeDockerPluginInterface[]
  ipc_host: boolean
  linux: NodeDockerPluginLinux
  mount_source: string
  network_source: string
  pid_host: boolean
  propagated_mount: string
  work_dir: string
  rootfs_propagation: string
  user: NodeDockerPluginUser
  capabilities: string[]
  mounts: NodeDockerPluginMount[]
  env: string[]
  args: string[]
}

export interface NodeDockerPluginInterface {
  capabilities: string[]
  socket: string
  types: string[]
}

export interface NodeDockerPluginLinux {
  allow_all_devices: boolean
  capabilities: string[]
  devices: string[]
}

export interface NodeDockerPluginUser {
  uid: number
  gid: number
}

export interface NodeDockerPluginMount {
  destination: string
  options: string[]
  name: string
  source: string
  type: string
}

export interface NodeDockerSecurity {
  security_options: string[]
  default_address_pools: NodeDockerAddressPool[]
  storage_driver: string
  logging_driver: string
  cgroup_driver: string
  userland_proxy: boolean
  live_restore: boolean
  userns_remap: string
  no_new_privileges: boolean
  seccomp_profile: string
  selinux_enabled: boolean
  apparmor_profile: string
  default_ulimits: NodeDockerUlimit[]
  default_runtime: string
  runtimes: Record<string, NodeDockerRuntime>
  init: boolean
  init_path: string
  swarm: NodeDockerSwarmInfo
  cluster_store: string
  cluster_advertise: string
  cluster_store_opts: Record<string, string>
}

export interface NodeDockerAddressPool {
  base: string
  size: number
}

export interface NodeDockerUlimit {
  name: string
  hard: number
  soft: number
}

export interface NodeDockerRuntime {
  path: string
  runtime_args: string[]
}

export interface NodeDockerSwarmInfo {
  node_id: string
  node_addr: string
  local_node_state: string
  control_available: boolean
  error: string
  remote_managers: NodeDockerSwarmManager[]
  nodes: number
  managers: number
  cluster: NodeDockerSwarmCluster
}

export interface NodeDockerSwarmManager {
  node_id: string
  addr: string
  status: string
  leader: boolean
  reachability: string
}

export interface NodeDockerSwarmCluster {
  id: string
  version: NodeDockerSwarmVersion
  created_at: string
  updated_at: string
  spec: NodeDockerSwarmSpec
  tls_info: NodeDockerSwarmTLSInfo
  root_rotationInProgress: boolean
  data_path_port: number
  default_addr_pool: string[]
  subnet_size: number
}

export interface NodeDockerSwarmVersion {
  index: number
}

export interface NodeDockerSwarmSpec {
  name: string
  labels: Record<string, string>
  orchestration: NodeDockerSwarmOrchestration
  raft: NodeDockerSwarmRaft
  dispatcher: NodeDockerSwarmDispatcher
  ca_config: NodeDockerSwarmCAConfig
  task_defaults: NodeDockerSwarmTaskDefaults
  encryption_config: NodeDockerSwarmEncryptionConfig
}

export interface NodeDockerSwarmOrchestration {
  task_history_retention_limit: number
}

export interface NodeDockerSwarmRaft {
  snapshot_interval: number
  keep_old_snapshots: number
  log_entries_for_slow_followers: number
  election_tick: number
  heartbeat_tick: number
}

export interface NodeDockerSwarmDispatcher {
  heartbeat_period: number
}

export interface NodeDockerSwarmCAConfig {
  node_cert_expiry: number
}

export interface NodeDockerSwarmTaskDefaults {
  log_driver: NodeDockerLogDriver
}

export interface NodeDockerLogDriver {
  name: string
  options: Record<string, string>
}

export interface NodeDockerSwarmEncryptionConfig {
  auto_lock_managers: boolean
  key_rotation_interval: number
}

export interface NodeDockerSwarmTLSInfo {
  trust_root: string
  cert_issuer_subject: string
  cert_subject: string
}

// 节点网络信息
export interface NodeNetworkInfo {
  interfaces: NodeNetworkInterface[]
  routes: NodeNetworkRoute[]
  dns: NodeNetworkDNS
  hostname: string
  domainname: string
  resolvers: string[]
  search_domains: string[]
  hosts: NodeNetworkHost[]
  firewall: NodeNetworkFirewall
  bandwidth: NodeNetworkBandwidth
  latency: NodeNetworkLatency
  packet_loss: NodeNetworkPacketLoss
}

export interface NodeNetworkRoute {
  destination: string
  gateway: string
  interface: string
  metric: number
  flags: string[]
}

export interface NodeNetworkDNS {
  nameservers: string[]
  search: string[]
  options: string[]
}

export interface NodeNetworkHost {
  ip: string
  hostname: string[]
  aliases: string[]
}

export interface NodeNetworkFirewall {
  enabled: boolean
  rules: NodeFirewallRule[]
  chains: NodeFirewallChain[]
  policies: NodeFirewallPolicy[]
}

export interface NodeFirewallRule {
  id: string
  chain: string
  action: string
  protocol: string
  source: string
  destination: string
  source_port?: string
  destination_port?: string
  interface?: string
  comment?: string
  enabled: boolean
  hits: number
  last_hit?: string
}

export interface NodeFirewallChain {
  name: string
  policy: string
  rules: NodeFirewallRule[]
  packets: number
  bytes: number
}

export interface NodeFirewallPolicy {
  name: string
  target: string
  rules: NodeFirewallRule[]
  priority: number
}

export interface NodeNetworkBandwidth {
  current: NodeNetworkBandwidthCurrent
  history: NodeNetworkBandwidthHistory[]
  limits: NodeNetworkBandwidthLimits
}

export interface NodeNetworkBandwidthCurrent {
  rx_bps: number
  tx_bps: number
  rx_pps: number
  tx_pps: number
}

export interface NodeNetworkBandwidthHistory {
  timestamp: string
  rx_bps: number
  tx_bps: number
  rx_pps: number
  tx_pps: number
}

export interface NodeNetworkBandwidthLimits {
  rx_bps: number
  tx_bps: number
  rx_pps: number
  tx_pps: number
}

export interface NodeNetworkLatency {
  current: NodeNetworkLatencyCurrent
  history: NodeNetworkLatencyHistory[]
  targets: NodeNetworkLatencyTarget[]
}

export interface NodeNetworkLatencyCurrent {
  min: number
  avg: number
  max: number
  stddev: number
}

export interface NodeNetworkLatencyHistory {
  timestamp: string
  min: number
  avg: number
  max: number
  stddev: number
}

export interface NodeNetworkLatencyTarget {
  host: string
  latency: number
  jitter: number
  packet_loss: number
  last_check: string
}

export interface NodeNetworkPacketLoss {
  current: NodeNetworkPacketLossCurrent
  history: NodeNetworkPacketLossHistory[]
  interfaces: NodeNetworkInterfacePacketLoss[]
}

export interface NodeNetworkPacketLossCurrent {
  rx_dropped: number
  tx_dropped: number
  rx_errors: number
  tx_errors: number
}

export interface NodeNetworkPacketLossHistory {
  timestamp: string
  rx_dropped: number
  tx_dropped: number
  rx_errors: number
  tx_errors: number
}

export interface NodeNetworkInterfacePacketLoss {
  interface: string
  rx_dropped: number
  tx_dropped: number
  rx_errors: number
  tx_errors: number
}

// 节点存储信息
export interface NodeStorageInfo {
  filesystems: NodeFilesystemUsage[]
  disks: NodeDiskInfo[]
  volumes: NodeVolumeInfo[]
  io_stats: NodeStorageIOStats
  space: NodeStorageSpace
  performance: NodeStoragePerformance
  health: NodeStorageHealth
}

export interface NodeDiskInfo {
  name: string
  model: string
  vendor: string
  serial: string
  size: number
  type: string
  rotational: boolean
  read_only: boolean
  temperature?: number
  smart_status: NodeDiskSmartStatus
  partitions: NodeDiskPartition[]
  io_stats: NodeDiskIOStats
}

export interface NodeDiskSmartStatus {
  status: string
  health: string
  attributes: NodeDiskSmartAttribute[]
  last_test: NodeDiskSmartTest
  temperature: number
  power_on_hours: number
}

export interface NodeDiskSmartAttribute {
  id: number
  name: string
  raw_value: number
  normalized_value: number
  threshold: number
  status: string
}

export interface NodeDiskSmartTest {
  type: string
  status: string
  remaining_percent: number
  lifetime_hours: number
  lba_of_first_error: number
}

export interface NodeDiskPartition {
  name: string
  size: number
  type: string
  mountpoint?: string
  filesystem?: string
  uuid?: string
  label?: string
  flags: string[]
}

export interface NodeDiskIOStats {
  read_bytes: number
  write_bytes: number
  read_operations: number
  write_operations: number
  read_time: number
  write_time: number
  io_time: number
  weighted_io_time: number
}

export interface NodeStorageIOStats {
  reads: {
    total: number
    merged: number
    bytes: number
    time: number
  }
  writes: {
    total: number
    merged: number
    bytes: number
    time: number
  }
  io: {
    total: number
    time: number
    weighted_time: number
  }
  iops: {
    read: number
    write: number
    total: number
  }
  throughput: {
    read: number
    write: number
    total: number
  }
  latency: {
    read: number
    write: number
    avg: number
  }
}

export interface NodeStorageSpace {
  total: number
  used: number
  free: number
  available: number
  usage_percent: number
  reserved: number
  quotas: NodeStorageQuota[]
  snapshots: NodeStorageSnapshot[]
}

export interface NodeStorageQuota {
  path: string
  used: number
  limit: number
  usage_percent: number
}

export interface NodeStorageSnapshot {
  name: string
  path: string
  size: number
  created: string
  expires?: string
}

export interface NodeStoragePerformance {
  io_stats: NodeStorageIOStats
  benchmarks: NodeStorageBenchmark[]
  predictions: NodeStoragePrediction[]
  recommendations: NodeStorageRecommendation[]
}

export interface NodeStorageBenchmark {
  name: string
  type: 'seq_read' | 'seq_write' | 'rand_read' | 'rand_write' | 'mixed'
  score: number
  unit: string
  timestamp: string
  details: Record<string, any>
}

export interface NodeStoragePrediction {
  metric: string
  prediction: number
  confidence: number
  timeframe: string
  model: string
  last_updated: string
}

export interface NodeStorageRecommendation {
  type: string
  priority: 'low' | 'medium' | 'high'
  title: string
  description: string
  impact: string
  implementation: string
}

export interface NodeStorageHealth {
  status: 'healthy' | 'warning' | 'error' | 'unknown'
  checks: NodeStorageHealthCheck[]
  issues: NodeStorageIssue[]
  last_check: string
}

export interface NodeStorageHealthCheck {
  name: string
  status: 'pass' | 'fail' | 'warn'
  message: string
  timestamp: string
  details?: Record<string, any>
}

export interface NodeStorageIssue {
  severity: 'low' | 'medium' | 'high' | 'critical'
  category: string
  title: string
  description: string
  detected_at: string
  resolved_at?: string
}

export interface NodeVolumeInfo {
  name: string
  driver: string
  mountpoint: string
  created: string
  size: number
  usage: NodeVolumeUsage
  status: string
  labels: Record<string, string>
  options: Record<string, string>
}

export interface NodeVolumeUsage {
  size: number
  used: number
  available: number
  usage_percent: number
  inodes: {
    total: number
    used: number
    free: number
    usage_percent: number
  }
}

// 节点安全信息
export interface NodeSecurity {
  authentication: NodeAuthentication
  authorization: NodeAuthorization
  certificates: NodeCertificate[]
  firewall: NodeNetworkFirewall
  intrusion_detection: NodeIntrusionDetection
  compliance: NodeCompliance
  vulnerabilities: NodeVulnerability[]
  patches: NodePatch[]
  policies: NodeSecurityPolicy[]
  audits: NodeSecurityAudit[]
}

export interface NodeAuthentication {
  methods: string[]
  password_policy: NodePasswordPolicy
  session_timeout: number
  max_attempts: number
  lockout_duration: number
  two_factor: NodeTwoFactorAuth
}

export interface NodePasswordPolicy {
  min_length: number
  require_uppercase: boolean
  require_lowercase: boolean
  require_numbers: boolean
  require_symbols: boolean
  prevent_reuse: number
  expiration_days: number
}

export interface NodeTwoFactorAuth {
  enabled: boolean
  methods: string[]
  backup_codes: boolean
  enforcement: 'optional' | 'required' | 'conditional'
}

export interface NodeAuthorization {
  roles: NodeRole[]
  permissions: NodePermission[]
  policies: NodeAuthorizationPolicy[]
}

export interface NodeAuthorizationPolicy {
  name: string
  type: string
  rules: NodeAuthorizationRule[]
  effect: string
}

export interface NodeAuthorizationRule {
  principal: string
  resource: string
  action: string
  condition?: string
  effect: string
}

export interface NodeCertificate {
  id: string
  name: string
  subject: string
  issuer: string
  serial_number: string
  fingerprint: string
  not_before: string
  not_after: string
  status: string
  key_algorithm: string
  signature_algorithm: string
  key_size: number
  purposes: string[]
}

export interface NodeIntrusionDetection {
  enabled: boolean
  rules: NodeIDSRules[]
  alerts: NodeIDSAlert[]
  logs: NodeIDSLog[]
}

export interface NodeIDSRules {
  id: string
  name: string
  pattern: string
  action: string
  severity: string
  enabled: boolean
}

export interface NodeIDSAlert {
  id: string
  rule: string
  severity: string
  message: string
  timestamp: string
  source: string
  details: Record<string, any>
}

export interface NodeIDSLog {
  timestamp: string
  level: string
  message: string
  source: string
  details: Record<string, any>
}

export interface NodeCompliance {
  standards: NodeComplianceStandard[]
  status: NodeComplianceStatus
  last_assessment: string
  issues: NodeComplianceIssue[]
}

export interface NodeComplianceStandard {
  name: string
  version: string
  description: string
  requirements: NodeComplianceRequirement[]
}

export interface NodeComplianceRequirement {
  id: string
  description: string
  category: string
  mandatory: boolean
  status: string
  evidence?: string
  last_checked: string
}

export interface NodeComplianceStatus {
  compliant: boolean
  score: number
  issues_count: number
  last_check: string
}

export interface NodeComplianceIssue {
  standard: string
  requirement: string
  severity: string
  description: string
  remediation: string
  detected_at: string
  resolved_at?: string
}

export interface NodeVulnerability {
  id: string
  name: string
  severity: string
  description: string
  affected_package: string
  fixed_version?: string
  cve?: string
  cvss_score?: number
  published_date: string
  detected_at: string
  status: string
}

export interface NodePatch {
  id: string
  name: string
  version: string
  description: string
  severity: string
  category: string
  installed: boolean
  installed_date?: string
  reboot_required: boolean
  dependencies: string[]
}

export interface NodeSecurityPolicy {
  id: string
  name: string
  description: string
  category: string
  enabled: boolean
  rules: NodeSecurityRule[]
  schedule?: string
  exceptions: string[]
}

export interface NodeSecurityRule {
  id: string
  name: string
  type: string
  condition: string
  action: string
  severity: string
  enabled: boolean
}

export interface NodeSecurityAudit {
  id: string
  type: string
  status: string
  started_at: string
  completed_at?: string
  findings: NodeSecurityAuditFinding[]
  score: number
}

export interface NodeSecurityAuditFinding {
  id: string
  severity: string
  category: string
  title: string
  description: string
  recommendation: string
  evidence: string[]
}

// 节点监控
export interface NodeMonitoring {
  enabled: boolean
  metrics: NodeMetrics
  alerts: NodeAlert[]
  logging: NodeLogging
  health_checks: NodeHealthCheck[]
  dashboards: NodeDashboard[]
}

export interface NodeMetrics {
  enabled: boolean
  interval: number
  retention: number
  categories: NodeMetricsCategory[]
}

export interface NodeMetricsCategory {
  name: string
  enabled: boolean
  metrics: string[]
}

export interface NodeAlert {
  id: string
  name: string
  type: string
  severity: string
  condition: string
  threshold?: number
  enabled: boolean
  notifications: NodeNotification[]
}

export interface NodeNotification {
  type: string
  destination: string
  template?: string
  enabled: boolean
}

export interface NodeLogging {
  enabled: boolean
  level: string
  format: string
  outputs: NodeLogOutput[]
  fields: NodeLogField[]
}

export interface NodeLogOutput {
  type: string
  config: Record<string, any>
  enabled: boolean
}

export interface NodeLogField {
  name: string
  source: string
  enabled: boolean
  transform?: string
}

export interface NodeHealthCheck {
  id: string
  name: string
  type: string
  enabled: boolean
  interval: number
  timeout: number
  config: Record<string, any>
  last_check?: string
  status: string
  message?: string
}

export interface NodeDashboard {
  id: string
  name: string
  description: string
  widgets: NodeWidget[]
  layout: NodeLayout
  refresh_interval: number
}

export interface NodeWidget {
  id: string
  type: string
  title: string
  query: string
  visualization: NodeVisualization
  position: NodePosition
  size: NodeSize
}

export interface NodeVisualization {
  type: string
  options: Record<string, any>
}

export interface NodePosition {
  x: number
  y: number
}

export interface NodeSize {
  width: number
  height: number
}

export interface NodeLayout {
  columns: number
  gap: number
}

// 节点元数据
export interface NodeMetadata {
  version: string
  tags: string[]
  categories: string[]
  description?: string
  documentation?: string
  contact?: NodeContact
  license?: string
  changelog?: string
}

export interface NodeContact {
  name?: string
  email?: string
  url?: string
}

// 节点使用情况
export interface NodeUsage {
  resources: NodeResourceUsage
  containers: NodeContainerUsage
  images: NodeImageUsage
  volumes: NodeVolumeUsage[]
  networks: NodeNetworkUsage[]
  performance: NodePerformanceUsage
  last_updated: string
}

export interface NodeContainerUsage {
  total: number
  running: number
  paused: number
  stopped: number
  by_state: Record<string, number>
  by_image: Record<string, number>
  resource_usage: NodeContainerResourceUsage[]
}

export interface NodeContainerResourceUsage {
  container_id: string
  container_name: string
  cpu_percent: number
  memory_usage: number
  memory_limit: number
  memory_percent: number
  network_rx: number
  network_tx: number
  block_read: number
  block_write: number
  pids: number
}

export interface NodeImageUsage {
  total: number
  total_size: number
  by_repository: Record<string, number>
  dangling: number
  unused: number
  size_by_repository: Record<string, number>
  last_pulled?: string
}

export interface NodePerformanceUsage {
  cpu: NodeCPUUsage
  memory: NodeMemoryUsage
  disk: NodeDiskIOStats
  network: NodeNetworkUsage
  io_wait: number
  load_average: {
    load1: number
    load5: number
    load15: number
  }
  uptime: number
  processes: number
  context_switches: number
  interrupts: number
}

// 节点连接状态
export interface NodeConnectionStatus {
  status: 'connected' | 'disconnected' | 'connecting' | 'error'
  last_connected?: string
  last_disconnected?: string
  connection_duration?: number
  latency?: number
  bandwidth?: number
  error_message?: string
  retry_count: number
}

// 节点创建请求
export interface CreateNodeRequest {
  name: string
  address: string
  port: number
  role: NodeRole
  labels?: Record<string, string>
  annotations?: Record<string, string>
  taints?: Record<string, string>
  config?: NodeConfig
  monitoring?: NodeMonitoring
  security?: NodeSecurity
}

export interface NodeConfig {
  kubelet: NodeKubeletConfig
  proxy: NodeProxyConfig
  docker: NodeDockerConfig
  network: NodeNetworkConfig
  storage: NodeStorageConfig
}

export interface NodeKubeletConfig {
  address: string
  port: number
  read_only_port: number
  cluster_dns: string[]
  cluster_domain: string
  allow_privileged: boolean
  host_network_sources: string[]
  host_pid_sources: string[]
  host_ipc_sources: string[]
  pod_manifest_path: string
  resolv_conf: string
  cpu_cfs_quota: boolean
  cpu_cfs_period: boolean
  max_pods: number
  pod_cidr: string
  network_plugin: string
  network_plugin_dir: string
  cgroup_driver: string
  cgroups_per_qos: boolean
  enforce_node_allocatable: string[]
  experimental_allocatable_ignore_eviction: boolean
  max_pods: number
  pod_infra_container_image: string
}

export interface NodeProxyConfig {
  bind_address: string
  healthz_port: number
  metrics_port: number
  hostname_override: string
  cluster_cidr: string
  conntrack_max: number
  conntrack_max_per_core: number
  conntrack_tcp_timeout_established: number
  conntrack_tcp_timeout_close_wait: number
  oom_score_adj: number
}

export interface NodeDockerConfig {
  endpoint: string
  version: string
  tls_verify: boolean
  tls_ca_file: string
  tls_cert_file: string
  tls_key_file: string
}

export interface NodeNetworkConfig {
  pod_cidr: string
  service_cluster_ip_range: string
  cluster_dns: string[]
  cluster_domain: string
}

export interface NodeStorageConfig {
  driver: string
  options: Record<string, string>
}

// 节点搜索和过滤
export interface NodeSearchParams extends PaginationParams {
  name?: string
  status?: string
  role?: string
  address?: string
  port?: number
  architecture?: string
  os?: string
  label?: string
  annotation?: string
  taint?: string
  filters?: Record<string, string[]>
  sort?: 'created' | 'name' | 'status' | 'cpu' | 'memory'
  order?: 'asc' | 'desc'
}

export interface NodeSearchResponse {
  nodes: Node[]
  total: number
  filtered: number
  page: number
  pageSize: number
  totalPages?: number
  hasNext?: boolean
  hasPrev?: boolean
}

// 节点统计信息
export interface NodeStatistics {
  total_nodes: number
  ready_nodes: number
  not_ready_nodes: number
  master_nodes: number
  worker_nodes: number
  nodes_by_status: Record<string, number>
  nodes_by_architecture: Record<string, number>
  nodes_by_os: Record<string, number>
  total_cpu: number
  total_memory: number
  total_storage: number
  average_cpu_usage: number
  average_memory_usage: number
  average_storage_usage: number
  last_updated: string
}

// 批量节点操作
export interface BatchNodeOperation {
  nodeIds: string[]
  operation: 'drain' | 'cordon' | 'uncordon' | 'label' | 'taint' | 'delete' | 'update' | 'restart'
  options?: {
    force?: boolean
    grace_period?: number
    labels?: Record<string, string>
    taints?: Record<string, string>
    config?: Partial<NodeConfig>
  }
}

export interface BatchNodeResult {
  total_count: number
  success_count: number
  failure_count: number
  results: Array<{
    node_id: string
    success: boolean
    error?: string
    details?: any
  }>
}