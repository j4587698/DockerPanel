<template>
  <el-dropdown 
    trigger="click" 
    placement="bottom-start"
    @command="handleSelectNode"
    class="node-selector"
  >
    <div class="selector-trigger" :class="{ 'is-offline': !currentNodeOnline }">
      <el-icon class="node-icon"><Platform /></el-icon>
      <span class="node-name">{{ currentNode?.name || (nodesStore.state.loading ? t('common.loading') : t('node.selectNode')) }}</span>
      <el-tag 
        v-if="currentNode?.groupName" 
        size="small" 
        class="group-tag"
        :color="getNodeGroupColor(currentNode.groupId)"
      >
        {{ currentNode.groupName }}
      </el-tag>
      <span class="status-dot" :class="currentNodeOnline ? 'online' : 'offline'"></span>
      <el-icon class="dropdown-arrow"><ArrowDown /></el-icon>
    </div>

    <template #dropdown>
      <el-dropdown-menu class="node-dropdown-menu">
        <!-- 搜索框 -->
        <div class="search-box">
          <el-input 
            v-model="searchText" 
            :placeholder="t('common.search')"
            :prefix-icon="Search"
            clearable
            size="small"
          />
        </div>

        <!-- 分组列表 -->
        <template v-if="groupedNodes.length > 0">
          <template v-for="group in groupedNodes" :key="group.id">
            <div class="group-header">
              <span class="group-name">{{ group.name }}</span>
              <span class="group-count">{{ group.nodes.length }}</span>
            </div>
            <el-dropdown-item 
              v-for="node in group.nodes" 
              :key="node.id"
              :command="node.id"
              :class="{ 'is-active': node.id === currentNodeId }"
            >
              <div class="node-item">
                <span class="status-dot" :class="isNodeOnline(node) ? 'online' : 'offline'"></span>
                <span class="node-name">{{ node.name }}</span>
                <span class="node-host">{{ getNodeEndpointLabel(node) }}</span>
                <el-icon v-if="node.isDefault" class="default-icon"><Star /></el-icon>
              </div>
            </el-dropdown-item>
          </template>
        </template>

        <!-- 未分组节点 -->
        <template v-if="ungroupedNodes.length > 0">
          <div class="group-header" v-if="groupedNodes.length > 0">
            <span class="group-name">{{ t('node.ungrouped') }}</span>
          </div>
          <el-dropdown-item 
            v-for="node in ungroupedNodes" 
            :key="node.id"
            :command="node.id"
            :class="{ 'is-active': node.id === currentNodeId }"
          >
            <div class="node-item">
              <span class="status-dot" :class="isNodeOnline(node) ? 'online' : 'offline'"></span>
              <span class="node-name">{{ node.name }}</span>
              <span class="node-host">{{ getNodeEndpointLabel(node) }}</span>
              <el-icon v-if="node.isDefault" class="default-icon"><Star /></el-icon>
            </div>
          </el-dropdown-item>
        </template>

        <!-- 空状态 -->
        <div v-if="filteredNodes.length === 0" class="empty-state">
          <el-empty :description="t('node.noNodes')" :image-size="60" />
        </div>

        <!-- 底部操作 -->
        <div class="dropdown-footer">
          <el-button type="primary" link size="small" @click="goToNodeManagement">
            <el-icon><Plus /></el-icon>
            {{ t('node.addNode') }}
          </el-button>
          <el-button type="primary" link size="small" @click="refreshNodes">
            <el-icon><Refresh /></el-icon>
            {{ t('common.refresh') }}
          </el-button>
        </div>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { Platform, ArrowDown, Search, Star, Plus, Refresh } from '@element-plus/icons-vue'
import { useNodesStore } from '@/stores/nodes'
import { DockerConnectionType, type NodeInfo } from '@/api/nodes'

const { t } = useI18n()
const router = useRouter()
const nodesStore = useNodesStore()

const searchText = ref('')

// 当前选中的节点
const currentNodeId = computed(() => nodesStore.state.currentNodeId)
const currentNode = computed(() => (nodesStore.state.nodes || []).find((n: NodeInfo) => n.id === currentNodeId.value))
const currentNodeOnline = computed(() => currentNode.value ? isNodeOnline(currentNode.value) : false)

// 过滤后的节点列表
const filteredNodes = computed(() => {
  const nodes = nodesStore.state.nodes || []
  if (!searchText.value) return nodes
  
  const search = searchText.value.toLowerCase()
  return nodes.filter((node: NodeInfo) => 
    node.name?.toLowerCase().includes(search) ||
    node.host?.toLowerCase().includes(search) ||
    node.groupName?.toLowerCase().includes(search)
  )
})

