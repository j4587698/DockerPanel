<template>
  <div class="realtime-logs">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-left">
        <h1>实时日志</h1>
        <p class="description">查看系统推送的实时日志数据</p>
      </div>
      <div class="header-right">
        <el-button @click="clearLogs" :disabled="logs.length === 0">
          <el-icon><Delete /></el-icon>
          清空日志
        </el-button>
        <el-button @click="exportLogs" :disabled="logs.length === 0">
          <el-icon><Download /></el-icon>
          导出日志
        </el-button>
        <el-button @click="toggleAutoScroll" :type="autoScroll ? 'primary' : 'default'">
          <el-icon><Bottom /></el-icon>
          {{ autoScroll ? '停止滚动' : '自动滚动' }}
        </el-button>
      </div>
    </div>

    <!-- 日志统计 -->
    <div class="stats-section">
      <el-row :gutter="20">
        <el-col :span="6">
          <el-card class="stat-card">
            <div class="stat-content">
              <div class="stat-icon">
                <el-icon><Document /></el-icon>
              </div>
              <div class="stat-info">
                <div class="stat-value">{{ logs.length }}</div>
                <div class="stat-label">总日志数</div>
              </div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card class="stat-card error">
            <div class="stat-content">
              <div class="stat-icon">
                <el-icon><Warning /></el-icon>
              </div>
              <div class="stat-info">
                <div class="stat-value">{{ errorCount }}</div>
                <div class="stat-label">错误日志</div>
              </div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card class="stat-card warning">
            <div class="stat-content">
              <div class="stat-icon">
                <el-icon><InfoFilled /></el-icon>
              </div>
              <div class="stat-info">
                <div class="stat-value">{{ warningCount }}</div>
                <div class="stat-label">警告日志</div>
              </div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card class="stat-card success">
            <div class="stat-content">
              <div class="stat-icon">
                <el-icon><SuccessFilled /></el-icon>
              </div>
              <div class="stat-info">
                <div class="stat-value">{{ infoCount }}</div>
                <div class="stat-label">信息日志</div>
              </div>
            </div>
          </el-card>
        </el-col>
      </el-row>
    </div>

    <!-- 过滤器 -->
    <div class="filters-section">
      <el-card>
        <div class="filters-row">
          <div class="filter-item">
            <el-input
              v-model="searchKeyword"
              placeholder="搜索日志内容"
              clearable
              @input="handleSearch"
            >
              <template #prefix>
                <el-icon><Search /></el-icon>
              </template>
            </el-input>
          </div>
          <div class="filter-item">
            <el-select v-model="selectedLevel" placeholder="日志级别" clearable @change="handleFilterChange">
              <el-option label="全部" value="" />
              <el-option label="错误" value="error" />
              <el-option label="警告" value="warning" />
              <el-option label="信息" value="info" />
              <el-option label="调试" value="debug" />
            </el-select>
          </div>
          <div class="filter-item">
            <el-select v-model="selectedContainer" placeholder="容器筛选" clearable @change="handleFilterChange">
              <el-option label="全部" value="" />
              <el-option
                v-for="container in containerNames"
                :key="container"
                :label="container"
                :value="container"
              />
            </el-select>
          </div>
          <div class="filter-item">
            <el-switch
              v-model="showTimestamps"
              active-text="显示时间戳"
              inactive-text="隐藏时间戳"
            />
          </div>
        </div>
      </el-card>
    </div>

    <!-- 日志内容 -->
    <div class="logs-section">
      <el-card>
        <div class="log-container" ref="logContainer">
          <!-- 连接状态指示器 -->
          <div v-if="!isConnected" class="connection-status disconnected">
            <el-icon><WarningFilled /></el-icon>
            <span>SignalR连接已断开，无法接收实时日志</span>
            <el-button type="text" @click="connectSignalR">重新连接</el-button>
          </div>

          <div v-else class="connection-status connected">
            <el-icon><SuccessFilled /></el-icon>
            <span>SignalR连接正常，正在接收实时日志</span>
          </div>

          <!-- 日志列表 -->
          <div v-if="filteredLogs.length === 0" class="empty-logs">
            <el-empty description="暂无日志数据" />
          </div>

          <div v-else class="log-viewer">
            <div
              v-for="(log, index) in filteredLogs"
              :key="index"
              class="log-entry"
              :class="getLogEntryClass(log)"
              ref="logEntries"
            >
              <div class="log-header">
                <span class="log-timestamp" v-if="showTimestamps">
                  {{ formatTimestamp(log.timestamp) }}
                </span>
                <span class="log-container-name">{{ log.container }}</span>
                <el-tag
                  :type="getLevelTagType(log.level)"
                  size="small"
                  class="log-level"
                >
                  {{ log.level.toUpperCase() }}
                </el-tag>
              </div>
              <div class="log-message" v-html="highlightSearch(log.message)"></div>
            </div>
          </div>
        </div>
      </el-card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import {
  Delete,
  Download,
  Bottom,
  Document,
  Warning,
  InfoFilled,
  SuccessFilled,
  Search,
  WarningFilled
} from '@element-plus/icons-vue'
import { useWebSocketStore } from '@/stores/websocket'
import type { LogEntry } from '@/services/signalr'
import { formatLocalizedDateTime } from '@/utils/date'

