// ==================== 存储卷管理相关类型定义 ====================

import { BaseConfig, Status, Priority, PaginationParams, PaginationResponse, Metadata } from './common'

// 存储卷基础信息
export interface Volume extends BaseConfig {
  name: string
  driver: string
  mountpoint: string
  createdAt: string
  created: string
  size: number
  labels: Record<string, string>
  options: Record<string, string>
  scope: string
  nodeId: string
  nodeName: string
  status: string
  usageCount: number
  containers: string[]
  usedBy: VolumeUsage[]
  driverStatus: VolumeDriverStatus
  accessMode: VolumeAccessMode
  capacity: VolumeCapacity
  metadata: VolumeMetadata
  replication?: VolumeReplication
  backup?: VolumeBackupInfo
  encryption?: VolumeEncryption
  compression?: VolumeCompression
  quotas?: VolumeQuotas
  snapshots?: VolumeSnapshot[]
  health?: VolumeHealth
  performance?: VolumePerformance
  security?: VolumeSecurity
}

// 存储卷使用情况
export interface VolumeUsage {
  containerId: string
  containerName: string
  mountPath: string
  readOnly: boolean
  bindType: 'volume' | 'bind' | 'tmpfs'
  nodeId?: string
  nodeName?: string
}

// 存储卷驱动状态
export interface VolumeDriverStatus {
  status: 'available' | 'unavailable' | 'error' | 'maintenance'
  version?: string
  capabilities: string[]
  supportedOptions: Record<string, string>
  lastCheck: string
  errorMessage?: string
  driverSpecific: Record<string, any>
}

// 存储卷访问模式
export interface VolumeAccessMode {
  mode: 'readWriteOnce' | 'readOnlyMany' | 'readWriteMany' | 'readWriteOncePod'
  nodeAffinity?: VolumeNodeAffinity
  allowedCapabilities?: string[]
  deniedCapabilities?: string[]
}

export interface VolumeNodeAffinity {
  required?: VolumeNodeSelector
  preferred?: VolumeNodeSelector[]
}

export interface VolumeNodeSelector {
  nodeSelectorTerms: VolumeNodeSelectorTerm[]
}

export interface VolumeNodeSelectorTerm {
  matchExpressions?: VolumeNodeSelectorRequirement[]
  matchFields?: VolumeNodeSelectorRequirement[]
}

export interface VolumeNodeSelectorRequirement {
  key: string
  operator: 'In' | 'NotIn' | 'Exists' | 'DoesNotExist' | 'Gt' | 'Lt'
  values?: string[]
}

// 存储卷容量
export interface VolumeCapacity {
  storage: string
  storageClass?: string
  accessModes: string[]
  capacity: number
  requested: number
  allocated: number
  available: number
  used: number
  reserved: number
  filesystem?: string
  mountOptions?: string[]
  volumeMode?: 'filesystem' | 'block'
}

// 存储卷元数据
export interface VolumeMetadata {
  annotations: Record<string, string>
  labels: Record<string, string>
  name: string
  uid: string
  resourceVersion: string
  creationTimestamp: string
  deletionTimestamp?: string
  generation: number
  ownerReferences?: VolumeOwnerReference[]
  finalizers?: string[]
  managedFields?: VolumeManagedField[]
}

export interface VolumeOwnerReference {
  apiVersion: string
  kind: string
  name: string
  uid: string
  controller?: boolean
  blockOwnerDeletion?: boolean
}

export interface VolumeManagedField {
  manager: string
  operation: 'Apply' | 'Update'
  apiVersion: string
  time: string
  fieldsType: string
  fieldsV1: Record<string, any>
}

// 存储卷复制
export interface VolumeReplication {
  enabled: boolean
  strategy: 'synchronous' | 'asynchronous'
  replicationFactor: number
  replicas: VolumeReplica[]
  status: VolumeReplicationStatus
  schedule?: string
  bandwidthLimit?: number
  compressionEnabled: boolean
  encryptionEnabled: boolean
}

