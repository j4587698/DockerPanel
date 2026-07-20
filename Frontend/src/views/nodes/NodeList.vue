<template>
  <div class="page-container" v-loading="loading">
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ t('node.title') }}</h1>
        <p class="page-subtitle">{{ t('node.pageSubtitle') }}</p>
      </div>
      <div class="header-actions">
        <el-button type="primary" :icon="Plus" @click="showAddDialog = true">{{ t('node.addNode') }}</el-button>
        <el-button :icon="Refresh" @click="refreshData">{{ t('common.refresh') }}</el-button>
      </div>
    </div>

    <!-- 统计卡片 -->
    <div class="stats-cards">
      <div class="stat-card">
        <div class="stat-icon total"><el-icon><Platform /></el-icon></div>
        <div class="stat-content">
          <div class="stat-value">{{ nodeStats.total }}</div>
          <div class="stat-label">{{ t('node.totalNodes') }}</div>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon online"><el-icon><CircleCheck /></el-icon></div>
        <div class="stat-content">
          <div class="stat-value">{{ nodeStats.online }}</div>
          <div class="stat-label">{{ t('node.onlineNodes') }}</div>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon offline"><el-icon><CircleClose /></el-icon></div>
        <div class="stat-content">
          <div class="stat-value">{{ nodeStats.offline }}</div>
          <div class="stat-label">{{ t('node.offlineNodes') }}</div>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon groups"><el-icon><Folder /></el-icon></div>
        <div class="stat-content">
          <div class="stat-value">{{ groups.length }}</div>
          <div class="stat-label">{{ t('node.groups') }}</div>
        </div>
      </div>
    </div>

    <!-- 工具栏 -->
    <div class="saas-card">
      <div class="toolbar">
        <el-input v-model="searchText" :placeholder="t('node.searchPlaceholder')" :prefix-icon="Search" class="toolbar-search" clearable />
        
        <div class="toolbar-filters">
          <el-select v-model="filterStatus" :placeholder="t('node.filterStatus')" clearable style="width: 140px">
            <el-option :label="t('node.statusOnline')" value="Online" />
            <el-option :label="t('node.statusOffline')" value="Offline" />
          </el-select>
          
          <el-select v-model="filterGroup" :placeholder="t('node.filterGroup')" clearable style="width: 160px">
            <el-option v-for="group in groups" :key="group.id" :label="group.name" :value="group.id" />
          </el-select>
          
          <el-select v-model="filterConnectionType" :placeholder="t('node.filterConnectionType')" clearable style="width: 140px">
            <el-option label="Local" value="Local" />
            <el-option label="TCP" value="Tcp" />
            <el-option label="TLS" value="Tls" />
            <el-option label="SSH Tunnel" value="SshTunnel" />
          </el-select>
        </div>
      </div>

      <!-- 节点表格 -->
      <el-table :data="filteredNodes" style="width: 100%">
        <el-table-column :label="t('node.nodeName')" min-width="200">
          <template #default="{ row }">
            <div class="name-cell">
              <el-icon class="icon"><Platform /></el-icon>
              <div class="text">
                <span class="main">
                  {{ row.name }}
                  <el-tag v-if="row.isDefault" type="warning" size="small" class="default-tag">{{ t('node.default') }}</el-tag>
                </span>
                <span class="sub">{{ getNodeEndpointLabel(row) }}</span>
              </div>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('node.connectionType')" width="120">
          <template #default="{ row }">
            <el-tag size="small" :type="getConnectionTypeTagType(row.connectionType)">
              {{ getConnectionTypeLabel(row.connectionType) }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column :label="t('node.group')" width="120">
          <template #default="{ row }">
            <el-tag v-if="row.groupName" size="small" :color="getGroupColor(row.groupId)">
              {{ row.groupName }}
            </el-tag>
            <span v-else class="text-muted">-</span>
          </template>
        </el-table-column>

        <el-table-column prop="engineType" :label="t('node.engineType')" width="100" />
        
        <el-table-column :label="t('common.status')" width="100">
          <template #default="{ row }">
            <div class="status-pill">
              <span class="dot" :class="row.status === 'Online' ? 'online' : 'offline'"></span>
              <span>{{ row.status === 'Online' ? t('node.statusOnline') : t('node.statusOffline') }}</span>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('node.version')" width="120" prop="version">
          <template #default="{ row }">
            <span class="font-mono text-secondary">{{ row.version || '-' }}</span>
          </template>
        </el-table-column>

        <el-table-column :label="t('common.actions')" width="200" align="center" fixed="right">
          <template #default="{ row }">
            <div class="actions-cell">
              <el-button circle size="small" :icon="Connection" :title="t('node.testConnection')" @click="testConnection(row)" />
              <el-button circle size="small" :icon="Star" :title="t('node.setDefault')" @click="setDefaultNode(row)" :disabled="row.isDefault" />
              <el-button circle size="small" type="primary" plain :icon="Edit" :title="t('common.edit')" @click="editNode(row)" />
              <el-button circle size="small" type="danger" plain :icon="Delete" :title="t('common.delete')" :disabled="isLocalNode(row)" @click="deleteNode(row)" />
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 添加/编辑节点对话框 -->
    <el-dialog 
      v-model="showAddDialog" 
      :title="editingNode ? t('node.editNode') : t('node.addNode')" 
      width="650px"
      destroy-on-close
    >
      <el-form :model="nodeForm" label-position="top" :rules="formRules" ref="formRef">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item :label="t('node.nodeName')" prop="name">
              <el-input v-model="nodeForm.name" :placeholder="t('node.nodeNamePlaceholder')" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item :label="t('node.connectionType')" prop="connectionType">
              <el-select v-model="nodeForm.connectionType" style="width: 100%" @change="onConnectionTypeChange">
                <el-option label="Local (Unix/Named Pipe)" value="Local" />
                <el-option label="TCP" value="Tcp" />
                <el-option label="TLS/HTTPS" value="Tls" />
                <el-option label="SSH Tunnel" value="SshTunnel" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>

        <!-- TCP/TLS/SSH 配置 -->
        <template v-if="nodeForm.connectionType !== 'Local'">
          <el-row :gutter="16">
            <el-col :span="16">
              <el-form-item :label="t('node.hostAddress')" prop="host">
                <el-input v-model="nodeForm.host" :placeholder="t('node.hostPlaceholder')" />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item :label="t('node.port')" prop="port">
                <el-input-number v-model="nodeForm.port" :min="1" :max="65535" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
        </template>

        <!-- SSH 隧道配置 -->
        <template v-if="nodeForm.connectionType === 'SshTunnel'">
          <el-divider content-position="left">{{ t('node.sshConfig') }}</el-divider>
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item :label="t('node.sshUsername')" prop="sshUsername">
                <el-input v-model="nodeForm.sshUsername" :placeholder="t('node.sshUsernamePlaceholder')" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item :label="t('node.sshPort')">
                <el-input-number v-model="nodeForm.sshPort" :min="1" :max="65535" style="width: 100%" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item :label="t('node.sshPassword')">
                <el-input v-model="nodeForm.sshPassword" type="password" show-password :placeholder="t('node.sshPasswordPlaceholder')" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item :label="t('node.sshPrivateKeyPath')">
                <el-input v-model="nodeForm.sshPrivateKeyPath" :placeholder="t('node.sshPrivateKeyPathPlaceholder')" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-form-item :label="t('node.remoteDockerSocket')">
            <el-input v-model="nodeForm.remoteDockerSocket" placeholder="/var/run/docker.sock" />
          </el-form-item>
        </template>

        <!-- TLS 配置 -->
        <template v-if="nodeForm.connectionType === 'Tls'">
          <el-divider content-position="left">{{ t('node.tlsConfig') }}</el-divider>
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item :label="t('node.caCertPath')">
                <el-input v-model="nodeForm.tlsCaCertPath" :placeholder="t('node.certPathPlaceholder')" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item :label="t('node.clientCertPath')">
                <el-input v-model="nodeForm.tlsClientCertPath" :placeholder="t('node.certPathPlaceholder')" />
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item :label="t('node.clientKeyPath')">
                <el-input v-model="nodeForm.tlsClientKeyPath" :placeholder="t('node.keyPathPlaceholder')" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item :label="t('node.skipVerify')">
                <el-switch v-model="nodeForm.tlsSkipVerify" />
              </el-form-item>
            </el-col>
          </el-row>
        </template>

        <el-divider content-position="left">{{ t('node.additionalConfig') }}</el-divider>

        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item :label="t('node.group')">
              <el-select v-model="nodeForm.groupId" :placeholder="t('node.selectGroup')" clearable style="width: 100%">
                <el-option v-for="group in groups" :key="group.id" :label="group.name" :value="group.id" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item :label="t('node.engineType')">
              <el-select v-model="nodeForm.engineType" style="width: 100%">
                <el-option label="Docker" value="docker" />
                <el-option label="Podman" value="podman" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item :label="t('node.description')">
          <el-input v-model="nodeForm.description" type="textarea" :rows="2" :placeholder="t('node.descriptionPlaceholder')" />
        </el-form-item>

        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item :label="t('node.connectionTimeout')">
              <el-input-number v-model="nodeForm.connectionTimeout" :min="5" :max="120" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item :label="t('node.setAsDefault')">
              <el-switch v-model="nodeForm.isDefault" />
            </el-form-item>
          </el-col>
        </el-row>

        <!-- 测试连接按钮 -->
        <el-form-item>
          <el-button type="info" :icon="Connection" @click="testNewConnection" :loading="testingConnection">
            {{ t('node.testConnection') }}
          </el-button>
          <span v-if="testResult" class="test-result" :class="{ success: testResult.success, error: !testResult.success }">
            {{ testResult.success ? t('node.connectionSuccess') : testResult.errorMessage }}
          </span>
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showAddDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="saveNode" :loading="saving">{{ t('common.save') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Plus, Refresh, Search, Delete, Platform, Edit, Connection, Star, CircleCheck, CircleClose, Folder } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { useNodesStore } from '@/stores/nodes'
import type { NodeInfo, TestNodeConnectionResult } from '@/api/nodes'

const { t } = useI18n()
const nodesStore = useNodesStore()

const loading = ref(false)
const saving = ref(false)
const testingConnection = ref(false)
const showAddDialog = ref(false)
const editingNode = ref<NodeInfo | null>(null)
const testResult = ref<TestNodeConnectionResult | null>(null)
const formRef = ref<FormInstance>()

const searchText = ref('')
const filterStatus = ref('')
const filterGroup = ref('')
const filterConnectionType = ref('')

// 节点表单
const nodeForm = reactive({
  name: '',
  host: '',
  port: 2375,
  connectionType: 'Local' as string,
  engineType: 'docker',
  groupId: '',
  description: '',
  isDefault: false,
  connectionTimeout: 30,
  // SSH
  sshPort: 22,
  sshUsername: '',
  sshPassword: '',
  sshPrivateKeyPath: '',
  remoteDockerSocket: '/var/run/docker.sock',
  // TLS
  tlsCaCertPath: '',
  tlsClientCertPath: '',
  tlsClientKeyPath: '',
  tlsSkipVerify: false
})

const formRules: FormRules = {
  name: [{ required: true, message: t('node.nameRequired'), trigger: 'blur' }],
  host: [{ required: true, message: t('node.hostRequired'), trigger: 'blur' }],
  sshUsername: [{ required: true, message: t('node.sshUsernameRequired'), trigger: 'blur' }]
}

// 计算属性
const nodes = computed(() => nodesStore.nodes)
const groups = computed(() => nodesStore.groups)
const nodeStats = computed(() => nodesStore.nodeStats)

const filteredNodes = computed(() => {
  let result = nodes.value || []
  
  if (searchText.value) {
    const search = searchText.value.toLowerCase()
    result = result.filter(n => 
      n.name?.toLowerCase().includes(search) ||
      n.host?.toLowerCase().includes(search)
    )
  }
  
  if (filterStatus.value) {
    result = result.filter(n => n.status === filterStatus.value)
  }
  
  if (filterGroup.value) {
    result = result.filter(n => n.groupId === filterGroup.value)
  }
  
  if (filterConnectionType.value) {
    result = result.filter(n => n.connectionType === filterConnectionType.value)
  }
  
  return result
})

// 方法
const refreshData = async () => {
  loading.value = true
  try {
    await nodesStore.fetchNodes()
    await nodesStore.fetchGroups()
  } finally {
    loading.value = false
  }
}

const getConnectionTypeLabel = (type?: string) => {
  const labels: Record<string, string> = {
    Local: 'Local',
    Tcp: 'TCP',
    Tls: 'TLS',
    SshTunnel: 'SSH'
  }
  return labels[type || 'Local'] || type
}

const getConnectionTypeTagType = (type?: string) => {
  const types: Record<string, string> = {
    Local: 'info',
    Tcp: 'success',
    Tls: 'warning',
    SshTunnel: 'danger'
  }
  return types[type || 'Local'] || 'info'
}

const isLocalNode = (node: NodeInfo) => {
  return node.id === 'local' || node.connectionType === 'Local'
}

const getNodeEndpointLabel = (node: NodeInfo) => {
  if (isLocalNode(node)) {
    return node.dockerEndpoint || t('node.localEndpoint')
  }

  return node.host ? `${node.host}:${node.port}` : '-'
}

const getGroupColor = (groupId?: string) => {
  const colors = ['#3b82f6', '#22c55e', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899']
  if (!groupId) return colors[0]
  const index = groupId.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0) % colors.length
  return colors[index]
}

const onConnectionTypeChange = () => {
  // 重置相关字段
  if (nodeForm.connectionType === 'Local') {
    nodeForm.host = ''
    nodeForm.port = 2375
  } else if (nodeForm.connectionType === 'Tcp') {
    nodeForm.port = 2375
  } else if (nodeForm.connectionType === 'Tls') {
    nodeForm.port = 2376
  }
}

const testConnection = async (row: NodeInfo) => {
  try {
    const result = await nodesStore.testNodeConnection(row.id)
    if (result) {
      ElMessage.success(t('node.connectionSuccess'))
      // 更新节点状态
      await nodesStore.fetchNodes()
    }
  } catch (error) {
    // 错误已在 store 中处理
  }
}

const testNewConnection = async () => {
  testingConnection.value = true
  testResult.value = null
  
  try {
    const result = await nodesStore.testConnection({
      host: nodeForm.host,
      port: nodeForm.port,
      connectionType: nodeForm.connectionType as any,
      connectionTimeout: nodeForm.connectionTimeout,
      useSsh: nodeForm.connectionType === 'SshTunnel',
      sshPort: nodeForm.sshPort,
      sshUsername: nodeForm.sshUsername,
      sshPassword: nodeForm.sshPassword,
      sshPrivateKeyPath: nodeForm.sshPrivateKeyPath,
      remoteDockerSocket: nodeForm.remoteDockerSocket
    })
    testResult.value = result
  } catch (error: any) {
    testResult.value = {
      success: false,
      errorMessage: error.message || 'Connection failed'
    }
  } finally {
    testingConnection.value = false
  }
}

const setDefaultNode = async (row: NodeInfo) => {
  try {
    await nodesStore.setDefaultNode(row.id)
    ElMessage.success(t('node.defaultSetSuccess'))
  } catch (error) {
    // 错误已在 store 中处理
  }
}

const editNode = (row: NodeInfo) => {
  editingNode.value = row
  // 填充表单
  Object.assign(nodeForm, {
    name: row.name || '',
    host: row.host || '',
    port: row.port || 2375,
    connectionType: row.connectionType || 'Local',
    engineType: row.engineType || 'docker',
    groupId: row.groupId || '',
    description: row.description || '',
    isDefault: row.isDefault || false,
    connectionTimeout: row.connectionTimeout || 30,
    sshPort: row.sshTunnelConfig?.sshPort || 22,
    sshUsername: row.sshTunnelConfig?.sshUsername || '',
    sshPassword: '',
    sshPrivateKeyPath: row.sshTunnelConfig?.sshPrivateKeyPath || '',
    remoteDockerSocket: row.sshTunnelConfig?.remoteDockerSocket || '/var/run/docker.sock',
    tlsCaCertPath: row.tlsConfig?.caCertPath || '',
    tlsClientCertPath: row.tlsConfig?.clientCertPath || '',
    tlsClientKeyPath: row.tlsConfig?.clientKeyPath || '',
    tlsSkipVerify: row.tlsConfig?.skipVerify || false
  })
  showAddDialog.value = true
}

const deleteNode = async (row: NodeInfo) => {
  if (isLocalNode(row)) {
    ElMessage.warning(t('node.localNodeCannotDelete'))
    return
  }

  try {
    await ElMessageBox.confirm(
      t('node.deleteConfirm', { name: row.name }),
      t('common.delete'),
      { type: 'warning' }
    )
    await nodesStore.deleteNode(row.id)
    ElMessage.success(t('common.deleted'))
  } catch (error) {
    // 用户取消或删除失败
  }
}

const saveNode = async () => {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }

  saving.value = true
  
  try {
    const data = {
      name: nodeForm.name,
      host: nodeForm.host || 'localhost',
      port: nodeForm.port,
      connectionType: nodeForm.connectionType,
      engineType: nodeForm.engineType,
      groupId: nodeForm.groupId || undefined,
      description: nodeForm.description || undefined,
      isDefault: nodeForm.isDefault,
      connectionTimeout: nodeForm.connectionTimeout,
      useSsh: nodeForm.connectionType === 'SshTunnel',
      sshPort: nodeForm.sshPort,
      sshUsername: nodeForm.sshUsername,
      sshPassword: nodeForm.sshPassword || undefined,
      sshPrivateKeyPath: nodeForm.sshPrivateKeyPath || undefined,
      remoteDockerSocket: nodeForm.remoteDockerSocket
    }

    if (editingNode.value) {
      await nodesStore.updateNode(editingNode.value.id, data)
    } else {
      await nodesStore.createNode(data as any)
    }
    
    showAddDialog.value = false
    resetForm()
    await nodesStore.fetchNodes()
  } catch (error) {
    // 错误已在 store 中处理
  } finally {
    saving.value = false
  }
}

const resetForm = () => {
  editingNode.value = null
  testResult.value = null
  Object.assign(nodeForm, {
    name: '',
    host: '',
    port: 2375,
    connectionType: 'Local',
    engineType: 'docker',
    groupId: '',
    description: '',
    isDefault: false,
    connectionTimeout: 30,
    sshPort: 22,
    sshUsername: '',
    sshPassword: '',
    sshPrivateKeyPath: '',
    remoteDockerSocket: '/var/run/docker.sock',
    tlsCaCertPath: '',
    tlsClientCertPath: '',
    tlsClientKeyPath: '',
    tlsSkipVerify: false
  })
}

onMounted(() => {
  refreshData()
})
</script>

<style scoped>
.page-container { padding: 32px; max-width: 1600px; margin: 0 auto; }

/* 统计卡片 */
.stats-cards {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.stat-card {
  background: var(--bg-surface);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-lg);
  padding: 20px;
  display: flex;
  align-items: center;
  gap: 16px;
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
}

.stat-icon.total { background: rgba(59, 130, 246, 0.1); color: #3b82f6; }
.stat-icon.online { background: rgba(34, 197, 94, 0.1); color: #22c55e; }
.stat-icon.offline { background: rgba(239, 68, 68, 0.1); color: #ef4444; }
.stat-icon.groups { background: rgba(139, 92, 246, 0.1); color: #8b5cf6; }

.stat-value {
  font-size: 28px;
  font-weight: 700;
  color: var(--text-main);
  line-height: 1;
}

.stat-label {
  font-size: 13px;
  color: var(--text-secondary);
  margin-top: 4px;
}

/* 工具栏 */
.toolbar {
  padding: 16px 24px;
  border-bottom: 1px solid var(--border-color);
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
}

.toolbar-search { width: 300px; }
.toolbar-filters { display: flex; gap: 12px; margin-left: auto; }

/* 表格 */
.name-cell { display: flex; align-items: center; gap: 12px; }
.name-cell .icon { font-size: 18px; color: var(--color-primary); background: #eff6ff; padding: 8px; border-radius: 8px; }
.name-cell .text { display: flex; flex-direction: column; }
.name-cell .main { font-weight: 600; color: var(--text-main); display: flex; align-items: center; gap: 8px; }
.name-cell .sub { font-size: 11px; color: var(--text-muted); font-family: monospace; }
.default-tag { font-size: 10px; }
.font-mono { font-family: monospace; }

.status-pill { display: flex; align-items: center; gap: 6px; font-size: 13px; }
.status-pill .dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}
.status-pill .dot.online { background: var(--color-success); box-shadow: 0 0 0 3px rgba(34, 197, 94, 0.2); }
.status-pill .dot.offline { background: var(--color-danger); }

.actions-cell { display: flex; justify-content: center !important; gap: 8px; width: 100%; }

/* 测试结果 */
.test-result {
  margin-left: 12px;
  font-size: 13px;
}
.test-result.success { color: var(--color-success); }
.test-result.error { color: var(--color-danger); }

.text-muted { color: var(--text-muted); }

/* 响应式 */
@media (max-width: 1200px) {
  .stats-cards { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 768px) {
  .page-container { padding: 16px; }
  .stats-cards { grid-template-columns: 1fr; }
  .toolbar-search { width: 100%; }
  .toolbar-filters { margin-left: 0; flex-wrap: wrap; }
  .toolbar { padding: 12px 16px; }
}
</style>