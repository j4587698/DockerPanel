<template>
  <div class="command-execution">
    <!-- 连接选择和命令输入 -->
    <el-card class="command-card">
      <template #header>
        <div class="card-header">
          <span>命令执行</span>
          <el-button size="small" text @click="toggleHistory">
            <el-icon><Clock /></el-icon>
            历史命令
          </el-button>
        </div>
      </template>

      <el-form :model="form" label-width="100px">
        <el-form-item label="选择连接">
          <el-select
            v-model="form.connectionId"
            placeholder="选择SSH连接"
            filterable
            style="width: 100%"
            @change="handleConnectionChange"
          >
            <el-option
              v-for="conn in connections"
              :key="conn.id"
              :label="`${conn.username}@${conn.host}:${conn.port}`"
              :value="conn.id"
            >
              <div class="connection-option">
                <span class="conn-name">{{ conn.host }}</span>
                <span class="conn-user">{{ conn.username }}</span>
              </div>
            </el-option>
          </el-select>
        </el-form-item>

        <el-form-item label="执行命令">
          <el-input
            ref="commandInputRef"
            v-model="form.command"
            type="textarea"
            :rows="3"
            placeholder="输入要执行的命令，支持多行"
            @keydown.enter.ctrl="executeCommand"
          />
          <div class="input-hint">
            <el-text type="info" size="small">提示: Ctrl+Enter 快速执行</el-text>
          </div>
        </el-form-item>

        <el-form-item label="工作目录">
          <el-input v-model="form.workingDir" placeholder="可选，指定工作目录" />
        </el-form-item>

        <el-form-item label="超时时间">
          <el-input-number v-model="form.timeout" :min="5" :max="300" :step="5" />
          <span style="margin-left: 8px; color: #909399">秒</span>
        </el-form-item>

        <el-form-item>
          <el-button
            type="primary"
            @click="executeCommand"
            :loading="executing"
            :disabled="!canExecute"
          >
            <el-icon><VideoPlay /></el-icon>
            执行命令
          </el-button>
          <el-button @click="clearOutput">
            <el-icon><Delete /></el-icon>
            清空输出
          </el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 命令输出 -->
    <el-card class="output-card" v-if="executions.length > 0">
      <template #header>
        <div class="card-header">
          <span>执行结果</span>
          <el-button-group size="small">
            <el-button @click="copyOutput" :disabled="!currentExecution">
              <el-icon><CopyDocument /></el-icon>
              复制
            </el-button>
            <el-button @click="downloadOutput" :disabled="!currentExecution">
              <el-icon><Download /></el-icon>
              下载
            </el-button>
          </el-button-group>
        </div>
      </template>

      <el-tabs v-model="activeTab" type="card">
        <el-tab-pane
          v-for="(exec, index) in executions"
          :key="exec.id"
          :label="`执行 #${index + 1}`"
          :name="exec.id"
        >
          <div class="execution-info">
            <el-descriptions :column="4" size="small" border>
              <el-descriptions-item label="命令">
                <code>{{ exec.command }}</code>
              </el-descriptions-item>
              <el-descriptions-item label="状态">
                <el-tag v-if="exec.executing" type="warning">执行中...</el-tag>
                <el-tag v-else-if="exec.exitCode === 0" type="success">成功</el-tag>
                <el-tag v-else type="danger">失败 ({{ exec.exitCode }})</el-tag>
              </el-descriptions-item>
              <el-descriptions-item label="耗时">
                {{ exec.duration ? `${exec.duration}ms` : '-' }}
              </el-descriptions-item>
              <el-descriptions-item label="时间">
                {{ formatTime(exec.timestamp) }}
              </el-descriptions-item>
            </el-descriptions>
          </div>

          <div class="output-container" ref="outputContainer">
            <pre class="command-output" v-html="formatOutput(exec.output)"></pre>
            <div v-if="exec.error" class="error-output">
              <pre>{{ exec.error }}</pre>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <!-- 历史命令抽屉 -->
    <el-drawer v-model="showHistory" title="历史命令" size="400px">
      <div class="history-list">
        <div
          v-for="cmd in commandHistory"
          :key="cmd.id"
          class="history-item"
          @click="useHistoryCommand(cmd)"
        >
          <div class="history-command">
            <code>{{ cmd.command }}</code>
          </div>
          <div class="history-meta">
            <span>{{ cmd.host }}</span>
            <span>{{ formatTime(cmd.timestamp) }}</span>
          </div>
        </div>
        <el-empty v-if="commandHistory.length === 0" description="暂无历史命令" />
      </div>
    </el-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import {
  Clock, VideoPlay, Delete, CopyDocument, Download
} from '@element-plus/icons-vue'
import { useSshStore } from '@/stores/ssh'
import type { SshConnectionConfig } from '@/types/ssh'
import { formatLocalizedDateTime } from '@/utils/date'

