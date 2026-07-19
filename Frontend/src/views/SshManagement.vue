<!--
  SSH管理页面
  提供SSH连接管理、密钥管理、命令执行、文件传输等功能
-->
<template>
  <div class="ssh-management">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-content">
        <div class="title-section">
          <h1>
            <el-icon><Connection /></el-icon>
            {{ t('ssh.title') }}
          </h1>
          <p>{{ t('ssh.subtitle') }}</p>
        </div>
        <div class="action-section">
          <el-button type="primary" @click="showCreateConnectionDialog = true" :icon="Plus">{{ t('ssh.newConnection') }}</el-button>
          <el-button @click="showKeyPairDialog = true" :icon="Key">{{ t('ssh.generateKeyPair') }}</el-button>
          <el-dropdown @command="handleBatchCommand">
            <el-button>
              {{ t('ssh.batchOperation') }}
              <el-icon><ArrowDown /></el-icon>
            </el-button>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="test">{{ t('ssh.batchTestConnection') }}</el-dropdown-item>
                <el-dropdown-item command="execute">{{ t('ssh.batchExecuteCommand') }}</el-dropdown-item>
                <el-dropdown-item command="upload">{{ t('ssh.batchUploadFile') }}</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </div>
    </div>

    <!-- 统计卡片 -->
    <div class="stats-cards">
      <el-card class="stats-card">
        <div class="stats-content">
          <div class="stats-icon connections">
            <el-icon><Connection /></el-icon>
          </div>
          <div class="stats-info">
            <div class="stats-value">{{ statistics?.totalConnections || 0 }}</div>
            <div class="stats-label">{{ t('ssh.totalConnections') }}</div>
          </div>
        </div>
      </el-card>
      <el-card class="stats-card">
        <div class="stats-content">
          <div class="stats-icon active">
            <el-icon><SuccessFilled /></el-icon>
          </div>
          <div class="stats-info">
            <div class="stats-value">{{ statistics?.activeConnections || 0 }}</div>
            <div class="stats-label">{{ t('ssh.activeConnections') }}</div>
          </div>
        </div>
      </el-card>
      <el-card class="stats-card">
        <div class="stats-content">
          <div class="stats-icon keys">
            <el-icon><Key /></el-icon>
          </div>
          <div class="stats-info">
            <div class="stats-value">{{ statistics?.totalKeyPairs || 0 }}</div>
            <div class="stats-label">{{ t('ssh.keyPairsCount') }}</div>
          </div>
        </div>
      </el-card>
      <el-card class="stats-card">
        <div class="stats-content">
          <div class="stats-icon commands">
            <el-icon><DocumentCopy /></el-icon>
          </div>
              <div class="stats-info">
                <div class="stats-value">{{ statistics?.totalCommands || 0 }}</div>
                <div class="stats-label">{{ t('ssh.executedCommands') }}</div>
              </div>
            </div>
          </el-card>
    </div>

    <!-- 主要内容区域 -->
    <div class="main-content">
      <el-tabs v-model="activeTab" @tab-change="handleTabChange">
        <!-- SSH连接管理 -->
        <el-tab-pane :label="t('ssh.connectionManagement')" name="connections">
          <div class="tab-content">
            <!-- 搜索和过滤 -->
            <div class="filter-bar">
              <el-row :gutter="20" style="width: 100%">
                <el-col :span="8">
                  <el-input
                    v-model="filter.search"
                    :placeholder="t('ssh.searchHostOrUsername')"
                    clearable
                    @input="handleFilter"
                  >
                    <template #prefix>
                      <el-icon><Search /></el-icon>
                    </template>
                  </el-input>
                </el-col>
                <el-col :span="6">
                  <el-select v-model="filter.status" :placeholder="t('ssh.status')" clearable @change="handleFilter" style="width: 100%">
                    <el-option :label="t('ssh.statusConnected')" value="connected" />
                    <el-option :label="t('ssh.statusDisconnected')" value="disconnected" />
                    <el-option :label="t('ssh.statusError')" value="error" />
                  </el-select>
                </el-col>
                <el-col :span="4">
                  <el-button @click="refreshConnections" :icon="Refresh">{{ t('ssh.refresh') }}</el-button>
                </el-col>
                <el-col :span="6" class="text-right">
                  <el-button type="success" size="small" @click="testAllConnections">
                    {{ t('ssh.testAllConnections') }}
                  </el-button>
                </el-col>
              </el-row>
            </div>

            <!-- 连接列表 -->
            <div class="connection-list">
              <!-- 自定义表格 -->
              <div class="data-table" v-if="filteredConnections.length > 0">
                <!-- 表头 -->
                <div class="table-header">
                  <div class="th th-checkbox">
                    <input type="checkbox" @change="toggleSelectAll" :checked="isAllSelected" />
                  </div>
                  <div class="th th-host">{{ t('ssh.host') }}</div>
                  <div class="th th-port">{{ t('ssh.port') }}</div>
                  <div class="th th-user">{{ t('ssh.username') }}</div>
                  <div class="th th-status">{{ t('ssh.status') }}</div>
                  <div class="th th-activity">{{ t('ssh.lastActivity') }}</div>
                  <div class="th th-actions">{{ t('common.actions') }}</div>
                </div>

                <!-- 表格行 -->
                <div 
                  v-for="row in filteredConnections"
                  :key="row.id || row.host"
                  class="table-row"
                  :class="{ selected: isRowSelected(row), connected: row.status === 'connected' }"
                >
                  <div class="td td-checkbox">
                    <input type="checkbox" :checked="isRowSelected(row)" @change="toggleRowSelect(row)" />
                  </div>
                  
                  <div class="td td-host">
                    <div class="status-dot" :class="row.status || 'disconnected'"></div>
                    <div class="host-info">
                      <span class="hostname">{{ row.host }}</span>
                      <span class="host-name" v-if="row.name">{{ row.name }}</span>
                    </div>
                  </div>

                  <div class="td td-port">
                    <code class="port-code">{{ row.port }}</code>
                  </div>

                  <div class="td td-user">
                    <span class="username">{{ row.username }}</span>
                  </div>

                  <div class="td td-status">
                    <span class="status-badge" :class="row.status || 'disconnected'">
                      {{ getStatusText(row.status || '') }}
                    </span>
                  </div>

                  <div class="td td-activity">
                    <span class="time">{{ formatTime(row.lastConnectedAt || '') }}</span>
                  </div>

                  <div class="td td-actions">
                    <button class="action-btn" @click="testConnection(row)" title="测试连接">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>
                    </button>
                    <button class="action-btn" @click="openTerminal(row)" title="终端">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="4 17 10 11 4 5"></polyline><line x1="12" y1="19" x2="20" y2="19"></line></svg>
                    </button>
                    <button class="action-btn" @click="executeCommand(row)" title="执行命令">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="4" y1="9" x2="20" y2="9"></line><line x1="4" y1="15" x2="20" y2="15"></line><line x1="10" y1="3" x2="8" y2="21"></line><line x1="16" y1="3" x2="14" y2="21"></line></svg>
                    </button>
                    <button class="action-btn danger" @click="handleConnectionAction('delete', row)" title="删除">
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
                    </button>
                  </div>
                </div>
              </div>

              <!-- 空状态 -->
              <div v-else class="empty-state">
                <div class="empty-icon">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path>
                  </svg>
                </div>
                <h3 class="empty-title">{{ t('ssh.noConnections') }}</h3>
                <p class="empty-desc">{{ t('ssh.noConnectionsHint') }}</p>
              </div>

              <!-- 分页 -->
              <div class="pagination-wrapper">
                <el-pagination
                  v-model:current-page="pagination.page"
                  v-model:page-size="pagination.pageSize"
                  :total="pagination.total"
                  :page-sizes="[10, 20, 50, 100]"
                  layout="total, sizes, prev, pager, next, jumper"
                  @size-change="handlePageSizeChange"
                  @current-change="handlePageChange"
                />
              </div>
            </div>
          </div>
        </el-tab-pane>

        <!-- 密钥管理 -->
        <el-tab-pane :label="t('ssh.keyPairManagement')" name="keypairs">
          <div class="tab-content">
            <KeyPairManagement />
          </div>
        </el-tab-pane>

        <!-- 命令执行 -->
        <el-tab-pane :label="t('ssh.commandExecution')" name="commands">
          <div class="tab-content">
            <CommandExecution />
          </div>
        </el-tab-pane>

        <!-- 文件传输 -->
        <el-tab-pane :label="t('ssh.fileTransfer')" name="transfers">
          <div class="tab-content">
            <FileTransfer />
          </div>
        </el-tab-pane>

        <!-- 会话管理 -->
        <el-tab-pane :label="t('ssh.sessionManagement')" name="sessions">
          <div class="tab-content">
            <SessionManagement />
          </div>
        </el-tab-pane>

        <!-- 操作日志 -->
        <el-tab-pane :label="t('ssh.operationLogs')" name="logs">
          <div class="tab-content">
            <OperationLogs />
          </div>
        </el-tab-pane>

        <!-- 设置 -->
        <el-tab-pane :label="t('ssh.settings.title')" name="settings">
          <div class="tab-content">
            <SshSettings />
          </div>
        </el-tab-pane>
      </el-tabs>
    </div>

    <!-- 创建连接对话框 -->
    <CreateConnectionDialog
      v-model="showCreateConnectionDialog"
      @success="handleConnectionCreated"
    />

    <!-- 生成密钥对对话框 -->
    <GenerateKeyPairDialog
      v-model="showKeyPairDialog"
      @success="handleKeyPairGenerated"
    />

    <!-- 批量操作对话框 -->
    <BatchOperationDialog
      v-model="showBatchDialog"
      :operation="batchOperation as 'test' | 'execute' | 'upload'"
      :selected-connections="selectedConnections"
      @success="handleBatchOperationSuccess"
    />

    <!-- SSH 终端对话框 -->
    <SshTerminalDialog
      v-model="showTerminalDialog"
      :connection="terminalConnection"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import {
  Connection,
  Plus,
  ArrowDown,
  Key,
  SuccessFilled,
  DocumentCopy,
  Search,
  Refresh
} from '@element-plus/icons-vue'
import { useSshStore } from '@/stores/ssh'
import { useSettingsStore } from '@/stores/settings'
import type { SshConnectionConfig, SshConnectionTestRequest } from '@/types/ssh'

