import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import { ElMessage } from 'element-plus'
import { volumeApi } from '@/api/volumes'
import type {
  VolumeInfo,
  VolumeDetailInfo,
  CreateVolumeRequest,
  UpdateVolumeRequest,
  VolumeStatistics,
  VolumeUsageInfo,
  VolumeBackupResult,
  VolumeRestoreResult,
  PruneVolumesRequest,
  VolumePruneResult
} from '@/types/volume'

// 卷状态接口
interface VolumeState {
  volumes: VolumeInfo[]
  selectedVolume: VolumeDetailInfo | null
  loading: boolean
  error: string | null
  filter: {
    search: string
    driver: string
    nodeId: string | null
    status: string
  }
  statistics: VolumeStatistics | null
  refreshing: boolean
}

// 创建卷状态管理
export const useVolumesStore = defineStore('volumes', () => {
  // 状态
  const state = ref<VolumeState>({
    volumes: [],
    selectedVolume: null,
    loading: false,
    error: null,
    filter: {
      search: '',
      driver: '',
      nodeId: null,
      status: ''
    },
    statistics: null,
    refreshing: false
  })

  // 计算属性
  const loading = computed(() => state.value.loading)
  const volumes = computed(() => state.value.volumes)
  const selectedVolume = computed(() => state.value.selectedVolume)
  const error = computed(() => state.value.error)
  const statistics = computed(() => state.value.statistics)
  const refreshing = computed(() => state.value.refreshing)

  // 过滤后的卷列表
  const filteredVolumes = computed(() => {
    const volumes = state.value.volumes || []
    let filtered = volumes

    // 搜索过滤
    if (state.value.filter.search) {
      const searchTerm = state.value.filter.search.toLowerCase()
      filtered = filtered.filter(volume =>
        volume.name.toLowerCase().includes(searchTerm) ||
        volume.id.toLowerCase().includes(searchTerm) ||
        volume.driver.toLowerCase().includes(searchTerm)
      )
    }

    // 驱动类型过滤
    if (state.value.filter.driver) {
      filtered = filtered.filter(volume => volume.driver === state.value.filter.driver)
    }

    // 节点过滤
    if (state.value.filter.nodeId) {
      filtered = filtered.filter(volume => volume.nodeId === state.value.filter.nodeId)
    }

    // 状态过滤
    if (state.value.filter.status) {
      filtered = filtered.filter(volume => volume.status === state.value.filter.status)
    }

    return filtered
  })

  // 卷驱动类型列表
  const driverOptions = computed(() => {
    const volumes = state.value.volumes || []
    const drivers = [...new Set(volumes.map(volume => volume.driver))]
    return drivers.map(driver => ({
      label: driver,
      value: driver
    }))
  })

  // 节点选项列表
  const nodeOptions = computed(() => {
    const volumes = state.value.volumes || []
    const nodes = [...new Set(volumes.map(volume => volume.nodeId))]
    return nodes.map(nodeId => ({
      label: volumes.find(v => v.nodeId === nodeId)?.nodeName || nodeId,
      value: nodeId
    }))
  })

  // 状态选项列表
  const statusOptions = computed(() => {
    const volumes = state.value.volumes || []
    const statuses = [...new Set(volumes.map(volume => volume.status))]
    return statuses.map(status => ({
      label: status,
      value: status
    }))
  })

  // 卷统计信息
  const volumeStats = computed(() => {
    const volumes = state.value.volumes || []
    const stats = {
      total: volumes.length,
      local: 0,
      global: 0,
      used: 0,
      unused: 0,
      byDriver: {} as Record<string, number>,
      byStatus: {} as Record<string, number>
    }

    volumes.forEach(volume => {
      // 按作用域统计
      if (volume.scope === 'local') {
        stats.local++
      } else {
        stats.global++
      }

      // 按使用状态统计
      if (volume.containersCount > 0) {
        stats.used++
      } else {
        stats.unused++
      }

      // 按驱动类型统计
      stats.byDriver[volume.driver] = (stats.byDriver[volume.driver] || 0) + 1

      // 按状态统计
      stats.byStatus[volume.status] = (stats.byStatus[volume.status] || 0) + 1
    })

    return stats
  })

  // 获取卷列表
  const fetchVolumes = async (params?: { nodeId?: string; page?: number; pageSize?: number }) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await volumeApi.getVolumes(params)
      // 处理API响应结构
      let volumes = []
      if (Array.isArray(response.data)) {
        volumes = response.data
      } else if (Array.isArray(response)) {
        volumes = response
      } else {
        console.warn('Volumes Store - 意外的响应结构:', response)
        volumes = []
      }
      state.value.volumes = volumes
    } catch (error: any) {
      state.value.error = error.message || '获取卷列表失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 刷新卷列表
  const refreshVolumes = async () => {
    state.value.refreshing = true
    try {
      await fetchVolumes(state.value.filter.nodeId || undefined)
      await fetchStatistics()
    } finally {
      state.value.refreshing = false
    }
  }

  // 获取卷详情
  const fetchVolumeDetails = async (volumeId: string, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await volumeApi.getVolume(volumeId, nodeId)
      state.value.selectedVolume = response.data
    } catch (error: any) {
      state.value.error = error.message || '获取卷详情失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 创建卷
  const createVolume = async (data: CreateVolumeRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await volumeApi.createVolume(data)
      // 响应拦截器已经返回 response.data，所以这里 response 就是 VolumeInfo
      const volumeInfo = response as any
      state.value.volumes.push(volumeInfo)
      ElMessage.success('卷创建成功')
      return volumeInfo
    } catch (error: any) {
      state.value.error = error.message || '创建卷失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 删除卷
  const deleteVolume = async (volumeId: string, force = false, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      await volumeApi.deleteVolume(volumeId, force, nodeId)
      state.value.volumes = state.value.volumes.filter(volume => volume.id !== volumeId)
      if (state.value.selectedVolume?.id === volumeId) {
        state.value.selectedVolume = null
      }
      ElMessage.success('卷删除成功')
    } catch (error: any) {
      state.value.error = error.message || '删除卷失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 更新卷
  const updateVolume = async (volumeId: string, data: UpdateVolumeRequest, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await volumeApi.updateVolume(volumeId, data, nodeId)
      const index = state.value.volumes.findIndex(volume => volume.id === volumeId)
      if (index !== -1) {
        state.value.volumes[index] = response.data
      }
      if (state.value.selectedVolume?.id === volumeId) {
        state.value.selectedVolume = { ...state.value.selectedVolume, ...response.data }
      }
      ElMessage.success('卷更新成功')
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '更新卷失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取卷使用情况
  const fetchVolumeUsage = async (volumeId: string, nodeId?: string): Promise<VolumeUsageInfo> => {
    try {
      const response = await volumeApi.getVolumeUsage(volumeId, nodeId)
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取卷使用情况失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // 获取统计信息
  const fetchStatistics = async (nodeId?: string) => {
    try {
      const response = await volumeApi.getVolumeStatistics(nodeId)
      state.value.statistics = response.data
    } catch (error: any) {
      state.value.error = error.message || '获取卷统计信息失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // 清理未使用的卷
  const pruneVolumes = async (request: PruneVolumesRequest): Promise<VolumePruneResult> => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await volumeApi.pruneVolumes(request)
      // 响应拦截器已经返回 response.data
      const result = response as any
      ElMessage.success(`成功清理 ${result.volumesDeleted?.length || 0} 个未使用的卷`)

      // 刷新卷列表
      await refreshVolumes()

      return result
    } catch (error: any) {
      state.value.error = error.message || '清理卷失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 备份卷
  const backupVolume = async (volumeId: string, data: VolumeBackupRequest): Promise<any> => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await volumeApi.backupVolume(volumeId, data)
      ElMessage.success('卷备份成功')
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '备份卷失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 恢复卷
  const restoreVolume = async (data: VolumeRestoreRequest): Promise<any> => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await volumeApi.restoreVolume(data)
      ElMessage.success('卷恢复成功')
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '恢复卷失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 搜索卷
  const searchVolumes = (searchTerm: string) => {
    state.value.filter.search = searchTerm
  }

  // 更新过滤器
  const updateFilter = (filter: Partial<VolumeState['filter']>) => {
    state.value.filter = { ...state.value.filter, ...filter }

    // 如果节点ID改变，重新获取数据
    if ('nodeId' in filter && filter.nodeId !== state.value.filter.nodeId) {
      fetchVolumes(filter.nodeId || undefined)
      fetchStatistics(filter.nodeId || undefined)
    }
  }

  // 选择卷
  const selectVolume = (volume: VolumeInfo) => {
    fetchVolumeDetails(volume.id, volume.nodeId)
  }

  // 清除选择
  const clearSelection = () => {
    state.value.selectedVolume = null
  }

  // 清除错误
  const clearError = () => {
    state.value.error = null
  }

  // 重置过滤器
  const resetFilter = () => {
    state.value.filter = {
      search: '',
      driver: '',
      nodeId: null,
      status: ''
    }
    fetchVolumes()
    fetchStatistics()
  }

  return {
    // 状态
    state,
    loading,
    volumes,
    selectedVolume,
    error,
    statistics,
    refreshing,

    // 计算属性
    filteredVolumes,
    driverOptions,
    nodeOptions,
    statusOptions,
    volumeStats,

    // 方法
    fetchVolumes,
    refreshVolumes,
    fetchVolumeDetails,
    createVolume,
    deleteVolume,
    updateVolume,
    fetchVolumeUsage,
    fetchStatistics,
    pruneVolumes,
    backupVolume,
    restoreVolume,
    searchVolumes,
    updateFilter,
    selectVolume,
    clearSelection,
    clearError,
    resetFilter
  }
})

export default useVolumesStore