interface Execution {
  id: string
  command: string
  output: string
  error?: string
  exitCode?: number
  duration?: number
  timestamp: string
  executing: boolean
}

interface HistoryCommand {
  id: string
  command: string
  host: string
  timestamp: string
}

const sshStore = useSshStore()
const commandInputRef = ref()
const outputContainer = ref()

const executing = ref(false)
const showHistory = ref(false)
const activeTab = ref('')
const connections = ref<SshConnectionConfig[]>([])
const executions = ref<Execution[]>([])
const commandHistory = ref<HistoryCommand[]>([])

const form = reactive({
  connectionId: '',
  command: '',
  workingDir: '',
  timeout: 30
})

const canExecute = computed(() => {
  return form.connectionId && form.command.trim()
})

const currentExecution = computed(() => {
  return executions.value.find(e => e.id === activeTab.value)
})

onMounted(async () => {
  await fetchConnections()
  loadCommandHistory()
})

const fetchConnections = async () => {
  try {
    const response = await sshStore.fetchConnectionConfigs({ page: 1, pageSize: 100 })
    connections.value = response.items || []
  } catch {
    ElMessage.error('获取连接列表失败')
  }
}

const loadCommandHistory = () => {
  try {
    const history = localStorage.getItem('ssh_command_history')
    if (history) {
      commandHistory.value = JSON.parse(history)
    }
  } catch {
    commandHistory.value = []
  }
}

const saveCommandHistory = (command: string, host: string) => {
  const newItem: HistoryCommand = {
    id: Date.now().toString(),
    command,
    host,
    timestamp: new Date().toISOString()
  }

  // 去重并限制数量
  commandHistory.value = [
    newItem,
    ...commandHistory.value.filter(h => h.command !== command)
  ].slice(0, 50)

  localStorage.setItem('ssh_command_history', JSON.stringify(commandHistory.value))
}

const handleConnectionChange = () => {
  // 连接改变时的处理
}

const executeCommand = async () => {
  if (!canExecute.value) return

  const connection = connections.value.find(c => c.id === form.connectionId)
  if (!connection) return

  const executionId = Date.now().toString()
  const execution: Execution = {
    id: executionId,
    command: form.command,
    output: '',
    timestamp: new Date().toISOString(),
    executing: true
  }

  executions.value.unshift(execution)
  activeTab.value = executionId
  executing.value = true

  const startTime = Date.now()

  try {
    const response = await sshStore.executeCommand({
      connectionId: form.connectionId,
      command: form.command,
      workingDir: form.workingDir || undefined,
      timeout: form.timeout
    })

    execution.output = response.output || ''
    execution.error = response.error
    execution.exitCode = response.exitCode
    execution.duration = Date.now() - startTime

    saveCommandHistory(form.command, connection.host)

    if (response.exitCode === 0) {
      ElMessage.success('命令执行成功')
    } else {
      ElMessage.warning(`命令执行完成，退出码: ${response.exitCode}`)
    }
  } catch (error: any) {
    execution.error = error.message || '命令执行失败'
    execution.exitCode = -1
    execution.duration = Date.now() - startTime
    ElMessage.error('命令执行失败')
  } finally {
    execution.executing = false
    executing.value = false

    await nextTick()
    scrollToBottom()
  }
}