export interface VolumeReplica {
  id: string
  nodeId: string
  nodeName: string
  status: 'available' | 'syncing' | 'error' | 'offline'
  lastSync: string
  size: number
  lagBytes?: number
  health: 'healthy' | 'degraded' | 'unhealthy'
  endpoint?: string
}

export interface VolumeReplicationStatus {
  phase: 'ready' | 'progressing' | 'failed' | 'unknown'
  conditions: VolumeReplicationCondition[]
  lastSyncTime: string
  syncProgress: number
  estimatedCompletion?: string
}

export interface VolumeReplicationCondition {
  type: string
  status: 'True' | 'False' | 'Unknown'
  lastTransitionTime: string
  reason: string
  message: string
}

// 存储卷备份信息
export interface VolumeBackupInfo {
  enabled: boolean
  schedule: string
  retention: VolumeBackupRetention
  lastBackup: string
  nextBackup: string
  status: VolumeBackupStatus
  backups: VolumeBackup[]
  location: string
  compressionEnabled: boolean
  encryptionEnabled: boolean
  incrementalBackups: boolean
  backupType: 'full' | 'incremental' | 'differential'
}

export interface VolumeBackupRetention {
  count: number
  period: string
  maxSize?: number
  maxAge?: string
}

export interface VolumeBackupStatus {
  lastBackupTime: string
  lastBackupSuccess: boolean
  lastBackupSize: number
  lastBackupDuration: number
  totalBackups: number
  successfulBackups: number
  failedBackups: number
  lastError?: string
}

export interface VolumeBackup {
  id: string
  name: string
  timestamp: string
  size: number
  type: 'full' | 'incremental' | 'differential'
  status: 'completed' | 'in_progress' | 'failed'
  location: string
  checksum?: string
  encrypted: boolean
  compressed: boolean
  duration?: number
  retentionExpires?: string
  metadata: Record<string, any>
}

// 存储卷加密
export interface VolumeEncryption {
  enabled: boolean
  algorithm: string
  keySize: number
  keyId?: string
  keyRotation: VolumeKeyRotation
  status: 'active' | 'inactive' | 'error'
  lastRotated?: string
  nextRotation?: string
  encryptionAtRest: boolean
  encryptionInTransit: boolean
}

export interface VolumeKeyRotation {
  enabled: boolean
  schedule: string
  lastRotation?: string
  nextRotation?: string
  rotationCount: number
  autoRotate: boolean
}

// 存储卷压缩
export interface VolumeCompression {
  enabled: boolean
  algorithm: string
  level: number
  ratio: number
  spaceSaved: number
  performanceImpact: 'low' | 'medium' | 'high'
  status: 'active' | 'inactive' | 'error'
}

// 存储卷配额
export interface VolumeQuotas {
  enabled: boolean
  sizeLimit: number
  fileLimit?: number
  inodeLimit?: number
  used: number
  available: number
  percentage: number
  enforcement: 'soft' | 'hard'
  gracePeriod?: number
  warnings: VolumeQuotaWarning[]
}

export interface VolumeQuotaWarning {
  type: 'size' | 'files' | 'inodes'
  threshold: number
  current: number
  message: string
  timestamp: string
}

// 存储卷快照
export interface VolumeSnapshot {
  id: string
  name: string
  timestamp: string
  size: number
  status: 'available' | 'creating' | 'deleting' | 'error'
  sourceVolume: string
  retentionExpires?: string
  metadata: Record<string, any>
  labels: Record<string, string>
  creationTime: number
  readyToUse: boolean
  restoreSize?: number
  error?: VolumeSnapshotError
}

export interface VolumeSnapshotError {
  time: string
  message: string
  reason: string
}

// 存储卷健康状态
export interface VolumeHealth {
  status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown'
  checks: VolumeHealthCheck[]
  lastCheck: string
  issues: VolumeHealthIssue[]
  recommendations: string[]
}

export interface VolumeHealthCheck {
  name: string
  type: 'readiness' | 'liveness' | 'capacity' | 'performance' | 'security'
  status: 'pass' | 'fail' | 'warn'
  message: string
  timestamp: string
  duration: number
  details?: Record<string, any>
}

