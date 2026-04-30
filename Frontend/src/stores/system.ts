/**
 * System Store
 * 系统设置和状态管理
 */

import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { systemApi } from '@/api/system'
import type { SystemHealth, DockerStats } from '@/types/system'

export const useSystemStore = defineStore('system', () => {
  // 状态
  const systemInfo = ref(null)
  const settings = ref({})
  const systemHealth = ref<SystemHealth | null>(null)
  const dockerStats = ref<DockerStats | null>(null)
  const loading = ref(false)
  const error = ref(null)

  // Historical data for charts (keep last 60 data points)
  const cpuHistory = ref<number[]>([])
  const memoryHistory = ref<number[]>([])
  const memoryUsedHistory = ref<number[]>([]) // 内存占用量（字节）
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

  // 获取系统健康状态
  const fetchSystemHealth = async () => {
    try {
      loading.value = true
      error.value = null
      const data = await systemApi.getSystemHealth()
      systemHealth.value = data
    } catch (err) {
      error.value = err instanceof Error ? err.message : '获取系统健康状态失败'
      ElMessage.error('获取系统健康状态失败')
    } finally {
      loading.value = false
    }
  }

  // 获取Docker统计信息
  const fetchDockerStats = async () => {
    try {
      const data = await systemApi.getDockerStats()
      updateDockerStats(data)
    } catch (err) {
      // Don't show error message for stats to avoid spam
      console.error('Failed to fetch Docker stats:', err)
    }
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

    // Update history arrays
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

  // 重置历史数据
  const resetHistory = () => {
    cpuHistory.value = []
    memoryHistory.value = []
    memoryUsedHistory.value = []
    rxSpeedHistory.value = []
    txSpeedHistory.value = []
  }

  return {
    // 状态
    systemInfo,
    settings,
    systemHealth,
    dockerStats,
    loading,
    error,
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
    fetchSystemHealth,
    fetchDockerStats,
    updateDockerStats,
    fetchSettings,
    updateSettings,
    resetSettings,
    clearError,
    resetHistory
  }
})