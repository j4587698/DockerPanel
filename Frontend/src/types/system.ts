// ==================== 系统管理相关类型定义 ====================

import { BaseConfig, Status, Priority, PaginationParams, PaginationResponse, OperationResult } from './common'

// 系统基础信息
export interface System extends BaseConfig {
  hostname: string
  domain: string
  platform: SystemPlatform
  kernel: SystemKernel
  hardware: SystemHardware
  memory: SystemMemory
  cpu: SystemCPU
  storage: SystemStorage
  network: SystemNetwork
  processes: SystemProcesses
  services: SystemServices
  users: SystemUsers
  security: SystemSecurity
  monitoring: SystemMonitoring
  performance: SystemPerformance
  uptime: number
  boot_time: string
  timezone: string
  locale: string
  environment: SystemEnvironment
}

// 系统平台信息
export interface SystemPlatform {
  os: string
  os_type: string
  os_version: string
  os_release: string
  os_codename: string
  architecture: string
  platform: string
  platform_family: string
  platform_version: string
  virtualization: SystemVirtualization
  container: SystemContainer
}

export interface SystemVirtualization {
  type: 'none' | 'kvm' | 'xen' | 'vmware' | 'hyper-v' | 'virtualbox' | 'qemu' | 'docker' | 'lxc' | 'openvz'
  role: 'host' | 'guest' | 'none'
  systems: string[]
}

export interface SystemContainer {
  runtime: string
  version: string
  daemon: boolean
  podman: boolean
  docker: boolean
  containerd: boolean
  cri_o: boolean
}

// 系统内核信息
export interface SystemKernel {
  version: string
  release: string
  architecture: string
  machine: string
  processor: string
  hardware_platform: string
  operating_system: string
  compile_time: string
  compile_domain: string
  compile_user: string
  modules: SystemKernelModule[]
  parameters: Record<string, string>
  config: Record<string, string>
  sysctl: Record<string, string>
}

export interface SystemKernelModule {
  name: string
  size: number
  used: number
  used_by: string[]
  loaded: boolean
  live: boolean
  signature: string
  srcversion: string
  depends: string[]
  alias: string[]
  parm: Record<string, string>
}

// 系统硬件信息
export interface SystemHardware {
  motherboard: SystemMotherboard
  bios: SystemBIOS
  chassis: SystemChassis
  processors: SystemProcessor[]
  memory_modules: SystemMemoryModule[]
  storage_devices: SystemStorageDevice[]
  network_interfaces: SystemNetworkInterface[]
  graphics: SystemGraphics[]
  audio: SystemAudio[]
  usb: SystemUSB[]
  pci: SystemPCI[]
  sensors: SystemSensors[]
  power: SystemPower
}

export interface SystemMotherboard {
  manufacturer: string
  product: string
  version: string
  serial: string
  uuid: string
  asset_tag: string
}

export interface SystemBIOS {
  vendor: string
  version: string
  release_date: string
  mode: string
  type: string
  characteristics: string[]
}

export interface SystemChassis {
  manufacturer: string
  type: string
  version: string
  serial: string
  asset_tag: string
  boot_up_state: string
  power_supply_state: string
  thermal_state: string
  security_status: string
}

export interface SystemProcessor {
  id: number
  socket: string
  vendor: string
  model: string
  family: string
  stepping: string
  architecture: string
  speed: number
  max_speed: number
  current_speed: number
  cores: number
  logical_cores: number
  threads: number
  cache: SystemProcessorCache
  flags: string[]
  virtualization: boolean
  l1d_cache: number
  l1i_cache: number
  l2_cache: number
  l3_cache: number
  temperature?: number
  voltage?: number
  power_usage?: number
}

export interface SystemProcessorCache {
  l1d: number
  l1i: number
  l2: number
  l3: number
  size: number
  type: string
  level: number
  shared: boolean
}

export interface SystemMemoryModule {
  slot: string
  size: number
  type: string
  speed: number
  manufacturer: string
  part_number: string
  serial_number: string
  form_factor: string
  voltage: number
  ecc: boolean
  registered: boolean
  temperature?: number
  errors: number
}

export interface SystemStorageDevice {
  name: string
  model: string
  vendor: string
  serial: string
  size: number
  type: string
  interface: string
  rotational: boolean
  rpm: number
  firmware: string
  temperature?: number
  smart_status: SystemDeviceSmartStatus
  partitions: SystemDevicePartition[]
  io_stats: SystemDeviceIOStats
}