// 组件导入（这些组件稍后创建）
import CreateConnectionDialog from '@/components/ssh/CreateConnectionDialog.vue'
import GenerateKeyPairDialog from '@/components/ssh/GenerateKeyPairDialog.vue'
import BatchOperationDialog from '@/components/ssh/BatchOperationDialog.vue'
import KeyPairManagement from '@/components/ssh/KeyPairManagement.vue'
import CommandExecution from '@/components/ssh/CommandExecution.vue'
import FileTransfer from '@/components/ssh/FileTransfer.vue'
import SessionManagement from '@/components/ssh/SessionManagement.vue'
import OperationLogs from '@/components/ssh/OperationLogs.vue'
import SshSettings from '@/components/ssh/SshSettings.vue'
import SshTerminalDialog from '@/components/ssh/SshTerminalDialog.vue'
import { formatLocalizedDateTime } from '@/utils/date'

// 状态管理
const sshStore = useSshStore()
const { state } = sshStore
const settingsStore = useSettingsStore()

// i18n
const { t } = useI18n()

// 响应式数据
const activeTab = ref('connections')
const loading = ref(false)
const showCreateConnectionDialog = ref(false)
const showKeyPairDialog = ref(false)
const showBatchDialog = ref(false)
const batchOperation = ref('')
const selectedConnections = ref<SshConnectionConfig[]>([])
const statistics = ref<any>(null)
const filter = ref({
  search: '',
  status: ''
})
const pagination = ref({
  page: 1,
  pageSize: settingsStore.defaultPageSize,
  total: 0
})

