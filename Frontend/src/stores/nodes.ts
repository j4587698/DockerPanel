import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import { ElMessage } from 'element-plus'
import { nodeApi, type NodeInfo, type NodeStats, type NodeHealthStatus, type AddNodeRequest, type UpdateNodeRequest, type NodeGroup, type TestNodeConnectionRequest, type TestNodeConnectionResult } from '@/api/nodes'

// 节点状态接口
interface NodeState {
  nodes: NodeInfo[]
  currentNodeId: string | null
  groups: NodeGroup[]
  loading: boolean
  error: string | null
  filter: {
    search: string
    status: string
    engineType: string
    groupId: string
    connectionType: string
  }
  statistics: Record<string, NodeStats>
  refreshing: boolean
  autoRefresh: boolean
  autoRefreshInterval: number | null
}

// 本地存储键
const CURRENT_NODE_KEY = 'docker-panel-current-node'

type ApiMaybeWrapped<T> = T | { data: T }

const unwrapResponseData = <T>(response: ApiMaybeWrapped<T>): T => {
  if (response && typeof response === 'object' && 'data' in response && (response as { data?: T }).data !== undefined) {
    return (response as { data: T }).data
  }

  return response as T
}

const normalizeNodeList = (nodes: unknown): NodeInfo[] => {
  return Array.isArray(nodes) ? nodes : []
}

const normalizeGroupList = (groups: unknown): NodeGroup[] => {
  return Array.isArray(groups) ? groups : []
}

