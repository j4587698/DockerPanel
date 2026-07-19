<template>
  <div class="session-management">
    <!-- 工具栏 -->
    <div class="toolbar">
      <el-row :gutter="16" align="middle">
        <el-col :span="6">
          <el-input
            v-model="searchKeyword"
            placeholder="搜索会话"
            clearable
          >
            <template #prefix>
              <el-icon><Search /></el-icon>
            </template>
          </el-input>
        </el-col>
        <el-col :span="4">
          <el-select v-model="filterStatus" placeholder="状态" clearable>
            <el-option label="全部" value="" />
            <el-option label="活跃" value="active" />
            <el-option label="空闲" value="idle" />
            <el-option label="已断开" value="disconnected" />
          </el-select>
        </el-col>
        <el-col :span="14" class="toolbar-right">
          <el-button @click="refreshSessions" :icon="Refresh">刷新</el-button>
          <el-button type="danger" @click="terminateAll" :disabled="activeSessions.length === 0">
            终止所有会话
          </el-button>
        </el-col>
      </el-row>
    </div>

    <!-- 会话统计 -->
    <el-row :gutter="20" class="stats-row">
      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-content">
            <div class="stat-icon total">
              <el-icon><Connection /></el-icon>
            </div>
            <div class="stat-info">
              <div class="stat-value">{{ sessions.length }}</div>
              <div class="stat-label">总会话数</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-content">
            <div class="stat-icon active">
              <el-icon><CircleCheck /></el-icon>
            </div>
            <div class="stat-info">
              <div class="stat-value">{{ activeSessions.length }}</div>
              <div class="stat-label">活跃会话</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-content">
            <div class="stat-icon idle">
              <el-icon><Clock /></el-icon>
            </div>
            <div class="stat-info">
              <div class="stat-value">{{ idleSessions.length }}</div>
              <div class="stat-label">空闲会话</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card class="stat-card">
          <div class="stat-content">
            <div class="stat-icon disconnected">
              <el-icon><CircleClose /></el-icon>
            </div>
            <div class="stat-info">
              <div class="stat-value">{{ disconnectedSessions.length }}</div>
              <div class="stat-label">已断开</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 会话列表 -->
    <el-table v-loading="loading" :data="filteredSessions">
      <el-table-column prop="id" label="会话ID" width="120" align="center">
        <template #default="{ row }">
          <code class="session-id">{{ row.id.substring(0, 8) }}...</code>
        </template>
      </el-table-column>
      <el-table-column label="连接信息" min-width="200" align="center">
        <template #default="{ row }">
          <div class="connection-info">
            <span class="host">{{ row.username }}@{{ row.host }}</span>
            <span class="port">:{{ row.port }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="类型" width="100" align="center">
        <template #default="{ row }">
          <el-tag size="small" :type="getTypeTag(row.type)">
            {{ row.type.toUpperCase() }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="状态" width="100" align="center">
        <template #default="{ row }">
          <div class="status-cell">
            <span class="status-dot" :class="row.status"></span>
            <span>{{ getStatusText(row.status) }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="开始时间" width="160" align="center">
        <template #default="{ row }">
          {{ formatDate(row.createdAt) }}
        </template>
      </el-table-column>
      <el-table-column label="持续时间" width="120" align="center">
        <template #default="{ row }">
          {{ formatDuration(row.createdAt) }}
        </template>
      </el-table-column>
      <el-table-column prop="lastActivityAt" label="最后活动" width="160" align="center">
        <template #default="{ row }">
          {{ formatDate(row.lastActivityAt) }}
        </template>
      </el-table-column>
      <el-table-column label="数据传输" width="150" align="center">
        <template #default="{ row }">
          <div class="data-transfer">
            <span class="upload">↑ {{ formatBytes(row.bytesSent) }}</span>
            <span class="download">↓ {{ formatBytes(row.bytesReceived) }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right" align="center">
        <template #default="{ row }">
          <el-button
            v-if="row.status === 'active' || row.status === 'idle'"
            size="small"
            type="danger"
            @click="terminateSession(row)"
          >
            终止
          </el-button>
          <el-button
            v-if="row.status === 'disconnected'"
            size="small"
            @click="reconnectSession(row)"
          >
            重连
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 分页 -->
    <div class="pagination-wrapper">
      <el-pagination
        v-model:current-page="pagination.page"
        v-model:page-size="pagination.pageSize"
        :total="pagination.total"
        :page-sizes="[10, 20, 50, 100]"
        layout="total, sizes, prev, pager, next"
        @size-change="handlePageSizeChange"
        @current-change="handlePageChange"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, onUnmounted, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  Search, Refresh, Connection, CircleCheck, Clock, CircleClose
} from '@element-plus/icons-vue'
import { useSshStore } from '@/stores/ssh'
import { useSettingsStore } from '@/stores/settings'
import { formatLocalizedDateTime } from '@/utils/date'

interface SshSession {
  id: string
  connectionId: string
  host: string
  port: number
  username: string
  type: 'shell' | 'exec' | 'sftp'
  status: 'active' | 'idle' | 'disconnected'
  createdAt: string
  lastActivityAt: string
  bytesSent: number
  bytesReceived: number
}

const sshStore = useSshStore()
const settingsStore = useSettingsStore()
const loading = ref(false)
const searchKeyword = ref('')
const filterStatus = ref('')
const sessions = ref<SshSession[]>([])
let refreshTimer: ReturnType<typeof setInterval> | null = null

const pagination = reactive({
  page: 1,
  pageSize: settingsStore.defaultPageSize,
  total: 0
})

const activeSessions = computed(() =>
  sessions.value.filter(s => s.status === 'active')
)

const idleSessions = computed(() =>
  sessions.value.filter(s => s.status === 'idle')
)

const disconnectedSessions = computed(() =>
  sessions.value.filter(s => s.status === 'disconnected')
)

const filteredSessions = computed(() => {
  let result = sessions.value

  if (searchKeyword.value) {
    const keyword = searchKeyword.value.toLowerCase()
    result = result.filter(s =>
      s.host.toLowerCase().includes(keyword) ||
      s.username.toLowerCase().includes(keyword) ||
      s.id.toLowerCase().includes(keyword)
    )
  }

  if (filterStatus.value) {
    result = result.filter(s => s.status === filterStatus.value)
  }

  return result
})

onMounted(() => {
  fetchSessions()
  // 禁用自动刷新 - 用户可以点击刷新按钮手动刷新
  // refreshTimer = setInterval(fetchSessions, 10000)
})

onUnmounted(() => {
  if (refreshTimer) {
    clearInterval(refreshTimer)
  }
})

const fetchSessions = async () => {
  loading.value = true
  try {
    const response = await sshStore.fetchSessions({
      page: pagination.page,
      pageSize: pagination.pageSize
    })
    sessions.value = response.items || []
    pagination.total = response.total || 0
  } catch {
    ElMessage.error('获取会话列表失败')
  } finally {
    loading.value = false
  }
}

watch(() => settingsStore.defaultPageSize, (size) => {
  pagination.pageSize = size
  pagination.page = 1
  fetchSessions()
})

const refreshSessions = () => {
  fetchSessions()
}

const handlePageChange = (page: number) => {
  pagination.page = page
  fetchSessions()
}

const handlePageSizeChange = (size: number) => {
  pagination.pageSize = size
  pagination.page = 1
  fetchSessions()
}

const terminateSession = async (session: SshSession) => {
  try {
    await ElMessageBox.confirm(
      `确定要终止会话 ${session.id.substring(0, 8)} 吗？`,
      '终止会话',
      { type: 'warning' }
    )

    await sshStore.terminateSession(session.id)
    ElMessage.success('会话已终止')
    fetchSessions()
  } catch {
    // 用户取消
  }
}

const terminateAll = async () => {
  try {
    await ElMessageBox.confirm(
      `确定要终止所有 ${activeSessions.value.length + idleSessions.value.length} 个活跃会话吗？`,
      '终止所有会话',
      { type: 'warning' }
    )

    for (const session of [...activeSessions.value, ...idleSessions.value]) {
      await sshStore.terminateSession(session.id)
    }

    ElMessage.success('所有会话已终止')
    fetchSessions()
  } catch {
    // 用户取消
  }
}

const reconnectSession = async (session: SshSession) => {
  try {
    await sshStore.reconnectSession(session.id)
    ElMessage.success('正在重新连接...')
    fetchSessions()
  } catch {
    ElMessage.error('重连失败')
  }
}

const getTypeTag = (type: string) => {
  const typeMap: Record<string, string> = {
    shell: 'primary',
    exec: 'success',
    sftp: 'warning'
  }
  return typeMap[type] || 'info'
}

const getStatusText = (status: string) => {
  const statusMap: Record<string, string> = {
    active: '活跃',
    idle: '空闲',
    disconnected: '已断开'
  }
  return statusMap[status] || status
}

const formatDate = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

const formatDuration = (startTime: string) => {
  const start = new Date(startTime).getTime()
  const now = Date.now()
  const diff = now - start

  const hours = Math.floor(diff / 3600000)
  const minutes = Math.floor((diff % 3600000) / 60000)
  const seconds = Math.floor((diff % 60000) / 1000)

  if (hours > 0) {
    return `${hours}h ${minutes}m`
  } else if (minutes > 0) {
    return `${minutes}m ${seconds}s`
  } else {
    return `${seconds}s`
  }
}

const formatBytes = (bytes: number) => {
  if (bytes === null || bytes === undefined || isNaN(bytes) || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const absBytes = Math.abs(bytes)
  if (absBytes < 1) return absBytes.toFixed(1) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}
</script>

<style scoped>
.session-management {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.toolbar-right {
  text-align: right;
}

.stats-row {
  margin-bottom: 0;
}

.stat-card {
  border: none;
}

.stat-content {
  display: flex;
  align-items: center;
  gap: 16px;
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22px;
  color: white;
}

.stat-icon.total {
  background: linear-gradient(135deg, #667eea, #764ba2);
}

.stat-icon.active {
  background: linear-gradient(135deg, #43e97b, #38f9d7);
}

.stat-icon.idle {
  background: linear-gradient(135deg, #f093fb, #f5576c);
}

.stat-icon.disconnected {
  background: linear-gradient(135deg, #868f96, #596164);
}

.stat-value {
  font-size: 28px;
  font-weight: 700;
  color: #303133;
}

.stat-label {
  font-size: 13px;
  color: #909399;
}

.session-id {
  font-size: 12px;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  color: #606266;
}

.connection-info {
  font-size: 14px;
}

.host {
  font-weight: 500;
  color: #303133;
}

.port {
  color: #909399;
}

.status-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.status-dot.active {
  background-color: #67c23a;
  box-shadow: 0 0 6px rgba(103, 194, 58, 0.5);
}

.status-dot.idle {
  background-color: #e6a23c;
}

.status-dot.disconnected {
  background-color: #909399;
}

.data-transfer {
  display: flex;
  flex-direction: column;
  gap: 2px;
  font-size: 12px;
  align-items: center;
}

.upload {
  color: #67c23a;
}

.download {
  color: #409eff;
}

.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
}

</style>

<style>
/* === Dark Mode === */
html.dark .stat-value,
html.dark .host {
  color: #e5eaf3;
}

html.dark .session-id {
  color: #a3a6ad;
}
</style>
