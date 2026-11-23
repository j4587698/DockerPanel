/**
 * System API
 * 系统设置和状态相关的API调用
 */

import api from "./index"
import type { HostMetricsSnapshot } from "@/types/system"

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

  // 获取系统日志
  getLogs: (params?: {
    level?: string
    limit?: number
    offset?: number
  }) => {
    return api.get("system/logs", { params })
  },

  // 获取Docker统计信息
  // includeStats=false 时后端跳过「逐容器 stats 采样」，只返回引擎信息/容器数/镜像数，
  // 用于不需要容器 CPU/内存/网络聚合的页面（如概览页）。
  getDockerStats: (params?: { includeStats?: boolean }) => {
    return api.get("system/docker-stats", { params })
  },

  // 获取宿主机实时指标（未启用 /host/proc 时返回 available=false）
  getHostMetrics: () => {
    return api.get<HostMetricsSnapshot>("system/host-metrics")
  },

  // 检查 DockerPanel 自身更新
  checkSelfUpdate: (force?: boolean) => {
    // 远程 Registry 查询（digest 比对）可能较慢，默认 30s 不够
    return api.get<SelfUpdateCheckResult>("system/update/check", {
      params: { force },
      timeout: 60000
    })
  },

  // 执行 DockerPanel 自身升级
  // 接口只做秒级前置校验，通过后立即返回 202 并在后台拉镜像 + 重建容器，
  // 前端需配合 getSelfUpgradeStatus 轮询跟踪进度
  executeSelfUpgrade: (data?: SelfUpgradeRequest) => {
    return api.post<SelfUpgradeJobStatus>("system/update/upgrade", data || {})
  },

  // 查询自身升级任务状态
  getSelfUpgradeStatus: () => {
    return api.get<SelfUpgradeJobStatus>("system/update/upgrade/status")
  }
}

export interface SelfUpdateCheckResult {
  currentVersion: string
  imageName?: string
  currentDigest?: string
  remoteDigest?: string
  hasUpdate: boolean
  canSelfUpgrade: boolean
  containerId?: string
  containerName?: string
  reason?: string
  checkTime?: string
}

export interface SelfUpgradeRequest {
  targetImage?: string
  connectionId?: string
}

/** 自升级任务阶段 */
export type SelfUpgradePhase = 'idle' | 'pulling' | 'restarting' | 'failed'

export interface SelfUpgradeJobStatus {
  jobId: string
  phase: SelfUpgradePhase
  message: string
  progress: number
  isRunning: boolean
  targetImage?: string
  currentVersion?: string
  oldContainerId?: string
  error?: string
  startedAt?: string
  finishedAt?: string
  updatedAt?: string
}

export default systemApi