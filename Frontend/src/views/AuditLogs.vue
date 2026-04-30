<template>
  <div class="audit-page">
    <div class="page-header">
      <div>
        <h1>{{ t('audit.title') }}</h1>
        <p>{{ t('audit.subtitle') }}</p>
      </div>
      <div class="header-actions">
        <el-button :icon="Download" @click="exportCsv" :disabled="logs.length === 0">
          {{ t('audit.exportCsv') }}
        </el-button>
        <el-button type="primary" :icon="Refresh" @click="fetchLogs" :loading="loading">
          {{ t('common.refresh') }}
        </el-button>
      </div>
    </div>

    <el-card class="filter-card" shadow="never">
      <el-form :model="filters" class="filter-form" label-position="top">
        <el-row :gutter="16">
          <el-col :xs="24" :sm="12" :lg="6">
            <el-form-item :label="t('common.search')">
              <el-input
                v-model="filters.search"
                :placeholder="t('audit.searchPlaceholder')"
                :prefix-icon="Search"
                clearable
                @keyup.enter="handleSearch"
              />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12" :lg="4">
            <el-form-item :label="t('audit.operationType')">
              <el-select v-model="filters.operationType" clearable :placeholder="t('common.all')" @change="handleSearch">
                <el-option
                  v-for="type in operationTypes"
                  :key="type"
                  :label="getOperationTypeText(type)"
                  :value="type"
                />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12" :lg="4">
            <el-form-item :label="t('audit.resourceType')">
              <el-input v-model="filters.resourceType" clearable :placeholder="t('common.all')" @keyup.enter="handleSearch" />
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12" :lg="4">
            <el-form-item :label="t('common.status')">
              <el-select v-model="filters.status" clearable :placeholder="t('common.all')" @change="handleSearch">
                <el-option :label="t('audit.statuses.success')" value="success" />
                <el-option :label="t('audit.statuses.failed')" value="failed" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :xs="24" :sm="12" :lg="6">
            <el-form-item :label="t('audit.dateRange')">
              <el-date-picker
                v-model="filters.dateRange"
                type="datetimerange"
                :start-placeholder="t('audit.startTime')"
                :end-placeholder="t('audit.endTime')"
                value-format="YYYY-MM-DDTHH:mm:ss"
                class="date-range"
                @change="handleSearch"
              />
            </el-form-item>
          </el-col>
        </el-row>
        <div class="filter-actions">
          <el-input
            v-model="filters.nodeId"
            :placeholder="t('audit.nodeId')"
            clearable
            class="node-filter"
            @keyup.enter="handleSearch"
          />
          <el-button @click="resetFilters">{{ t('common.reset') }}</el-button>
          <el-button type="primary" @click="handleSearch">{{ t('common.search') }}</el-button>
        </div>
      </el-form>
    </el-card>

    <el-card class="table-card" shadow="never">
      <el-table v-loading="loading" :data="logs" row-key="id" stripe>
        <el-table-column type="expand">
          <template #default="{ row }">
            <div class="expanded-content">
              <el-descriptions :column="2" border size="small">
                <el-descriptions-item :label="t('audit.requestPath')" :span="2">
                  <code>{{ row.method }} {{ row.path }}</code>
                </el-descriptions-item>
                <el-descriptions-item :label="t('audit.clientIp')">{{ row.clientIp || '-' }}</el-descriptions-item>
                <el-descriptions-item :label="t('audit.userAgent')">{{ row.userAgent || '-' }}</el-descriptions-item>
                <el-descriptions-item :label="t('audit.routeValues')" :span="2">
                  <pre>{{ formatJson(row.routeValues) }}</pre>
                </el-descriptions-item>
                <el-descriptions-item :label="t('audit.queryValues')" :span="2">
                  <pre>{{ formatJson(row.query) }}</pre>
                </el-descriptions-item>
                <el-descriptions-item v-if="row.errorMessage" :label="t('audit.errorMessage')" :span="2">
                  <el-alert :title="row.errorMessage" type="error" :closable="false" />
                </el-descriptions-item>
              </el-descriptions>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="timestamp" :label="t('common.created')" min-width="170">
          <template #default="{ row }">{{ formatDateTime(row.timestamp) }}</template>
        </el-table-column>
        <el-table-column prop="operationType" :label="t('audit.operationType')" width="120">
          <template #default="{ row }">
            <el-tag :type="getOperationTag(row.operationType)" size="small">
              {{ getOperationTypeText(row.operationType) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column :label="t('audit.resource')" min-width="180">
          <template #default="{ row }">
            <div class="resource-cell">
              <span>{{ row.resourceType || '-' }}</span>
              <small>{{ row.resourceId || row.controller || '-' }}</small>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="path" :label="t('audit.requestPath')" min-width="280" show-overflow-tooltip>
          <template #default="{ row }">
            <code>{{ row.method }} {{ row.path }}</code>
          </template>
        </el-table-column>
        <el-table-column prop="nodeId" :label="t('audit.nodeId')" width="140" show-overflow-tooltip>
          <template #default="{ row }">{{ row.nodeId || '-' }}</template>
        </el-table-column>
        <el-table-column prop="status" :label="t('common.status')" width="110">
          <template #default="{ row }">
            <el-tag :type="row.status === 'success' ? 'success' : 'danger'" size="small">
              {{ getStatusText(row.status) }} / {{ row.statusCode }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="durationMs" :label="t('audit.duration')" width="100">
          <template #default="{ row }">{{ row.durationMs.toFixed(0) }}ms</template>
        </el-table-column>
        <el-table-column :label="t('common.actions')" width="100" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="openDetail(row)">{{ t('common.details') }}</el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :total="pagination.total"
          :page-sizes="[10, 20, 50, 100, 200]"
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="handlePageSizeChange"
          @current-change="fetchLogs"
        />
      </div>
    </el-card>

    <el-drawer v-model="detailVisible" :title="t('audit.detailTitle')" size="560px">
      <div v-if="selectedLog" class="detail-drawer">
        <el-descriptions :title="t('audit.requestInfo')" :column="1" border>
          <el-descriptions-item :label="t('common.id')">{{ selectedLog.id }}</el-descriptions-item>
          <el-descriptions-item :label="t('common.created')">{{ formatDateTime(selectedLog.timestamp) }}</el-descriptions-item>
          <el-descriptions-item :label="t('audit.requestPath')">{{ selectedLog.method }} {{ selectedLog.path }}</el-descriptions-item>
          <el-descriptions-item :label="t('common.status')">{{ getStatusText(selectedLog.status) }} / {{ selectedLog.statusCode }}</el-descriptions-item>
          <el-descriptions-item :label="t('audit.duration')">{{ selectedLog.durationMs.toFixed(2) }}ms</el-descriptions-item>
        </el-descriptions>

        <el-descriptions :title="t('audit.contextInfo')" :column="1" border>
          <el-descriptions-item :label="t('audit.operationType')">{{ getOperationTypeText(selectedLog.operationType) }}</el-descriptions-item>
          <el-descriptions-item :label="t('audit.resourceType')">{{ selectedLog.resourceType || '-' }}</el-descriptions-item>
          <el-descriptions-item :label="t('audit.resource')">{{ selectedLog.resourceId || '-' }}</el-descriptions-item>
          <el-descriptions-item :label="t('audit.nodeId')">{{ selectedLog.nodeId || '-' }}</el-descriptions-item>
          <el-descriptions-item :label="t('audit.clientIp')">{{ selectedLog.clientIp || '-' }}</el-descriptions-item>
          <el-descriptions-item :label="t('audit.userAgent')">{{ selectedLog.userAgent || '-' }}</el-descriptions-item>
        </el-descriptions>

        <el-alert v-if="selectedLog.errorMessage" :title="selectedLog.errorMessage" type="error" :closable="false" />
        <div class="json-section">
          <h3>{{ t('audit.routeValues') }}</h3>
          <pre>{{ formatJson(selectedLog.routeValues) }}</pre>
        </div>
        <div class="json-section">
          <h3>{{ t('audit.queryValues') }}</h3>
          <pre>{{ formatJson(selectedLog.query) }}</pre>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { Download, Refresh, Search } from '@element-plus/icons-vue'
import { auditApi, type OperationAuditLog, type OperationAuditLogFilter } from '@/api/audit'
import { useSettingsStore } from '@/stores/settings'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()
const settingsStore = useSettingsStore()

const operationTypes = [
  'create', 'update', 'delete', 'prune', 'exec', 'backup', 'restore',
  'export', 'upload', 'start', 'stop', 'restart', 'rename'
]

const loading = ref(false)
const logs = ref<OperationAuditLog[]>([])
const selectedLog = ref<OperationAuditLog | null>(null)
const detailVisible = ref(false)

const filters = reactive({
  search: '',
  operationType: '',
  resourceType: '',
  status: '',
  nodeId: '',
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

watch(() => settingsStore.defaultPageSize, (size) => {
  pagination.pageSize = size
  pagination.page = 1
  fetchLogs()
})

const buildQuery = (): OperationAuditLogFilter => {
  const query: OperationAuditLogFilter = {
    page: pagination.page,
    pageSize: pagination.pageSize
  }

  if (filters.search.trim()) query.search = filters.search.trim()
  if (filters.operationType) query.operationType = filters.operationType
  if (filters.resourceType.trim()) query.resourceType = filters.resourceType.trim()
  if (filters.status) query.status = filters.status
  if (filters.nodeId.trim()) query.nodeId = filters.nodeId.trim()
  if (filters.dateRange?.length === 2) {
    query.startDate = new Date(filters.dateRange[0]).toISOString()
    query.endDate = new Date(filters.dateRange[1]).toISOString()
  }

  return query
}

const fetchLogs = async () => {
  loading.value = true
  try {
    const result = await auditApi.getLogs(buildQuery())
    logs.value = result.items || []
    pagination.total = result.total || 0
  } catch (error: any) {
    ElMessage.error(error.message || t('audit.fetchFailed'))
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  pagination.page = 1
  fetchLogs()
}

const handlePageSizeChange = (size: number) => {
  pagination.pageSize = size
  pagination.page = 1
  fetchLogs()
}

const resetFilters = () => {
  filters.search = ''
  filters.operationType = ''
  filters.resourceType = ''
  filters.status = ''
  filters.nodeId = ''
  filters.dateRange = null
  handleSearch()
}

const openDetail = async (row: OperationAuditLog) => {
  selectedLog.value = row
  detailVisible.value = true
  try {
    selectedLog.value = await auditApi.getLog(row.id)
  } catch {
    selectedLog.value = row
  }
}

const getOperationTypeText = (type: string) => {
  return t(`audit.operationTypes.${type}`, type)
}

const getStatusText = (status: string) => {
  return t(`audit.statuses.${status}`, status)
}

const getOperationTag = (type: string) => {
  const dangerTypes = ['delete', 'prune', 'stop']
  const successTypes = ['create', 'start', 'backup', 'restore']
  const warningTypes = ['update', 'restart', 'exec']
  if (dangerTypes.includes(type)) return 'danger'
  if (successTypes.includes(type)) return 'success'
  if (warningTypes.includes(type)) return 'warning'
  return 'info'
}

const formatDateTime = (value: string) => {
  return formatLocalizedDateTime(value, '-')
}

const formatJson = (value: Record<string, string> | null | undefined) => {
  if (!value || Object.keys(value).length === 0) return t('audit.noExtraData')
  return JSON.stringify(value, null, 2)
}

const escapeCsv = (value: unknown) => {
  const text = String(value ?? '')
  if (/[",\n\r]/.test(text)) return `"${text.replace(/"/g, '""')}"`
  return text
}

const exportCsv = () => {
  const headers = [
    t('common.created'), t('audit.operationType'), t('audit.resourceType'), t('audit.resource'),
    'Method', t('audit.requestPath'), t('audit.nodeId'), t('common.status'),
    t('audit.statusCode'), t('audit.duration'), t('audit.clientIp'), t('audit.errorMessage')
  ]

  const rows = logs.value.map(log => [
    formatDateTime(log.timestamp), getOperationTypeText(log.operationType), log.resourceType, log.resourceId || '',
    log.method, log.path, log.nodeId || '', getStatusText(log.status), log.statusCode,
    `${log.durationMs.toFixed(2)}ms`, log.clientIp || '', log.errorMessage || ''
  ])

  const csv = [headers, ...rows].map(row => row.map(escapeCsv).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `operation-audit-${new Date().toISOString().slice(0, 10)}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  ElMessage.success(t('audit.exportSuccess'))
}
</script>

<style scoped>
.audit-page {
  padding: 24px;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 20px;
}

.page-header h1 {
  margin: 0 0 8px;
  color: var(--text-main);
  font-size: 28px;
}

.page-header p {
  margin: 0;
  color: var(--text-secondary);
}

.header-actions {
  display: flex;
  gap: 12px;
}

.filter-card,
.table-card {
  margin-bottom: 16px;
  border: 1px solid var(--border-color);
  background: var(--bg-card);
}

.filter-form :deep(.el-form-item) {
  margin-bottom: 14px;
}

.date-range {
  width: 100%;
}

.filter-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.node-filter {
  max-width: 240px;
}

.expanded-content {
  padding: 12px 32px;
}

.expanded-content pre,
.json-section pre {
  margin: 0;
  padding: 12px;
  overflow: auto;
  border-radius: 8px;
  background: var(--bg-secondary);
  color: var(--text-main);
  font-size: 12px;
  line-height: 1.6;
}

.resource-cell {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.resource-cell small {
  color: var(--text-secondary);
}

.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}

.detail-drawer {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.json-section h3 {
  margin: 0 0 10px;
  font-size: 15px;
}

code {
  color: var(--text-main);
  word-break: break-all;
}

@media (max-width: 768px) {
  .audit-page {
    padding: 16px;
  }

  .page-header,
  .filter-actions {
    align-items: stretch;
    flex-direction: column;
  }

  .header-actions,
  .node-filter {
    width: 100%;
    max-width: none;
  }
}
</style>