<template>
  <div class="container-health">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-left">
        <h1>{{ t('container.healthCheck.title') }}</h1>
        <p class="description">{{ t('container.healthCheck.description') }}</p>
      </div>
      <div class="header-right">
        <el-button @click="refreshData" :loading="loading" :icon="Refresh">{{ t('common.refresh') }}</el-button>
      </div>
    </div>

    <!-- 容器选择器 -->
    <el-card class="filter-card">
      <el-form :model="filterForm" inline>
        <el-form-item :label="t('common.containers')">
          <el-select
            v-model="filterForm.containerId"
            :placeholder="t('container.healthCheck.selectContainer')"
            filterable
            clearable
            @change="handleContainerChange"
            style="width: 300px"
          >
            <el-option
              v-for="container in containers"
              :key="container.id"
              :label="container.name || container.id"
              :value="container.id"
            >
              <div class="container-option">
                <span class="container-name">{{ container.name || container.id }}</span>
                <el-tag
                  :type="getStatusType(container.status)"
                  size="small"
                  class="container-status"
                >
                  {{ container.status }}
                </el-tag>
              </div>
            </el-option>
          </el-select>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 健康检查状态概览 -->
    <el-row v-if="selectedContainer" :gutter="16">
      <el-col :span="6">
        <el-card class="status-card">
          <div class="status-item">
            <div class="status-label">{{ t('container.healthCheck.healthStatus') }}</div>
            <div class="status-value">
              <el-tag
                :type="getHealthStatusType(healthStatus?.status)"
                size="large"
                effect="dark"
              >
                {{ getHealthStatusText(healthStatus?.status) }}
              </el-tag>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card class="status-card">
          <div class="status-item">
            <div class="status-label">{{ t('container.healthCheck.consecutiveSuccesses') }}</div>
            <div class="status-value success">
              {{ healthStats?.consecutiveSuccesses || 0 }}
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card class="status-card">
          <div class="status-item">
            <div class="status-label">{{ t('container.healthCheck.consecutiveFailures') }}</div>
            <div class="status-value error">
              {{ healthStats?.consecutiveFailures || 0 }}
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card class="status-card">
          <div class="status-item">
            <div class="status-label">{{ t('container.healthCheck.successRate') }}</div>
            <div class="status-value">
              {{ Math.round(healthStats?.successRate || 0) }}%
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 健康检查配置 -->
    <el-card v-if="selectedContainer && healthConfig" class="config-card" :header="t('container.healthCheck.config')">
      <el-descriptions :column="2" border>
        <el-descriptions-item :label="t('container.healthCheck.checkCommand')">
          <code>{{ healthConfig.test?.join(' ') || 'N/A' }}</code>
        </el-descriptions-item>
        <el-descriptions-item :label="t('container.healthCheck.checkInterval')">
          {{ formatDuration(healthConfig.interval) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('container.healthCheck.timeout')">
          {{ formatDuration(healthConfig.timeout) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('container.healthCheck.retries')">
          {{ healthConfig.retries || 0 }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('container.healthCheck.startPeriod')">
          {{ formatDuration(healthConfig.startPeriod) }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('container.healthCheck.lastCheck')">
          {{ healthStatus?.lastCheck ? formatTime(healthStatus.lastCheck) : 'N/A' }}
        </el-descriptions-item>
      </el-descriptions>

      <div class="config-actions">
        <el-button type="primary" @click="showEditDialog = true">
          {{ t('container.healthCheck.editConfig') }}
        </el-button>
        <el-button type="danger" @click="handleRemoveHealthCheck">
          {{ t('container.healthCheck.removeHealthCheck') }}
        </el-button>
      </div>
    </el-card>

    <!-- 健康检查日志 -->
    <el-card v-if="selectedContainer" :header="t('container.healthCheck.logs')">
      <div class="log-controls">
        <el-form inline>
          <el-form-item :label="t('container.healthCheck.timeRange')">
            <el-date-picker
              v-model="logTimeRange"
              type="datetimerange"
              :range-separator="t('container.healthCheck.to')"
              :start-placeholder="t('container.healthCheck.startTime')"
              :end-placeholder="t('container.healthCheck.endTime')"
              format="YYYY-MM-DD HH:mm:ss"
              value-format="YYYY-MM-DD HH:mm:ss"
              @change="loadHealthLogs"
            />
          </el-form-item>
          <el-form-item :label="t('container.healthCheck.displayCount')">
            <el-select v-model="logLimit" @change="loadHealthLogs" style="width: 120px">
              <el-option :value="50" label="50" />
              <el-option :value="100" label="100" />
              <el-option :value="200" label="200" />
              <el-option :value="500" label="500" />
            </el-select>
          </el-form-item>
        </el-form>
      </div>

      <el-table
        :data="healthLogs"
        v-loading="logsLoading"
        stripe
        style="width: 100%"
      >
        <el-table-column prop="timestamp" :label="t('container.healthCheck.time')" width="180">
          <template #default="{ row }">
            {{ formatTime(row.timestamp) }}
          </template>
        </el-table-column>
        <el-table-column prop="status" :label="t('common.status')" width="100">
          <template #default="{ row }">
            <el-tag :type="getHealthStatusType(row.status)" size="small">
              {{ getHealthStatusText(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="exitCode" :label="t('container.healthCheck.exitCode')" width="80" />
        <el-table-column prop="output" :label="t('container.healthCheck.output')" show-overflow-tooltip />
        <el-table-column prop="duration" :label="t('container.healthCheck.duration')" width="100">
          <template #default="{ row }">
            {{ row.duration }}ms
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 编辑健康检查配置对话框 -->
    <el-dialog
      v-model="showEditDialog"
      :title="t('container.healthCheck.editDialogTitle')"
      width="600px"
      :close-on-click-modal="false"
    >
      <el-form
        ref="editFormRef"
        :model="editForm"
        :rules="editRules"
        label-width="120px"
      >
        <el-form-item :label="t('container.healthCheck.enableHealthCheck')">
          <el-switch v-model="editForm.enabled" />
        </el-form-item>
        <template v-if="editForm.enabled">
          <el-form-item :label="t('container.healthCheck.checkCommand')" prop="test">
            <el-input
              v-model="editForm.testCommand"
              :placeholder="t('container.healthCheck.checkCommandPlaceholder')"
            />
          </el-form-item>
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item :label="t('container.healthCheck.checkIntervalSec')" prop="interval">
                <el-input-number
                  v-model="editForm.interval"
                  :min="1"
                  :max="3600"
                  controls-position="right"
                />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item :label="t('container.healthCheck.timeoutSec')" prop="timeout">
                <el-input-number
                  v-model="editForm.timeout"
                  :min="1"
                  :max="300"
                  controls-position="right"
                />
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item :label="t('container.healthCheck.retries')" prop="retries">
                <el-input-number
                  v-model="editForm.retries"
                  :min="0"
                  :max="10"
                  controls-position="right"
                />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item :label="t('container.healthCheck.startPeriodSec')" prop="startPeriod">
                <el-input-number
                  v-model="editForm.startPeriod"
                  :min="0"
                  :max="300"
                  controls-position="right"
                />
              </el-form-item>
            </el-col>
          </el-row>
        </template>
      </el-form>

      <template #footer>
        <span class="dialog-footer">
          <el-button @click="showEditDialog = false">{{ t('common.cancel') }}</el-button>
          <el-button type="primary" @click="handleUpdateHealthCheck" :loading="updateLoading">
            {{ t('common.save') }}
          </el-button>
        </span>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh } from '@element-plus/icons-vue'
import { containerApi, type ContainerInfo, type HealthCheckConfig, type HealthCheckStatus, type HealthCheckStats, type HealthCheckLog } from '@/api/containers'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()

// 响应式数据
const loading = ref(false)
const logsLoading = ref(false)
const updateLoading = ref(false)
const containers = ref<ContainerInfo[]>([])
const selectedContainer = ref<ContainerInfo | null>(null)
const healthStatus = ref<HealthCheckStatus | null>(null)
const healthStats = ref<HealthCheckStats | null>(null)
const healthLogs = ref<HealthCheckLog[]>([])
const healthConfig = ref<HealthCheckConfig | null>(null)
const showEditDialog = ref(false)

// 过滤表单
const filterForm = reactive({
  containerId: ''
})

// 日志查询参数
const logTimeRange = ref<[string, string]>(['', ''])
const logLimit = ref(100)

// 编辑表单
const editFormRef = ref()
const editForm = reactive({
  enabled: false,
  testCommand: '',
  interval: 30,
  timeout: 10,
  retries: 3,
  startPeriod: 0
})

const editRules = {
  testCommand: [
    { required: true, message: t('container.healthCheck.validation.checkCommandRequired'), trigger: 'blur' }
  ],
  interval: [
    { required: true, message: t('container.healthCheck.validation.intervalRequired'), trigger: 'blur' }
  ],
  timeout: [
    { required: true, message: t('container.healthCheck.validation.timeoutRequired'), trigger: 'blur' }
  ],
  retries: [
    { required: true, message: t('container.healthCheck.validation.retriesRequired'), trigger: 'blur' }
  ]
}

// 方法
const loadContainers = async () => {
  try {
    const response = await containerApi.getContainers({ all: true })
    containers.value = response
  } catch (error) {
    console.error('加载容器列表失败:', error)
    ElMessage.error(t('container.healthCheck.loadContainersFailed'))
  }
}

const loadHealthStatus = async () => {
  if (!selectedContainer.value) return

  try {
    const response = await containerApi.getContainerHealthStatus(selectedContainer.value.id)
    healthStatus.value = response
  } catch (error) {
    console.error('加载健康状态失败:', error)
    healthStatus.value = null
  }
}

const loadHealthStats = async () => {
  if (!selectedContainer.value) return

  try {
    const response = await containerApi.getContainerHealthStats(selectedContainer.value.id)
    healthStats.value = response
  } catch (error) {
    console.error('加载健康统计失败:', error)
    healthStats.value = null
  }
}

const loadHealthLogs = async () => {
  if (!selectedContainer.value) return

  logsLoading.value = true
  try {
    const params: any = {
      limit: logLimit.value
    }

    if (logTimeRange.value[0] && logTimeRange.value[1]) {
      params.since = logTimeRange.value[0]
      params.until = logTimeRange.value[1]
    }

    const response = await containerApi.getContainerHealthLogs(selectedContainer.value.id, params)
    healthLogs.value = response
  } catch (error) {
    console.error('加载健康日志失败:', error)
    ElMessage.error(t('container.healthCheck.loadLogsFailed'))
  } finally {
    logsLoading.value = false
  }
}

const handleContainerChange = async () => {
  if (!filterForm.containerId) {
    selectedContainer.value = null
    healthStatus.value = null
    healthStats.value = null
    healthLogs.value = []
    healthConfig.value = null
    return
  }

  selectedContainer.value = containers.value.find(c => c.id === filterForm.containerId) || null

  if (selectedContainer.value) {
    await Promise.all([
      loadHealthStatus(),
      loadHealthStats(),
      loadHealthLogs()
    ])

    // 从容器信息中获取健康检查配置
    healthConfig.value = (selectedContainer.value as any).health || null

    // 初始化编辑表单
    if (healthConfig.value) {
      editForm.enabled = true
      editForm.testCommand = healthConfig.value.test?.join(' ') || ''
      editForm.interval = (healthConfig.value.interval || 30000) / 1000
      editForm.timeout = (healthConfig.value.timeout || 10000) / 1000
      editForm.retries = healthConfig.value.retries || 3
      editForm.startPeriod = (healthConfig.value.startPeriod || 0) / 1000
    } else {
      editForm.enabled = false
      editForm.testCommand = ''
      editForm.interval = 30
      editForm.timeout = 10
      editForm.retries = 3
      editForm.startPeriod = 0
    }
  }
}

const handleUpdateHealthCheck = async () => {
  if (!editFormRef.value || !selectedContainer.value) return

  try {
    await editFormRef.value.validate()

    updateLoading.value = true

    if (editForm.enabled) {
      const config: HealthCheckConfig = {
        test: editForm.testCommand.trim().split(/\s+/),
        interval: editForm.interval * 1000,
        timeout: editForm.timeout * 1000,
        retries: editForm.retries,
        startPeriod: editForm.startPeriod * 1000
      }

      await containerApi.updateContainerHealthCheck(selectedContainer.value.id, config)
      ElMessage.success(t('container.healthCheck.updateSuccess'))
    } else {
      await containerApi.removeContainerHealthCheck(selectedContainer.value.id)
      ElMessage.success(t('container.healthCheck.removed'))
    }

    showEditDialog.value = false
    await refreshData()
  } catch (error: any) {
    console.error('更新健康检查配置失败:', error)
    ElMessage.error(error.response?.data?.message || t('container.healthCheck.updateFailed'))
  } finally {
    updateLoading.value = false
  }
}

const handleRemoveHealthCheck = async () => {
  if (!selectedContainer.value) return

  try {
    await ElMessageBox.confirm(
      t('container.healthCheck.removeConfirm', { name: selectedContainer.value.name || selectedContainer.value.id }),
      t('common.deleteConfirm'),
      {
        confirmButtonText: t('common.confirm'),
        cancelButtonText: t('common.cancel'),
        type: 'warning'
      }
    )

    await containerApi.removeContainerHealthCheck(selectedContainer.value.id)
    ElMessage.success(t('container.healthCheck.removed'))
    await refreshData()
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('移除健康检查失败:', error)
      ElMessage.error(error.response?.data?.message || t('container.healthCheck.removeFailed'))
    }
  }
}

const refreshData = async () => {
  loading.value = true
  try {
    await Promise.all([
      loadContainers(),
      handleContainerChange()
    ])
  } finally {
    loading.value = false
  }
}

// 工具方法
const getStatusType = (status: string) => {
  switch (status?.toLowerCase()) {
    case 'running':
      return 'success'
    case 'stopped':
      return 'danger'
    case 'paused':
      return 'warning'
    default:
      return 'info'
  }
}

const getHealthStatusType = (status?: string) => {
  switch (status?.toLowerCase()) {
    case 'healthy':
      return 'success'
    case 'unhealthy':
      return 'danger'
    case 'starting':
      return 'warning'
    default:
      return 'info'
  }
}

const getHealthStatusText = (status?: string) => {
  switch (status?.toLowerCase()) {
    case 'healthy':
      return t('container.healthCheck.statusHealthy')
    case 'unhealthy':
      return t('container.healthCheck.statusUnhealthy')
    case 'starting':
      return t('container.healthCheck.statusStarting')
    case 'none':
      return t('container.healthCheck.statusNone')
    default:
      return t('common.unknown')
  }
}

const formatDuration = (ms?: number) => {
  if (!ms) return 'N/A'
  const seconds = Math.floor(ms / 1000)
  return t('container.healthCheck.secondsFormat', { seconds })
}

const formatTime = (time: string) => {
  return formatLocalizedDateTime(time, '--')
}

// 生命周期
onMounted(() => {
  refreshData()
})

// 监听器
watch([logTimeRange, logLimit], () => {
  if (selectedContainer.value) {
    loadHealthLogs()
  }
})
</script>

<style scoped>
.container-health {
  padding: 20px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.header-left h1 {
  margin: 0;
  color: #303133;
}

.header-left .description {
  margin: 5px 0 0 0;
  color: #909399;
  font-size: 14px;
}

.filter-card {
  margin-bottom: 20px;
}

.container-option {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.container-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.container-status {
  margin-left: 10px;
}

.status-card {
  margin-bottom: 20px;
}

.status-item {
  text-align: center;
}

.status-label {
  font-size: 14px;
  color: #909399;
  margin-bottom: 8px;
}

.status-value {
  font-size: 24px;
  font-weight: bold;
  color: #303133;
}

.status-value.success {
  color: #67c23a;
}

.status-value.error {
  color: #f56c6c;
}

.config-card {
  margin-bottom: 20px;
}

.config-actions {
  margin-top: 20px;
  text-align: right;
}

.log-controls {
  margin-bottom: 20px;
}

code {
  background-color: #f5f7fa;
  padding: 2px 6px;
  border-radius: 3px;
  font-family: 'Courier New', monospace;
}
</style>