watch(() => settingsStore.defaultPageSize, (size) => {
  pagination.value.pageSize = size
  pagination.value.page = 1
  fetchConnectionConfigs()
})

// 定时器
let refreshTimer: ReturnType<typeof setInterval> | null = null

// 计算属性
const filteredConnections = computed(() => {
  let result = state.connectionConfigs

  if (filter.value.search) {
    result = result.filter((item: SshConnectionConfig) =>
      item.host.toLowerCase().includes(filter.value.search.toLowerCase()) ||
      item.username.toLowerCase().includes(filter.value.search.toLowerCase())
    )
  }

  if (filter.value.status) {
    result = result.filter((item: SshConnectionConfig) => item.status === filter.value.status)
  }

  return result
})

// 生命周期
onMounted(() => {
  initializeData()
  startAutoRefresh()
})

onUnmounted(() => {
  stopAutoRefresh()
})

// 方法
const initializeData = async () => {
  await Promise.all([
    fetchConnectionConfigs(),
    fetchStatistics()
  ])
}

const fetchConnectionConfigs = async () => {
  try {
    loading.value = true
    await sshStore.fetchConnectionConfigs({
      page: pagination.value.page,
      pageSize: pagination.value.pageSize
    })
    pagination.value.total = state.pagination.total
  } catch (error) {
    console.error('获取连接配置失败:', error)
  } finally {
    loading.value = false
  }
}