export interface SystemDeviceSmartStatus {
  status: string
  health: string
  attributes: SystemDeviceSmartAttribute[]
  last_test: SystemDeviceSmartTest
  temperature: number
  power_on_hours: number
}

export interface SystemDeviceSmartAttribute {
  id: number
  name: string
  raw_value: number
  normalized_value: number
  threshold: number
  status: string
}

export interface SystemDeviceSmartTest {
  type: string
  status: string
  remaining_percent: number
  lifetime_hours: number
  lba_of_first_error: number
}

export interface SystemDevicePartition {
  name: string
  size: number
  type: string
  mountpoint?: string
  filesystem?: string
  uuid?: string
  label?: string
  flags: string[]
}

export interface SystemDeviceIOStats {
  read_bytes: number
  write_bytes: number
  read_operations: number
  write_operations: number
  read_time: number
  write_time: number
  io_time: number
  weighted_io_time: number
}

export interface SystemNetworkInterface {
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
  driver: string
  firmware: string
  bus_info: string
}

export interface SystemGraphics {
  id: number
  vendor: string
  model: string
  driver: string
  memory: number
  temperature?: number
  utilization?: number
  power_usage?: number
  displays: SystemDisplay[]
}

export interface SystemDisplay {
  id: number
  name: string
  width: number
  height: number
  refresh_rate: number
  bit_depth: number
  rotation: number
  primary: boolean
  connected: boolean
}

export interface SystemAudio {
  id: number
  name: string
  driver: string
  type: string
  channels: number
  sample_rate: number
  bit_depth: number
}

export interface SystemUSB {
  bus: number
  device: number
  vendor: string
  product: string
  serial: string
  class: string
  subclass: string
  protocol: string
  speed: number
  max_power: number
}

export interface SystemPCI {
  domain: number
  bus: number
  slot: number
  function: number
  vendor: string
  device: string
  class: string
  subclass: string
  prog_if: string
  driver: string
  irq: number
  memory: string[]
  io_ports: string[]
}

export interface SystemSensors {
  temperature: SystemTemperatureSensor[]
  fans: SystemFanSensor[]
  voltage: SystemVoltageSensor[]
  power: SystemPowerSensor[]
  current: SystemCurrentSensor[]
}

export interface SystemTemperatureSensor {
  name: string
  value: number
  unit: string
  min: number
  max: number
  critical: number
  high: number
  low: number
}

export interface SystemFanSensor {
  name: string
  value: number
  unit: string
  min: number
  max: number
}

export interface SystemVoltageSensor {
  name: string
  value: number
  unit: string
  min: number
  max: number
}

export interface SystemPowerSensor {
  name: string
  value: number
  unit: string
}

export interface SystemCurrentSensor {
  name: string
  value: number
  unit: string
}

export interface SystemPower {
  battery: SystemBattery[]
  ac_adapter: SystemACAdapter[]
  ups: SystemUPS[]
}

export interface SystemBattery {
  id: number
  model: string
  manufacturer: string
  technology: string
  capacity: number
  capacity_design: number
  energy: number
  energy_full: number
  energy_full_design: number
  voltage: number
  voltage_min_design: number
  percentage: number
  status: string
  health: string
  cycle_count: number
  temperature?: number
  power_now: number
  time_to_empty?: number
  time_to_full?: number
}

export interface SystemACAdapter {
  id: number
  online: boolean
  power: number
}

export interface SystemUPS {
  id: number
  model: string
  manufacturer: string
  status: string
  battery_charge: number
  battery_voltage: number
  input_voltage: number
  output_voltage: number
  load: number
  load_percent: number
  time_left: number
  temperature?: number
}

// 系统内存信息
export interface SystemMemory {
  total: number
  available: number
  used: number
  free: number
  cached: number
  buffers: number
  shared: number
  slab: number
  swap: SystemSwap
  numa: SystemNUMA[]
  memory_info: SystemMemoryInfo
  hugepages: SystemHugePages
}

export interface SystemSwap {
  total: number
  used: number
  free: number
  cached: number
  sin: number
  sout: number
  devices: SystemSwapDevice[]
}

export interface SystemSwapDevice {
  name: string
  type: string
  size: number
  used: number
  priority: number
}

export interface SystemNUMA {
  node: number
  memory: number
  free: number
  used: number
  cpus: number[]
}