const scrollToBottom = () => {
  if (outputContainer.value) {
    outputContainer.value.scrollTop = outputContainer.value.scrollHeight
  }
}

const clearOutput = () => {
  executions.value = []
  activeTab.value = ''
}

const copyOutput = async () => {
  if (!currentExecution.value) return

  try {
    await navigator.clipboard.writeText(currentExecution.value.output)
    ElMessage.success('输出已复制到剪贴板')
  } catch {
    ElMessage.error('复制失败')
  }
}

const downloadOutput = () => {
  if (!currentExecution.value) return

  const content = `Command: ${currentExecution.value.command}\nTimestamp: ${currentExecution.value.timestamp}\nExit Code: ${currentExecution.value.exitCode}\n\n${currentExecution.value.output}`
  const blob = new Blob([content], { type: 'text/plain' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `command-output-${Date.now()}.txt`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

const toggleHistory = () => {
  showHistory.value = !showHistory.value
}

const useHistoryCommand = (cmd: HistoryCommand) => {
  form.command = cmd.command
  showHistory.value = false
  commandInputRef.value?.focus()
}

const formatTime = (timestamp: string) => {
  return formatLocalizedDateTime(timestamp, '--')
}

const formatOutput = (output: string) => {
  if (!output) return ''
  // 简单的ANSI颜色代码转换
  return escapeHtml(output)
    .replace(/\x1b\[31m/g, '<span style="color: #f56c6c">')
    .replace(/\x1b\[32m/g, '<span style="color: #67c23a">')
    .replace(/\x1b\[33m/g, '<span style="color: #e6a23c">')
    .replace(/\x1b\[34m/g, '<span style="color: #409eff">')
    .replace(/\x1b\[0m/g, '</span>')
    .replace(/\n/g, '<br>')
}

const escapeHtml = (value: string) => {
  return value.replace(/[&<>'"]/g, (char) => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    "'": '&#39;',
    '"': '&quot;'
  }[char] || char))
}
</script>

<style scoped>
.command-execution {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.connection-option {
  display: flex;
  justify-content: space-between;
  width: 100%;
}

.conn-name {
  font-weight: 500;
}

.conn-user {
  color: #909399;
  font-size: 12px;
}

.input-hint {
  margin-top: 4px;
}

.output-card :deep(.el-card__body) {
  padding: 0;
}

.execution-info {
  padding: 16px;
  border-bottom: 1px solid #ebeef5;
}

.output-container {
  max-height: 400px;
  overflow: auto;
  background-color: #1a1a1a;
}

.command-output {
  margin: 0;
  padding: 16px;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: #e5eaf3;
  white-space: pre-wrap;
  word-break: break-all;
}

.error-output {
  border-top: 1px solid #303133;
  background-color: rgba(245, 108, 108, 0.1);
}

.error-output pre {
  margin: 0;
  padding: 16px;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  font-size: 13px;
  color: #f56c6c;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.history-item {
  padding: 12px;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.history-item:hover {
  border-color: #409eff;
  background-color: #f5f7fa;
}

.history-command code {
  display: block;
  font-size: 13px;
  color: #303133;
  word-break: break-all;
}

.history-meta {
  display: flex;
  justify-content: space-between;
  margin-top: 8px;
  font-size: 12px;
  color: #909399;
}

</style>

<style>
/* === Dark Mode === */
html.dark .execution-info {
  border-color: #303133;
}

html.dark .history-item {
  border-color: #303133;
}

html.dark .history-item:hover {
  background-color: #1a1a1a;
}

html.dark .history-command code {
  color: #e5eaf3;
}
</style>
