<template>
  <el-dialog
    v-model="isDialogVisible"
    :title="t('volume.duplicateVolume.dialogTitle')"
    width="600px"
    :before-close="handleClose"
  >
    <el-form
      :model="form"
      :rules="formRules"
      ref="formRef"
      label-width="120px"
    >
      <el-form-item :label="t('volume.duplicateVolume.sourceVolumeName')" prop="sourceName">
        <el-input
          v-model="form.sourceName"
          :placeholder="t('volume.duplicateVolume.sourceVolumeName')"
          :disabled="true"
        />
      </el-form-item>

      <el-form-item :label="t('volume.duplicateVolume.targetVolumeName')" prop="targetName">
        <el-input
          v-model="form.targetName"
          :placeholder="t('volume.duplicateVolume.targetVolumeNamePlaceholder')"
          :disabled="loading"
        />
      </el-form-item>

      <el-form-item :label="t('volume.duplicateVolume.targetNode')" prop="targetNodeId">
        <el-select
          v-model="form.targetNodeId"
          :placeholder="t('volume.duplicateVolume.selectTargetNode')"
          :disabled="loading"
        >
          <el-option
            v-for="node in nodes"
            :key="node.id"
            :label="node.name"
            :value="node.id"
          />
        </el-select>
      </el-form-item>

      <el-form-item :label="t('volume.duplicateVolume.copyOptions')">
        <el-checkbox-group v-model="form.copyOptions" :disabled="loading">
          <el-checkbox label="data">{{ t('volume.duplicateVolume.copyData') }}</el-checkbox>
          <el-checkbox label="metadata">{{ t('volume.duplicateVolume.copyMetadata') }}</el-checkbox>
          <el-checkbox label="permissions">{{ t('volume.duplicateVolume.copyPermissions') }}</el-checkbox>
          <el-checkbox label="timestamps">{{ t('volume.duplicateVolume.copyTimestamps') }}</el-checkbox>
        </el-checkbox-group>
      </el-form-item>

      <el-form-item :label="t('volume.duplicateVolume.compressOption')">
        <el-checkbox v-model="form.compress" :disabled="loading">
          {{ t('volume.duplicateVolume.enableCompression') }}
        </el-checkbox>
      </el-form-item>

      <el-form-item :label="t('volume.duplicateVolume.verifyOption')">
        <el-checkbox v-model="form.verify" :disabled="loading">
          {{ t('volume.duplicateVolume.verifyIntegrity') }}
        </el-checkbox>
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="handleCancel" :disabled="loading">
        {{ t('common.cancel') }}
      </el-button>
      <el-button
        type="primary"
        @click="handleConfirm"
        :loading="loading"
        :disabled="!canDuplicate"
      >
        {{ t('volume.duplicateVolume.startDuplicate') }}
      </el-button>
    </template>
  </el-dialog>

  <!-- 复制进度 -->
  <el-dialog
    v-model="showProgress"
    :title="t('volume.duplicateVolume.progressTitle')"
    width="500px"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
    :show-close="false"
  >
    <div class="duplicate-progress">
      <el-progress
        :percentage="progressPercentage"
        :status="duplicateStatus"
      />
      <div class="progress-info">
        <p>{{ progressMessage }}</p>
        <div v-if="duplicateResults" class="duplicate-results">
          <el-descriptions :column="1" border size="small">
            <el-descriptions-item :label="t('volume.duplicateVolume.resultTargetVolume')">
              {{ duplicateResults.targetVolume }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('volume.duplicateVolume.resultCopiedSize')">
              {{ duplicateResults.copiedSize }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('volume.duplicateVolume.resultDuration')">
              {{ duplicateResults.duration }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('volume.duplicateVolume.resultStatus')">
              <el-tag :type="duplicateResults.success ? 'success' : 'danger'">
                {{ duplicateResults.success ? t('volume.duplicateVolume.resultSuccess') : t('volume.duplicateVolume.resultFailed') }}
              </el-tag>
            </el-descriptions-item>
            <el-descriptions-item :label="t('volume.duplicateVolume.resultError')" v-if="duplicateResults.error">
              <el-text type="danger">{{ duplicateResults.error }}</el-text>
            </el-descriptions-item>
          </el-descriptions>
        </div>
      </div>
    </div>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { volumeApi } from '@/api/volumes'
import { nodeApi, type NodeInfo } from '@/api/nodes'

const { t } = useI18n()

interface DuplicateForm {
  sourceName: string
  targetName: string
  targetNodeId: string
  copyOptions: string[]
  compress: boolean
  verify: boolean
}

interface DuplicateResults {
  targetVolume: string
  copiedSize: string
  duration: string
  success: boolean
  error?: string
}

interface VolumeLike {
  id?: string
  name: string
  nodeId?: string
}

const props = defineProps<{
  visible: boolean
  volume?: VolumeLike
}>()

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'success'): void
}>()

// 表单数据
const form = ref<DuplicateForm>({
  sourceName: '',
  targetName: '',
  targetNodeId: '',
  copyOptions: ['data', 'metadata'],
  compress: true,
  verify: true
})

// 表单验证规则
const formRules = {
  targetName: [
    { required: true, message: t('volume.duplicateVolume.validationTargetNameRequired'), trigger: 'blur' }
  ],
  targetNodeId: [
    { required: true, message: t('volume.duplicateVolume.validationTargetNodeRequired'), trigger: 'change' }
  ]
}

// 节点列表
const nodes = ref<NodeInfo[]>([])

// 状态
const loading = ref(false)
const formRef = ref()
const showProgress = ref(false)
const progressPercentage = ref(0)
const progressMessage = ref('')
const duplicateStatus = ref<'success' | 'exception' | 'warning'>('success')
const duplicateResults = ref<DuplicateResults | null>(null)

// 计算属性
const isDialogVisible = computed({
  get: () => props.visible,
  set: (value: boolean) => emit('update:visible', value)
})

const canDuplicate = computed(() => {
  return form.value.targetName &&
         form.value.targetNodeId &&
         form.value.copyOptions.length > 0
})

// 监听props变化
watch(() => props.volume, (newVolume) => {
  if (newVolume) {
    form.value.sourceName = newVolume.name
    // 生成默认目标卷名称
    form.value.targetName = `${newVolume.name}-copy-${Date.now()}`
  }
}, { immediate: true })

// 加载节点列表
const loadNodes = async () => {
  try {
    const response = await nodeApi.getNodes()
    const list = (((response as any).data ?? response) as NodeInfo[]).filter(node =>
      !node.status || ['active', 'online', 'healthy', 'connected'].includes(node.status.toLowerCase()) || node.isOnline === true
    )
    nodes.value = list.length > 0 ? list : (((response as any).data ?? response) as NodeInfo[])
    if (nodes.value.length > 0 && !form.value.targetNodeId) {
      form.value.targetNodeId = nodes.value[0].id
    }
  } catch (error) {
    ElMessage.error(t('volume.duplicateVolume.loadNodesFailed'))
  }
}

// 取消操作
const handleCancel = () => {
  isDialogVisible.value = false
}

// 确认复制
const handleConfirm = async () => {
  try {
    await formRef.value.validate()

    await ElMessageBox.confirm(
      t('volume.duplicateVolume.confirmDuplicateMessage', { source: form.value.sourceName, target: form.value.targetName }),
      t('volume.duplicateVolume.confirmDuplicateTitle'),
      {
        confirmButtonText: t('volume.duplicateVolume.startDuplicate'),
        cancelButtonText: t('common.cancel'),
        type: 'warning'
      }
    )

    startDuplicate()
  } catch (error) {
    // 用户取消操作
  }
}

// 开始复制
const startDuplicate = async () => {
  loading.value = true
  showProgress.value = true
  progressPercentage.value = 0
  progressMessage.value = t('volume.duplicateVolume.progressPreparing')
  duplicateStatus.value = 'success'
  duplicateResults.value = null

  const startTime = Date.now()

  try {
    if (!props.volume?.name) {
      throw new Error(t('volume.duplicateVolume.sourceVolumeName'))
    }

    progressPercentage.value = 15
    progressMessage.value = t('volume.duplicateVolume.progressPreparingTransfer')

    const backupResponse = await volumeApi.backupVolume(props.volume.name, {
      volumeName: props.volume.name,
      volumeId: props.volume.id || props.volume.name,
      nodeId: props.volume.nodeId,
      compress: form.value.compress,
      metadata: {
        operation: 'duplicate-volume',
        targetVolumeName: form.value.targetName,
        targetNodeId: form.value.targetNodeId,
        copyOptions: form.value.copyOptions,
        verify: form.value.verify
      }
    })
    const backupResult = ((backupResponse as any).data ?? backupResponse) as any
    if (!backupResult.success || !backupResult.backupId) {
      throw new Error(backupResult.errorMessage || t('volume.duplicateVolume.duplicateFailed'))
    }

    progressPercentage.value = 60
    progressMessage.value = t('volume.duplicateVolume.progressCopyingData')

    const restoreResponse = await volumeApi.restoreVolume({
      backupId: backupResult.backupId,
      targetVolumeName: form.value.targetName,
      overwriteExisting: false
    })
    const restoreResult = ((restoreResponse as any).data ?? restoreResponse) as any
    if (!restoreResult.success) {
      throw new Error(restoreResult.errorMessage || t('volume.duplicateVolume.duplicateFailed'))
    }

    const endTime = Date.now()
    const duration = Math.round((endTime - startTime) / 1000)

    duplicateResults.value = {
      targetVolume: restoreResult.restoredVolumeName || form.value.targetName,
      copiedSize: formatBytes(restoreResult.restoredSize || backupResult.backupSize || 0),
      duration: `${duration} ${t('common.seconds')}`,
      success: true
    }

    progressPercentage.value = 100
    progressMessage.value = t('volume.duplicateVolume.progressComplete')
    duplicateStatus.value = 'success'

    ElMessage.success(t('volume.duplicateVolume.duplicateSuccess'))
    setTimeout(() => {
      showProgress.value = false
      isDialogVisible.value = false
    }, 2000)

    emit('success')
  } catch (error: any) {
    const endTime = Date.now()
    const duration = Math.round((endTime - startTime) / 1000)

    progressPercentage.value = 0
    progressMessage.value = t('volume.duplicateVolume.progressFailed', { error: error.message || t('common.unknown') })
    duplicateStatus.value = 'exception'
    duplicateResults.value = {
      targetVolume: form.value.targetName,
      copiedSize: '0 MB',
      duration: `${duration} ${t('common.seconds')}`,
      success: false,
      error: error.message || t('common.unknown')
    }

    ElMessage.error(t('volume.duplicateVolume.duplicateFailed'))
  } finally {
    loading.value = false
  }
}

const formatBytes = (bytes: number) => {
  if (!bytes || Number.isNaN(bytes)) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  return `${(bytes / Math.pow(1024, index)).toFixed(index === 0 ? 0 : 2)} ${units[index]}`
}

// 关闭对话框
const handleClose = () => {
  if (loading.value) {
    return false
  }
  isDialogVisible.value = false
}

// 初始化
onMounted(() => {
  loadNodes()
})
</script>

<style scoped>
.duplicate-progress {
  padding: 20px 0;
}

.progress-info {
  margin-top: 16px;
  text-align: center;
}

.progress-info p {
  margin-bottom: 12px;
  color: #606266;
}

.duplicate-results {
  margin-top: 16px;
  text-align: left;
}
</style>