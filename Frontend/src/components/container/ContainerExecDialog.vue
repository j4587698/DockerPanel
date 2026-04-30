<template>
  <el-dialog
    v-model="visible"
    :title="`${t('container.execDialog.title')} - ${containerName || ''}`"
    width="800px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <!-- 命令输入 -->
    <div class="command-section">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
        <el-form-item :label="t('container.execDialog.command')" prop="command">
          <el-input
            v-model="form.commandStr"
            :placeholder="t('container.execDialog.commandPlaceholder')"
            clearable
            @keyup.enter="executeCommand"
          >
            <template #append>
              <el-button @click="showCommandHistory = !showCommandHistory">
                <el-icon><Clock /></el-icon>
                {{ t('container.execDialog.history') }}
              </el-button>
            </template>
          </el-input>
        </el-form-item>

        <el-form-item :label="t('container.execDialog.workingDir')">
          <el-input
            v-model="form.workingDir"
            :placeholder="t('container.execDialog.workingDirPlaceholder')"
            clearable
          />
        </el-form-item>

        <el-form-item :label="t('container.execDialog.user')">
          <el-input
            v-model="form.user"
            :placeholder="t('container.execDialog.userPlaceholder')"
            clearable
          />
        </el-form-item>

        <el-form-item :label="t('container.execDialog.envVars')">
          <div class="env-inputs">
            <div
              v-for="(env, index) in form.envList"
              :key="index"
              class="env-item"
            >
              <el-input
                v-model="env.key"
                :placeholder="t('container.execDialog.varName')"
                style="width: 120px"
                clearable
              />
              <span>=</span>
              <el-input
                v-model="env.value"
                :placeholder="t('container.execDialog.varValue')"
                style="flex: 1"
                clearable
              />
              <el-button
                type="danger"
                size="small"
                @click="removeEnv(index)"
                :disabled="form.envList.length <= 1"
              >
                {{ t('common.delete') }}
              </el-button>
            </div>
            <el-button type="primary" size="small" @click="addEnv">
              <el-icon><Plus /></el-icon>
              {{ t('container.execDialog.addEnvVar') }}
            </el-button>
          </div>
        </el-form-item>

        <el-form-item>
          <el-checkbox v-model="form.tty">{{ t('container.execDialog.allocateTTY') }}</el-checkbox>
          <el-checkbox v-model="form.attachStdout">{{ t('container.execDialog.attachStdout') }}</el-checkbox>
          <el-checkbox v-model="form.attachStderr">{{ t('container.execDialog.attachStderr') }}</el-checkbox>
        </el-form-item>
      </el-form>
    </div>

    <!-- 命令历史 -->
    <div v-if="showCommandHistory" class="history-section">
      <el-divider content-position="left">
        <span style="color: #909399;">{{ t('container.execDialog.commandHistory') }}</span>
      </el-divider>
      <div class="history-list">
        <div
          v-for="(cmd, index) in commandHistory"
          :key="index"
          class="history-item"
          @click="selectCommand(cmd)"
        >
          <el-text>{{ cmd }}</el-text>
          <el-button
            type="text"
            size="small"
            @click.stop="removeFromHistory(index)"
          >
            <el-icon><Delete /></el-icon>
          </el-button>
        </div>
        <div v-if="commandHistory.length === 0" class="history-empty">
          <el-text type="info">{{ t('container.execDialog.noCommandHistory') }}</el-text>
        </div>
      </div>
    </div>

    <!-- 执行结果 -->
    <div v-if="executionResult" class="result-section">
      <el-divider content-position="left">
        <span style="color: #909399;">{{ t('container.execDialog.executionResult') }}</span>
      </el-divider>

      <div class="result-info">
        <el-descriptions :column="3" border size="small">
          <el-descriptions-item :label="t('container.execDialog.exitCode')">
            <el-tag :type="executionResult.exitCode === 0 ? 'success' : 'danger'">
              {{ executionResult.exitCode }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item :label="t('container.execDialog.startTime')">
            {{ formatDateTime(executionResult.startTime) }}
          </el-descriptions-item>
          <el-descriptions-item :label="t('container.execDialog.endTime')">
            {{ formatDateTime(executionResult.endTime) }}
          </el-descriptions-item>
        </el-descriptions>
      </div>

      <!-- 标准输出 -->
      <div v-if="executionResult.stdout" class="output-section">
        <div class="output-header">
          <span>{{ t('container.execDialog.stdout') }}</span>
          <el-button type="text" size="small" @click="copyOutput(executionResult.stdout)">
            <el-icon><CopyDocument /></el-icon>
            {{ t('container.execDialog.copy') }}
          </el-button>
        </div>
        <pre class="output-content stdout">{{ executionResult.stdout }}</pre>
      </div>

      <!-- 标准错误 -->
      <div v-if="executionResult.stderr" class="output-section">
        <div class="output-header">
          <span>{{ t('container.execDialog.stderr') }}</span>
          <el-button type="text" size="small" @click="copyOutput(executionResult.stderr)">
            <el-icon><CopyDocument /></el-icon>
            {{ t('container.execDialog.copy') }}
          </el-button>
        </div>
        <pre class="output-content stderr">{{ executionResult.stderr }}</pre>
      </div>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('container.execDialog.close') }}</el-button>
        <el-button type="primary" @click="executeCommand" :loading="executing">
          <el-icon><VideoPlay /></el-icon>
          {{ t('container.execDialog.execute') }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage, type FormInstance } from 'element-plus'
import { Clock, Delete, Plus, CopyDocument, VideoPlay } from '@element-plus/icons-vue'
import { containerApi, type ExecCommandRequest, type ExecResult } from '@/api/containers'
import { useI18n } from 'vue-i18n'
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