// 分组的节点
const groupedNodes = computed(() => {
  const groups: { id: string; name: string; nodes: NodeInfo[] }[] = []
  const nodeMap = new Map<string, NodeInfo[]>()
  
  filteredNodes.value.forEach((node: NodeInfo) => {
    if (node.groupId && node.groupName) {
      if (!nodeMap.has(node.groupId)) {
        nodeMap.set(node.groupId, [])
      }
      nodeMap.get(node.groupId)!.push(node)
    }
  })
  
  nodeMap.forEach((nodes, groupId) => {
    const groupName = nodes[0]?.groupName || groupId
    groups.push({ id: groupId, name: groupName, nodes })
  })
  
  return groups
})

// 未分组的节点
const ungroupedNodes = computed(() => {
  return filteredNodes.value.filter((node: NodeInfo) => !node.groupId)
})

const isNodeOnline = (node: NodeInfo) => {
  return node.status === 'Online' || node.isOnline === true
}

const getNodeEndpointLabel = (node: NodeInfo) => {
  if (node.id === 'local' || node.connectionType === DockerConnectionType.Local) {
    return node.dockerEndpoint || t('node.localEndpoint')
  }

  return node.host ? `${node.host}:${node.port}` : '-'
}

// 获取分组颜色
const getNodeGroupColor = (groupId?: string) => {
  const colors = ['#3b82f6', '#22c55e', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899']
  if (!groupId) return colors[0]
  const index = groupId.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0) % colors.length
  return colors[index]
}

// 选择节点
const handleSelectNode = async (nodeId: string) => {
  try {
    await nodesStore.setCurrentNode(nodeId)
    ElMessage.success(t('node.switchedTo', { name: currentNode.value?.name || nodeId }))
  } catch (error) {
    ElMessage.error(t('node.switchFailed'))
  }
}

// 跳转到节点管理
const goToNodeManagement = () => {
  router.push('/nodes')
}

// 刷新节点列表
const refreshNodes = async () => {
  await nodesStore.fetchNodes()
}

onMounted(async () => {
  if (nodesStore.state.nodes.length === 0) {
    try {
      await nodesStore.fetchNodes()
    } catch {
      // 错误已由 store 统一提示，这里避免布局组件重复抛错。
    }
  }
})
</script>

<style scoped>
.node-selector {
  margin-right: 12px;
}

.selector-trigger {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  background: var(--bg-subtle);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  cursor: pointer;
  transition: all 0.2s ease;
  max-width: 280px;
}

.selector-trigger:hover {
  background: var(--bg-surface);
  border-color: var(--color-primary);
}

.selector-trigger.is-offline {
  border-color: var(--color-danger);
}

.node-icon {
  color: var(--color-primary);
  font-size: 16px;
}

.node-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-main);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1;
}

.group-tag {
  font-size: 10px;
  padding: 0 4px;
  height: 18px;
  line-height: 16px;
  border: none;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.status-dot.online {
  background: var(--color-success);
  box-shadow: 0 0 0 2px rgba(34, 197, 94, 0.2);
}

.status-dot.offline {
  background: var(--color-danger);
  box-shadow: 0 0 0 2px rgba(239, 68, 68, 0.2);
}

.dropdown-arrow {
  font-size: 12px;
  color: var(--text-muted);
  flex-shrink: 0;
}

/* Dropdown Menu */
.node-dropdown-menu {
  width: 320px;
  max-height: 400px;
  overflow-y: auto;
}

.search-box {
  padding: 8px 12px;
  border-bottom: 1px solid var(--border-color);
}

.group-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  font-size: 12px;
  color: var(--text-secondary);
  background: var(--bg-subtle);
}

.group-count {
  background: var(--bg-surface);
  padding: 2px 6px;
  border-radius: 10px;
  font-size: 10px;
}

.node-item {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
}

.node-item .node-name {
  flex: 1;
  font-weight: 500;
}

.node-item .node-host {
  font-size: 11px;
  color: var(--text-muted);
  font-family: monospace;
}

.default-icon {
  color: var(--color-warning);
  font-size: 14px;
}

.empty-state {
  padding: 24px;
  text-align: center;
}

.dropdown-footer {
  display: flex;
  justify-content: space-between;
  padding: 8px 12px;
  border-top: 1px solid var(--border-color);
  background: var(--bg-subtle);
}

/* Active state */
:deep(.el-dropdown-menu__item.is-active) {
  background: var(--color-primary-light);
  color: var(--color-primary);
}

/* Responsive */
@media (max-width: 768px) {
  .selector-trigger {
    max-width: 180px;
    padding: 4px 8px;
  }

  .group-tag {
    display: none;
  }

  .node-host {
    display: none;
  }
}
</style>
