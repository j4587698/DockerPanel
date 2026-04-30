import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { containerApi } from '@/api/containers'
import type { Container as ContainerInfo, ContainerStats } from '@/types/container'
import { ElMessage } from 'element-plus'
import { signalrService } from '@/services/signalr'

export interface ContainerState {
  containers: ContainerInfo[]
  selectedContainer: ContainerInfo | null
  loading: boolean
  error: string | null
  filter: {
    search: string
    state: string
    nodeId: string | null
    showAll: boolean
  }
  stats: Record<string, ContainerStats>
  refreshing: boolean
}

export const useContainersStore = defineStore('containers', () => {
  const state = ref<ContainerState>({
    containers: [],
    selectedContainer: null,
    loading: false,
    error: null,
    filter: {
      search: '',
      state: '',
      nodeId: null,
      showAll: false
    },
    stats: {},
    refreshing: false
  })

  // 计算属性
  const filteredContainers = computed(() => {
    let result = state.value.containers

    // 状态筛选 - 使用 status 字段（字符串类型）
    if (state.value.filter.state && state.value.filter.state !== 'all') {
      result = result.filter(container =>
        (container.status?.toLowerCase() || '').includes(state.value.filter.state.toLowerCase())
      )
    }

    // 搜索筛选
    if (state.value.filter.search) {
      const searchLower = state.value.filter.search.toLowerCase()
      result = result.filter(container =>
        (container.name?.toLowerCase() || '').includes(searchLower) ||
        container.id.toLowerCase().includes(searchLower) ||
        (container.image?.toLowerCase() || '').includes(searchLower)
      )
    }

    // 节点筛选
    if (state.value.filter.nodeId) {
      result = result.filter(container =>
        container.nodeId === state.value.filter.nodeId
      )
    }

    return result
  })

  const runningContainers = computed(() =>
    state.value.containers.filter(c => c.status === 'running')
  )

  const stoppedContainers = computed(() =>
    state.value.containers.filter(c => c.status === 'exited')
  )

  const selectedContainerId = computed(() =>
    state.value.selectedContainer?.id || null
  )

  const hasContainers = computed(() => state.value.containers.length > 0)

  // 获取容器列表
  const fetchContainers = async (params?: {
    nodeId?: string
    all?: boolean
    limit?: number
  }) => {
    try {
      state.value.loading = true
      state.value.error = null

      const response = await containerApi.getContainers(params)

      // 处理响应数据 - 显式声明类型
      let containers: ContainerInfo[] = []
      const res = response as unknown as ContainerInfo[]
      if (Array.isArray(res)) {
        containers = res
      } else if (Array.isArray((response as any).data)) {
        containers = (response as any).data
      }

      state.value.containers = containers

      // 移除成功提示，避免界面干扰
    } catch (error: any) {
      console.error('获取容器列表失败:', error)
      state.value.error = error.message || '获取容器列表失败'
      ElMessage.error(state.value.error || '获取容器列表失败')
    } finally {
      state.value.loading = false
    }
  }

  // 获取容器详情
  const fetchContainerDetails = async (containerId: string) => {
    try {
      state.value.loading = true
      state.value.error = null

      const response = await containerApi.getContainer(containerId)
      state.value.selectedContainer = response as unknown as ContainerInfo
      return response as unknown as ContainerInfo
    } catch (error: any) {
      console.error(`获取容器 ${containerId} 详情失败:`, error)
      state.value.error = error.message || '获取容器详情失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 刷新容器列表
  const refreshContainers = async () => {
    try {
      state.value.refreshing = true
      await fetchContainers({
        nodeId: state.value.filter.nodeId ?? undefined,
        all: state.value.filter.showAll,
        limit: 100
      })
    } finally {
      state.value.refreshing = false
    }
  }

  // 选择容器
  const selectContainer = (container: ContainerInfo | null) => {
    state.value.selectedContainer = container
  }

  // 清除选择
  const clearSelection = () => {
    state.value.selectedContainer = null
  }

  // 更新过滤器
  const updateFilter = (filter: Partial<ContainerState['filter']>) => {
    Object.assign(state.value.filter, filter)
  }

  // 搜索容器
  const searchContainers = (keyword: string) => {
    state.value.filter.search = keyword
  }

  // 按状态筛选
  const filterByState = (newState: string) => {
    state.value.filter.state = newState
  }

  // 获取容器统计信息
  const fetchContainerStats = async (containerId: string) => {
    try {
      const response = await containerApi.getContainerStats(containerId)
      state.value.stats[containerId] = response as unknown as ContainerStats
    } catch (error: any) {
      console.error(`获取容器 ${containerId} 统计信息失败:`, error)
      ElMessage.error(`获取容器统计信息失败: ${error.message}`)
    }
  }

  // 启动容器
  const startContainer = async (containerId: string, silent = true) => {
    try {
      await containerApi.startContainer(containerId)
      if (!silent) {
        ElMessage.success(`容器 ${containerId} 启动成功`)
      }
      // 刷新容器状态
      await refreshContainers()
    } catch (error: any) {
      console.error(`启动容器 ${containerId} 失败:`, error)
      if (!silent) {
        ElMessage.error(`启动容器失败: ${error.message}`)
      }
      throw error
    }
  }

  // 停止容器
  const stopContainer = async (containerId: string, timeout = 30, silent = true) => {
    try {
      await containerApi.stopContainer(containerId, timeout)
      if (!silent) {
        ElMessage.success(`容器 ${containerId} 停止成功`)
      }
      // 刷新容器状态
      await refreshContainers()
    } catch (error: any) {
      console.error(`停止容器 ${containerId} 失败:`, error)
      if (!silent) {
        ElMessage.error(`停止容器失败: ${error.message}`)
      }
      throw error
    }
  }

  // 重启容器
  const restartContainer = async (containerId: string, timeout = 30, silent = true) => {
    try {
      await containerApi.restartContainer(containerId, timeout)
      if (!silent) {
        ElMessage.success(`容器 ${containerId} 重启成功`)
      }
      // 刷新容器状态
      await refreshContainers()
    } catch (error: any) {
      console.error(`重启容器 ${containerId} 失败:`, error)
      if (!silent) {
        ElMessage.error(`重启容器失败: ${error.message}`)
      }
      throw error
    }
  }

  // 删除容器
  const removeContainer = async (containerId: string, force = false) => {
    try {
      await containerApi.removeContainer(containerId, force)
      ElMessage.success(`容器 ${containerId} 删除成功`)

      // 从列表中移除
      state.value.containers = state.value.containers.filter(c => c.id !== containerId)

      // 如果删除的是选中的容器，清除选择
      if (state.value.selectedContainer?.id === containerId) {
        clearSelection()
      }

      // 清除统计数据
      delete state.value.stats[containerId]
    } catch (error: any) {
      console.error(`删除容器 ${containerId} 失败:`, error)
      // 如果需要强制删除，抛出特殊错误让调用方处理
      if (error.needForce) {
        throw { needForce: true, message: error.error || '容器正在运行，需要强制删除' }
      }
      throw error
    }
  }

  // 批量操作
  const batchOperation = async (containerIds: string[], operation: 'start' | 'stop' | 'restart' | 'remove', options?: {
    force?: boolean
    timeout?: number
  }) => {
    try {
      const request = {
        containerIds,
        operation,
        ...options
      }

      const response = await containerApi.batchOperation(request)
      const data = response as unknown as { successful: number; failed: number }

      const successCount = data.successful
      const failCount = data.failed

      if (successCount > 0) {
        ElMessage.success(`成功${operation === 'start' ? '启动' : operation === 'stop' ? '停止' : operation === 'restart' ? '重启' : '删除'}了 ${successCount} 个容器`)
      }

      if (failCount > 0) {
        ElMessage.warning(`有 ${failCount} 个容器操作失败`)
      }

      // 刷新容器列表
      await refreshContainers()

      return response.data
    } catch (error: any) {
      console.error('批量操作失败:', error)
      ElMessage.error(`批量操作失败: ${error.message}`)
      throw error
    }
  }

  // 获取容器日志
  const getContainerLogs = async (containerId: string, params?: {
    since?: string
    until?: string
    tail?: number
    follow?: boolean
  }) => {
    try {
      const response = await containerApi.getContainerLogs(containerId, params)
      return response as unknown as any
    } catch (error: any) {
      console.error(`获取容器 ${containerId} 日志失败:`, error)
      ElMessage.error(`获取容器日志失败: ${error.message}`)
      throw error
    }
  }

  // 清除错误
  const clearError = () => {
    state.value.error = null
  }

  // 重置状态
  const reset = () => {
    state.value.containers = []
    state.value.selectedContainer = null
    state.value.loading = false
    state.value.error = null
    state.value.filter = {
      search: '',
      state: '',
      nodeId: null,
      showAll: false
    }
    state.value.stats = {}
    state.value.refreshing = false
  }

  // 统计信息监控
  let statsUnsubscribe: (() => void) | null = null

  const startStatsMonitoring = async () => {
    if (statsUnsubscribe) return

    // 确保SignalR已连接
    if (!signalrService.isConnected()) {
      await signalrService.connect()
    }

    // 订阅统计信息更新
    statsUnsubscribe = signalrService.subscribe('container-stats', (msg) => {
      const statsList = msg.data as ContainerStats[]
      if (Array.isArray(statsList)) {
        statsList.forEach(stat => {
          state.value.stats[stat.containerId] = stat
        })
      }
    })

    // 告知后端我们想要容器统计信息
    await signalrService.subscribeToContainerStats()
  }

  const stopStatsMonitoring = async () => {
    if (statsUnsubscribe) {
      statsUnsubscribe()
      statsUnsubscribe = null
    }
    // 取消后端订阅
    await signalrService.unsubscribeFromContainerStats().catch(() => {})
  }

  return {
    // 状态
    containers: computed(() => state.value.containers),
    selectedContainer: computed(() => state.value.selectedContainer),
    loading: computed(() => state.value.loading),
    error: computed(() => state.value.error),
    filter: computed(() => state.value.filter),
    stats: computed(() => state.value.stats),
    refreshing: computed(() => state.value.refreshing),

    // 计算属性
    filteredContainers,
    runningContainers,
    stoppedContainers,
    selectedContainerId,
    hasContainers,

    // 方法
    fetchContainers,
    fetchContainerDetails,
    refreshContainers,
    selectContainer,
    clearSelection,
    updateFilter,
    searchContainers,
    filterByState,
    fetchContainerStats,
    startContainer,
    stopContainer,
    restartContainer,
    removeContainer,
    batchOperation,
    getContainerLogs,
    clearError,
    reset,
    startStatsMonitoring,
    stopStatsMonitoring
  }
})