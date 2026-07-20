<template>
  <el-drawer
    v-model="visible"
    :title="`${t('containerDetailDrawer.title')} - ${container?.name || ''}`"
    direction="rtl"
    size="800px"
    @close="handleClose"
  >
    <div v-if="loading" class="loading-container">
      <el-skeleton :rows="10" animated />
    </div>

    <div v-else-if="!container" class="empty-container">
      <el-empty :description="t('containerDetailDrawer.containerNotFound')" />
    </div>

    <div v-else class="container-detail">
      <!-- 基本信息 -->
      <el-card class="detail-section" :header="t('containerDetailDrawer.basicInfo')">
        <el-descriptions :column="2" border>
          <el-descriptions-item :label="t('containerDetailDrawer.containerId')">
            <el-text type="info" copyable>{{ container.id }}</el-text>
          </el-descriptions-item>
          <el-descriptions-item :label="t('common.name')">
            {{ container.name }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('containerDetailDrawer.image')">
            {{ container.image }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('common.status')">
            <el-tag :type="getStatusType(container.state)">{{ container.state }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item :label="t('containerDetailDrawer.created')">
            {{ formatDateTime(container.created) }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('containerDetailDrawer.runningStatus')">
            {{ container.status }}
          </el-descriptions-item>
        </el-descriptions>
      </el-card>

      <!-- 端口映射 -->
      <el-card class="detail-section" :header="t('containerDetailDrawer.portMappings')">
        <div v-if="!container.portMappings || container.portMappings.length === 0" class="empty-section">
          <el-text type="info">{{ t('containerDetailDrawer.noPortMappings') }}</el-text>
        </div>
        <div v-else class="port-mappings">
          <div
            v-for="(port, index) in container.portMappings"
            :key="index"
            class="port-item"
          >
            <el-tag type="primary">
              {{ port.hostPort }}:{{ port.containerPort }}/{{ port.protocol }}
            </el-tag>
            <el-text v-if="port.hostIp" type="info" size="small">
              {{ t('containerDetailDrawer.hostIp') }}: {{ port.hostIp }}
            </el-text>
          </div>
        </div>
      </el-card>

      <!-- 环境变量 -->
      <el-card class="detail-section" :header="t('containerDetailDrawer.environmentVars')">
        <div v-if="!container.environment || Object.keys(container.environment).length === 0" class="empty-section">
          <el-text type="info">{{ t('containerDetailDrawer.noEnvironmentVars') }}</el-text>
        </div>
        <div v-else class="env-list">
          <div
            v-for="(value, key) in container.environment"
            :key="key"
            class="env-item"
          >
            <el-text type="primary" class="env-key">{{ key }}</el-text>
            <el-text>=</el-text>
            <el-text class="env-value">{{ value }}</el-text>
          </div>
        </div>
      </el-card>

      <!-- 卷挂载 -->
      <el-card class="detail-section" :header="t('containerDetailDrawer.volumes')">
        <div v-if="!container.volumes || container.volumes.length === 0" class="empty-section">
          <el-text type="info">{{ t('containerDetailDrawer.noVolumes') }}</el-text>
        </div>
        <div v-else class="volume-list">
          <div
            v-for="(volume, index) in container.volumes"
            :key="index"
            class="volume-item"
          >
            <el-text class="volume-path">{{ volume.hostPath }}</el-text>
            <el-icon><ArrowRight /></el-icon>
            <el-text class="volume-path">{{ volume.containerPath }}</el-text>
            <el-tag v-if="volume.readOnly" type="warning" size="small">{{ t('containerDetailDrawer.readOnly') }}</el-tag>
          </div>
        </div>
      </el-card>

      <!-- 网络配置 -->
      <el-card class="detail-section" :header="t('containerDetailDrawer.networkConfig')">
        <div v-if="!container.networks || container.networks.length === 0" class="empty-section">
          <el-text type="info">{{ t('containerDetailDrawer.noNetworkConfig') }}</el-text>
        </div>
        <div v-else class="network-list">
          <el-tag
            v-for="network in container.networks"
            :key="network"
            type="success"
            class="network-item"
          >
            {{ network }}
          </el-tag>
        </div>
      </el-card>

      <!-- 重启策略 -->
      <el-card class="detail-section" :header="t('containerDetailDrawer.restartPolicy')">
        <div v-if="!container.restartPolicy" class="empty-section">
          <el-text type="info">{{ t('containerDetailDrawer.useDefaultRestartPolicy') }}</el-text>
        </div>
        <div v-else class="restart-policy">
          <el-descriptions :column="2" border>
            <el-descriptions-item :label="t('containerDetailDrawer.policy')">
              <el-tag>{{ container.restartPolicy.name }}</el-tag>
            </el-descriptions-item>
            <el-descriptions-item :label="t('containerDetailDrawer.maxRetryCount')" v-if="container.restartPolicy.maximumRetryCount > 0">
              {{ container.restartPolicy.maximumRetryCount }}
            </el-descriptions-item>
          </el-descriptions>
        </div>
      </el-card>

      <!-- 标签 -->
      <el-card class="detail-section" :header="t('containerDetailDrawer.labels')">
        <div v-if="!container.labels || Object.keys(container.labels).length === 0" class="empty-section">
          <el-text type="info">{{ t('containerDetailDrawer.noLabels') }}</el-text>
        </div>
        <div v-else class="label-list">
          <div
            v-for="(value, key) in container.labels"
            :key="key"
            class="label-item"
          >
            <el-tag type="info">
              {{ key }}: {{ value }}
            </el-tag>
          </div>
        </div>
      </el-card>

      <!-- 资源使用情况 -->
      <el-card class="detail-section" :header="t('containerDetailDrawer.resourceUsage')">
        <div v-if="!stats" class="empty-section">
          <el-button @click="loadStats" :loading="statsLoading">
            {{ t('containerDetailDrawer.loadStats') }}
          </el-button>
        </div>
        <div v-else class="stats-info">
          <el-row :gutter="16">
            <el-col :span="12">
              <div class="stat-item">
                <div class="stat-label">{{ t('containerDetailDrawer.cpuUsage') }}</div>
                <div class="stat-value">{{ stats.cpuStats.percent.toFixed(2) }}%</div>
              </div>
            </el-col>
            <el-col :span="12">
              <div class="stat-item">
                <div class="stat-label">{{ t('containerDetailDrawer.memoryUsage') }}</div>
                <div class="stat-value">{{ stats.memoryStats.percent.toFixed(2) }}%</div>
              </div>
            </el-col>
          </el-row>
          <el-row :gutter="16" style="margin-top: 16px">
            <el-col :span="12">
              <div class="stat-item">
                <div class="stat-label">{{ t('containerDetailDrawer.networkRx') }}</div>
                <div class="stat-value">{{ formatBytes(stats.networkStats.rxBytes) }}</div>
              </div>
            </el-col>
            <el-col :span="12">
              <div class="stat-item">
                <div class="stat-label">{{ t('containerDetailDrawer.networkTx') }}</div>
                <div class="stat-value">{{ formatBytes(stats.networkStats.txBytes) }}</div>
              </div>
            </el-col>
          </el-row>
        </div>
      </el-card>

      <!-- 操作按钮 -->
      <div class="action-section">
        <el-button-group>
          <el-button
            v-if="container.state === 'exited'"
            type="success"
            @click="startContainer"
            :loading="actionLoading"
          >
            {{ t('container.start') }}
          </el-button>
          <el-button
            v-if="container.state === 'running'"
            type="warning"
            @click="stopContainer"
            :loading="actionLoading"
          >
            {{ t('container.stop') }}
          </el-button>
          <el-button
            v-if="container.state === 'running'"
            type="info"
            @click="restartContainer"
            :loading="actionLoading"
          >
            {{ t('container.restart') }}
          </el-button>
        </el-button-group>
        <el-button @click="showLogs = true">{{ t('containerDetailDrawer.viewLogs') }}</el-button>
        <el-button @click="refreshContainer">{{ t('common.refresh') }}</el-button>
      </div>
    </div>

    <!-- 日志对话框 -->
    <ContainerLogsDialog
      v-model="showLogs"
      :container-id="containerId"
    />
  </el-drawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { ArrowRight } from '@element-plus/icons-vue'
import { useContainersStore } from '@/stores/containers'
import { containerApi, type ContainerInfo, type ContainerStats } from '@/api/containers'
import ContainerLogsDialog from './ContainerLogsDialog.vue'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()

interface Props {
  modelValue: boolean
  containerId?: string
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const containersStore = useContainersStore()

// 响应式数据
const loading = ref(false)
const statsLoading = ref(false)
const actionLoading = ref(false)
const container = ref<ContainerInfo | null>(null)
const stats = ref<ContainerStats | null>(null)
const showLogs = ref(false)

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const containerId = computed(() => props.containerId)

// 监听容器ID变化
watch(containerId, (newId) => {
  if (newId && visible.value) {
    loadContainerDetail()
  }
}, { immediate: true })

// 监听对话框打开
watch(visible, (newValue) => {
  if (newValue && containerId.value) {
    loadContainerDetail()
  }
})

// 方法
const loadContainerDetail = async () => {
  if (!containerId.value) return

  loading.value = true
  try {
    const response = await containerApi.getContainer(containerId.value)
    container.value = response
  } catch (error: any) {
    console.error('获取容器详情失败:', error)
    ElMessage.error(error.response?.data?.message || t('containerDetailDrawer.loadDetailFailed'))
  } finally {
    loading.value = false
  }
}

const loadStats = async () => {
  if (!containerId.value) return

  statsLoading.value = true
  try {
    const response = await containerApi.getContainerStats(containerId.value)
    stats.value = response
  } catch (error: any) {
    console.error('获取容器统计信息失败:', error)
    ElMessage.error(t('containerDetailDrawer.loadStatsFailed'))
  } finally {
    statsLoading.value = false
  }
}

const startContainer = async () => {
  if (!containerId.value) return

  actionLoading.value = true
  try {
    await containersStore.startContainer(containerId.value)
    ElMessage.success(t('containerDetailDrawer.startSuccess'))
    await loadContainerDetail()
  } catch (e: any) {
    ElMessage.error(`${t('containerDetailDrawer.startFailed')}: ${e.message || t('common.unknown')}`)
  } finally {
    actionLoading.value = false
  }
}

const stopContainer = async () => {
  if (!containerId.value) return

  actionLoading.value = true
  try {
    await containersStore.stopContainer(containerId.value)
    ElMessage.success(t('containerDetailDrawer.stopSuccess'))
    await loadContainerDetail()
  } catch (e: any) {
    ElMessage.error(`${t('containerDetailDrawer.stopFailed')}: ${e.message || t('common.unknown')}`)
  } finally {
    actionLoading.value = false
  }
}

const restartContainer = async () => {
  if (!containerId.value) return

  actionLoading.value = true
  try {
    await containersStore.restartContainer(containerId.value)
    ElMessage.success(t('containerDetailDrawer.restartSuccess'))
    await loadContainerDetail()
  } catch (e: any) {
    ElMessage.error(`${t('containerDetailDrawer.restartFailed')}: ${e.message || t('common.unknown')}`)
  } finally {
    actionLoading.value = false
  }
}

const refreshContainer = () => {
  loadContainerDetail()
  loadStats()
}

const handleClose = () => {
  visible.value = false
  container.value = null
  stats.value = null
}

const getStatusType = (state: string) => {
  switch (state) {
    case 'running':
      return 'success'
    case 'exited':
      return 'info'
    case 'paused':
      return 'warning'
    case 'restarting':
      return 'warning'
    default:
      return 'danger'
  }
}

const formatDateTime = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

const formatBytes = (bytes: number) => {
  if (bytes === null || bytes === undefined || isNaN(bytes) || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const absBytes = Math.abs(bytes)
  if (absBytes < 1) return absBytes.toFixed(2) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}
</script>

<style scoped>
.loading-container,
.empty-container {
  padding: 40px 20px;
  text-align: center;
}

.container-detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.detail-section {
  margin-bottom: 16px;
}

.empty-section {
  padding: 20px;
  text-align: center;
}

.port-mappings {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.port-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.env-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.env-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-family: 'Consolas', 'Monaco', monospace;
}

.env-key {
  font-weight: 600;
}

.env-value {
  color: var(--text-secondary);
}

.volume-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.volume-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.volume-path {
  font-family: 'Consolas', 'Monaco', monospace;
}

.network-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.network-item {
  margin: 0;
}

.label-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.label-item {
  margin: 0;
}

.stats-info {
  padding: 16px 0;
}

.stat-item {
  text-align: center;
  padding: 16px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
}

.stat-label {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 8px;
}

.stat-value {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-main);
}

.action-section {
  display: flex;
  justify-content: center;
  gap: 16px;
  padding: 20px 0;
  border-top: 1px solid var(--border-color);
  margin-top: 20px;
}

@media (max-width: 768px) {
  .action-section {
    flex-direction: column;
    align-items: stretch;
  }

  .stats-info .el-row {
    flex-direction: column;
  }
}
</style>