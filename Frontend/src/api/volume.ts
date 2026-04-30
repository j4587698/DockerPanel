import { volumeApi as canonicalVolumeApi } from "./volumes"

// 卷相关的类型定义
export interface VolumeInfo {
  id: string
  name: string
  driver: string
  scope: string
  createdAt: string
  labels: Record<string, string>
  options: Record<string, string>
  usage: VolumeUsage
  status: string
  containersCount: number
  nodeId: string
  nodeName: string
}

export interface VolumeDetailInfo extends VolumeInfo {
  containers: VolumeContainerInfo[]
  mountInfo: VolumeMountInfo
  config: VolumeConfig
  driverInfo?: VolumeDriverInfo
}

export interface VolumeUsage {
  size: number
  refCount: number
  lastUsedAt?: string
  sizeDisplay: string
}

export interface VolumeContainerInfo {
  id: string
  name: string
  mountPath: string
  mountMode: string
  isReadWrite: boolean
  labels: Record<string, string>
}

export interface VolumeMountInfo {
  mountPoint: string
  source: string
  destination: string
  mode: string
  isWritable: boolean
  propagation: string
}

export interface VolumeConfig {
  options: Record<string, string>
  labels: Record<string, string>
  driverOptions: Record<string, string>
}

export interface VolumeDriverInfo {
  name: string
  version: string
  capabilities: Record<string, any>
  options: Record<string, string>
}

export interface CreateVolumeRequest {
  name: string
  driver?: string
  labels?: Record<string, string>
  options?: Record<string, string>
  scope?: string
  nodeId?: string
}

export interface UpdateVolumeRequest {
  labels?: Record<string, string>
  options?: Record<string, string>
  scope?: string
}

export interface VolumePruneOptions {
  filters: boolean
  labelFilter?: string
  all: boolean
}

export interface VolumePruneResult {
  volumesDeleted: number
  spaceReclaimed: number
  deletedVolumeIds: string[]
  errors: string[]
}

export interface VolumeStatistics {
  totalVolumes: number
  localVolumes: number
  globalVolumes: number
  usedVolumes: number
  unusedVolumes: number
  totalSize: number
  usedSize: number
  availableSize: number
  volumesByDriver: Record<string, number>
  volumesByStatus: Record<string, number>
  lastUpdated: string
  nodeId: string
}

export interface VolumeUsageInfo {
  volumeId: string
  volumeName: string
  size: number
  refCount: number
  containerUsages: VolumeContainerUsage[]
  fileSystemInfo: VolumeFileSystemInfo
  lastAccessed: string
  lastModified: string
}

export interface VolumeContainerUsage {
  containerId: string
  containerName: string
  mountPath: string
  mountMode: string
  isReadWrite: boolean
  spaceUsed: number
}

export interface VolumeFileSystemInfo {
  fileSystem: string
  totalSize: number
  usedSize: number
  availableSize: number
  blockSize: number
  inodeCount: number
  inodeFree: number
}

export interface VolumeBackupRequest {
  volumeId: string
  backupName: string
  backupPath: string
  compress: boolean
  tags: Record<string, string>
  description?: string
  nodeId?: string
}

export interface VolumeBackupResult {
  success: boolean
  backupId: string
  backupPath: string
  backupSize: number
  backupTime: string
  duration: string
  message: string
  errors: string[]
}

export interface VolumeRestoreRequest {
  volumeId: string
  backupId: string
  backupPath: string
  overwrite: boolean
  targetVolumeName?: string
  nodeId?: string
}

export interface VolumeRestoreResult {
  success: boolean
  restoredVolumeId: string
  restoredSize: number
  restoreTime: string
  duration: string
  message: string
  errors: string[]
}

export interface VolumeBackupInfo {
  backupId: string
  backupName: string
  volumeId: string
  volumeName: string
  backupPath: string
  backupSize: number
  compressed: boolean
  backupTime: string
  tags: Record<string, string>
  description?: string
  checksum: string
}

export interface PruneVolumesRequest {
  filters: boolean
  labelFilter?: string
  all: boolean
  nodeId?: string
}

// 卷管理API服务：统一复用 volumes.ts 的实现，避免两套 API 行为漂移。
export const volumeApi = canonicalVolumeApi

export default volumeApi