const fetchStatistics = async () => {
  try {
    const response = await sshStore.fetchStatistics()
    statistics.value = response
  } catch (error) {
    console.error('获取统计信息失败:', error)
  }
}

const startAutoRefresh = () => {
  // 禁用自动刷新 - 用户可以点击刷新按钮手动刷新
  // refreshTimer = setInterval(() => {
  //   if (activeTab.value === 'connections') {
  //     fetchStatistics()
  //   }
  // }, 30000)
}

const stopAutoRefresh = () => {
  if (refreshTimer) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
}

const handleTabChange = (tab: string) => {
  activeTab.value = tab
  if (tab === 'connections') {
    fetchConnectionConfigs()
    fetchStatistics()
  }
}

const handleFilter = () => {
  pagination.value.page = 1
  fetchConnectionConfigs()
}

const refreshConnections = () => {
  fetchConnectionConfigs()
  fetchStatistics()
}

const handleSelectionChange = (selection: SshConnectionConfig[]) => {
  selectedConnections.value = selection
}

// 自定义表格选择方法
const isRowSelected = (row: SshConnectionConfig) => selectedConnections.value.some(c => c.id === row.id || (c.host === row.host && c.port === row.port))
const isAllSelected = computed(() => 
  filteredConnections.value.length > 0 && filteredConnections.value.every(c => isRowSelected(c))
)

const toggleRowSelect = (row: SshConnectionConfig) => {
  const idx = selectedConnections.value.findIndex(c => c.id === row.id || (c.host === row.host && c.port === row.port))
  if (idx > -1) selectedConnections.value.splice(idx, 1)
  else selectedConnections.value.push(row)
}

const toggleSelectAll = () => {
  if (isAllSelected.value) {
    filteredConnections.value.forEach(c => {
      const idx = selectedConnections.value.findIndex(s => s.id === c.id || (s.host === c.host && s.port === c.port))
      if (idx > -1) selectedConnections.value.splice(idx, 1)
    })
  } else {
    filteredConnections.value.forEach(c => {
      if (!isRowSelected(c)) selectedConnections.value.push(c)
    })
  }
}

const handlePageChange = (page: number) => {
  pagination.value.page = page
  fetchConnectionConfigs()
}

const handlePageSizeChange = (pageSize: number) => {
  pagination.value.pageSize = pageSize
  pagination.value.page = 1
  fetchConnectionConfigs()
}

const handleBatchCommand = (command: string) => {
  if (selectedConnections.value.length === 0) {
    ElMessage.warning(t('common.pleaseSelect'))
    return
  }
  batchOperation.value = command
  showBatchDialog.value = true
}

