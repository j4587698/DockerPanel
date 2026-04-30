import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import { ElMessage } from 'element-plus'
import { networkApi } from '@/api/network'
import type {
  NetworkInfo,
  NetworkDetailInfo,
  CreateNetworkRequest,
  UpdateNetworkRequest,
  NetworkStatistics,
  NetworkContainerInfo,
  NetworkConnectionConfig,
  PruneNetworksRequest,
  NetworkPruneResult
} from '@/types/network'

// 网络状态接口
interface NetworkState {
  networks: NetworkInfo[]
  selectedNetwork: NetworkDetailInfo | null
  loading: boolean
  error: string | null
  filter: {
    search: string
    driver: string
    nodeId: string | null
    showInternal: boolean
  }
  statistics: NetworkStatistics | null
  refreshing: boolean
}

// 创建网络状态管理
export const useNetworksStore = defineStore('networks', () => {
  // 状态
  const state = ref<NetworkState>({
    networks: [],
    selectedNetwork: null,
    loading: false,
    error: null,
    filter: {
      search: '',
      driver: '',
      nodeId: null,
      showInternal: false
    },
    statistics: null,
    refreshing: false
  })

  // 计算属性
  const loading = computed(() => state.value.loading)
  const networks = computed(() => state.value.networks)
  const selectedNetwork = computed(() => state.value.selectedNetwork)
  const error = computed(() => state.value.error)
  const statistics = computed(() => state.value.statistics)
  const refreshing = computed(() => state.value.refreshing)

  // 过滤后的网络列表
  const filteredNetworks = computed(() => {
    try {
      const networks = state.value.networks || []
      let filtered = networks

      if (state.value.filter.search) {
        const searchTerm = state.value.filter.search.toLowerCase()
        filtered = filtered.filter(network =>
          network.name.toLowerCase().includes(searchTerm) ||
          network.id.toLowerCase().includes(searchTerm) ||
          network.driver.toLowerCase().includes(searchTerm)
        )
      }

      if (state.value.filter.driver) {
        filtered = filtered.filter(network => network.driver === state.value.filter.driver)
      }

      if (state.value.filter.nodeId) {
        filtered = filtered.filter(network => network.nodeId === state.value.filter.nodeId)
      }

      if (!state.value.filter.showInternal) {
        filtered = filtered.filter(network => !network.internal)
      }

      return filtered
    } catch (error) {
      console.error('Error in filteredNetworks computed:', error)
      return []
    }
  })

  // 网络驱动类型列表
  const driverOptions = computed(() => {
    try {
      const drivers = [...new Set((state.value.networks || []).map(network => network.driver).filter(Boolean))]
      return drivers.map(driver => ({ label: driver, value: driver }))
    } catch (error) {
      console.error('Error in driverOptions computed:', error)
      return []
    }
  })

  // 节点选项列表
  const nodeOptions = computed(() => {
    try {
      const networks = state.value.networks || []
      const nodes = new Map<string, string>()
      networks.forEach(network => {
        if (network.nodeId) {
          nodes.set(network.nodeId, (network as any).nodeName || network.nodeId)
        }
      })

      return Array.from(nodes.entries()).map(([value, label]) => ({ label, value }))
    } catch (error) {
      console.error('Error in nodeOptions computed:', error)
      return []
    }
  })

  // 网络统计信息
  const networkStats = computed(() => {
    try {
      const networks = state.value.networks || []
      const countByDriver = (driver: string) => networks.filter(network => network.driver === driver).length

      return {
        total: networks.length,
        bridge: countByDriver('bridge'),
        overlay: countByDriver('overlay'),
        host: countByDriver('host'),
        macvlan: countByDriver('macvlan'),
        none: countByDriver('null') + countByDriver('none'),
        custom: networks.filter(network => !['bridge', 'overlay', 'host', 'macvlan', 'none', 'null'].includes(network.driver)).length,
        withContainers: networks.filter(network => (network.containersCount || network.containers?.length || 0) > 0).length,
        internal: networks.filter(network => network.internal).length,
        attachable: networks.filter(network => network.attachable).length,
        ingress: networks.filter(network => network.ingress).length
      }
    } catch (error) {
      console.error('Error in networkStats computed:', error)
      return {
        total: 0,
        bridge: 0,
        overlay: 0,
        host: 0,
        macvlan: 0,
        none: 0,
        custom: 0,
        withContainers: 0,
        internal: 0,
        attachable: 0,
        ingress: 0
      }
    }
  })

  // 获取网络列表
  const fetchNetworks = async (params?: { nodeId?: string; page?: number; pageSize?: number }) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await networkApi.getNetworks(params)
      // 处理API响应结构
      let networks = []
      if (Array.isArray(response.data)) {
        networks = response.data
      } else if (Array.isArray(response)) {
        networks = response
      } else {
        console.warn('Networks Store - 意外的响应结构:', response)
        networks = []
      }

      // 确保containersCount字段正确计算
      networks = networks.map(network => ({
        ...network,
        containersCount: network.containers ? network.containers.length : 0
      }))

      state.value.networks = networks
    } catch (error: any) {
      state.value.error = error.message || '获取网络列表失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 刷新网络列表
  const refreshNetworks = async () => {
    state.value.refreshing = true
    try {
      // 串行执行以避免并发请求问题
      await fetchNetworks({ nodeId: state.value.filter.nodeId || undefined })
      await fetchStatistics({ nodeId: state.value.filter.nodeId || undefined })
    } catch (error) {
      console.error('刷新网络列表失败:', error)
    } finally {
      state.value.refreshing = false
    }
  }

  // 获取网络详情
  const fetchNetworkDetails = async (networkId: string, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await networkApi.getNetwork(networkId, { nodeId })
      state.value.selectedNetwork = response.data
    } catch (error: any) {
      state.value.error = error.message || '获取网络详情失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 创建网络
  const createNetwork = async (data: CreateNetworkRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await networkApi.createNetwork(data)
      state.value.networks.push(response.data)
      ElMessage.success('网络创建成功')
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '创建网络失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 删除网络
  const deleteNetwork = async (networkId: string, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      await networkApi.deleteNetwork(networkId, { nodeId })
      state.value.networks = state.value.networks.filter(network => network.id !== networkId)
      if (state.value.selectedNetwork?.id === networkId) {
        state.value.selectedNetwork = null
      }
      ElMessage.success('网络删除成功')
    } catch (error: any) {
      state.value.error = error.message || '删除网络失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 更新网络
  const updateNetwork = async (networkId: string, data: UpdateNetworkRequest, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await networkApi.updateNetwork(networkId, data, { nodeId })
      const index = state.value.networks.findIndex(network => network.id === networkId)
      if (index !== -1) {
        state.value.networks[index] = response.data
      }
      if (state.value.selectedNetwork?.id === networkId) {
        state.value.selectedNetwork = { ...state.value.selectedNetwork, ...response.data }
      }
      ElMessage.success('网络更新成功')
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '更新网络失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 连接容器到网络
  const connectContainer = async (networkId: string, containerId: string, config?: NetworkConnectionConfig, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      await networkApi.connectContainerToNetwork(networkId, containerId, config, { nodeId })
      ElMessage.success('容器连接网络成功')

      // 刷新网络详情
      if (state.value.selectedNetwork?.id === networkId) {
        await fetchNetworkDetails(networkId, nodeId)
      }
    } catch (error: any) {
      state.value.error = error.message || '连接容器到网络失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 批量连接容器到网络
  const connectContainers = async (networkId: string, connections: Array<{ containerId: string; ipv4Address?: string; ipv6Address?: string; aliases?: string[] }>, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      let successCount = 0
      for (const conn of connections) {
        try {
          const config: NetworkConnectionConfig = {
            ipv4Address: conn.ipv4Address,
            ipv6Address: conn.ipv6Address,
            aliases: conn.aliases
          }
          await networkApi.connectContainerToNetwork(networkId, conn.containerId, config, { nodeId })
          successCount++
        } catch (err: any) {
          console.error(`连接容器 ${conn.containerId} 失败:`, err)
        }
      }

      // 刷新网络列表
      await fetchNetworks({ nodeId: nodeId || undefined })

      return { successCount, totalCount: connections.length }
    } catch (error: any) {
      state.value.error = error.message || '批量连接容器失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 断开容器与网络的连接
  const disconnectContainer = async (networkId: string, containerId: string, nodeId?: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      await networkApi.disconnectContainerFromNetwork(networkId, containerId, { nodeId })
      ElMessage.success('容器断开网络连接成功')

      // 刷新网络详情
      if (state.value.selectedNetwork?.id === networkId) {
        await fetchNetworkDetails(networkId, nodeId)
      }
    } catch (error: any) {
      state.value.error = error.message || '断开容器网络连接失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取网络中的容器列表
  const fetchNetworkContainers = async (networkId: string, nodeId?: string): Promise<NetworkContainerInfo[]> => {
    try {
      const response = await networkApi.getNetworkContainers(networkId, { nodeId })
      return response.data
    } catch (error: any) {
      state.value.error = error.message || '获取网络容器列表失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // 获取统计信息
  const fetchStatistics = async (params?: { nodeId?: string }) => {
    try {
      const response = await networkApi.getNetworkStatistics(params)
      state.value.statistics = (response as any).data || response as any
    } catch (error: any) {
      state.value.error = error.message || '获取网络统计信息失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // 清理未使用的网络
  const pruneNetworks = async (request: PruneNetworksRequest): Promise<NetworkPruneResult> => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await networkApi.pruneNetworks(request)
      ElMessage.success(`成功清理 ${response.data.networksDeleted} 个未使用的网络`)

      // 刷新网络列表
      await refreshNetworks()

      return response.data
    } catch (error: any) {
      state.value.error = error.message || '清理网络失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 搜索网络
  const searchNetworks = (searchTerm: string) => {
    state.value.filter.search = searchTerm
  }

  // 更新过滤器
  const updateFilter = (filter: Partial<NetworkState['filter']>) => {
    const previousNodeId = state.value.filter.nodeId
    state.value.filter = { ...state.value.filter, ...filter }

    // 如果节点ID改变，重新获取数据
    if ('nodeId' in filter && filter.nodeId !== previousNodeId) {
      fetchNetworks({ nodeId: filter.nodeId || undefined })
      fetchStatistics({ nodeId: filter.nodeId || undefined })
    }
  }

  // 选择网络
  const selectNetwork = (network: NetworkInfo) => {
    fetchNetworkDetails(network.id, network.nodeId)
  }

  // 清除选择
  const clearSelection = () => {
    state.value.selectedNetwork = null
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
      showInternal: false
    }
    fetchNetworks()
    fetchStatistics()
  }

  return {
    // 状态
    state,
    loading,
    networks,
    selectedNetwork,
    error,
    statistics,
    refreshing,

    // 计算属性
    filteredNetworks,
    driverOptions,
    nodeOptions,
    networkStats,

    // 方法
    fetchNetworks,
    refreshNetworks,
    fetchNetworkDetails,
    createNetwork,
    deleteNetwork,
    updateNetwork,
    connectContainer,
    connectContainers,
    disconnectContainer,
    fetchNetworkContainers,
    fetchStatistics,
    pruneNetworks,
    searchNetworks,
    updateFilter,
    selectNetwork,
    clearSelection,
    clearError,
    resetFilter
  }
})

export default useNetworksStore