export interface VolumeHealthIssue {
  severity: 'low' | 'medium' | 'high' | 'critical'
  category: 'capacity' | 'performance' | 'accessibility' | 'security' | 'corruption'
  title: string
  description: string
  detectedAt: string
  lastOccurrence: string
  occurrences: number
  autoResolved?: boolean
  requiresAction: boolean
}

// 存储卷性能
export interface VolumePerformance {
  metrics: VolumePerformanceMetrics
  benchmarks: VolumePerformanceBenchmark[]
  trends: VolumePerformanceTrend[]
  alerts: VolumePerformanceAlert[]
}

export interface VolumePerformanceMetrics {
  readIOPS: number
  writeIOPS: number
  readThroughput: number
  writeThroughput: number
  readLatency: number
  writeLatency: number
  averageLatency: number
  queueDepth: number
  utilization: number
  errors: number
  timestamp: string
}

export interface VolumePerformanceBenchmark {
  name: string
  type: 'seq_read' | 'seq_write' | 'rand_read' | 'rand_write' | 'mixed'
  score: number
  unit: string
  baseline?: number
  timestamp: string
  details: Record<string, any>
}

export interface VolumePerformanceTrend {
  metric: string
  direction: 'improving' | 'degrading' | 'stable'
  changeRate: number
  period: string
  confidence: number
  forecast?: VolumePerformanceForecast
}

export interface VolumePerformanceForecast {
  timestamp: string
  predictedValue: number
  confidenceInterval: {
    lower: number
    upper: number
  }
  accuracy: number
}

export interface VolumePerformanceAlert {
  id: string
  type: 'threshold' | 'anomaly' | 'trend'
  severity: 'info' | 'warning' | 'error' | 'critical'
  metric: string
  threshold: number
  currentValue: number
  message: string
  timestamp: string
  acknowledged: boolean
  resolvedAt?: string
}

// 存储卷安全
export interface VolumeSecurity {
  accessControl: VolumeAccessControl
  encryption: VolumeSecurityEncryption
  integrity: VolumeIntegrity
  audit: VolumeAudit
  compliance: VolumeCompliance
}

export interface VolumeAccessControl {
  permissions: VolumePermission[]
  roles: VolumeRole[]
  policies: VolumeSecurityPolicy[]
  lastUpdated: string
}

export interface VolumePermission {
  principal: string
  type: 'user' | 'group' | 'service'
  permissions: string[]
  grantedAt: string
  grantedBy: string
  expiresAt?: string
}

export interface VolumeRole {
  name: string
  description: string
  permissions: string[]
  users: string[]
  groups: string[]
}

export interface VolumeSecurityPolicy {
  name: string
  description: string
  rules: VolumeSecurityRule[]
  enabled: boolean
  priority: number
}

export interface VolumeSecurityRule {
  action: 'allow' | 'deny' | 'audit'
  principal: string
  resource: string
  operations: string[]
  conditions?: Record<string, any>
  effect: 'allow' | 'deny'
}

export interface VolumeSecurityEncryption {
  atRest: boolean
  inTransit: boolean
  keyManagement: VolumeKeyManagement
  algorithms: string[]
  compliance: string[]
}

export interface VolumeKeyManagement {
  provider: string
  keyId?: string
  rotationSchedule: string
  lastRotation: string
  nextRotation: string
}

export interface VolumeIntegrity {
  checksumEnabled: boolean
  checksumAlgorithm: string
  lastVerification: string
  verificationStatus: 'passed' | 'failed' | 'pending'
  issues: VolumeIntegrityIssue[]
}

export interface VolumeIntegrityIssue {
  type: 'corruption' | 'tampering' | 'inconsistent'
  description: string
  detectedAt: string
  severity: 'low' | 'medium' | 'high' | 'critical'
  resolvedAt?: string
}

export interface VolumeAudit {
  enabled: boolean
  logLevel: 'info' | 'debug' | 'warn' | 'error'
  retention: string
  events: VolumeAuditEvent[]
}

