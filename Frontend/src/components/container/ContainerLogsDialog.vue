<template>
  <el-dialog
    v-model="visible"
    :title="`${t('container.logsDialog.title')} - ${containerName || ''}`"
    width="900px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <!-- 日志控制 -->
    <div class="log-controls">
      <div class="control-left">
        <el-select v-model="logOptions.tail" :placeholder="t('container.logsDialog.showLines')" style="width: 120px">
          <el-option :label="t('container.logsDialog.last100Lines')" :value="100" />
          <el-option :label="t('container.logsDialog.last200Lines')" :value="200" />
          <el-option :label="t('container.logsDialog.last500Lines')" :value="500" />
          <el-option :label="t('container.logsDialog.last1000Lines')" :value="1000" />
          <el-option :label="t('container.logsDialog.allLines')" :value="0" />
        </el-select>
        <el-checkbox v-model="logOptions.follow" @change="handleFollowChange">
          {{ t('container.logsDialog.realtimeFollow') }}
        </el-checkbox>
        <el-button @click="refreshLogs" :loading="loading">
          <el-icon><Refresh /></el-icon>
          {{ t('container.logsDialog.refresh') }}
        </el-button>
        <el-button @click="clearLogs">
          <el-icon><Delete /></el-icon>
          {{ t('container.logsDialog.clear') }}
        </el-button>
        <el-button @click="downloadLogs">
          <el-icon><Download /></el-icon>
          {{ t('container.logsDialog.download') }}
        </el-button>
      </div>
      <div class="control-right">
        <el-input
          v-model="searchKeyword"
          :placeholder="t('container.logsDialog.searchPlaceholder')"
          clearable
          style="width: 200px"
          @input="handleSearch"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
      </div>
    </div>

    <!-- 日志内容 -->
    <div class="log-content" ref="logContainer">
      <div v-if="loading && logs.length === 0" class="loading-container">
        <el-skeleton :rows="10" animated />
      </div>

      <div v-else-if="logs.length === 0" class="empty-container">
        <el-empty :description="t('container.logsDialog.noLogs')" />
      </div>

      <div v-else class="log-viewer">
        <div
          v-for="(log, index) in filteredLogs"
          :key="index"
          class="log-entry"
          :class="{ 'log-error': log.level === 'error', 'log-warning': log.level === 'warning' }"
        >
          <span class="log-timestamp">{{ formatTimestamp(log.timestamp) }}</span>
          <span class="log-stream">{{ log.stream.toUpperCase() }}</span>
          <span class="log-message" v-html="highlightSearch(log.message)"></span>
        </div>

        <!-- 加载更多指示器 -->
        <div v-if="hasMore" class="load-more" ref="loadMoreTrigger">
          <el-text type="info">{{ t('container.logsDialog.loadingMore') }}</el-text>
        </div>

        <!-- 实时跟踪指示器 -->
        <div v-if="logOptions.follow" class="follow-indicator">
          <el-text type="success" size="small">
            <el-icon><VideoPlay /></el-icon>
            {{ t('container.logsDialog.following') }}
          </el-text>
        </div>
      </div>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('container.logsDialog.close') }}</el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { Refresh, Delete, Download, Search, VideoPlay } from '@element-plus/icons-vue'
import { containerApi, type LogEntry } from '@/api/containers'
import { useI18n } from 'vue-i18n'
import { formatLocalizedTime } from '@/utils/date'

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
const loading = ref(false)
const logs = ref<LogEntry[]>([])
const searchKeyword = ref('')
const hasMore = ref(false)
const logContainer = ref<HTMLElement>()
const loadMoreTrigger = ref<HTMLElement>()
let pollingTimer: number | null = null
let scrollObserver: IntersectionObserver | null = null

const logOptions = ref({
  tail: 100,
  follow: false,
  since: undefined as string | undefined,
  until: undefined as string | undefined
})

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const containerId = computed(() => props.containerId)
const containerName = computed(() => props.containerId?.slice(0, 12) || '')

// 过滤后的日志
const filteredLogs = computed(() => {
  if (!searchKeyword.value) return logs.value

  const keyword = searchKeyword.value.toLowerCase()
  return logs.value.filter(log =>
    log.message.toLowerCase().includes(keyword) ||
    log.stream.toLowerCase().includes(keyword) ||
    log.timestamp.toLowerCase().includes(keyword)
  )
})

// 监听对话框打开
watch(visible, (newValue) => {
  if (newValue && containerId.value) {
    loadLogs()
  } else {
    stopPolling()
  }
})

// 监听容器ID变化
watch(containerId, (newId) => {
  if (newId && visible.value) {
    loadLogs()
  }
})

// 监听日志选项变化
watch(logOptions, () => {
  if (visible.value && containerId.value) {
    loadLogs()
  }
}, { deep: true })

// 生命周期
onMounted(() => {
  setupScrollObserver()
})

onUnmounted(() => {
  stopPolling()
  if (scrollObserver) {
    scrollObserver.disconnect()
  }
})

