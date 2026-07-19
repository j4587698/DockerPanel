<template>
  <el-dialog
    v-model="visible"
    :title="t('volume.backup.dialogTitle')"
    width="600px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="120px"
      label-position="left"
    >
      <!-- 基本信息 -->
      <el-card class="form-section" :header="t('volume.backup.config')">
        <el-form-item :label="t('volume.backup.volumeName')">
          <el-text type="primary">{{ volumeInfo?.name || volumeId }}</el-text>
        </el-form-item>

        <el-form-item :label="t('volume.backup.backupName')" prop="backupName">
          <el-input
            v-model="form.backupName"
            :placeholder="t('volume.backup.backupNamePlaceholder')"
            clearable
          >
            <template #append>
              <el-button @click="generateBackupName">{{ t('volume.backup.autoGenerate') }}</el-button>
            </template>
          </el-input>
          <div class="form-tip">
            {{ t('volume.backup.backupNameHint') }}
          </div>
        </el-form-item>

        <el-form-item :label="t('volume.backup.backupPath')" prop="backupPath">
          <el-input
            v-model="form.backupPath"
            :placeholder="t('volume.backup.backupPathPlaceholder')"
            clearable
          >
            <template #prepend>
              <el-select v-model="pathType" style="width: 100px">
                <el-option :label="t('volume.backup.pathLocal')" value="local" />
                <el-option :label="t('volume.backup.pathNetwork')" value="network" />
              </el-select>
            </template>
          </el-input>
          <div class="form-tip">
            {{ t('volume.backup.backupPathHint') }}
          </div>
        </el-form-item>

        <el-form-item :label="t('volume.backup.compressBackup')">
          <el-switch
            v-model="form.compress"
            :active-text="t('common.enable')"
            :inactive-text="t('common.disable')"
          />
          <div class="form-tip">
            {{ t('volume.backup.compressHint') }}
          </div>
        </el-form-item>

        <el-form-item :label="t('volume.backup.targetNode')" prop="nodeId" v-if="volumeInfo?.scope === 'global'">
          <el-select v-model="form.nodeId" style="width: 100%" :placeholder="t('volume.backup.selectNodePlaceholder')">
            <el-option
              v-for="node in availableNodes"
              :key="node.id"
              :label="node.name"
              :value="node.id"
            />
          </el-select>
          <div class="form-tip">
            {{ t('volume.backup.targetNodeHint') }}
          </div>
        </el-form-item>
      </el-card>

      <!-- 备份选项 -->
      <el-card class="form-section" :header="t('volume.backup.options')">
        <el-form-item :label="t('volume.backup.description')">
          <el-input
            v-model="form.description"
            type="textarea"
            :rows="3"
            :placeholder="t('volume.backup.descriptionPlaceholder')"
            maxlength="500"
            show-word-limit
          />
        </el-form-item>

        <!-- 标签 -->
        <div class="backup-labels">
          <div class="section-header">
            <span>{{ t('volume.backup.backupLabels') }}</span>
            <el-button type="text" @click="addTag" :icon="Plus">{{ t('volume.backup.addLabel') }}</el-button>
          </div>
          <div
            v-for="(tag, index) in formArrays.tags"
            :key="index"
            class="tag-item"
          >
            <el-form-item
              :label="index === 0 ? t('common.key') : ''"
              :prop="`tags.${index}.key`"
            >
              <el-input
                v-model="tag.key"
                :placeholder="t('volume.backup.labelKeyPlaceholder')"
                clearable
              />
            </el-form-item>
            <el-form-item
              :label="index === 0 ? t('common.value') : ''"
              :prop="`tags.${index}.value`"
            >
              <el-input
                v-model="tag.value"
                :placeholder="t('volume.backup.labelValuePlaceholder')"
                clearable
              />
            </el-form-item>
            <el-form-item :label="index === 0 ? t('common.actions') : ''">
              <el-button
                type="danger"
                size="small"
                @click="removeTag(index)"
                :disabled="formArrays.tags.length <= 1"
              >
                {{ t('common.delete') }}
              </el-button>
            </el-form-item>
          </div>
        </div>

        <!-- 预设标签 -->
        <el-divider content-position="left">{{ t('volume.backup.commonLabels') }}</el-divider>
        <div class="tag-presets">
          <el-button
            v-for="preset in tagPresets"
            :key="preset.name"
            size="small"
            @click="applyTagPreset(preset.tags)"
          >
            {{ preset.name }}
          </el-button>
        </div>
      </el-card>

      <!-- 备份预览 -->
      <el-card class="form-section" :header="t('volume.backup.preview')" v-if="form.backupName && form.backupPath">
        <el-descriptions :column="1" border>
          <el-descriptions-item :label="t('volume.backup.backupFileName')">
            {{ getBackupFileName() }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.backup.estimatedSize')">
            <el-text v-if="volumeInfo" type="info">{{ volumeInfo.usage.sizeDisplay }}</el-text>
            <el-text v-else type="warning">{{ t('common.unknown') }}</el-text>
          </el-descriptions-item>
          <el-descriptions-item :label="t('volume.backup.compressedSize')">
            <el-text v-if="form.compress" type="success">
              {{ t('volume.backup.aboutSize', { size: getCompressedSize() }) }}
            </el-text>
            <el-text v-else type="info">{{ t('volume.backup.noCompress') }}</el-text>
          </el-descriptions-item>
        </el-descriptions>
      </el-card>
    </el-form>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="loading">
          {{ t('volume.backup.startBackup') }}
        </el-button>
      </div>
    </template>

    <!-- 备份进度对话框 -->
    <el-dialog
      v-model="showProgress"
      :title="t('volume.backup.progressTitle')"
      width="500px"
      :close-on-click-modal="false"
      :close-on-press-escape="false"
      :show-close="false"
    >
      <div class="backup-progress">
        <el-progress
          :percentage="backupProgress.percentage"
          :status="backupProgress.status"
          :stroke-width="8"
        />
        <div class="progress-info">
          <el-text>{{ backupProgress.message }}</el-text>
          <el-text v-if="backupProgress.duration" type="info">
            {{ t('volume.backup.duration', { time: backupProgress.duration }) }}
          </el-text>
        </div>
        <div v-if="backupProgress.details" class="progress-details">
          <el-text type="info" size="small">{{ backupProgress.details }}</el-text>
        </div>
      </div>
      <template #footer>
        <div class="progress-footer">
          <el-button
            v-if="backupProgress.status === 'success'"
            type="primary"
            @click="handleProgressComplete"
          >
            {{ t('common.confirm') }}
          </el-button>
          <el-button
            v-else-if="backupProgress.status === 'exception'"
            type="danger"
            @click="handleProgressComplete"
          >
            {{ t('common.close') }}
          </el-button>
          <el-button
            v-else
            @click="cancelBackup"
          >
            {{ t('volume.backup.cancelBackup') }}
          </el-button>
        </div>
      </template>
    </el-dialog>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage, type FormInstance } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import { volumeApi } from '@/api/volumes'