// 响应式数据
const logContainer = ref<HTMLElement>()
const logEntries = ref<HTMLElement[]>([])
const searchKeyword = ref('')
const selectedLevel = ref('')
const selectedContainer = ref('')
const showTimestamps = ref(true)
const autoScroll = ref(true)

// Store
const websocketStore = useWebSocketStore()

// 计算属性
const isConnected = computed(() => websocketStore.isConnected)
const logs = computed(() => websocketStore.recentLogs)

// 过滤后的日志
const filteredLogs = computed(() => {
  let filteredLogs = logs.value

  // 按级别过滤
  if (selectedLevel.value) {
    filteredLogs = filteredLogs.filter(log => log.level === selectedLevel.value)
  }

  // 按容器过滤
  if (selectedContainer.value) {
    filteredLogs = filteredLogs.filter(log => log.container === selectedContainer.value)
  }

  // 按关键词搜索
  if (searchKeyword.value) {
    const keyword = searchKeyword.value.toLowerCase()
    filteredLogs = filteredLogs.filter(log =>
      log.message.toLowerCase().includes(keyword) ||
      log.container.toLowerCase().includes(keyword)
    )
  }

  return filteredLogs
})

// 统计数据
const errorCount = computed(() => logs.value.filter(log => log.level === 'error').length)
const warningCount = computed(() => logs.value.filter(log => log.level === 'warning').length)
const infoCount = computed(() => logs.value.filter(log => log.level === 'info').length)

// 容器名称列表
const containerNames = computed(() => {
  const containers = new Set(logs.value.map(log => log.container))
  return Array.from(containers).sort()
})

// 监听新日志，自动滚动到底部
watch(logs, async () => {
  if (autoScroll.value) {
    await nextTick()
    scrollToBottom()
  }
}, { deep: true })

// 监听过滤后的日志变化，更新自动滚动
watch(filteredLogs, async () => {
  if (autoScroll.value) {
    await nextTick()
    scrollToBottom()
  }
})

// 生命周期
onMounted(() => {
  // 确保SignalR连接
  if (!isConnected.value) {
    connectSignalR()
  }
})

// 方法
const connectSignalR = async () => {
  try {
    await websocketStore.initWebSocket()
  } catch (error) {
    console.error('连接SignalR失败:', error)
    ElMessage.error('连接SignalR失败')
  }
}

const clearLogs = () => {
  websocketStore.clearMessages()
  ElMessage.success('日志已清空')
}

const exportLogs = () => {
  if (filteredLogs.value.length === 0) {
    ElMessage.warning('暂无日志可导出')
    return
  }

  const logContent = filteredLogs.value.map(log =>
    `${log.timestamp} [${log.level.toUpperCase()}] ${log.container}: ${log.message}`
  ).join('\n')

  const blob = new Blob([logContent], { type: 'text/plain' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `realtime-logs-${new Date().toISOString().slice(0, 19)}.txt`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)

  ElMessage.success('日志导出成功')
}

const toggleAutoScroll = () => {
  autoScroll.value = !autoScroll.value
  if (autoScroll.value) {
    nextTick(() => scrollToBottom())
  }
}

const scrollToBottom = () => {
  if (logContainer.value) {
    logContainer.value.scrollTop = logContainer.value.scrollHeight
  }
}

const handleSearch = () => {
  // 搜索逻辑在 computed 中处理
}

const handleFilterChange = () => {
  // 过滤逻辑在 computed 中处理
}

const getLogEntryClass = (log: LogEntry) => {
  return {
    'log-error': log.level === 'error',
    'log-warning': log.level === 'warning',
    'log-info': log.level === 'info',
    'log-debug': log.level === 'debug'
  }
}

const getLevelTagType = (level: string) => {
  switch (level) {
    case 'error': return 'danger'
    case 'warning': return 'warning'
    case 'info': return 'info'
    case 'debug': return ''
    default: return ''
  }
}

const formatTimestamp = (timestamp: string) => {
  return formatLocalizedDateTime(timestamp, '--', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false
  })
}

