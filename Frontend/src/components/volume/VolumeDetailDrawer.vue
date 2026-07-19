<template>
  <el-drawer
    v-model="visible"
    :title="t('volume.detail.title', { name: volume?.name || '' })"
    direction="rtl"
    size="800px"
    @close="handleClose"
  >
    <div v-if="loading" class="loading-container">
      <el-skeleton :rows="10" animated />
    </div>

    <div v-else-if="!volume" class="empty-container">
      <el-empty :description="t('volume.detail.notFound')" />
    </div>

    <div v-else class="volume-detail">
      <!-- 基本信息 -->
      <el-card class="detail-section" :header="t('volume.detail.basicInfo')">
        <el-descriptions :column="2" border>
          <el-descriptions-item :label="t('volume.detail.volumeId')">
            <el-text type="info" copyable>{{ volume.id }}</el-text>
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.name')">
            {{ volume.name }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.driverType')">
            <el-tag type="primary">{{ volume.driver }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.scope')">
            <el-tag :type="volume.scope === 'local' ? 'info' : 'success'">
              {{ volume.scope === 'local' ? t('volume.localScope') : t('volume.globalScope') }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item :label="t('common.status')">
            <el-tag :type="getStatusType(volume.status)">{{ getStatusText(volume.status) }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item :label="t('common.created')">
            {{ formatDateTime(volume.createdAt) }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.node')">
            {{ volume.nodeName || 'N/A' }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.containerCount')">
            <el-tag type="warning">{{ volume.containersCount }}</el-tag>
          </el-descriptions-item>
        </el-descriptions>
      </el-card>

      <!-- 使用情况 -->
      <el-card class="detail-section" :header="t('volume.detail.usage')">
        <el-descriptions :column="2" border>
          <el-descriptions-item :label="t('common.size')">
            {{ volume.usage.sizeDisplay }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.refCount')">
            <el-tag type="info">{{ volume.usage.refCount }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.lastUsedAt')" v-if="volume.usage.lastUsedAt">
            {{ formatDateTime(volume.usage.lastUsedAt) }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.usageRate')" v-if="usageInfo">
            <div class="usage-progress">
              <el-progress
                :percentage="usageInfo.percentage"
                :status="usageInfo.percentage > 80 ? 'warning' : 'success'"
                :show-text="true"
              />
            </div>
          </el-descriptions-item>
        </el-descriptions>
      </el-card>

      <!-- 挂载信息 -->
      <el-card class="detail-section" :header="t('volume.detail.mountInfo')" v-if="volume.mountInfo">
        <el-descriptions :column="1" border>
          <el-descriptions-item :label="t('volume.detail.mountPoint')">
            <el-text type="primary" copyable>{{ volume.mountInfo.mountPoint }}</el-text>
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.sourcePath')">
            <el-text type="info" copyable>{{ volume.mountInfo.source }}</el-text>
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.destPath')">
            <el-text type="info" copyable>{{ volume.mountInfo.destination }}</el-text>
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.mountMode')">
            <el-tag>{{ volume.mountInfo.mode }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.writable')">
            <el-tag :type="volume.mountInfo.isWritable ? 'success' : 'warning'">
              {{ volume.mountInfo.isWritable ? t('volume.detail.readWrite') : t('volume.detail.readOnly') }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.propagationMode')">
            {{ volume.mountInfo.propagation }}
          </el-descriptions-item>
        </el-descriptions>
      </el-card>

      <!-- 关联容器 -->
      <el-card class="detail-section" :header="t('volume.detail.relatedContainers')">
        <div v-if="!volume.containers || volume.containers.length === 0" class="empty-section">
          <el-text type="info">{{ t('volume.detail.noContainers') }}</el-text>
        </div>
        <div v-else class="container-list">
          <div
            v-for="container in volume.containers"
            :key="container.id"
            class="container-item"
          >
            <div class="container-info">
              <el-text type="primary" class="container-name">{{ container.name }}</el-text>
              <el-text type="info" size="small" copyable>{{ container.id }}</el-text>
            </div>
            <div class="mount-info">
              <el-tag type="info">{{ container.mountPath }}</el-tag>
              <el-tag :type="container.isReadWrite ? 'success' : 'warning'">
                {{ container.isReadWrite ? t('volume.detail.readWrite') : t('volume.detail.readOnly') }}
              </el-tag>
            </div>
          </div>
        </div>
      </el-card>

      <!-- 配置信息 -->
      <el-card class="detail-section" :header="t('volume.detail.configInfo')">
        <!-- 选项 -->
        <div class="config-subsection" v-if="volume.config.options && Object.keys(volume.config.options).length > 0">
          <h4>{{ t('volume.detail.driverOptions') }}</h4>
          <div class="options-list">
            <div
              v-for="(value, key) in volume.config.options"
              :key="key"
              class="option-item"
            >
              <el-text type="primary" class="option-key">{{ key }}</el-text>
              <el-text>=</el-text>
              <el-text class="option-value">{{ value }}</el-text>
            </div>
          </div>
        </div>

        <!-- 标签 -->
        <div class="config-subsection" v-if="volume.config.labels && Object.keys(volume.config.labels).length > 0">
          <h4>{{ t('volume.labels') }}</h4>
          <div class="label-list">
            <el-tag
              v-for="(value, key) in volume.config.labels"
              :key="key"
              type="info"
              class="label-item"
            >
              {{ key }}: {{ value }}
            </el-tag>
          </div>
        </div>

        <!-- 驱动特定选项 -->
        <div class="config-subsection" v-if="volume.config.driverOptions && Object.keys(volume.config.driverOptions).length > 0">
          <h4>{{ t('volume.detail.driverSpecificOptions') }}</h4>
          <div class="options-list">
            <div
              v-for="(value, key) in volume.config.driverOptions"
              :key="key"
              class="option-item"
            >
              <el-text type="primary" class="option-key">{{ key }}</el-text>
              <el-text>=</el-text>
              <el-text class="option-value">{{ value }}</el-text>
            </div>
          </div>
        </div>

        <!-- 如果没有任何配置 -->
        <div v-if="!hasConfig" class="empty-section">
          <el-text type="info">{{ t('volume.detail.noSpecialConfig') }}</el-text>
        </div>
      </el-card>

      <!-- 驱动信息 -->
      <el-card class="detail-section" :header="t('volume.detail.driverInfo')" v-if="volume.driverInfo">
        <el-descriptions :column="2" border>
          <el-descriptions-item :label="t('volume.detail.driverName')">
            {{ volume.driverInfo.name }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.detail.driverVersion')">
            <el-tag type="success">{{ volume.driverInfo.version }}</el-tag>
          </el-descriptions-item>
        </el-descriptions>

        <!-- 驱动能力 -->
        <div v-if="volume.driverInfo.capabilities" class="driver-capabilities">
          <h4>{{ t('volume.detail.driverCapabilities') }}</h4>
          <div class="capabilities-list">
            <div
              v-for="(capability, key) in volume.driverInfo.capabilities"
              :key="key"
              class="capability-item"
            >
              <el-text type="primary">{{ key }}:</el-text>
              <pre class="capability-value">{{ JSON.stringify(capability, null, 2) }}</pre>
            </div>
          </div>
        </div>

        <!-- 驱动选项 -->
        <div v-if="volume.driverInfo.options && Object.keys(volume.driverInfo.options).length > 0" class="driver-options">
          <h4>{{ t('volume.detail.supportedOptions') }}</h4>
          <div class="options-list">
            <div
              v-for="(value, key) in volume.driverInfo.options"
              :key="key"
              class="option-item"
            >
              <el-text type="primary" class="option-key">{{ key }}</el-text>
              <el-text>=</el-text>
              <el-text class="option-value">{{ value }}</el-text>
            </div>
          </div>
        </div>
      </el-card>

      <!-- 操作按钮 -->
      <div class="action-section">
        <el-button-group>
          <el-button
            type="primary"
            @click="showBackupDialog = true"
            :disabled="volume.status !== 'in-use'"
           :icon="FolderOpened">{{ t('volume.backupVolume') }}</el-button>
          <el-button
            type="warning"
            @click="loadUsageInfo"
            :loading="usageLoading"
           :icon="Refresh">{{ t('volume.detail.refreshUsage') }}</el-button>
        </el-button-group>
        <el-button @click="refreshVolume">{{ t('volume.detail.refreshDetail') }}</el-button>
        <el-button
          type="danger"
          @click="confirmDelete"
          :disabled="volume.containersCount > 0"
        >
          {{ t('volume.removeVolume') }}
        </el-button>
      </div>
    </div>

    <!-- 备份对话框 -->
    <VolumeBackupDialog
      v-model="showBackupDialog"
      :volume-id="volumeId"
      @success="handleBackupSuccess"
    />
  </el-drawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh, FolderOpened } from '@element-plus/icons-vue'
import { volumeApi, type VolumeDetailInfo, type VolumeUsageInfo } from '@/api/volume'
import VolumeBackupDialog from './VolumeBackupDialog.vue'
import { useI18n } from 'vue-i18n'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()

interface Props {
  modelValue: boolean
  volumeId?: string
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'refresh'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// 响应式数据
const loading = ref(false)
const usageLoading = ref(false)
const volume = ref<VolumeDetailInfo | null>(null)
const usageInfo = ref<VolumeUsageInfo | null>(null)
const showBackupDialog = ref(false)

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const volumeId = computed(() => props.volumeId)

// 计算属性
const hasConfig = computed(() => {
  if (!volume.value?.config) return false
  const { options, labels, driverOptions } = volume.value.config
  return (
    (options && Object.keys(options).length > 0) ||
    (labels && Object.keys(labels).length > 0) ||
    (driverOptions && Object.keys(driverOptions).length > 0)
  )
})

// 监听卷ID变化
watch(volumeId, (newId) => {
  if (newId && visible.value) {
    loadVolumeDetail()
  }
}, { immediate: true })

// 监听对话框打开
watch(visible, (newValue) => {
  if (newValue && volumeId.value) {
    loadVolumeDetail()
  }
})

// 方法
const loadVolumeDetail = async () => {
  if (!volumeId.value) return

  loading.value = true
  try {
    const response = await volumeApi.getVolume(volumeId.value)
    volume.value = ((response as any).data ?? response) as VolumeDetailInfo
  } catch (error: any) {
    console.error(t('volume.detail.loadFailed'), error)
    ElMessage.error(error.response?.data?.message || t('volume.detail.loadFailed'))
  } finally {
    loading.value = false
  }
}

const loadUsageInfo = async () => {
  if (!volumeId.value) return

  usageLoading.value = true
  try {
    const response = await volumeApi.getVolumeUsage(volumeId.value)
    usageInfo.value = ((response as any).data ?? response) as VolumeUsageInfo
  } catch (error: any) {
    console.error(t('volume.detail.loadUsageFailed'), error)
    ElMessage.error(t('volume.detail.loadUsageFailed'))
  } finally {
    usageLoading.value = false
  }
}

const refreshVolume = () => {
  loadVolumeDetail()
  loadUsageInfo()
}

const confirmDelete = async () => {
  if (!volume.value) return

  try {
    await ElMessageBox.confirm(
      t('volume.detail.deleteConfirm', { name: volume.value.name }),
      t('common.deleteConfirm'),
      {
        confirmButtonText: t('common.confirm'),
        cancelButtonText: t('common.cancel'),
        type: 'warning'
      }
    )

    await volumeApi.deleteVolume(volume.value.id)
    ElMessage.success(t('volume.detail.deleteSuccess'))
    emit('refresh')
    handleClose()
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error(t('volume.detail.deleteFailed'), error)
      ElMessage.error(error.response?.data?.message || t('volume.detail.deleteFailed'))
    }
  }
}

const handleBackupSuccess = () => {
  ElMessage.success(t('volume.backup.success'))
  loadVolumeDetail()
}

const handleClose = () => {
  visible.value = false
  volume.value = null
  usageInfo.value = null
}

const getStatusType = (status: string) => {
  switch (status) {
    case 'in-use':
      return 'success'
    case 'created':
      return 'info'
    case 'error':
      return 'danger'
    default:
      return 'warning'
  }
}

const getStatusText = (status: string) => {
  switch (status) {
    case 'in-use':
      return t('volume.detail.statusInUse')
    case 'created':
      return t('volume.detail.statusCreated')
    case 'error':
      return t('volume.detail.statusError')
    default:
      return status
  }
}

const formatDateTime = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}
</script>

<style scoped>
.loading-container,
.empty-container {
  padding: 40px 20px;
  text-align: center;
}

.volume-detail {
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

.usage-progress {
  width: 100%;
}

.container-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.container-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
}

.container-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.container-name {
  font-weight: 600;
}

.mount-info {
  display: flex;
  gap: 8px;
}

.config-subsection {
  margin-bottom: 20px;
}

.config-subsection h4 {
  margin: 0 0 12px 0;
  color: var(--text-main);
  font-size: 14px;
  font-weight: 600;
}

.options-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.option-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-family: 'Consolas', 'Monaco', monospace;
  padding: 8px;
  background: var(--bg-subtle);
  border-radius: 4px;
}

.option-key {
  font-weight: 600;
}

.option-value {
  color: var(--text-secondary);
}

.label-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.label-item {
  margin: 0;
}

.driver-capabilities,
.driver-options {
  margin-top: 16px;
}

.driver-capabilities h4,
.driver-options h4 {
  margin: 0 0 12px 0;
  color: var(--text-main);
  font-size: 14px;
  font-weight: 600;
}

.capabilities-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.capability-item {
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 12px;
}

.capability-value {
  margin: 8px 0 0 0;
  padding: 8px;
  background: var(--bg-subtle);
  border-radius: 4px;
  font-size: 12px;
  overflow-x: auto;
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

  .container-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 8px;
  }

  .mount-info {
    align-self: stretch;
    justify-content: flex-start;
  }
}
</style>