import type { VolumeBackupRequest, VolumeInfo } from '@/api/volume'
import { useNodesStore } from '@/stores/nodes'
import type { NodeInfo } from '@/api/nodes'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface Props {
  modelValue: boolean
  volumeId: string
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'success'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const nodesStore = useNodesStore()

// 响应式数据
const formRef = ref<FormInstance>()
const loading = ref(false)
const showProgress = ref(false)
const pathType = ref('local')
const volumeInfo = ref<VolumeInfo | null>(null)
const availableNodes = ref<NodeInfo[]>([])

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

// 表单数据
const form = reactive<VolumeBackupRequest>({
  volumeId: props.volumeId,
  backupName: '',
  backupPath: '/backups/volumes',
  compress: true,
  tags: {},
  description: '',
  nodeId: ''
})

// 为了方便绑定，使用数组形式管理标签
const formArrays = reactive({
  tags: [{ key: '', value: '' }]
})

// 备份进度
const backupProgress = reactive({
  percentage: 0,
  status: 'success' as 'success' | 'exception' | 'warning',
  message: t('volume.backup.progress.preparing'),
  duration: '',
  details: ''
})

// 标签预设
const tagPresets = computed(() => [
  {
    name: t('volume.backup.presetDaily'),
    tags: {
      type: 'daily',
      environment: 'production'
    }
  },
  {
    name: t('volume.backup.presetMigration'),
    tags: {
      type: 'migration',
      purpose: 'node-migration'
    }
  },
  {
    name: t('volume.backup.presetPreUpgrade'),
    tags: {
      type: 'pre-upgrade',
      purpose: 'safety-backup'
    }
  }
])

// 表单验证规则
const rules = computed(() => ({
  backupName: [
    { required: true, message: t('volume.backup.validation.backupNameRequired'), trigger: 'blur' },
    { min: 1, max: 100, message: t('volume.backup.validation.backupNameLength'), trigger: 'blur' }
  ],
  backupPath: [
    { required: true, message: t('volume.backup.validation.backupPathRequired'), trigger: 'blur' }
  ],
  nodeId: [
    {
      validator: (rule: any, value: string, callback: Function) => {
        if (volumeInfo.value?.scope === 'global' && !value) {
          callback(new Error(t('volume.backup.validation.globalVolumeNodeRequired')))
        } else {
          callback()
        }
      },
      trigger: 'change'
    }
  ]
}))

// 监听表单数组变化
watch(formArrays.tags, (tags) => {
  form.tags = {}
  tags.forEach(tag => {
    if (tag.key && tag.value) {
      form.tags[tag.key] = tag.value
    }
  })
}, { deep: true })

// 监听路径类型变化
watch(pathType, (newType) => {
  if (newType === 'local') {
    form.backupPath = '/backups/volumes'
  } else {
    form.backupPath = 'nfs://backup-server/volumes'
  }
})

// 监听对话框打开
watch(visible, async (newValue) => {
  if (newValue) {
    await loadVolumeInfo()
    await loadAvailableNodes()
    generateBackupName()
  }
})

// 方法
const loadVolumeInfo = async () => {
  try {
    const response = await volumeApi.getVolume(props.volumeId)
    volumeInfo.value = ((response as any).data ?? response) as VolumeInfo
  } catch (error) {
    console.error(t('volume.backup.loadVolumeInfoFailed'), error)
  }
}

const loadAvailableNodes = async () => {
  try {
    availableNodes.value = await nodesStore.fetchNodes()
  } catch (error) {
    console.error(t('volume.backup.loadNodesFailed'), error)
  }
}

const generateBackupName = () => {
  const volumeName = volumeInfo.value?.name || 'volume'
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19)
  form.backupName = `${volumeName}_${timestamp}`
}