export interface VolumeAuditEvent {
  timestamp: string
  principal: string
  action: string
  resource: string
  result: 'success' | 'failure'
  details: Record<string, any>
  source: string
}

export interface VolumeCompliance {
  standards: VolumeComplianceStandard[]
  status: 'compliant' | 'non_compliant' | 'unknown'
  lastAssessment: string
  issues: VolumeComplianceIssue[]
}

export interface VolumeComplianceStandard {
  name: string
  version: string
  description: string
  requirements: VolumeComplianceRequirement[]
}

export interface VolumeComplianceRequirement {
  id: string
  description: string
  category: string
  mandatory: boolean
  status: 'compliant' | 'non_compliant' | 'not_applicable'
  evidence?: string
  lastChecked: string
}

export interface VolumeComplianceIssue {
  standard: string
  requirement: string
  severity: 'low' | 'medium' | 'high' | 'critical'
  description: string
  remediation: string
  detectedAt: string
  resolvedAt?: string
}

// 存储卷创建请求
export interface CreateVolumeRequest {
  name: string
  driver?: string
  driverOpts?: Record<string, string>
  labels?: Record<string, string>
  capacity?: VolumeCapacity
  accessMode?: VolumeAccessMode
  replication?: VolumeReplication
  backup?: VolumeBackupInfo
  encryption?: VolumeEncryption
  compression?: VolumeCompression
  quotas?: VolumeQuotas
  nodeId?: string
  description?: string
  metadata?: Metadata
}

// 存储卷更新请求
export interface UpdateVolumeRequest {
  labels?: Record<string, string>
  driverOpts?: Record<string, string>
  capacity?: VolumeCapacity
  accessMode?: VolumeAccessMode
  replication?: VolumeReplication
  backup?: VolumeBackupInfo
  encryption?: VolumeEncryption
  compression?: VolumeCompression
  quotas?: VolumeQuotas
  description?: string
  metadata?: Metadata
}

// 存储卷快照请求
export interface CreateVolumeSnapshotRequest {
  volumeId: string
  name?: string
  labels?: Record<string, string>
  description?: string
  retention?: string
  metadata?: Record<string, any>
}

// 存储卷恢复请求
export interface RestoreVolumeRequest {
  volumeId: string
  snapshotId: string
  force?: boolean
  preserveMetadata?: boolean
  targetName?: string
}

// 存储卷克隆请求
export interface CloneVolumeRequest {
  sourceVolumeId: string
  targetVolumeName: string
  labels?: Record<string, string>
  description?: string
  nodeId?: string
}

// 存储卷迁移请求
export interface MigrateVolumeRequest {
  sourceVolumeId: string
  targetNodeId: string
  strategy: 'copy' | 'move' | 'sync'
  preserveSource?: boolean
  compression?: boolean
  encryption?: boolean
  bandwidthLimit?: number
}

// 存储卷扩展请求
export interface ExpandVolumeRequest {
  volumeId: string
  newSize: number
  allowShrink?: boolean
  online?: boolean
}

// 存储卷统计信息
export interface VolumeStatistics {
  totalVolumes: number
  totalSize: number
  usedSize: number
  availableSize: number
  volumesByDriver: Record<string, number>
  volumesByNode: Record<string, number>
  volumesByStatus: Record<string, number>
  volumesByAccessMode: Record<string, number>
  averageVolumeSize: number
  largestVolume?: VolumeInfo
  smallestVolume?: VolumeInfo
  oldestVolume?: VolumeInfo
  newestVolume?: VolumeInfo
  totalSnapshots: number
  totalBackups: number
  lastUpdated: string
}

export interface VolumeInfo {
  id: string
  name: string
  size: number
  driver: string
  status: string
  created: string
}

// 存储卷使用情况
export interface VolumeUsageStats {
  volumeId: string
  volumeName: string
  containersCount: number
  totalSize: number
  usedSize: number
  availableSize: number
  usagePercentage: number
  lastAccessed?: string
  accessFrequency: number
  readOperations: number
  writeOperations: number
  dataTransfer: number
}

