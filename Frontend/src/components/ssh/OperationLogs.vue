<template>
  <div class="operation-logs">
    <!-- 工具栏 -->
    <div class="toolbar">
      <el-row :gutter="16" align="middle">
        <el-col :span="6">
          <el-input
            v-model="filter.search"
            placeholder="搜索操作内容"
            clearable
            @input="handleSearch"
          >
            <template #prefix>
              <el-icon><Search /></el-icon>
            </template>
          </el-input>
        </el-col>
        <el-col :span="4">
          <el-select v-model="filter.type" placeholder="操作类型" clearable @change="handleFilter">
            <el-option label="全部" value="" />
            <el-option label="连接" value="connect" />
            <el-option label="断开" value="disconnect" />
            <el-option label="执行命令" value="execute" />
            <el-option label="上传文件" value="upload" />
            <el-option label="下载文件" value="download" />
            <el-option label="删除文件" value="delete" />
          </el-select>
        </el-col>
        <el-col :span="4">
          <el-select v-model="filter.status" placeholder="状态" clearable @change="handleFilter">
            <el-option label="全部" value="" />
            <el-option label="成功" value="success" />
            <el-option label="失败" value="failed" />
          </el-select>
        </el-col>
        <el-col :span="6">
          <el-date-picker
            v-model="filter.dateRange"
            type="daterange"
            range-separator="至"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            format="YYYY-MM-DD"
            value-format="YYYY-MM-DD"
            @change="handleFilter"
          />
        </el-col>
        <el-col :span="4" class="toolbar-right">
          <el-button @click="refreshLogs" :icon="Refresh">刷新</el-button>
          <el-button @click="exportLogs" :icon="Download">导出</el-button>
        </el-col>
      </el-row>
    </div>

    <!-- 日志列表 -->
    <el-table v-loading="loading" :data="logs" stripe>
      <el-table-column type="expand">
        <template #default="{ row }">
          <div class="log-detail">
            <el-descriptions :column="2" border size="small">
              <el-descriptions-item label="操作ID">{{ row.id }}</el-descriptions-item>
              <el-descriptions-item label="会话ID">{{ row.sessionId || '-' }}</el-descriptions-item>
              <el-descriptions-item label="IP地址">{{ row.clientIp }}</el-descriptions-item>
              <el-descriptions-item label="耗时">{{ row.duration ? `${row.duration}ms` : '-' }}</el-descriptions-item>
              <el-descriptions-item label="详细信息" :span="2">
                <pre class="detail-content">{{ formatDetail(row.detail) }}</pre>
              </el-descriptions-item>
              <el-descriptions-item v-if="row.error" label="错误信息" :span="2">
                <el-alert :title="row.error" type="error" :closable="false" />
              </el-descriptions-item>
            </el-descriptions>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="timestamp" label="时间" width="180" align="center">
        <template #default="{ row }">
          {{ formatDate(row.timestamp) }}
        </template>
      </el-table-column>
      <el-table-column label="目标主机" width="180" align="center">
        <template #default="{ row }">
          <div class="host-info">
            <span class="username">{{ row.username }}</span>@<span class="host">{{ row.host }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="type" label="操作类型" width="120" align="center">
        <template #default="{ row }">
          <el-tag size="small" :type="getTypeTag(row.type)">
            {{ getTypeText(row.type) }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="content" label="操作内容" min-width="250" show-overflow-tooltip align="center">
        <template #default="{ row }">
          <code class="operation-content">{{ row.content }}</code>
        </template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="100" align="center">
        <template #default="{ row }">
          <el-tag
            size="small"
            :type="row.status === 'success' ? 'success' : 'danger'"
          >
            {{ row.status === 'success' ? '成功' : '失败' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="operator" label="操作人" width="120" align="center" />
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
import { ref, reactive, onMounted, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Search, Refresh, Download } from '@element-plus/icons-vue'
import { useSshStore } from '@/stores/ssh'
import { useSettingsStore } from '@/stores/settings'
import { formatLocalizedDateTime } from '@/utils/date'

interface OperationLog {
  id: string
  timestamp: string
  host: string
  port: number
  username: string
  type: 'connect' | 'disconnect' | 'execute' | 'upload' | 'download' | 'delete'
  content: string
  status: 'success' | 'failed'
  operator: string
  sessionId?: string
  clientIp: string
  duration?: number
  detail?: any
  error?: string
}

const sshStore = useSshStore()
const settingsStore = useSettingsStore()
const loading = ref(false)
const logs = ref<OperationLog[]>([])

const filter = reactive({
  search: '',
  type: '',
  status: '',
  dateRange: null as [string, string] | null
})

const pagination = reactive({
  page: 1,
  pageSize: settingsStore.defaultPageSize,
  total: 0
})

onMounted(() => {
  fetchLogs()
})

const fetchLogs = async () => {
  loading.value = true
  try {
    const params: any = {
      page: pagination.page,
      pageSize: pagination.pageSize
    }

    if (filter.search) params.search = filter.search
    if (filter.type) params.type = filter.type
    if (filter.status) params.status = filter.status
    if (filter.dateRange) {
      params.startDate = filter.dateRange[0]
      params.endDate = filter.dateRange[1]
    }

    const response = await sshStore.fetchOperationLogs(params)
    logs.value = response.items || []
    pagination.total = response.total || 0
  } catch {
    ElMessage.error('获取操作日志失败')
  } finally {
    loading.value = false
  }
}

watch(() => settingsStore.defaultPageSize, (size) => {
  pagination.pageSize = size
  pagination.page = 1
  fetchLogs()
})

const handleSearch = () => {
  pagination.page = 1
  fetchLogs()
}

const handleFilter = () => {
  pagination.page = 1
  fetchLogs()
}

const handlePageChange = (page: number) => {
  pagination.page = page
  fetchLogs()
}

const handlePageSizeChange = (size: number) => {
  pagination.pageSize = size
  pagination.page = 1
  fetchLogs()
}

const refreshLogs = () => {
  fetchLogs()
}

const exportLogs = async () => {
  try {
    const allLogs = await sshStore.fetchOperationLogs({
      page: 1,
      pageSize: 10000,
      ...(filter.search && { search: filter.search }),
      ...(filter.type && { type: filter.type }),
      ...(filter.status && { status: filter.status }),
      ...(filter.dateRange && {
        startDate: filter.dateRange[0],
        endDate: filter.dateRange[1]
      })
    })

    const csvContent = generateCsv(allLogs.items || [])
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `ssh-operation-logs-${new Date().toISOString().slice(0, 10)}.csv`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)

    ElMessage.success('日志导出成功')
  } catch {
    ElMessage.error('导出失败')
  }
}

const generateCsv = (data: OperationLog[]) => {
  const headers = ['时间', '主机', '用户名', '操作类型', '操作内容', '状态', '操作人', 'IP地址', '耗时(ms)']
  const rows = data.map(log => [
    log.timestamp,
    log.host,
    log.username,
    getTypeText(log.type),
    log.content,
    log.status === 'success' ? '成功' : '失败',
    log.operator,
    log.clientIp,
    log.duration || ''
  ])

  return [headers, ...rows].map(row => row.map(cell => `"${cell}"`).join(',')).join('\n')
}

const formatDate = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

const formatDetail = (detail: any) => {
  if (!detail) return '-'
  try {
    return typeof detail === 'string' ? detail : JSON.stringify(detail, null, 2)
  } catch {
    return String(detail)
  }
}

const getTypeTag = (type: string) => {
  const typeMap: Record<string, string> = {
    connect: 'success',
    disconnect: 'info',
    execute: 'primary',
    upload: 'warning',
    download: '',
    delete: 'danger'
  }
  return typeMap[type] || 'info'
}

const getTypeText = (type: string) => {
  const typeMap: Record<string, string> = {
    connect: '连接',
    disconnect: '断开',
    execute: '执行命令',
    upload: '上传',
    download: '下载',
    delete: '删除'
  }
  return typeMap[type] || type
}
</script>

<style scoped>
.operation-logs {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.toolbar-right {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.log-detail {
  padding: 16px 24px;
}

.detail-content {
  margin: 0;
  padding: 12px;
  background-color: #f5f7fa;
  border-radius: 4px;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 200px;
  overflow: auto;
}

.host-info {
  font-size: 13px;
}

.username {
  color: #409eff;
}

.host {
  font-weight: 500;
  color: #303133;
}

.operation-content {
  font-size: 12px;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  color: #606266;
}

.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
}

</style>

<style>
/* === Dark Mode === */
html.dark .detail-content {
  background-color: #1a1a1a;
  color: #e5eaf3;
}

html.dark .host {
  color: #e5eaf3;
}

html.dark .operation-content {
  color: #a3a6ad;
}
</style>