const getBackupFileName = () => {
  const extension = form.compress ? '.tar.gz' : '.tar'
  return `${form.backupName}${extension}`
}

const getCompressedSize = () => {
  if (!volumeInfo.value) return t('common.unknown')
  const originalSize = volumeInfo.value.usage.size
  const compressedSize = Math.floor(originalSize * 0.6)
  return formatBytes(compressedSize)
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

const addTag = () => {
  formArrays.tags.push({ key: '', value: '' })
}

const removeTag = (index: number) => {
  formArrays.tags.splice(index, 1)
}

const applyTagPreset = (presetTags: Record<string, string>) => {
  formArrays.tags = Object.entries(presetTags).map(([key, value]) => ({
    key,
    value
  }))
}

const handleSubmit = async () => {
  if (!formRef.value) return

  try {
    await formRef.value.validate()
    loading.value = true
    const startTime = Date.now()

    showProgress.value = true
    backupProgress.percentage = 15
    backupProgress.status = 'warning'
    backupProgress.message = t('volume.backup.progress.starting')
    backupProgress.details = ''

    const payload = {
      volumeName: volumeInfo.value?.name || props.volumeId,
      volumeId: props.volumeId,
      backupLocation: form.backupPath,
      nodeId: form.nodeId || undefined,
      compress: form.compress,
      metadata: {
        backupName: form.backupName,
        description: form.description || '',
        tags: form.tags
      }
    }

    const response = await volumeApi.backupVolume(props.volumeId, payload)
    const result = ((response as any).data ?? response) as any

    if (result.success) {
      backupProgress.percentage = 100
      backupProgress.status = 'success'
      backupProgress.message = t('volume.backup.progress.complete')
      backupProgress.duration = `${Math.max(1, Math.round((Date.now() - startTime) / 1000))}s`
      backupProgress.details = t('volume.backup.progress.backupFile', { path: result.backupPath || result.backupId })

      ElMessage.success(t('volume.backup.success'))
      emit('success')
    } else {
      throw new Error(result.errorMessage || t('volume.backup.failed'))
    }
  } catch (error: any) {
    console.error(t('volume.backup.failedLog'), error)
    backupProgress.status = 'exception'
    backupProgress.message = t('volume.backup.progress.failed')
    backupProgress.details = error.response?.data?.message || error.message
    ElMessage.error(error.response?.data?.message || t('volume.backup.failed'))
  } finally {
    loading.value = false
  }
}

const cancelBackup = () => {
  showProgress.value = false
  ElMessage.info(t('volume.backup.cancelled'))
}

const handleProgressComplete = () => {
  showProgress.value = false
  if (backupProgress.status === 'success') {
    handleClose()
  }
}

const handleClose = () => {
  visible.value = false
  resetForm()
}

const resetForm = () => {
  if (formRef.value) {
    formRef.value.resetFields()
  }

  Object.assign(form, {
    volumeId: props.volumeId,
    backupName: '',
    backupPath: '/backups/volumes',
    compress: true,
    tags: {},
    description: '',
    nodeId: ''
  })

  Object.assign(formArrays, {
    tags: [{ key: '', value: '' }]
  })

  Object.assign(backupProgress, {
    percentage: 0,
    status: 'success',
    message: t('volume.backup.progress.preparing'),
    duration: '',
    details: ''
  })

  pathType.value = 'local'
  showProgress.value = false
}
</script>

<style scoped>
.form-section {
  margin-bottom: 16px;
}

.form-tip {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
  line-height: 1.4;
}

.backup-labels {
  margin-bottom: 16px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  font-weight: 600;
  color: #303133;
}

.tag-item {
  display: grid;
  grid-template-columns: 1fr 1fr 80px;
  gap: 12px;
  align-items: center;
  margin-bottom: 12px;
}

.tag-presets {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.dialog-footer {
  text-align: right;
}

.backup-progress {
  padding: 20px 0;
}

.progress-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 16px;
}

.progress-details {
  margin-top: 12px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 4px;
}

.progress-footer {
  text-align: right;
}

@media (max-width: 768px) {
  .tag-item {
    grid-template-columns: 1fr;
    gap: 8px;
  }

  .progress-info {
    flex-direction: column;
    align-items: flex-start;
    gap: 4px;
  }
}
</style>