// 存储卷事件
export interface VolumeEvent {
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

// 存储卷搜索和过滤
export interface VolumeSearchParams extends PaginationParams {
  name?: string
  driver?: string
  nodeId?: string
  status?: string
  label?: string
  scope?: string
  accessMode?: string
  minSize?: number
  maxSize?: number
  usedBy?: string
  filters?: Record<string, string[]>
  sort?: 'created' | 'name' | 'size' | 'driver' | 'status'
  order?: 'asc' | 'desc'
}

export interface VolumeSearchResponse {
  volumes: Volume[]
  total: number
  filtered: number
  page: number
  pageSize: number
  totalPages?: number
  hasNext?: boolean
  hasPrev?: boolean
}

// 存储卷模板
export interface VolumeTemplate extends BaseConfig {
  driver: string
  driverOpts: Record<string, string>
  capacity: VolumeCapacity
  accessMode: VolumeAccessMode
  replication: VolumeReplication
  backup: VolumeBackupInfo
  encryption: VolumeEncryption
  compression: VolumeCompression
  quotas: VolumeQuotas
  category: string
  tags: string[]
  description: string
  readme?: string
  parameters: VolumeTemplateParameter[]
  dependencies: VolumeTemplateDependency[]
}

export interface VolumeTemplateParameter {
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

export interface VolumeTemplateDependency {
  type: 'volume' | 'service' | 'network' | 'node'
  name: string
  condition: string
  optional: boolean
}

// 存储卷驱动
export interface VolumeDriver {
  name: string
  description: string
  version: string
  documentation?: string
  vendor?: string
  license?: string
  status: 'active' | 'inactive' | 'deprecated'
  enabled: boolean
  autoUpdate: boolean
  updateAvailable?: boolean
  installedAt: string
  lastUpdated: string
  size?: number
  checksum?: string
  signature?: string
  capabilities: VolumeDriverCapabilities
  configuration: VolumeDriverConfiguration
  metadata: VolumeDriverMetadata
}

export interface VolumeDriverCapabilities {
  scope: string[]
  accessModes: string[]
  features: string[]
  operations: string[]
  encryption: boolean
  compression: boolean
  replication: boolean
  snapshots: boolean
  backup: boolean
  quotas: boolean
  monitoring: boolean
  performanceMetrics: boolean
}

export interface VolumeDriverConfiguration {
  configPath?: string
  dataPath?: string
  logPath?: string
  options: Record<string, any>
  environment: Record<string, string>
  resources: VolumeDriverResources
  permissions: VolumeDriverPermissions
}

export interface VolumeDriverResources {
  cpu?: number
  memory?: number
  disk?: number
  network?: number
  devices?: string[]
}

export interface VolumeDriverPermissions {
  capabilities?: string[]
  privileged?: boolean
  hostAccess?: boolean
  networkAccess?: boolean
  filesystemAccess?: string[]
}

export interface VolumeDriverMetadata {
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
  compatibility: VolumeDriverCompatibility
}

export interface VolumeDriverCompatibility {
  platforms: string[]
  architectures: string[]
  dockerVersions: string[]
  kernelVersions: string[]
  osVersions: string[]
}

// 批量存储卷操作
export interface BatchVolumeOperation {
  volumeIds: string[]
  operation: 'backup' | 'snapshot' | 'clone' | 'migrate' | 'expand' | 'remove' | 'prune'
  options?: {
    force?: boolean
    targetNodeId?: string
    newSize?: number
    snapshotName?: string
    backupLocation?: string
    compression?: boolean
    encryption?: boolean
  }
}

export interface BatchVolumeResult {
  totalCount: number
  successCount: number
  failureCount: number
  results: Array<{
    volumeId: string
    success: boolean
    error?: string
    details?: any
  }>
}

// 存储卷清理请求
export interface PruneVolumesRequest {
  filters?: boolean
  labelFilter?: string
  all?: boolean
  nodeId?: string
}

// 存储卷清理结果
export interface VolumePruneResult {
  volumesDeleted: number
  spaceReclaimed: number
  deletedVolumeIds?: string[]
  errors?: string[]
}