const testConnection = async (connection: SshConnectionConfig) => {
  try {
    loading.value = true
    const testRequest: SshConnectionTestRequest = {
      host: connection.host,
      port: connection.port,
      username: connection.username,
      password: connection.password,
      privateKeyPath: connection.privateKeyPath
    }
    await sshStore.testConnection(testRequest)
    ElMessage.success(t('ssh.testSuccess'))
  } catch (error) {
    ElMessage.error(t('ssh.testFailed'))
  } finally {
    loading.value = false
  }
}

const testAllConnections = async () => {
  try {
    loading.value = true
    const connections = state.connectionConfigs
    const testRequests: SshConnectionTestRequest[] = connections.map((conn: SshConnectionConfig) => ({
      host: conn.host,
      port: conn.port,
      username: conn.username,
      password: conn.password,
      privateKeyPath: conn.privateKeyPath
    }))

    await sshStore.batchTestConnection({ connections: testRequests })
    ElMessage.success(t('ssh.batchTestComplete'))
  } catch (error) {
    ElMessage.error(t('ssh.batchTestFailed'))
  } finally {
    loading.value = false
  }
}

const showTerminalDialog = ref(false)
const terminalConnection = ref<SshConnectionConfig | undefined>(undefined)

const openTerminal = (connection: SshConnectionConfig) => {
  // 打开终端对话框
  terminalConnection.value = connection
  showTerminalDialog.value = true
}

const executeCommand = (_connection: SshConnectionConfig) => {
  // 执行命令
  activeTab.value = 'commands'
}

// 编辑连接相关
const showEditDialog = ref(false)
const editingConnection = ref<SshConnectionConfig | null>(null)

const handleConnectionAction = async (command: string, connection: SshConnectionConfig) => {
  switch (command) {
    case 'edit':
      // 编辑连接 - 打开编辑对话框
      editingConnection.value = { ...connection }
      showEditDialog.value = true
      break
    case 'file-manager':
      // 文件管理
      activeTab.value = 'transfers'
      break
    case 'metrics':
      // 性能指标
      try {
        await sshStore.getConnectionMetrics(connection.host, connection.port)
      } catch (error) {
        ElMessage.error(t('common.error'))
      }
      break
    case 'delete':
      try {
        await ElMessageBox.confirm(t('ssh.deleteConfirm'), t('common.deleteConfirm'), {
          type: 'warning'
        })
        if (connection.id) {
          await sshStore.deleteConnectionConfig(connection.id)
        }
        ElMessage.success(t('ssh.deleteSuccess'))
        fetchConnectionConfigs()
      } catch (error) {
        if (error !== 'cancel') {
          ElMessage.error(t('ssh.deleteFailed'))
        }
      }
      break
  }
}

const handleConnectionCreated = () => {
  fetchConnectionConfigs()
  fetchStatistics()
}

const handleKeyPairGenerated = () => {
  ElMessage.success(t('ssh.keyGenerated'))
}

const handleBatchOperationSuccess = () => {
  fetchConnectionConfigs()
  fetchStatistics()
}

const getStatusType = (status: string) => {
  const statusMap: Record<string, string> = {
    connected: 'success',
    disconnected: 'info',
    error: 'danger'
  }
  return statusMap[status] || 'info'
}

const getStatusText = (status: string) => {
  const statusMap: Record<string, string> = {
    connected: t('ssh.statusConnected'),
    disconnected: t('ssh.statusDisconnected'),
    error: t('ssh.statusError')
  }
  return statusMap[status] || t('ssh.statusUnknown')
}

const formatTime = (time: string) => {
  return formatLocalizedDateTime(time, '-')
}
</script>

<style scoped>
.ssh-management {
  padding: 20px;
  background-color: var(--bg-glass-dark);
  min-height: calc(100vh - 60px);
}

.page-header {
  background: var(--bg-surface);
  padding: 24px;
  border-radius: 8px;
  margin-bottom: 20px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 24px;
  width: 100%;
}

.title-section {
  flex: 1;
  min-width: 0;
}

.title-section h1 {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 0 0 8px 0;
  font-size: 24px;
  font-weight: 600;
  color: var(--text-main);
}

