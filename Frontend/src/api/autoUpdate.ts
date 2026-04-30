import api from '@/api'

// 类型定义
export interface ContainerAutoUpdateConfig {
  id: string
  containerId: string
  containerName: string
  enableUpdateCheck: boolean
  enableAutoPull: boolean
  enableAutoRestart: boolean
  checkIntervalHours: number
  lastCheckTime: string | null
  lastRemoteDigest: string | null
  currentLocalDigest: string | null
  hasUpdateAvailable: boolean
  status: AutoUpdateStatus
  statusMessage: string | null
  createdAt: string
  updatedAt: string
  updateHistory: ContainerUpdateRecord[]
}

export enum AutoUpdateStatus {
  Unknown = 0,
  Checking = 1,
  UpToDate = 2,
  UpdateAvailable = 3,
  Pulling = 4,
  Restarting = 5,
  UpdateSuccess = 6,
  UpdateFailed = 7,
  Disabled = 8
}

export interface ContainerUpdateRecord {
  id: string
  time: string
  action: UpdateAction
  oldDigest: string | null
  newDigest: string | null
  success: boolean
  errorMessage: string | null
  durationMs: number
}

export enum UpdateAction {
  Check = 0,
  Pull = 1,
  Restart = 2,
  FullUpdate = 3
}

export interface ImageUpdateCheckResult {
  containerId: string
  containerName: string
  image: string
  currentDigest: string | null
  remoteDigest: string | null
  hasUpdate: boolean
  checkTime: string
  errorMessage: string | null
}

export interface UpdateResult {
  success: boolean
  containerId: string
  oldDigest: string | null
  newDigest: string | null
  errorMessage: string | null
  durationMs: number
}

export interface GlobalAutoUpdateSettings {
  id: string
  enableGlobalCheck: boolean
  defaultCheckIntervalHours: number
  allowAutoPull: boolean
  allowAutoRestart: boolean
  checkSchedule: string
  excludedImages: string[]
  notifications: UpdateNotificationSettings
  updatedAt: string
}

export interface UpdateNotificationSettings {
  notifyOnUpdateAvailable: boolean
  notifyOnUpdateSuccess: boolean
  notifyOnUpdateFailed: boolean
  webhookUrl: string | null
}

// API 方法
export const autoUpdateApi = {
  // 获取所有自动升级配置
  getAllConfigs: () => 
    api.get<ContainerAutoUpdateConfig[]>('/auto-update/configs'),
  
  // 获取容器的自动升级配置
  getConfig: (containerId: string) => 
    api.get<ContainerAutoUpdateConfig>(`/auto-update/configs/${containerId}`),
  
  // 设置容器的自动升级配置
  setConfig: (containerId: string, config: Partial<ContainerAutoUpdateConfig>) => 
    api.put<ContainerAutoUpdateConfig>(`/auto-update/configs/${containerId}`, config),
  
  // 删除容器的自动升级配置
  deleteConfig: (containerId: string) => 
    api.delete(`/auto-update/configs/${containerId}`),
  
  // 检查容器的镜像更新
  checkUpdate: (containerId: string) => 
    api.post<ImageUpdateCheckResult>(`/auto-update/check/${containerId}`),
  
  // 检查所有容器的镜像更新
  checkAllUpdates: () => 
    api.post<ImageUpdateCheckResult[]>('/auto-update/check-all'),
  
  // 获取有可用更新的容器列表
  getAvailableUpdates: () => 
    api.get<ContainerAutoUpdateConfig[]>('/auto-update/available-updates'),
  
  // 更新容器
  updateContainer: (containerId: string, pullOnly = false) => 
    api.post<UpdateResult>(`/auto-update/update/${containerId}?pullOnly=${pullOnly}`),
  
  // 获取全局设置
  getGlobalSettings: () => 
    api.get<GlobalAutoUpdateSettings>('/auto-update/settings'),
  
  // 设置全局设置
  setGlobalSettings: (settings: Partial<GlobalAutoUpdateSettings>) => 
    api.put<GlobalAutoUpdateSettings>('/auto-update/settings', settings),
  
  // 获取镜像的所有可用标签
  getImageTags: (imageName: string) => 
    api.get<string[]>('/auto-update/image-tags', { params: { imageName } }),
  
  // 回滚容器到指定镜像版本
  rollbackContainer: (containerId: string, targetTag: string) => 
    api.post<UpdateResult>(`/auto-update/rollback/${containerId}?targetTag=${encodeURIComponent(targetTag)}`)
}

// 状态文本映射
export const statusTextMap: Record<AutoUpdateStatus, string> = {
  [AutoUpdateStatus.Unknown]: '未知',
  [AutoUpdateStatus.Checking]: '检测中',
  [AutoUpdateStatus.UpToDate]: '已是最新',
  [AutoUpdateStatus.UpdateAvailable]: '有更新',
  [AutoUpdateStatus.Pulling]: '拉取中',
  [AutoUpdateStatus.Restarting]: '重启中',
  [AutoUpdateStatus.UpdateSuccess]: '升级成功',
  [AutoUpdateStatus.UpdateFailed]: '升级失败',
  [AutoUpdateStatus.Disabled]: '已禁用'
}

// 状态颜色映射
export const statusColorMap: Record<AutoUpdateStatus, string> = {
  [AutoUpdateStatus.Unknown]: '',
  [AutoUpdateStatus.Checking]: 'warning',
  [AutoUpdateStatus.UpToDate]: 'success',
  [AutoUpdateStatus.UpdateAvailable]: 'danger',
  [AutoUpdateStatus.Pulling]: 'warning',
  [AutoUpdateStatus.Restarting]: 'warning',
  [AutoUpdateStatus.UpdateSuccess]: 'success',
  [AutoUpdateStatus.UpdateFailed]: 'danger',
  [AutoUpdateStatus.Disabled]: 'info'
}
