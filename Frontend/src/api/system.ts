/**
 * System API
 * 系统设置和状态相关的API调用
 */

import api from "./index"

// 系统API对象
export const systemApi = {
  // 获取系统信息
  getSystemInfo: () => {
    return api.get("system/info")
  },

  // 获取系统设置
  getSettings: () => {
    return api.get("settings")
  },

  // 更新系统设置
  updateSettings: (settings: any) => {
    return api.put("settings", settings)
  },

  // 重置系统设置
  resetSettings: () => {
    return api.post("settings/reset")
  },

  // 获取系统状态
  getStatus: () => {
    return api.get("system/status")
  },

  // 获取系统日志
  getLogs: (params?: {
    level?: string
    limit?: number
    offset?: number
  }) => {
    return api.get("system/logs", { params })
  },

  // 获取系统统计信息
  getStatistics: () => {
    return api.get("system/statistics")
  },

  // 系统健康检查
  healthCheck: () => {
    return api.get("system/health")
  },

  // 获取系统健康状态
  getSystemHealth: () => {
    return api.get("system/health")
  },

  // 获取版本信息
  getVersion: () => {
    return api.get("system/version")
  },

  // 获取Docker统计信息
  getDockerStats: () => {
    return api.get("system/docker-stats")
  },

  // 检查 DockerPanel 自身更新
  checkSelfUpdate: (force?: boolean) => {
    return api.get<SelfUpdateCheckResult>("system/update/check", { params: { force } })
  },

  // 执行 DockerPanel 自身升级
  executeSelfUpgrade: (data?: SelfUpgradeRequest) => {
    return api.post<SelfUpgradeResponse>("system/update/upgrade", data || {})
  }
}

export interface SelfUpdateCheckResult {
  currentVersion: string
  latestVersion: string
  hasUpdate: boolean
  releaseTitle?: string
  releaseNotes?: string
  publishedAt?: string
  htmlUrl?: string
  canSelfUpgrade: boolean
  containerId?: string
  containerName?: string
  imageName?: string
  reason?: string
}

export interface SelfUpgradeRequest {
  targetVersion?: string
  targetImage?: string
  connectionId?: string
}

export interface SelfUpgradeResponse {
  success: boolean
  message: string
  currentVersion: string
  targetVersion: string
  oldContainerId?: string
}

export default systemApi