.title-section p {
  margin: 0;
  color: var(--text-muted);
  font-size: 14px;
}

.action-section {
  display: flex;
  gap: 12px;
  flex-shrink: 0;
  align-items: center;
  flex-wrap: wrap;
  margin-left: auto;
}

.stats-cards {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 20px;
  margin-bottom: 20px;
  width: 100%;
}

.stats-card {
  margin: 0;
}

.stats-card {
  border: none;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.stats-content {
  display: flex;
  align-items: center;
  gap: 16px;
}

.stats-icon {
  width: 48px;
  height: 48px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  color: white;
}

.stats-icon.connections {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.stats-icon.active {
  background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
}

.stats-icon.keys {
  background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
}

.stats-icon.commands {
  background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%);
}

.stats-value {
  font-size: 24px;
  font-weight: bold;
  color: var(--text-main);
  line-height: 1;
}

.stats-label {
  font-size: 14px;
  color: var(--text-muted);
  margin-top: 4px;
}

.main-content {
  background: var(--bg-surface);
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.tab-content {
  padding: 20px;
}

.filter-bar {
  margin-bottom: 20px;
  padding: 16px;
  background: var(--bg-subtle);
  border-radius: 6px;
  width: 100%;
}

.filter-bar .el-row {
  width: 100%;
  display: flex;
  align-items: center;
}

.text-right {
  text-align: right;
}

.connection-list {
  margin-top: 20px;
}

/* === Custom Data Table (matching Containers.vue) === */
.data-table {
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
  overflow: hidden;
  overflow-x: auto;
}

.table-header {
  display: grid;
  grid-template-columns: 40px minmax(180px, 1.5fr) 80px 120px 100px 140px 160px;
  gap: 8px;
  padding: 12px 16px;
  background: var(--bg-glass-dark);
  border-bottom: 1px solid var(--border-color);
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  align-items: center;
}

.th {
  display: flex;
  align-items: center;
  justify-content: center;
}

.th-checkbox {
  justify-content: center;
}

.th-actions {
  justify-content: center;
}

.table-row {
  display: grid;
  grid-template-columns: 40px minmax(180px, 1.5fr) 80px 120px 100px 140px 160px;
  gap: 8px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-color-light);
  align-items: center;
  transition: background 0.15s ease;
}

.table-row:last-child { border-bottom: none; }
.table-row:hover { background: var(--bg-glass-dark); }
.table-row.selected { background: rgba(59, 130, 246, 0.05); }
.table-row.connected { border-left: 3px solid #22c55e; padding-left: 13px; }

.td {
  font-size: 13px;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
}

.td-checkbox {
  display: flex;
  justify-content: center;
}

.td-host {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.status-dot.connected { background: #22c55e; box-shadow: 0 0 6px rgba(34, 197, 94, 0.5); }
.status-dot.disconnected { background: var(--text-muted); }
.status-dot.error { background: #ef4444; }

.host-info { display: flex; flex-direction: column; gap: 2px; min-width: 0; align-items: center; text-align: center; }

.hostname {
  font-weight: 600;
  color: var(--text-main);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.host-name {
  font-size: 11px;
  color: var(--text-muted);
}

.port-code {
  font-size: 12px;
  font-family: 'JetBrains Mono', monospace;
  color: var(--text-muted);
  background: var(--bg-subtle);
  padding: 2px 6px;
  border-radius: 4px;
}

.username {
  font-weight: 500;
  color: var(--text-secondary);
}

.status-badge {
  display: inline-flex;
  padding: 3px 8px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 600;
}

.status-badge.connected { background: rgba(34, 197, 94, 0.1); color: #16a34a; }
.status-badge.disconnected { background: var(--bg-subtle); color: var(--text-muted); }
.status-badge.error { background: rgba(239, 68, 68, 0.1); color: #dc2626; }

.time { font-size: 12px; color: var(--text-muted); }

.td-actions { display: flex; gap: 4px; justify-content: center; }

.action-btn {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  background: var(--bg-surface);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
  color: var(--text-muted);
}

.action-btn svg { width: 14px; height: 14px; }
.action-btn:hover { border-color: #3b82f6; color: #3b82f6; }
.action-btn.danger:hover { border-color: #ef4444; color: #ef4444; background: rgba(239, 68, 68, 0.1); }

/* === Empty State === */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 60px 40px;
  text-align: center;
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
}

.empty-icon {
  width: 64px;
  height: 64px;
  margin-bottom: 16px;
  color: var(--text-secondary);
}

.empty-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-secondary);
  margin: 0 0 8px 0;
}

.empty-desc {
  font-size: 14px;
  color: var(--text-muted);
  margin: 0;
}

.pagination-wrapper {
  margin-top: 20px;
  text-align: right;
}

:deep(.el-tabs__header) {
  margin: 0;
  padding: 0 20px;
  background: var(--bg-glass-dark);
  border-bottom: 1px solid var(--border-color);
}

:deep(.el-tabs__nav-wrap) {
  padding: 0;
}

:deep(.el-tabs__item) {
  padding: 0 20px;
  height: 50px;
  line-height: 50px;
}

/* === Responsive === */
@media (max-width: 1200px) {
  /* Hide last-activity column */
  .table-header, .table-row { grid-template-columns: 40px minmax(160px, 1.5fr) 70px 100px 90px 140px; }
  .th-activity, .td-activity { display: none; }
}

@media (max-width: 1024px) {
  /* Hide port + activity columns */
  .table-header, .table-row { grid-template-columns: 40px minmax(140px, 1.5fr) 100px 90px 130px; }
  .th-port, .td-port, .th-activity, .td-activity { display: none; }

  /* Stats cards: 2 columns */
  .stats-cards { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 768px) {
  .ssh-management { padding: 12px; }
  .page-header { margin-bottom: 12px; }
  .header-content { flex-direction: column; gap: 12px; }
  .action-section { flex-wrap: wrap; }
  .title-section h1 { font-size: 20px; }

  /* Stats cards: stack to 1 column */
  .stats-cards { grid-template-columns: 1fr; gap: 12px; }

  /* Filter: stack vertically */
  .filter-bar .el-col { flex: 0 0 100%; max-width: 100%; margin-bottom: 8px; }
  .text-right { text-align: left; }

  /* Table: hide port, user, activity columns */
  .table-header, .table-row { grid-template-columns: 36px 1fr 80px 120px; }
  .th-port, .td-port, .th-user, .td-user, .th-activity, .td-activity { display: none; }

  /* Tabs: scrollable */
  :deep(.el-tabs__header) { padding: 0 12px; }
  :deep(.el-tabs__item) { padding: 0 12px; height: 42px; line-height: 42px; font-size: 13px; }
  :deep(.el-tabs__nav-wrap) { overflow-x: auto; }

  .tab-content { padding: 12px; }
  .pagination-wrapper { text-align: center; }
}

@media (max-width: 480px) {
  .ssh-management { padding: 8px; }
  /* Table: only checkbox + host + actions */
  .table-header, .table-row { grid-template-columns: 32px 1fr 100px; }
  .th-port, .td-port, .th-user, .td-user, .th-status, .td-status, .th-activity, .td-activity { display: none; }
  .action-btn { width: 26px; height: 26px; }
  .action-btn svg { width: 12px; height: 12px; }
  .stats-icon { width: 40px; height: 40px; font-size: 20px; }
  .stats-value { font-size: 20px; }
}
</style>

<style>
/* Dark mode support */
html.dark .data-table { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
html.dark .table-header { background: #0f172a; border-color: rgba(255, 255, 255, 0.05); color: #94a3b8; }
html.dark .table-row { border-color: rgba(255, 255, 255, 0.05); }
html.dark .table-row:hover { background: rgba(255, 255, 255, 0.03); }
html.dark .hostname { color: #f1f5f9; }
html.dark .td { color: #cbd5e1; }
html.dark .action-btn { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
html.dark .port-code { background: rgba(255, 255, 255, 0.1); }
html.dark .empty-state { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
</style>