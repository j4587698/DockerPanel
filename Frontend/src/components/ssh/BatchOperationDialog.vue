<template>
  <el-dialog
    v-model="visible"
    :title="dialogTitle"
    width="700px"
    :close-on-click-modal="false"
    @close="handleClose"
    class="batch-dialog"
  >
    <!-- 操作类型：批量测试连接 -->
    <div v-if="operation === 'test'" class="batch-content">
      <el-alert
        :title="`将测试 ${selectedConnections.length} 个连接`"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 16px"
      />

      <el-table :data="testResults" v-loading="loading" max-height="400">
        <el-table-column prop="host" label="主机" width="150" align="center" />
        <el-table-column prop="port" label="端口" width="80" align="center" />
        <el-table-column prop="username" label="用户名" width="120" align="center" />
        <el-table-column label="状态" width="120" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.testing" type="warning">测试中...</el-tag>
            <el-tag v-else-if="row.success" type="success">成功</el-tag>
            <el-tag v-else-if="row.success === false" type="danger">失败</el-tag>
            <el-tag v-else type="info">待测试</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="message" label="消息" show-overflow-tooltip align="center" />
      </el-table>
    </div>

    <!-- 操作类型：批量执行命令 -->
    <div v-else-if="operation === 'execute'" class="batch-content">
      <el-form :model="executeForm" label-width="100px">
        <el-form-item label="执行命令" required>
          <el-input
            v-model="executeForm.command"
            type="textarea"
            :rows="3"
            placeholder="请输入要执行的命令"
          />
        </el-form-item>
        <el-form-item label="超时时间">
          <el-input-number
            v-model="executeForm.timeout"
            :min="5"
            :max="300"
            :step="5"
          />
          <span style="margin-left: 8px; color: #909399">秒</span>
        </el-form-item>
        <el-form-item label="并行执行">
          <el-switch v-model="executeForm.parallel" />
          <span style="margin-left: 8px; color: #909399">同时在所有主机上执行</span>
        </el-form-item>
      </el-form>

      <el-divider v-if="executeResults.length > 0">执行结果</el-divider>

      <el-table v-if="executeResults.length > 0" :data="executeResults" max-height="300">
        <el-table-column prop="host" label="主机" width="150" align="center" />
        <el-table-column label="状态" width="100" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.executing" type="warning">执行中...</el-tag>
            <el-tag v-else-if="row.exitCode === 0" type="success">成功</el-tag>
            <el-tag v-else-if="row.exitCode !== undefined" type="danger">失败({{ row.exitCode }})</el-tag>
            <el-tag v-else type="info">待执行</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="输出" min-width="200" align="center">
          <template #default="{ row }">
            <el-popover
              v-if="row.output"
              trigger="click"
              width="500"
              placement="left"
            >
              <template #reference>
                <el-button size="small" text>查看输出</el-button>
              </template>
              <pre class="command-output">{{ row.output }}</pre>
            </el-popover>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 操作类型：批量上传文件 -->
    <div v-else-if="operation === 'upload'" class="batch-content">
      <el-form :model="uploadForm" label-width="100px">
        <el-form-item label="选择文件" required>
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :limit="10"
            :on-change="handleFileChange"
            :file-list="uploadForm.files"
            drag
            multiple
          >
            <el-icon class="el-icon--upload"><UploadFilled /></el-icon>
            <div class="el-upload__text">
              拖拽文件到此处，或<em>点击上传</em>
            </div>
          </el-upload>
        </el-form-item>
        <el-form-item label="目标路径" required>
          <el-input
            v-model="uploadForm.remotePath"
            placeholder="/home/user/"
          />
        </el-form-item>
        <el-form-item label="覆盖已存在">
          <el-switch v-model="uploadForm.overwrite" />
        </el-form-item>
      </el-form>

      <el-divider v-if="uploadResults.length > 0">上传进度</el-divider>

      <el-table v-if="uploadResults.length > 0" :data="uploadResults" max-height="250">
        <el-table-column prop="host" label="主机" width="150" align="center" />
        <el-table-column prop="filename" label="文件名" width="150" align="center" />
        <el-table-column label="进度" min-width="200" align="center">
          <template #default="{ row }">
            <el-progress
              :percentage="row.progress"
              :status="row.status === 'success' ? 'success' : row.status === 'error' ? 'exception' : ''"
            />
          </template>
        </el-table-column>
        <el-table-column prop="message" label="消息" show-overflow-tooltip align="center" />
      </el-table>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ finished ? '关闭' : '取消' }}</el-button>
        <el-button
          v-if="!finished"
          type="primary"
          @click="executeOperation"
          :loading="loading"
          :disabled="!canExecute"
        >
          {{ executeButtonText }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { UploadFilled } from '@element-plus/icons-vue'
import { useSshStore } from '@/stores/ssh'
import type { SshConnectionConfig } from '@/types/ssh'
import type { UploadFile, UploadInstance } from 'element-plus'

interface TestResult {
  host: string
  port: number
  username: string
  testing: boolean
  success?: boolean
  message?: string
}

interface ExecuteResult {
  host: string
  executing: boolean
  exitCode?: number
  output?: string
}

interface UploadResult {
  host: string
  filename: string
  progress: number
  status: 'pending' | 'uploading' | 'success' | 'error'
  message?: string
}

const props = defineProps<{
  modelValue: boolean
  operation: 'test' | 'execute' | 'upload'
  selectedConnections: SshConnectionConfig[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'success'): void
}>()

const sshStore = useSshStore()
const uploadRef = ref<UploadInstance>()
const loading = ref(false)
const finished = ref(false)

const visible = ref(props.modelValue)

watch(() => props.modelValue, (val) => {
  visible.value = val
  if (val) {
    resetState()
  }
})

watch(visible, (val) => {
  emit('update:modelValue', val)
})

// 测试连接相关
const testResults = ref<TestResult[]>([])

// 执行命令相关
const executeForm = reactive({
  command: '',
  timeout: 30,
  parallel: false
})
const executeResults = ref<ExecuteResult[]>([])

// 上传文件相关
const uploadForm = reactive({
  files: [] as UploadFile[],
  remotePath: '/tmp/',
  overwrite: false
})
const uploadResults = ref<UploadResult[]>([])

const dialogTitle = computed(() => {
  const titles: Record<string, string> = {
    test: '批量测试连接',
    execute: '批量执行命令',
    upload: '批量上传文件'
  }
  return titles[props.operation] || '批量操作'
})

const executeButtonText = computed(() => {
  const texts: Record<string, string> = {
    test: '开始测试',
    execute: '执行命令',
    upload: '开始上传'
  }
  return texts[props.operation] || '执行'
})

const canExecute = computed(() => {
  if (props.selectedConnections.length === 0) return false
  if (props.operation === 'execute' && !executeForm.command.trim()) return false
  if (props.operation === 'upload' && uploadForm.files.length === 0) return false
  return true
})

const resetState = () => {
  finished.value = false
  testResults.value = props.selectedConnections.map(conn => ({
    host: conn.host,
    port: conn.port,
    username: conn.username,
    testing: false
  }))
  executeResults.value = []
  uploadResults.value = []
  executeForm.command = ''
  executeForm.timeout = 30
  executeForm.parallel = false
  uploadForm.files = []
  uploadForm.remotePath = '/tmp/'
  uploadForm.overwrite = false
}

const handleFileChange = (uploadFile: UploadFile, uploadFiles: UploadFile[]) => {
  uploadForm.files = uploadFiles
}

const executeOperation = async () => {
  loading.value = true

  try {
    switch (props.operation) {
      case 'test':
        await executeBatchTest()
        break
      case 'execute':
        await executeBatchCommand()
        break
      case 'upload':
        await executeBatchUpload()
        break
    }
    finished.value = true
    emit('success')
  } catch (error) {
    ElMessage.error('操作执行失败')
  } finally {
    loading.value = false
  }
}

const executeBatchTest = async () => {
  for (const result of testResults.value) {
    result.testing = true
    try {
      await sshStore.testConnection({
        host: result.host,
        port: result.port,
        username: result.username
      })
      result.success = true
      result.message = '连接成功'
    } catch (error: any) {
      result.success = false
      result.message = error.message || '连接失败'
    } finally {
      result.testing = false
    }
  }
}

const executeBatchCommand = async () => {
  executeResults.value = props.selectedConnections.map(conn => ({
    host: conn.host,
    executing: false
  }))

  if (executeForm.parallel) {
    // 并行执行
    await Promise.all(executeResults.value.map(async (result, index) => {
      result.executing = true
      try {
        const response = await sshStore.executeCommand({
          connectionId: props.selectedConnections[index].id,
          command: executeForm.command,
          timeout: executeForm.timeout
        })
        result.exitCode = response.exitCode
        result.output = response.output
      } catch (error: any) {
        result.exitCode = -1
        result.output = error.message
      } finally {
        result.executing = false
      }
    }))
  } else {
    // 串行执行
    for (let i = 0; i < executeResults.value.length; i++) {
      const result = executeResults.value[i]
      result.executing = true
      try {
        const response = await sshStore.executeCommand({
          connectionId: props.selectedConnections[i].id,
          command: executeForm.command,
          timeout: executeForm.timeout
        })
        result.exitCode = response.exitCode
        result.output = response.output
      } catch (error: any) {
        result.exitCode = -1
        result.output = error.message
      } finally {
        result.executing = false
      }
    }
  }
}

const executeBatchUpload = async () => {
  uploadResults.value = []

  for (const conn of props.selectedConnections) {
    for (const file of uploadForm.files) {
      uploadResults.value.push({
        host: conn.host,
        filename: file.name,
        progress: 0,
        status: 'pending'
      })
    }
  }

  let resultIndex = 0
  for (const conn of props.selectedConnections) {
    for (const file of uploadForm.files) {
      const result = uploadResults.value[resultIndex]
      result.status = 'uploading'

      try {
        await sshStore.uploadFile({
          connectionId: conn.id,
          file: file.raw!,
          remotePath: uploadForm.remotePath,
          overwrite: uploadForm.overwrite,
          onProgress: (progress: number) => {
            result.progress = progress
          }
        })
        result.progress = 100
        result.status = 'success'
        result.message = '上传成功'
      } catch (error: any) {
        result.status = 'error'
        result.message = error.message || '上传失败'
      }

      resultIndex++
    }
  }
}

const handleClose = () => {
  visible.value = false
}
</script>

<style scoped>
.batch-dialog :deep(.el-dialog__body) {
  padding: 20px 24px;
}

.batch-content {
  min-height: 200px;
}

.command-output {
  max-height: 300px;
  overflow: auto;
  background-color: #1a1a1a;
  color: #e5eaf3;
  padding: 12px;
  border-radius: 4px;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

:deep(.el-upload-dragger) {
  padding: 20px;
}
</style>