export interface SystemMemoryInfo {
  rss: number
  vms: number
  shared: number
  text: number
  lib: number
  data: number
  dirty: number
  stack: number
  locked: number
}

export interface SystemHugePages {
  total: number
  free: number
  default_size: number
  pages: SystemHugePage[]
}

export interface SystemHugePage {
  size: number
  total: number
  free: number
}

// 系统CPU信息
export interface SystemCPU {
  total: number
  logical: number
  physical: number
  cores: number
  architecture: string
  vendor: string
  model: string
  family: number
  stepping: number
  speed: number
  max_speed: number
  min_speed: number
  cache: SystemProcessorCache
  flags: string[]
  utilization: SystemCPUUtilization
  load_average: SystemLoadAverage
  frequency: SystemCPUFrequency
  temperature: SystemCPUTemperature
  power: SystemCPUPower
  interrupts: SystemCPUInterrupts
  context_switches: number
  processes: number
  threads: number
}

export interface SystemCPUUtilization {
  user: number
  system: number
  idle: number
  nice: number
  iowait: number
  irq: number
  softirq: number
  steal: number
  guest: number
  guest_nice: number
  total: number
}

export interface SystemLoadAverage {
  load1: number
  load5: number
  load15: number
}

export interface SystemCPUFrequency {
  current: number
  min: number
  max: number
  scaling_governor: string
  scaling_driver: string
  scaling_min_freq: number
  scaling_max_freq: number
  available_frequencies: number[]
  available_governors: string[]
}

export interface SystemCPUTemperature {
  core: number
  package: number
  critical: number
  max: number
  sensors: SystemTemperatureSensor[]
}

export interface SystemCPUPower {
  package: number
  cores: number[]
  dram: number
}

export interface SystemCPUInterrupts {
  total: number
  per_cpu: number[]
  per_irq: Record<string, number>
}

// 系统存储信息
export interface SystemStorage {
  devices: SystemStorageDevice[]
  filesystems: SystemFilesystem[]
  space: SystemStorageSpace
  io: SystemStorageIO
  health: SystemStorageHealth
  performance: SystemStoragePerformance
}

export interface SystemFilesystem {
  device: string
  mountpoint: string
  fstype: string
  size: number
  used: number
  free: number
  available: number
  usage_percent: number
  inodes: SystemInodes
  options: string[]
  flags: string[]
}

export interface SystemInodes {
  total: number
  used: number
  free: number
  usage_percent: number
}

export interface SystemStorageSpace {
  total: number
  used: number
  free: number
  available: number
  usage_percent: number
  reserved: number
}

export interface SystemStorageIO {
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
  queue_depth: number
  await: number
  svctm: number
  util: number
}

export interface SystemStorageHealth {
  status: 'healthy' | 'warning' | 'error' | 'unknown'
  checks: SystemStorageHealthCheck[]
  issues: SystemStorageIssue[]
}

export interface SystemStorageHealthCheck {
  name: string
  status: 'pass' | 'fail' | 'warn'
  message: string
  timestamp: string
  details?: Record<string, any>
}

export interface SystemStorageIssue {
  severity: 'low' | 'medium' | 'high' | 'critical'
  category: string
  title: string
  description: string
  detected_at: string
  resolved_at?: string
}

export interface SystemStoragePerformance {
  benchmarks: SystemStorageBenchmark[]
  predictions: SystemStoragePrediction[]
  recommendations: SystemStorageRecommendation[]
}

export interface SystemStorageBenchmark {
  name: string
  type: 'seq_read' | 'seq_write' | 'rand_read' | 'rand_write' | 'mixed'
  score: number
  unit: string
  timestamp: string
  details: Record<string, any>
}

export interface SystemStoragePrediction {
  metric: string
  prediction: number
  confidence: number
  timeframe: string
  model: string
  last_updated: string
}

export interface SystemStorageRecommendation {
  type: string
  priority: 'low' | 'medium' | 'high'
  title: string
  description: string
  impact: string
  implementation: string
}

// 系统网络信息
export interface SystemNetwork {
  interfaces: SystemNetworkInterface[]
  connections: SystemNetworkConnection[]
  routes: SystemNetworkRoute[]
  dns: SystemNetworkDNS
  bandwidth: SystemNetworkBandwidth
  latency: SystemNetworkLatency
  packet_loss: SystemNetworkPacketLoss
  firewall: SystemNetworkFirewall
  proxies: SystemNetworkProxy[]
  vpn: SystemNetworkVPN[]
}