// 方法
const loadLogs = async () => {
  if (!containerId.value) return

  loading.value = true
  try {
    const params: any = {
      tail: logOptions.value.tail,
      follow: false
    }

    if (logOptions.value.since) {
      params.since = logOptions.value.since
    }

    if (logOptions.value.until) {
      params.until = logOptions.value.until
    }

    const response = await containerApi.getContainerLogs(containerId.value, params)
    logs.value = response.logs || []
    hasMore.value = response.hasMore || false

    await nextTick()
    scrollToBottom()
  } catch (error: any) {
    console.error('获取容器日志失败:', error)
    ElMessage.error(error.response?.data?.message || t('container.logsDialog.loadFailed'))
  } finally {
    loading.value = false
  }
}

const refreshLogs = () => {
  loadLogs()
}

const clearLogs = () => {
  logs.value = []
  ElMessage.success(t('container.logsDialog.clearSuccess'))
}

const downloadLogs = () => {
  if (logs.value.length === 0) {
    ElMessage.warning(t('container.logsDialog.noLogsToDownload'))
    return
  }

  const logContent = logs.value.map(log =>
    `${log.timestamp} [${log.stream.toUpperCase()}] ${log.message}`
  ).join('\n')

  const blob = new Blob([logContent], { type: 'text/plain' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `container-${containerId.value}-logs-${new Date().toISOString().slice(0, 19)}.txt`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)

  ElMessage.success(t('container.logsDialog.downloadSuccess'))
}

const handleFollowChange = (follow: boolean) => {
  if (follow) {
    startPolling()
  } else {
    stopPolling()
  }
}

const startPolling = () => {
  if (pollingTimer) return

  pollingTimer = window.setInterval(async () => {
    if (!containerId.value || !visible.value) return

    try {
      const response = await containerApi.getContainerLogs(containerId.value, {
        tail: 10,
        follow: false,
        since: new Date(Date.now() - 5000).toISOString() // 最近5秒的日志
      })

      if (response.data.logs && response.data.logs.length > 0) {
        logs.value.push(...response.data.logs)
        await nextTick()
        scrollToBottom()
      }
    } catch (error) {
      console.error('轮询日志失败:', error)
    }
  }, 2000) // 每2秒轮询一次
}

const stopPolling = () => {
  if (pollingTimer) {
    clearInterval(pollingTimer)
    pollingTimer = null
  }
}

const setupScrollObserver = () => {
  if (!loadMoreTrigger.value) return

  scrollObserver = new IntersectionObserver((entries) => {
    if (entries[0]?.isIntersecting && hasMore.value && !loading.value) {
      loadMoreLogs()
    }
  }, {
    root: logContainer.value,
    rootMargin: '100px'
  })

  scrollObserver.observe(loadMoreTrigger.value)
}

const loadMoreLogs = async () => {
  if (!containerId.value || loading.value) return

  loading.value = true
  try {
    const oldestTimestamp = logs.value[0]?.timestamp
    const response = await containerApi.getContainerLogs(containerId.value, {
      tail: 100,
      follow: false,
      until: oldestTimestamp
    })

    if (response.data.logs && response.data.logs.length > 0) {
      logs.value.unshift(...response.data.logs)
      hasMore.value = response.data.hasMore || false
    }
  } catch (error) {
    console.error('加载更多日志失败:', error)
  } finally {
    loading.value = false
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

const formatTimestamp = (timestamp: string) => {
  return formatLocalizedTime(timestamp, '--', {
    hour12: false,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

const handleClose = () => {
  visible.value = false
  stopPolling()
  logs.value = []
  searchKeyword.value = ''
  logOptions.value = {
    tail: 100,
    follow: false,
    since: undefined,
    until: undefined
  }
}
</script>

<style scoped>
.log-controls {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  padding: 12px;
  background-color: #f5f7fa;
  border-radius: 6px;
}

.control-left,
.control-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.log-content {
  height: 500px;
  overflow-y: auto;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  background-color: #000;
  color: #fff;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.4;
}

.loading-container,
.empty-container {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #909399;
}

.log-viewer {
  padding: 12px;
}

.log-entry {
  display: flex;
  align-items: flex-start;
  margin-bottom: 2px;
  word-break: break-all;
}

.log-timestamp {
  color: #909399;
  margin-right: 8px;
  flex-shrink: 0;
}

.log-stream {
  color: #409eff;
  margin-right: 8px;
  font-weight: bold;
  flex-shrink: 0;
  min-width: 40px;
}

.log-message {
  flex: 1;
  white-space: pre-wrap;
}

.log-error {
  color: #f56c6c;
}

.log-warning {
  color: #e6a23c;
}

.load-more {
  text-align: center;
  padding: 20px;
  color: #909399;
}

.follow-indicator {
  position: sticky;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.8);
  padding: 8px 12px;
  text-align: center;
  border-top: 1px solid #333;
}

:deep(mark) {
  background-color: #ff9800;
  color: #000;
  padding: 0 2px;
  border-radius: 2px;
}

.dialog-footer {
  text-align: right;
}

@media (max-width: 768px) {
  .log-controls {
    flex-direction: column;
    gap: 12px;
    align-items: stretch;
  }

  .control-left,
  .control-right {
    justify-content: center;
  }

  .log-content {
    height: 400px;
  }
}
</style>