// 创建节点状态管理
export const useNodesStore = defineStore('nodes', () => {
  // 状态
  const state = ref<NodeState>({
    nodes: [],
    currentNodeId: localStorage.getItem(CURRENT_NODE_KEY) || null,
    groups: [],
    loading: false,
    error: null,
    filter: {
      search: '',
      status: '',
      engineType: '',
      groupId: '',
      connectionType: ''
    },
    statistics: {},
    refreshing: false,
    autoRefresh: false,
    autoRefreshInterval: null
  })

  // 计算属性
  const loading = computed(() => state.value.loading)
  const nodes = computed(() => state.value.nodes)
  const currentNodeId = computed(() => state.value.currentNodeId)
  const groups = computed(() => state.value.groups)
  const error = computed(() => state.value.error)
  const statistics = computed(() => state.value.statistics)
  const refreshing = computed(() => state.value.refreshing)
  const autoRefresh = computed(() => state.value.autoRefresh)

  // 当前选中的节点
  const currentNode = computed(() => {
    return (state.value.nodes || []).find(n => n.id === state.value.currentNodeId) || null
  })

  // 当前节点是否在线
  const currentNodeOnline = computed(() => {
    return currentNode.value?.status === 'Online' || currentNode.value?.isOnline
  })

  // 过滤后的节点列表
  const filteredNodes = computed(() => {
    const nodes = state.value.nodes || []
    let filtered = nodes

    // 搜索过滤
    if (state.value.filter.search) {
      const searchTerm = state.value.filter.search.toLowerCase()
      filtered = filtered.filter(node =>
        node.name?.toLowerCase().includes(searchTerm) ||
        node.id.toLowerCase().includes(searchTerm) ||
        node.host?.toLowerCase().includes(searchTerm) ||
        node.engineType?.toLowerCase().includes(searchTerm)
      )
    }

    // 状态过滤
    if (state.value.filter.status) {
      filtered = filtered.filter(node => node.status === state.value.filter.status)
    }

    // 引擎类型过滤
    if (state.value.filter.engineType) {
      filtered = filtered.filter(node => node.engineType === state.value.filter.engineType)
    }

    // 分组过滤
    if (state.value.filter.groupId) {
      filtered = filtered.filter(node => node.groupId === state.value.filter.groupId)
    }

    // 连接类型过滤
    if (state.value.filter.connectionType) {
      filtered = filtered.filter(node => node.connectionType === state.value.filter.connectionType)
    }

    return filtered
  })

  // 按分组排列的节点
  const nodesByGroup = computed(() => {
    const result: Record<string, NodeInfo[]> = { 'ungrouped': [] }
    
    // 初始化分组
    state.value.groups.forEach(group => {
      result[group.id] = []
    })
    
    // 分配节点
    state.value.nodes.forEach(node => {
      if (node.groupId && result[node.groupId]) {
        result[node.groupId].push(node)
      } else {
        result['ungrouped'].push(node)
      }
    })
    
    return result
  })

  // 节点状态选项
  const statusOptions = computed(() => {
    const nodes = state.value.nodes || []
    const statuses = [...new Set(nodes.map(node => node.status).filter(Boolean))]
    return statuses.map(status => ({
      label: status,
      value: status
    }))
  })

  // 引擎类型选项
  const engineTypeOptions = computed(() => {
    const nodes = state.value.nodes || []
    const engines = [...new Set(nodes.map(node => node.engineType).filter(Boolean))]
    return engines.map(engine => ({
      label: engine,
      value: engine
    }))
  })

  // 连接类型选项
  const connectionTypeOptions = computed(() => [
    { label: 'Local', value: 'Local' },
    { label: 'TCP', value: 'Tcp' },
    { label: 'TLS', value: 'Tls' },
    { label: 'SSH Tunnel', value: 'SshTunnel' }
  ])

  // 节点统计信息
  const nodeStats = computed(() => {
    const nodes = state.value.nodes || []
    const stats = {
      total: nodes.length,
      online: 0,
      offline: 0,
      docker: 0,
      podman: 0,
      connected: 0,
      disconnected: 0,
      byStatus: {} as Record<string, number>,
      byEngineType: {} as Record<string, number>,
      byConnectionType: {} as Record<string, number>
    }

    nodes.forEach(node => {
      // 按状态统计
      if (node.status === 'Online' || node.isOnline) {
        stats.online++
      } else {
        stats.offline++
      }

      // 按引擎类型统计
      if (node.engineType === 'docker') {
        stats.docker++
      } else if (node.engineType === 'podman') {
        stats.podman++
      }

      // 按连接状态统计
      if (node.status === 'Online' || node.isOnline) {
        stats.connected++
      } else {
        stats.disconnected++
      }

      // 按状态详细统计
      const status = node.status || 'unknown'
      stats.byStatus[status] = (stats.byStatus[status] || 0) + 1

      // 按引擎类型详细统计
      const engine = node.engineType || 'unknown'
      stats.byEngineType[engine] = (stats.byEngineType[engine] || 0) + 1

      // 按连接类型统计
      const connType = node.connectionType || 'Local'
      stats.byConnectionType[connType] = (stats.byConnectionType[connType] || 0) + 1
    })

    return stats
  })

  // 获取节点列表
  const fetchNodes = async () => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await nodeApi.getNodes()
      state.value.nodes = normalizeNodeList(unwrapResponseData<NodeInfo[]>(response as any))

      // 如果没有当前节点，自动选择第一个或默认节点
      if (!state.value.currentNodeId && state.value.nodes.length > 0) {
        const defaultNode = state.value.nodes.find(n => n.isDefault) || state.value.nodes[0]
        state.value.currentNodeId = defaultNode.id
        localStorage.setItem(CURRENT_NODE_KEY, defaultNode.id)
      }

      // 验证当前节点是否存在于列表中
      if (state.value.currentNodeId && !state.value.nodes.find(n => n.id === state.value.currentNodeId)) {
        const defaultNode = state.value.nodes.find(n => n.isDefault) || state.value.nodes[0]
        state.value.currentNodeId = defaultNode?.id || null
        if (defaultNode) {
          localStorage.setItem(CURRENT_NODE_KEY, defaultNode.id)
        }
      }
    } catch (error: any) {
      state.value.error = error.message || '获取节点列表失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 设置当前节点
  const setCurrentNode = async (nodeId: string) => {
    const node = state.value.nodes.find(n => n.id === nodeId)
    if (!node) {
      throw new Error('节点不存在')
    }

    state.value.currentNodeId = nodeId
    localStorage.setItem(CURRENT_NODE_KEY, nodeId)

    // 获取节点统计
    await fetchNodeStats(nodeId)
  }

  // 获取默认节点
  const getDefaultNode = async () => {
    try {
      const response = await nodeApi.getDefaultNode()
      return unwrapResponseData<NodeInfo>(response as any)
    } catch (error) {
      return null
    }
  }

  // 设置默认节点
  const setDefaultNode = async (nodeId: string) => {
    try {
      await nodeApi.setDefaultNode(nodeId)
      // 更新本地状态
      state.value.nodes.forEach(node => {
        node.isDefault = node.id === nodeId
      })
      ElMessage.success('设置默认节点成功')
    } catch (error: any) {
      ElMessage.error('设置默认节点失败: ' + error.message)
      throw error
    }
  }

  // 刷新节点列表
  const refreshNodes = async () => {
    state.value.refreshing = true
    try {
      await fetchNodes()
      // 刷新统计信息
      const nodes = state.value.nodes || []
      for (const node of nodes) {
        await fetchNodeStats(node.id)
      }
    } finally {
      state.value.refreshing = false
    }
  }

  // 获取节点详情
  const fetchNodeDetails = async (nodeId: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await nodeApi.getNode(nodeId)
      const index = state.value.nodes.findIndex(n => n.id === nodeId)
      if (index !== -1) {
        state.value.nodes[index] = unwrapResponseData<NodeInfo>(response as any)
      }
    } catch (error: any) {
      state.value.error = error.message || '获取节点详情失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 添加节点
  const createNode = async (data: AddNodeRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await nodeApi.addNode(data)
      // 重新获取节点列表
      await fetchNodes()
      ElMessage.success('节点添加成功')
      return unwrapResponseData<string>(response as any)
    } catch (error: any) {
      state.value.error = error.message || '添加节点失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 更新节点
  const updateNode = async (nodeId: string, data: UpdateNodeRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await nodeApi.updateNode(nodeId, data)
      const index = state.value.nodes.findIndex(node => node.id === nodeId)
      if (index !== -1) {
        state.value.nodes[index] = { ...state.value.nodes[index], ...data }
      }
      ElMessage.success('节点更新成功')
      return unwrapResponseData<void>(response as any)
    } catch (error: any) {
      state.value.error = error.message || '更新节点失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 删除节点
  const deleteNode = async (nodeId: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      await nodeApi.removeNode(nodeId)
      state.value.nodes = state.value.nodes.filter(node => node.id !== nodeId)
      
      // 如果删除的是当前节点，切换到默认节点
      if (state.value.currentNodeId === nodeId) {
        const defaultNode = state.value.nodes.find(n => n.isDefault) || state.value.nodes[0]
        state.value.currentNodeId = defaultNode?.id || null
        if (defaultNode) {
          localStorage.setItem(CURRENT_NODE_KEY, defaultNode.id)
        } else {
          localStorage.removeItem(CURRENT_NODE_KEY)
        }
      }
      
      // 清除该节点的统计信息
      delete state.value.statistics[nodeId]
      ElMessage.success('节点删除成功')
    } catch (error: any) {
      state.value.error = error.message || '删除节点失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 测试节点连接
  const testNodeConnection = async (nodeId: string) => {
    try {
      const response = await nodeApi.testNodeConnection(nodeId)
      const result = unwrapResponseData<boolean>(response as any)
      if (result) {
        ElMessage.success('节点连接测试成功')
      } else {
        ElMessage.warning('节点连接测试失败')
      }
      return result
    } catch (error: any) {
      ElMessage.error('节点连接测试异常: ' + (error.message || '未知错误'))
      throw error
    }
  }

  // 测试连接参数（不保存）
  const testConnection = async (request: TestNodeConnectionRequest): Promise<TestNodeConnectionResult> => {
    try {
      const response = await nodeApi.testConnection(request)
      return unwrapResponseData<TestNodeConnectionResult>(response as any)
    } catch (error: any) {
      throw error
    }
  }

  // 获取节点统计信息
  const fetchNodeStats = async (nodeId: string) => {
    try {
      const response = await nodeApi.getNodeStats(nodeId)
      const stats = unwrapResponseData<NodeStats>(response as any)
      state.value.statistics[nodeId] = stats
      return stats
    } catch (error: any) {
      console.error(`获取节点 ${nodeId} 统计信息失败:`, error)
    }
  }

  // 获取节点详细信息
  const fetchNodeInfo = async (nodeId: string) => {
    try {
      const response = await nodeApi.getNodeInfo(nodeId)
      return unwrapResponseData<NodeInfo>(response as any)
    } catch (error: any) {
      state.value.error = error.message || '获取节点详细信息失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // 获取节点健康状态
  const fetchNodeHealthStatus = async (nodeId: string) => {
    try {
      const response = await nodeApi.getNodeHealthStatus(nodeId)
      return unwrapResponseData<NodeHealthStatus>(response as any)
    } catch (error: any) {
      console.error(`获取节点 ${nodeId} 健康状态失败:`, error)
    }
  }

  // 批量节点操作
  const batchOperation = async (nodeIds: string[], operation: string, parameters?: Record<string, any>) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await nodeApi.batchOperation({
        nodeIds,
        operation,
        parameters
      })

      const result = unwrapResponseData<{ results: any[]; Results?: any[] }>(response as any)
      const results = result.results || result.Results || []
      const successCount = results.filter(r => r.success).length
      const failureCount = results.length - successCount

      if (successCount > 0) {
        ElMessage.success(`成功操作 ${successCount} 个节点`)
      }
      if (failureCount > 0) {
        ElMessage.warning(`操作失败 ${failureCount} 个节点`)
      }

      return { ...result, results }
    } catch (error: any) {
      state.value.error = error.message || '批量操作失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 分组管理
  const fetchGroups = async () => {
    try {
      const response = await nodeApi.getGroups()
      state.value.groups = normalizeGroupList(unwrapResponseData<NodeGroup[]>(response as any))
    } catch (error: any) {
      console.error('获取分组失败:', error)
    }
  }

  const createGroup = async (data: any) => {
    try {
      const response = await nodeApi.createGroup(data)
      const group = unwrapResponseData<NodeGroup>(response as any)
      state.value.groups.push(group)
      ElMessage.success('创建分组成功')
      return group
    } catch (error: any) {
      ElMessage.error('创建分组失败: ' + error.message)
      throw error
    }
  }

  const updateGroup = async (groupId: string, data: any) => {
    try {
      await nodeApi.updateGroup(groupId, data)
      const index = state.value.groups.findIndex(g => g.id === groupId)
      if (index !== -1) {
        state.value.groups[index] = { ...state.value.groups[index], ...data }
      }
      ElMessage.success('更新分组成功')
    } catch (error: any) {
      ElMessage.error('更新分组失败: ' + error.message)
      throw error
    }
  }

  const deleteGroup = async (groupId: string) => {
    try {
      await nodeApi.deleteGroup(groupId)
      state.value.groups = state.value.groups.filter(g => g.id !== groupId)
      ElMessage.success('删除分组成功')
    } catch (error: any) {
      ElMessage.error('删除分组失败: ' + error.message)
      throw error
    }
  }

  // 搜索节点
  const searchNodes = (searchTerm: string) => {
    state.value.filter.search = searchTerm
  }

  // 更新过滤器
  const updateFilter = (filter: Partial<NodeState['filter']>) => {
    state.value.filter = { ...state.value.filter, ...filter }
  }

  // 选择节点
  const selectNode = (node: NodeInfo) => {
    setCurrentNode(node.id)
  }

  // 清除选择
  const clearSelection = () => {
    state.value.currentNodeId = null
    localStorage.removeItem(CURRENT_NODE_KEY)
  }

  // 清除错误
  const clearError = () => {
    state.value.error = null
  }

  // 重置过滤器
  const resetFilter = () => {
    state.value.filter = {
      search: '',
      status: '',
      engineType: '',
      groupId: '',
      connectionType: ''
    }
  }

  // 启用自动刷新
  const enableAutoRefresh = (interval: number) => {
    disableAutoRefresh()

    state.value.autoRefresh = true
    state.value.autoRefreshInterval = setInterval(() => {
      refreshNodes()
    }, interval * 1000)
  }

  // 禁用自动刷新
  const disableAutoRefresh = () => {
    if (state.value.autoRefreshInterval) {
      clearInterval(state.value.autoRefreshInterval)
      state.value.autoRefreshInterval = null
    }
    state.value.autoRefresh = false
  }

  return {
    // 状态
    state,
    loading,
    nodes,
    currentNodeId,
    currentNode,
    currentNodeOnline,
    groups,
    error,
    statistics,
    refreshing,
    autoRefresh,

    // 计算属性
    filteredNodes,
    nodesByGroup,
    statusOptions,
    engineTypeOptions,
    connectionTypeOptions,
    nodeStats,

    // 方法
    fetchNodes,
    setCurrentNode,
    getDefaultNode,
    setDefaultNode,
    refreshNodes,
    fetchNodeDetails,
    createNode,
    updateNode,
    deleteNode,
    testNodeConnection,
    testConnection,
    fetchNodeStats,
    fetchNodeInfo,
    fetchNodeHealthStatus,
    batchOperation,
    fetchGroups,
    createGroup,
    updateGroup,
    deleteGroup,
    searchNodes,
    updateFilter,
    selectNode,
    clearSelection,
    clearError,
    resetFilter,
    enableAutoRefresh,
    disableAutoRefresh
  }
})

export default useNodesStore