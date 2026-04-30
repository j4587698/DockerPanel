<template>
  <el-dialog
    v-model="isDialogVisible"
    :title="t('volume.cleanupVolumes.dialogTitle')"
    width="600px"
    :before-close="handleClose"
  >
    <el-form
      :model="form"
      label-width="120px"
    >
      <el-form-item :label="t('volume.cleanupVolumes.cleanupType')">
        <el-radio-group v-model="form.cleanupType" :disabled="loading">
          <el-radio value="unused">{{ t('volume.cleanupVolumes.unusedVolumes') }}</el-radio>
          <el-radio value="dangling">{{ t('volume.cleanupVolumes.danglingVolumes') }}</el-radio>
        </el-radio-group>
      </el-form-item>

      <el-form-item :label="t('volume.cleanupVolumes.forceDelete')">
        <el-switch
          v-model="form.force"
          :active-text="t('volume.cleanupVolumes.forceEnable')"
          :inactive-text="t('volume.cleanupVolumes.forceDisable')"
          :disabled="loading"
        />
      </el-form-item>

      <el-form-item :label="t('common.tips')">
        <el-alert
          :title="t('volume.cleanupVolumes.warningTitle')"
          type="warning"
          :description="cleanupWarning"
          show-icon
          :closable="false"
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="handleCancel" :disabled="loading">
        {{ t('common.cancel') }}
      </el-button>
      <el-button
        type="danger"
        @click="handleConfirm"
        :loading="loading"
      >
        {{ t('volume.cleanupVolumes.startCleanup') }}
      </el-button>
    </template>
  </el-dialog>

  <!-- 清理进度 -->
  <el-dialog
    v-model="showProgress"
    :title="t('volume.cleanupVolumes.progressTitle')"
    width="500px"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
    :show-close="false"
  >
    <div class="cleanup-progress">
      <el-progress
        :percentage="progressPercentage"
        :status="cleanupStatus"
      />
      <div class="progress-info">
        <p>{{ progressMessage }}</p>
        <div v-if="cleanupResults" class="cleanup-results">
          <el-descriptions :column="1" border size="small">
            <el-descriptions-item :label="t('volume.cleanupVolumes.resultDeleted')">
              {{ cleanupResults.deleted }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('volume.cleanupVolumes.resultSpaceReclaimed')">
              {{ cleanupResults.spaceReclaimed }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('volume.cleanupVolumes.resultError')" v-if="cleanupResults.error">
              <el-text type="danger">{{ cleanupResults.error }}</el-text>
            </el-descriptions-item>
          </el-descriptions>
        </div>
      </div>
    </div>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { volumeApi } from '@/api/volumes'

const { t } = useI18n()

interface CleanupForm {
  cleanupType: 'unused' | 'dangling' | 'all'
  nodeId: string
  force: boolean
}

interface CleanupResults {
  deleted: number
  spaceReclaimed: string
  error?: string
}

interface Node {
  id: string
  name: string
  status: string
}

const props = defineProps<{
  modelValue: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'success'): void
}>()

// 表单数据
const form = ref<CleanupForm>({
  cleanupType: 'unused',
  nodeId: 'local',
  force: false
})

// 状态
const loading = ref(false)
const showProgress = ref(false)
const progressPercentage = ref(0)
const progressMessage = ref('')
const cleanupStatus = ref<'success' | 'exception' | 'warning'>('success')
const cleanupResults = ref<CleanupResults | null>(null)

// 计算属性
const isDialogVisible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value)
})

const cleanupWarning = computed(() => {
  const warningMsg = {
    'unused': t('volume.cleanupVolumes.warningMessageUnused'),
    'dangling': t('volume.cleanupVolumes.warningMessageDangling'),
    'all': t('volume.cleanupVolumes.warningMessageAll')
  }[form.value.cleanupType]

  return warningMsg
})

// 取消操作
const handleCancel = () => {
  isDialogVisible.value = false
}

// 确认清理
const handleConfirm = async () => {
  try {
    const typeText = {
      'unused': t('volume.cleanupVolumes.unusedVolumes'),
      'dangling': t('volume.cleanupVolumes.danglingVolumes'),
      'all': t('volume.cleanupVolumes.allVolumes')
    }[form.value.cleanupType]

    await ElMessageBox.confirm(
      t('volume.cleanupVolumes.confirmCleanupMessage', { type: typeText }),
      t('volume.cleanupVolumes.confirmCleanupTitle'),
      {
        confirmButtonText: t('volume.cleanupVolumes.startCleanup'),
        cancelButtonText: t('common.cancel'),
        type: 'warning'
      }
    )

    startCleanup()
  } catch (error) {
    // 用户取消操作
  }
}

// 开始清理
const startCleanup = async () => {
  loading.value = true
  showProgress.value = true
  progressPercentage.value = 0
  progressMessage.value = t('volume.cleanupVolumes.progressCleaning')
  cleanupStatus.value = 'success'
  cleanupResults.value = null

  try {
    // 显示清理进度动画
    progressPercentage.value = 30
    progressMessage.value = t('volume.cleanupVolumes.progressPercent', { percent: 30 })

    // 调用实际的清理API
    // cleanupType: 'unused' -> 清理所有未使用的卷 (all=true)
    // cleanupType: 'dangling' -> 只清理匿名卷 (all=false, 默认行为)
    // cleanupType: 'all' -> 清理所有卷 (all=true)
    const response = await volumeApi.pruneVolumes({
      filters: false,
      all: form.value.cleanupType === 'unused' || form.value.cleanupType === 'all',
      nodeId: form.value.nodeId
    })

    progressPercentage.value = 100
    progressMessage.value = t('volume.cleanupVolumes.progressComplete')
    cleanupStatus.value = 'success'

    // 使用实际的清理结果（拦截器已经返回 response.data）
    const result = response
    cleanupResults.value = {
      deleted: result.volumesDeleted,
      spaceReclaimed: formatBytes(result.spaceReclaimed),
      error: result.errors?.length > 0 ? result.errors.join(', ') : undefined
    }

    if (result.volumesDeleted > 0) {
      ElMessage.success(t('volume.cleanupVolumes.cleanupSuccess'))
    } else {
      ElMessage.info(t('volume.cleanupVolumes.noVolumesToClean') || '没有可清理的卷')
    }

    setTimeout(() => {
      showProgress.value = false
      isDialogVisible.value = false
    }, 2000)

    emit('success')
  } catch (error: any) {
    progressPercentage.value = 0
    progressMessage.value = t('volume.cleanupVolumes.progressFailed', { error: error.message || t('common.unknown') })
    cleanupStatus.value = 'exception'
    cleanupResults.value = {
      deleted: 0,
      spaceReclaimed: '0 B',
      error: error.message || t('common.unknown')
    }

    ElMessage.error(t('volume.cleanupVolumes.cleanupFailed'))
  } finally {
    loading.value = false
  }
}

// 格式化字节大小
const formatBytes = (bytes: number): string => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

// 关闭对话框
const handleClose = () => {
  if (loading.value) {
    return false
  }
  isDialogVisible.value = false
}
</script>

<style scoped>
.cleanup-progress {
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

.cleanup-results {
  margin-top: 16px;
  text-align: left;
}
</style>