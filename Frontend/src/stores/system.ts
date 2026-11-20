/**
 * System Store
 * 系统设置和状态管理
 */

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { systemApi } from '@/api/system'
import type { DockerStats, HostMetricsSnapshot } from '@/types/system'

export const useSystemStore = defineStore('system', () => {
  // 状态
  const systemInfo = ref(null)
  const settings = ref({})
  const dockerStats = ref<DockerStats | null>(null)
  const hostMetrics = ref<HostMetricsSnapshot | null>(null)
  const loading = ref(false)
  const error = ref(null)

  // 宿主机历史数据（用于概览页趋势图，保留最近 60 个采样点）
  // 仅在宿主机监控可用时累积；未启用时保持为空，避免用容器/进程数据冒充宿主机。
  const hostCpuHistory = ref<number[]>([])
  const hostMemoryHistory = ref<number[]>([])
  const hostRxHistory = ref<number[]>([])
  const hostTxHistory = ref<number[]>([])

  // 容器聚合历史数据（仅当宿主机监控不可用、概览页回退到容器口径时累积）
  const cpuHistory = ref<number[]>([])
  const memoryHistory = ref<number[]>([])
  const memoryUsedHistory = ref<number[]>([])
  const rxSpeedHistory = ref<number[]>([])
  const txSpeedHistory = ref<number[]>([])
  const maxHistoryPoints = 60

  // 计算属性
  const isLoaded = computed(() => systemInfo.value !== null)
  const hasError = computed(() => error.value !== null)

  // 获取系统信息
  const fetchSystemInfo = async () => {
    try {
      loading.value = true
      error.value = null
      const data = await systemApi.getSystemInfo()
      systemInfo.value = data
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取系统信息失败'
      ElMessage.error('获取系统信息失败')
    } finally {
      loading.value = false
    }
  }

  // 获取系统设置
  const fetchSettings = async () => {
    try {
      loading.value = true
      error.value = null
      const data = await systemApi.getSettings()
      settings.value = data
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取系统设置失败'
      ElMessage.error('获取系统设置失败')
    } finally {
      loading.value = false
    }
  }

  // 更新系统设置
  const updateSettings = async (newSettings: any) => {
    try {
      loading.value = true
      error.value = null
      await systemApi.updateSettings(newSettings)
      settings.value = { ...settings.value, ...newSettings }
      ElMessage.success('设置已保存')
    } catch (err) {
      error.value = err instanceof Error ? err.message : '保存设置失败'
      ElMessage.error('保存设置失败')
      throw err
    } finally {
      loading.value = false
    }
  }

  // 重置设置
  const resetSettings = async () => {
    try {
      loading.value = true
      error.value = null
      await systemApi.resetSettings()
      await fetchSettings()
      ElMessage.success('设置已重置')
    } catch (err) {
      error.value = err instanceof Error ? err.message : '重置设置失败'
      ElMessage.error('重置设置失败')
      throw err
    } finally {
      loading.value = false
    }
  }

  // 获取Docker统计信息
  // includeStats=true 时会触发后端逐容器 stats 采样（开销较大），仅在宿主机监控不可用、
  // 概览页需要回退到容器聚合口径时使用；默认 false 只看引擎信息/容器数/镜像数。
  const fetchDockerStats = async (includeStats = false) => {
    try {
      const data = await systemApi.getDockerStats({ includeStats })
      updateDockerStats(data)
    } catch (err) {
      // Don't show error message for stats to avoid spam
      console.error('Failed to fetch Docker stats:', err)
    }
  }

  // 获取宿主机实时指标（未启用 /host/proc 时返回 available=false）
  const fetchHostMetrics = async () => {
    try {
      const data = await systemApi.getHostMetrics()
      updateHostMetrics(data)
    } catch (err) {
      // 主机监控是可选能力，静默失败，Dashboard 会显示未启用提示
      console.error('Failed to fetch host metrics:', err)
    }
  }

  // 从 SignalR 推送更新宿主机指标
  const updateHostMetrics = (data: HostMetricsSnapshot | null | undefined) => {
    if (!data) return
    hostMetrics.value = data

    // 未启用宿主机监控时清空历史，避免挂载被移除后仍显示旧曲线
    if (!data.available) {
      hostCpuHistory.value = []
      hostMemoryHistory.value = []
      hostRxHistory.value = []
      hostTxHistory.value = []
      return
    }

    hostCpuHistory.value = [...hostCpuHistory.value, data.cpu?.usagePercent || 0].slice(-maxHistoryPoints)
    hostMemoryHistory.value = [...hostMemoryHistory.value, data.memory?.usagePercent || 0].slice(-maxHistoryPoints)
    hostRxHistory.value = [...hostRxHistory.value, data.network?.rxBytesPerSec || 0].slice(-maxHistoryPoints)
    hostTxHistory.value = [...hostTxHistory.value, data.network?.txBytesPerSec || 0].slice(-maxHistoryPoints)
  }

  // 从外部数据更新Docker统计信息 (如SignalR)
  const updateDockerStats = (data: any) => {
    if (!data) return
    
    // 合并数据，保留已有的静态信息（如 Docker 版本、操作系统等）
    dockerStats.value = {
      ...dockerStats.value,
      ...data,
      // 保留已有的 docker 静态信息
      docker: {
        ...dockerStats.value?.docker,
        ...data.docker
      },
      // 保留已有的 images 信息（推送不包含）
      images: dockerStats.value?.images || data.images
    }

    // 只有真正带资源聚合的数据才累积历史（includeStats=false 不会更新曲线）
    if (!data.resources && !data.network) return

    cpuHistory.value = [...cpuHistory.value, data.resources?.cpuUsagePercent || 0].slice(-maxHistoryPoints)
    memoryHistory.value = [...memoryHistory.value, data.resources?.memoryPercent || 0].slice(-maxHistoryPoints)
    memoryUsedHistory.value = [...memoryUsedHistory.value, data.resources?.memoryUsed || 0].slice(-maxHistoryPoints)
    rxSpeedHistory.value = [...rxSpeedHistory.value, data.network?.rxBytesPerSec || 0].slice(-maxHistoryPoints)
    txSpeedHistory.value = [...txSpeedHistory.value, data.network?.txBytesPerSec || 0].slice(-maxHistoryPoints)
  }

  // 清除错误
  const clearError = () => {
    error.value = null
  }

  return {
    // 状态
    systemInfo,
    settings,
    dockerStats,
    hostMetrics,
    loading,
    error,
    hostCpuHistory,
    hostMemoryHistory,
    hostRxHistory,
    hostTxHistory,
    cpuHistory,
    memoryHistory,
    memoryUsedHistory,
    rxSpeedHistory,
    txSpeedHistory,

    // 计算属性
    isLoaded,
    hasError,

    // 方法
    fetchSystemInfo,
    fetchDockerStats,
    updateDockerStats,
    fetchHostMetrics,
    updateHostMetrics,
    fetchSettings,
    updateSettings,
    resetSettings,
    clearError
  }
})