const highlightSearch = (message: string) => {
  const escapedMessage = escapeHtml(message)
  if (!searchKeyword.value) return escapedMessage

  const keyword = escapeRegExp(escapeHtml(searchKeyword.value))
  const regex = new RegExp(`(${keyword})`, 'gi')
  return escapedMessage.replace(regex, '<mark>$1</mark>')
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

const escapeRegExp = (value: string) => {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}
</script>

<style scoped>
.realtime-logs {
  padding: 20px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.header-left h1 {
  margin: 0 0 8px 0;
  font-size: 24px;
  font-weight: 600;
  color: var(--text-main);
}

.header-left .description {
  margin: 0;
  color: var(--text-secondary);
  font-size: 14px;
}

.header-right {
  display: flex;
  gap: 12px;
}

.stats-section {
  margin-bottom: 20px;
}

.stat-card {
  height: 100px;
}

.stat-card.error {
  border-left: 4px solid #f56c6c;
}

.stat-card.warning {
  border-left: 4px solid #e6a23c;
}

.stat-card.success {
  border-left: 4px solid #67c23a;
}

.stat-content {
  display: flex;
  align-items: center;
  height: 100%;
  gap: 16px;
}

.stat-icon {
  font-size: 32px;
  color: #409eff;
}

.stat-card.error .stat-icon {
  color: #f56c6c;
}

.stat-card.warning .stat-icon {
  color: #e6a23c;
}

.stat-card.success .stat-icon {
  color: #67c23a;
}

.stat-info {
  flex: 1;
}

.stat-value {
  font-size: 28px;
  font-weight: 600;
  color: var(--text-main);
  line-height: 1;
}

.stat-label {
  font-size: 14px;
  color: var(--text-muted);
  margin-top: 4px;
}

.filters-section {
  margin-bottom: 20px;
}

.filters-row {
  display: flex;
  gap: 16px;
  align-items: center;
  flex-wrap: wrap;
}

.filter-item {
  min-width: 200px;
}

.logs-section {
  margin-bottom: 20px;
}

.log-container {
  height: 600px;
  overflow-y: auto;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background-color: var(--bg-glass-dark);
}

.connection-status {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-color);
  font-size: 14px;
}

.connection-status.disconnected {
  background-color: #fef0f0;
  color: #f56c6c;
}

.connection-status.connected {
  background-color: #f0f9ff;
  color: #67c23a;
}

.empty-logs {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 400px;
}

.log-viewer {
  padding: 16px;
}

.log-entry {
  margin-bottom: 12px;
  padding: 12px;
  background-color: var(--bg-surface);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  transition: all 0.2s;
}

.log-entry:hover {
  border-color: var(--border-color-hover, #c0c4cc);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.log-entry.log-error {
  border-left: 4px solid #f56c6c;
}

.log-entry.log-warning {
  border-left: 4px solid #e6a23c;
}

.log-entry.log-info {
  border-left: 4px solid #409eff;
}

.log-entry.log-debug {
  border-left: 4px solid #909399;
}

.log-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
  font-size: 12px;
}

.log-timestamp {
  color: var(--text-muted);
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
}

.log-container-name {
  color: #409eff;
  font-weight: 500;
  background-color: #ecf5ff;
  padding: 2px 6px;
  border-radius: 3px;
}

.log-level {
  font-size: 10px;
}

.log-message {
  font-size: 14px;
  line-height: 1.5;
  color: var(--text-main);
  white-space: pre-wrap;
  word-break: break-word;
}

:deep(mark) {
  background-color: #ff9800;
  color: #000;
  padding: 0 2px;
  border-radius: 2px;
}

@media (max-width: 1024px) {
  .stats-section .el-col {
    flex: 0 0 50%;
    max-width: 50%;
    margin-bottom: 12px;
  }

  .stat-card { height: auto; }
}

@media (max-width: 768px) {
  .realtime-logs {
    padding: 12px;
  }

  .page-header {
    flex-direction: column;
    gap: 16px;
    align-items: stretch;
  }

  .header-right {
    justify-content: center;
  }

  .stats-section .el-col {
    flex: 0 0 100%;
    max-width: 100%;
    margin-bottom: 12px;
  }

  .filters-row {
    flex-direction: column;
    align-items: stretch;
  }

  .filter-item {
    min-width: auto;
  }

  .log-container {
    height: 400px;
  }

  .log-header {
    flex-wrap: wrap;
    gap: 8px;
  }
}
</style>