export interface SystemNetworkConnection {
  local_address: string
  local_port: number
  remote_address: string
  remote_port: number
  protocol: string
  state: string
  pid?: number
  process?: string
  user?: string
  command?: string
}

export interface SystemNetworkRoute {
  destination: string
  gateway: string
  interface: string
  metric: number
  flags: string[]
  source?: string
}

export interface SystemNetworkDNS {
  nameservers: string[]
  search: string[]
  options: string[]
  domain: string
  suffix: string[]
  cache: SystemDNSCache
}

export interface SystemDNSCache {
  entries: SystemDNSEntry[]
  size: number
  hits: number
  misses: number
}

export interface SystemDNSEntry {
  domain: string
  type: string
  ttl: number
  addresses: string[]
  expires: string
}

export interface SystemNetworkBandwidth {
  current: SystemNetworkBandwidthCurrent
  history: SystemNetworkBandwidthHistory[]
  limits: SystemNetworkBandwidthLimits
}

export interface SystemNetworkBandwidthCurrent {
  rx_bps: number
  tx_bps: number
  rx_pps: number
  tx_pps: number
}

export interface SystemNetworkBandwidthHistory {
  timestamp: string
  rx_bps: number
  tx_bps: number
  rx_pps: number
  tx_pps: number
}

export interface SystemNetworkBandwidthLimits {
  rx_bps: number
  tx_bps: number
  rx_pps: number
  tx_pps: number
}

export interface SystemNetworkLatency {
  current: SystemNetworkLatencyCurrent
  history: SystemNetworkLatencyHistory[]
  targets: SystemNetworkLatencyTarget[]
}

export interface SystemNetworkLatencyCurrent {
  min: number
  avg: number
  max: number
  stddev: number
}

export interface SystemNetworkLatencyHistory {
  timestamp: string
  min: number
  avg: number
  max: number
  stddev: number
}

export interface SystemNetworkLatencyTarget {
  host: string
  latency: number
  jitter: number
  packet_loss: number
  last_check: string
}

export interface SystemNetworkPacketLoss {
  current: SystemNetworkPacketLossCurrent
  history: SystemNetworkPacketLossHistory[]
  interfaces: SystemNetworkInterfacePacketLoss[]
}

export interface SystemNetworkPacketLossCurrent {
  rx_dropped: number
  tx_dropped: number
  rx_errors: number
  tx_errors: number
}

export interface SystemNetworkPacketLossHistory {
  timestamp: string
  rx_dropped: number
  tx_dropped: number
  rx_errors: number
  tx_errors: number
}

export interface SystemNetworkInterfacePacketLoss {
  interface: string
  rx_dropped: number
  tx_dropped: number
  rx_errors: number
  tx_errors: number
}

export interface SystemNetworkFirewall {
  enabled: boolean
  rules: SystemFirewallRule[]
  chains: SystemFirewallChain[]
  policies: SystemFirewallPolicy[]
  zones: SystemFirewallZone[]
}