// 响应式数据
const formRef = ref<FormInstance>()
const executing = ref(false)
const showCommandHistory = ref(false)
const executionResult = ref<ExecResult | null>(null)
const commandHistory = ref<string[]>([])

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const containerId = computed(() => props.containerId)
const containerName = computed(() => containerId.value?.slice(0, 12) || '')

// 表单数据
const form = reactive({
  commandStr: '',
  command: [] as string[],
  workingDir: '',
  user: '',
  tty: true,
  attachStdout: true,
  attachStderr: true,
  envList: [{ key: '', value: '' }],
  env: {} as Record<string, string>
})

// 表单验证规则
const rules = {
  commandStr: [
    { required: true, message: t('container.execDialog.commandRequired'), trigger: 'blur' }
  ]
}

// 监听命令字符串变化
watch(() => form.commandStr, (value) => {
  form.command = value ? value.trim().split(/\s+/) : []
})

// 监听环境变量列表变化
watch(form.envList, (envList) => {
  form.env = {}
  envList.forEach(env => {
    if (env.key && env.value) {
      form.env[env.key] = env.value
    }
  })
}, { deep: true })

// 监听对话框打开
watch(visible, (newValue) => {
  if (newValue) {
    loadCommandHistory()
  } else {
    resetForm()
  }
})

// 方法
const loadCommandHistory = () => {
  const saved = localStorage.getItem(`container-exec-history-${containerId.value}`)
  if (saved) {
    try {
      commandHistory.value = JSON.parse(saved)
    } catch (error) {
      console.error('加载命令历史失败:', error)
    }
  }
}

const saveCommandHistory = () => {
  if (form.commandStr.trim()) {
    const index = commandHistory.value.indexOf(form.commandStr)
    if (index > -1) {
      commandHistory.value.splice(index, 1)
    }
    commandHistory.value.unshift(form.commandStr)

    // 最多保存20条历史记录
    if (commandHistory.value.length > 20) {
      commandHistory.value = commandHistory.value.slice(0, 20)
    }

    localStorage.setItem(
      `container-exec-history-${containerId.value}`,
      JSON.stringify(commandHistory.value)
    )
  }
}

const selectCommand = (command: string) => {
  form.commandStr = command
}

const removeFromHistory = (index: number) => {
  commandHistory.value.splice(index, 1)
  localStorage.setItem(
    `container-exec-history-${containerId.value}`,
    JSON.stringify(commandHistory.value)
  )
}

const addEnv = () => {
  form.envList.push({ key: '', value: '' })
}

const removeEnv = (index: number) => {
  form.envList.splice(index, 1)
}

const executeCommand = async () => {
  if (!formRef.value || !containerId.value) return

  try {
    await formRef.value.validate()

    executing.value = true
    executionResult.value = null

    const request: ExecCommandRequest = {
      command: form.command,
      tty: form.tty,
      attachStdout: form.attachStdout,
      attachStderr: form.attachStderr,
      workingDir: form.workingDir || undefined,
      user: form.user || undefined,
      env: Object.keys(form.env).length > 0 ? form.env : undefined
    }

    const response = await containerApi.executeCommand(containerId.value, request)
    executionResult.value = response.data

    saveCommandHistory()
    ElMessage.success(t('container.execDialog.executeSuccess'))
  } catch (error: any) {
    console.error('执行命令失败:', error)
    ElMessage.error(error.response?.data?.message || t('container.execDialog.executeFailed'))
  } finally {
    executing.value = false
  }
}

const copyOutput = async (text: string) => {
  try {
    await navigator.clipboard.writeText(text)
    ElMessage.success(t('container.execDialog.copySuccess'))
  } catch (error) {
    console.error('复制失败:', error)
    ElMessage.error(t('container.execDialog.copyFailed'))
  }
}

const formatDateTime = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

const handleClose = () => {
  visible.value = false
}

const resetForm = () => {
  if (formRef.value) {
    formRef.value.resetFields()
  }

  Object.assign(form, {
    commandStr: '',
    command: [],
    workingDir: '',
    user: '',
    tty: true,
    attachStdout: true,
    attachStderr: true,
    envList: [{ key: '', value: '' }],
    env: {}
  })

  executionResult.value = null
  showCommandHistory.value = false
}
</script>

<style scoped>
.command-section {
  margin-bottom: 20px;
}

.env-inputs {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.env-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.history-section {
  margin-bottom: 20px;
}

.history-list {
  max-height: 200px;
  overflow-y: auto;
  border: 1px solid #e4e7ed;
  border-radius: 4px;
}

.history-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  cursor: pointer;
  border-bottom: 1px solid #f0f0f0;
  transition: background-color 0.3s ease;
}

.history-item:hover {
  background-color: #f5f7fa;
}

.history-item:last-child {
  border-bottom: none;
}

.history-empty {
  padding: 20px;
  text-align: center;
}

.result-section {
  margin-top: 20px;
}

.result-info {
  margin-bottom: 16px;
}

.output-section {
  margin-bottom: 16px;
}

.output-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
  font-weight: 600;
  color: #303133;
}

.output-content {
  background-color: #000;
  color: #fff;
  padding: 12px;
  border-radius: 4px;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.4;
  overflow-x: auto;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 200px;
  overflow-y: auto;
}

.output-content.stderr {
  color: #f56c6c;
}

.dialog-footer {
  text-align: right;
}

@media (max-width: 768px) {
  .env-item {
    flex-direction: column;
    align-items: stretch;
    gap: 4px;
  }

  .env-item span {
    display: none;
  }

  .output-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 8px;
  }
}
</style>