export interface SystemFirewallRule {
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

export interface SystemFirewallChain {
  name: string
  policy: string
  rules: SystemFirewallRule[]
  packets: number
  bytes: number
}

export interface SystemFirewallPolicy {
  name: string
  target: string
  rules: SystemFirewallRule[]
  priority: number
}

export interface SystemFirewallZone {
  name: string
  interfaces: string[]
  target: string
  services: string[]
  ports: string[]
  masquerade: boolean
  forward_ports: SystemFirewallForwardPort[]
}

export interface SystemFirewallForwardPort {
  protocol: string
  port: string
  to_port: string
  to_addr: string
}

export interface SystemNetworkProxy {
  http?: string
  https?: string
  ftp?: string
  socks?: string
  no_proxy: string[]
  auto_config_url?: string
}

export interface SystemNetworkVPN {
  connections: SystemVPNConnection[]
  interfaces: SystemVPNInterface[]
}

export interface SystemVPNConnection {
  name: string
  type: string
  status: string
  server: string
  client_ip: string
  server_ip: string
  protocol: string
  port: number
  uptime: number
  bytes_sent: number
  bytes_received: number
  dns_servers: string[]
}

export interface SystemVPNInterface {
  name: string
  type: string
  local_ip: string
  remote_ip: string
  mtu: number
  status: string
}

// 系统进程信息
export interface SystemProcesses {
  total: number
  running: number
  sleeping: number
  stopped: number
  zombie: number
  threads: number
  processes: SystemProcess[]
  top: SystemProcessTop[]
  tree: SystemProcessTree[]
}

export interface SystemProcess {
  pid: number
  ppid: number
  name: string
  cmdline: string[]
  exe: string
  cwd: string
  user: string
  uid: number
  gid: number
  group: string
  status: string
  create_time: number
  start_time: string
  cpu_percent: number
  memory_percent: number
  memory_info: SystemProcessMemory
  cpu_times: SystemProcessCPUTimes
  io_counters: SystemProcessIOCounters
  num_threads: number
  threads: SystemProcessThread[]
  connections: SystemProcessConnection[]
  open_files: number
  file_descriptors: number
  environ: Record<string, string>
  nice: number
  priority: number
  children: number[]
  parent?: SystemProcess
}

export interface SystemProcessMemory {
  rss: number
  vms: number
  shared: number
  text: number
  lib: number
  data: number
  dirty: number
  stack: number
  locked: number
  peak: number
  swap: number
  pss: number
  uss: number
}

export interface SystemProcessCPUTimes {
  user: number
  system: number
  children_user: number
  children_system: number
  iowait: number
}

export interface SystemProcessIOCounters {
  read_count: number
  write_count: number
  read_bytes: number
  write_bytes: number
  read_chars: number
  write_chars: number
}

export interface SystemProcessThread {
  id: number
  name: string
  status: string
  user_time: number
  system_time: number
  cpu_percent: number
}

export interface SystemProcessConnection {
  fd: number
  family: string
  type: string
  local_address: string
  local_port: number
  remote_address: string
  remote_port: number
  status: string
}

export interface SystemProcessTop {
  pid: number
  name: string
  user: string
  cpu_percent: number
  memory_percent: number
  memory: number
  time: string
  command: string
}

export interface SystemProcessTree {
  pid: number
  ppid: number
  name: string
  status: string
  children: SystemProcessTree[]
}

// 系统服务信息
export interface SystemServices {
  total: number
  running: number
  stopped: number
  failed: number
  services: SystemService[]
}

export interface SystemService {
  name: string
  display_name: string
  description: string
  status: string
  enabled: boolean
  running: boolean
  loaded: boolean
  masked: boolean
  static: boolean
  active: boolean
  start_time: string
  main_pid: number
  memory: number
  cpu: number
  control_group: string
  slice: string
  user: string
  group: string
  command: string
  environment: string[]
  dependencies: string[]
  conflicts: string[]
  before: string[]
  after: string[]
  requires: string[]
  wants: string[]
  requisite: string[]
  bind_to: string[]
  part_of: string[]
  on_failure: string[]
  restart: string
  timeout: number
  restart_sec: number
  start_limit: number
  start_limit_burst: number
  start_limit_interval: number
  fragment_path: string
  drop_in_paths: string[]
}

// 系统用户信息
export interface SystemUsers {
  total: number
  active: number
  users: SystemUser[]
  groups: SystemGroup[]
  sessions: SystemSession[]
}

export interface SystemUser {
  name: string
  uid: number
  gid: number
  groups: number[]
  home: string
  shell: string
  gecos: string
  password_status: string
  last_login?: string
  login_count: number
  sudo: boolean
  admin: boolean
  locked: boolean
  expired: boolean
}

export interface SystemGroup {
  name: string
  gid: number
  members: string[]
  password_status: string
  system: boolean
}

export interface SystemSession {
  user: string
  tty: string
  host: string
  start_time: string
  idle_time: string
  command: string
  pid: number
}

// 系统安全信息
export interface SystemSecurity {
  authentication: SystemAuthentication
  authorization: SystemAuthorization
  certificates: SystemCertificate[]
  firewall: SystemNetworkFirewall
  intrusion_detection: SystemIntrusionDetection
  audit: SystemAudit
  selinux: SystemSELinux
  apparmor: SystemAppArmor
  pam: SystemPAM
  sudo: SystemSudo
  ssh: SystemSSH
  vulnerabilities: SystemVulnerability[]
  patches: SystemPatch[]
  policies: SystemSecurityPolicy[]
}

export interface SystemAuthentication {
  methods: string[]
  password_policy: SystemPasswordPolicy
  session_timeout: number
  max_attempts: number
  lockout_duration: number
  two_factor: SystemTwoFactorAuth
}

export interface SystemPasswordPolicy {
  min_length: number
  require_uppercase: boolean
  require_lowercase: boolean
  require_numbers: boolean
  require_symbols: boolean
  prevent_reuse: number
  expiration_days: number
  complexity: boolean
  dictionary_check: boolean
}

export interface SystemTwoFactorAuth {
  enabled: boolean
  methods: string[]
  backup_codes: boolean
  enforcement: 'optional' | 'required' | 'conditional'
}

export interface SystemAuthorization {
  roles: SystemRole[]
  permissions: SystemPermission[]
  policies: SystemAuthorizationPolicy[]
}

export interface SystemAuthorizationPolicy {
  name: string
  type: string
  rules: SystemAuthorizationRule[]
  effect: string
}

export interface SystemAuthorizationRule {
  principal: string
  resource: string
  action: string
  condition?: string
  effect: string
}

export interface SystemCertificate {
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

export interface SystemIntrusionDetection {
  enabled: boolean
  rules: SystemIDSRules[]
  alerts: SystemIDSAlert[]
  logs: SystemIDSLog[]
}

export interface SystemIDSRules {
  id: string
  name: string
  pattern: string
  action: string
  severity: string
  enabled: boolean
}

export interface SystemIDSAlert {
  id: string
  rule: string
  severity: string
  message: string
  timestamp: string
  source: string
  details: Record<string, any>
}

export interface SystemIDSLog {
  timestamp: string
  level: string
  message: string
  source: string
  details: Record<string, any>
}

export interface SystemAudit {
  enabled: boolean
  rules: SystemAuditRule[]
  logs: SystemAuditLog[]
  status: SystemAuditStatus
}

export interface SystemAuditRule {
  field: string
  operator: string
  value: string
  action: string
  key: string
}

export interface SystemAuditLog {
  timestamp: string
  type: string
  message: string
  fields: Record<string, any>
}

export interface SystemAuditStatus {
  enabled: boolean
  failure: boolean
  pid: number
  rate_limit: number
  backlog: number
  lost: number
  disk_usage: number
  disk_full: boolean
}

export interface SystemSELinux {
  enabled: boolean
  mode: string
  policy: string
  version: string
  status: SystemSELinuxStatus
  booleans: Record<string, boolean>
  contexts: SystemSELinuxContext[]
}

export interface SystemSELinuxStatus {
  enabled: boolean
  mode: string
  policy_version: string
  mls_enabled: boolean
  enforce: boolean
}

export interface SystemSELinuxContext {
  user: string
  role: string
  type: string
  level: string
}

export interface SystemAppArmor {
  enabled: boolean
  mode: string
  profiles: SystemAppArmorProfile[]
  status: SystemAppArmorStatus
}

export interface SystemAppArmorProfile {
  name: string
  mode: string
  status: string
  enforce: boolean
  kill: boolean
  complain: boolean
}

export interface SystemAppArmorStatus {
  enabled: boolean
  loaded: number
  enforce: number
  complain: number
  kill: number
  unconfined: number
}

export interface SystemPAM {
  modules: SystemPAMModule[]
  services: SystemPAMService[]
}

export interface SystemPAMModule {
  name: string
  description: string
  path: string
  interfaces: string[]
}

export interface SystemPAMService {
  name: string
  module_interface: string
  control_flag: string
  module_path: string
  arguments: string[]
}

export interface SystemSudo {
  enabled: boolean
  version: string
  config: SystemSudoConfig
  logs: SystemSudoLog[]
}

export interface SystemSudoConfig {
  host_alias: string[]
  user_alias: string[]
  runas_alias: string[]
  cmnd_alias: string[]
  defaults: Record<string, string[]>
  user_specs: SystemSudoUserSpec[]
}

export interface SystemSudoUserSpec {
  users: string[]
  hosts: string[]
  runas_users: string[]
  runas_groups: string[]
  tags: string[]
  commands: string[]
}

export interface SystemSudoLog {
  timestamp: string
  user: string
  tty: string
  pwd: string
  user_id: number
  command: string
  exit_status: number
}

export interface SystemSSH {
  server: SystemSSHServer
  client: SystemSSHClient
  keys: SystemSSHKey[]
  known_hosts: SystemSSHKnownHost[]
  sessions: SystemSSHSession[]
}

export interface SystemSSHServer {
  version: string
  port: number
  protocol: string
  host_keys: SystemSSHHostKey[]
  config: SystemSSHServerConfig
  status: SystemSSHServerStatus
}

export interface SystemSSHHostKey {
  type: string
  bits: number
  fingerprint: string
  path: string
}

export interface SystemSSHServerConfig {
  port: number
  protocol: string[]
  listen_address: string[]
  host_key: string[]
  ciphers: string[]
  macs: string[]
  key_exchange: string[]
  authentication: SystemSSHAuthentication
  logging: SystemSSHLogging
  security: SystemSSHSecurity
}

export interface SystemSSHAuthentication {
  password_authentication: boolean
  public_key_authentication: boolean
  keyboard_authentication: boolean
  challenge_response_authentication: boolean
  pam_authentication: boolean
  permit_root_login: boolean
  permit_empty_passwords: boolean
  max_auth_tries: number
  login_grace_time: number
}

export interface SystemSSHLogging {
  syslog_facility: string
  log_level: string
  log_verbose: boolean
  log_quiet: boolean
}

export interface SystemSSHSecurity {
  use_privilege_separation: boolean
  strict_modes: boolean
  permit_root_login: boolean
  allow_tcp_forwarding: boolean
  gateway_ports: boolean
  x11_forwarding: boolean
  permit_tunnel: boolean
  chroot_directory: string
}

export interface SystemSSHServerStatus {
  status: string
  uptime: number
  connections: number
  max_connections: number
  authenticated_users: number[]
}

export interface SystemSSHClient {
  version: string
  config: SystemSSHClientConfig
}

export interface SystemSSHClientConfig {
  host: string
  port: number
  user: string
  identity_file: string[]
  protocol: string[]
  compression: boolean
  server_alive_interval: number
  server_alive_count_max: number
  connect_timeout: number
  strict_host_key_checking: boolean
}

export interface SystemSSHKey {
  type: string
  bits: number
  fingerprint: string
  comment: string
  path: string
  encrypted: boolean
}

export interface SystemSSHKnownHost {
  host: string
  key_type: string
  key: string
  comment: string
}

export interface SystemSSHSession {
  id: string
  user: string
  host: string
  port: number
  protocol: string
  cipher: string
  mac: string
  compression: string
  start_time: string
  end_time?: string
  duration: number
  bytes_sent: number
  bytes_received: number
}

export interface SystemVulnerability {
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

export interface SystemPatch {
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

export interface SystemSecurityPolicy {
  id: string
  name: string
  description: string
  category: string
  enabled: boolean
  rules: SystemSecurityRule[]
  schedule?: string
  exceptions: string[]
}

export interface SystemSecurityRule {
  id: string
  name: string
  type: string
  condition: string
  action: string
  severity: string
  enabled: boolean
}

// 系统监控
export interface SystemMonitoring {
  enabled: boolean
  metrics: SystemMetrics
  alerts: SystemAlert[]
  logging: SystemLogging
  health_checks: SystemHealthCheck[]
  dashboards: SystemDashboard[]
}

export interface SystemMetrics {
  enabled: boolean
  interval: number
  retention: number
  categories: SystemMetricsCategory[]
}

export interface SystemMetricsCategory {
  name: string
  enabled: boolean
  metrics: string[]
}

export interface SystemAlert {
  id: string
  name: string
  type: string
  severity: string
  condition: string
  threshold?: number
  enabled: boolean
  notifications: SystemNotification[]
}

export interface SystemNotification {
  type: string
  destination: string
  template?: string
  enabled: boolean
}

export interface SystemLogging {
  enabled: boolean
  level: string
  format: string
  outputs: SystemLogOutput[]
  fields: SystemLogField[]
}

export interface SystemLogOutput {
  type: string
  config: Record<string, any>
  enabled: boolean
}

export interface SystemLogField {
  name: string
  source: string
  enabled: boolean
  transform?: string
}

export interface SystemHealthCheck {
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

export interface SystemDashboard {
  id: string
  name: string
  description: string
  widgets: SystemWidget[]
  layout: SystemLayout
  refresh_interval: number
}

export interface SystemWidget {
  id: string
  type: string
  title: string
  query: string
  visualization: SystemVisualization
  position: SystemPosition
  size: SystemSize
}

export interface SystemVisualization {
  type: string
  options: Record<string, any>
}

export interface SystemPosition {
  x: number
  y: number
}

export interface SystemSize {
  width: number
  height: number
}

export interface SystemLayout {
  columns: number
  gap: number
}

// 系统性能
export interface SystemPerformance {
  cpu: SystemCPUUtilization
  memory: SystemMemoryUsage
  disk: SystemStorageIO
  network: SystemNetworkBandwidth
  io_wait: number
  load_average: SystemLoadAverage
  uptime: number
  processes: number
  context_switches: number
  interrupts: number
  benchmarks: SystemBenchmark[]
  predictions: SystemPrediction[]
  recommendations: SystemRecommendation[]
}

export interface SystemMemoryUsage {
  total: number
  used: number
  free: number
  available: number
  usage_percent: number
  cached: number
  buffers: number
  swap: number
}

export interface SystemBenchmark {
  name: string
  type: 'cpu' | 'memory' | 'disk' | 'network'
  score: number
  unit: string
  timestamp: string
  details: Record<string, any>
}

export interface SystemPrediction {
  metric: string
  prediction: number
  confidence: number
  timeframe: string
  model: string
  last_updated: string
}

export interface SystemRecommendation {
  type: string
  priority: 'low' | 'medium' | 'high'
  title: string
  description: string
  impact: string
  implementation: string
}

// 系统环境
export interface SystemEnvironment {
  variables: Record<string, string>
  path: string[]
  shell: string
  user: string
  home: string
  lang: string
  locale: string[]
  timezone: string
  display: string
  desktop: string
  term: string
  editor: string
  browser: string
}

// 系统搜索和过滤
export interface SystemSearchParams extends PaginationParams {
  hostname?: string
  os_type?: string
  architecture?: string
  status?: string
  role?: string
  label?: string
  filters?: Record<string, string[]>
  sort?: 'created' | 'name' | 'hostname' | 'status' | 'cpu' | 'memory'
  order?: 'asc' | 'desc'
}

export interface SystemSearchResponse {
  systems: System[]
  total: number
  filtered: number
  page: number
  pageSize: number
  totalPages?: number
  hasNext?: boolean
  hasPrev?: boolean
}

// 系统统计信息
export interface SystemStatistics {
  total_systems: number
  online_systems: number
  offline_systems: number
  systems_by_os: Record<string, number>
  systems_by_architecture: Record<string, number>
  total_cpu_cores: number
  total_memory: number
  total_storage: number
  average_cpu_usage: number
  average_memory_usage: number
  average_storage_usage: number
  last_updated: string
}

// 批量系统操作
export interface BatchSystemOperation {
  systemIds: string[]
  operation: 'restart' | 'shutdown' | 'update' | 'backup' | 'scan' | 'cleanup'
  options?: {
    force?: boolean
    grace_period?: number
    backup_location?: string
    update_packages?: string[]
    scan_type?: string
    cleanup_type?: string
  }
}

export interface BatchSystemResult {
  total_count: number
  success_count: number
  failure_count: number
  results: Array<{
    system_id: string
    success: boolean
    error?: string
    details?: any
  }>
}

// 系统健康状态
export interface SystemHealth {
  docker?: SystemHealthCheckItem
  database?: SystemHealthCheckItem
  diskSpace?: SystemHealthCheckItem
  memory?: SystemHealthCheckItem
  network?: SystemHealthCheckItem
  cpu?: SystemHealthCheckItem
}

export interface SystemHealthCheckItem {
  status: 'Healthy' | 'Degraded' | 'Unhealthy'
  message?: string
  value?: number
  unit?: string
  threshold?: number
  lastCheck?: string
}

// Docker统计信息
export interface DockerStats {
  docker: {
    version: string
    apiVersion: string
    status: 'running' | 'stopped' | 'error'
    os: string
    arch: string
    kernelVersion: string
  }
  containers: {
    running: number
    stopped: number
    total: number
  }
  images: {
    count: number
    totalSize: number
    totalSizeFormatted: string
  }
  resources: {
    cpuUsagePercent: number
    memoryUsed: number
    memoryLimit: number
    memoryPercent: number
    memoryUsedFormatted: string
    memoryLimitFormatted: string
  }
  network: {
    rxBytesPerSec: number
    txBytesPerSec: number
    rxSpeedFormatted: string
    txSpeedFormatted: string
  }
  timestamp: string
}