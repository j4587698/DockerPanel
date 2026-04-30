<template>
  <div class="compose-page" v-loading="loading">
    <!-- Page Header -->
    <header class="page-header">
      <div class="header-content">
        <h1 class="page-title">{{ t('compose.title') }}</h1>
        <p class="page-subtitle">{{ t('compose.subtitle') }}</p>
      </div>
      <div class="header-actions">
        <button class="btn btn-primary" @click="openCreateDialog">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
          {{ t('compose.createProject') }}
        </button>
        <button class="btn btn-secondary" @click="refreshData">{{ t('common.refresh') }}</button>
      </div>
    </header>

    <!-- Toolbar -->
    <div class="toolbar">
      <div class="search-box">
        <svg class="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="11" cy="11" r="8"></circle>
          <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
        </svg>
        <input v-model="search" type="text" :placeholder="t('compose.searchPlaceholder')" class="search-input" />
      </div>
      <div class="stats">
        <span class="stat"><strong>{{ projects.length }}</strong> {{ t('compose.projectsCount') }}</span>
        <span class="stat"><strong>{{ runningCount }}</strong> {{ t('compose.running') }}</span>
      </div>
    </div>

    <!-- Data Table -->
    <div class="data-table" v-if="paginatedProjects.length > 0">
      <div class="table-header">
        <div class="th th-name">{{ t('compose.projectName') }}</div>
        <div class="th th-services">{{ t('compose.services') }}</div>
        <div class="th th-status">{{ t('common.status') }}</div>
        <div class="th th-created">{{ t('common.created') }}</div>
        <div class="th th-actions">{{ t('common.actions') }}</div>
      </div>

      <div v-for="project in paginatedProjects" :key="project.id || project.name" class="table-row" :class="project.status?.toLowerCase()">
        <div class="td td-name">
          <div class="project-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="3" y="3" width="7" height="7"></rect>
              <rect x="14" y="3" width="7" height="7"></rect>
              <rect x="14" y="14" width="7" height="7"></rect>
              <rect x="3" y="14" width="7" height="7"></rect>
            </svg>
          </div>
          <div class="name-info">
            <span class="name">{{ project.name }}</span>
            <code class="path">{{ project.path || t('compose.notDeployed') }}</code>
          </div>
        </div>

        <div class="td td-services">
          <span class="service-count">{{ project.services?.length || 0 }}</span>
        </div>

        <div class="td td-status">
          <span class="status-badge" :class="project.status?.toLowerCase() || 'unknown'">
            <span class="status-dot"></span>
            {{ getStatusText(project.status) }}
          </span>
        </div>

        <div class="td td-created">
          <span class="time">{{ formatDate(project.createdAt) }}</span>
        </div>

        <div class="td td-actions">
          <!-- 启动按钮：已停止/已创建状态显示 -->
          <button 
            v-if="['Stopped', 'Created'].includes(project.status)" 
            class="action-btn success" 
            @click="startProject(project)" 
            :title="t('compose.startProject')">
            <svg viewBox="0 0 24 24" fill="currentColor"><polygon points="5 3 19 12 5 21 5 3"></polygon></svg>
          </button>
          <!-- 停止按钮：运行中/部分运行状态显示 -->
          <button 
            v-if="['Running', 'PartiallyRunning'].includes(project.status)" 
            class="action-btn warning" 
            @click="stopProject(project)" 
            :title="t('compose.stopProject')">
            <svg viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="4" width="4" height="16"></rect><rect x="14" y="4" width="4" height="16"></rect></svg>
          </button>
          <!-- 部署按钮：错误/未知状态显示 -->
          <button 
            v-if="['Error', 'Unknown'].includes(project.status)" 
            class="action-btn success" 
            @click="deployProject(project)" 
            :title="t('compose.detail.deploy')">
            <svg viewBox="0 0 24 24" fill="currentColor"><polygon points="5 3 19 12 5 21 5 3"></polygon></svg>
          </button>
          <!-- 重启按钮：有容器运行时显示 -->
          <button 
            v-if="['Running', 'PartiallyRunning', 'Stopped'].includes(project.status)" 
            class="action-btn info" 
            @click="restartProject(project)" 
            :title="t('compose.restartProject')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 4 23 10 17 10"></polyline><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path></svg>
          </button>
          <button class="action-btn" @click="editProject(project)" :title="t('common.edit')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path></svg>
          </button>
          <button class="action-btn danger" @click="handleDelete(project)" :title="t('common.delete')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
          </button>
        </div>
      </div>
    </div>

    <!-- Pagination -->
    <div class="pagination" v-if="totalPages > 1">
      <div class="page-info">{{ t('compose.totalItems', { count: filteredProjects.length }) }}</div>
      <div class="page-controls">
        <button class="page-btn" :disabled="currentPage === 1" @click="currentPage--">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="15 18 9 12 15 6"></polyline></svg>
        </button>
        <span class="page-current">{{ currentPage }} / {{ totalPages }}</span>
        <button class="page-btn" :disabled="currentPage === totalPages" @click="currentPage++">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
        </button>
      </div>
    </div>

    <!-- Empty State -->
    <div v-if="!loading && filteredProjects.length === 0" class="empty-state">
      <div class="empty-icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <rect x="3" y="3" width="7" height="7"></rect>
          <rect x="14" y="3" width="7" height="7"></rect>
          <rect x="14" y="14" width="7" height="7"></rect>
          <rect x="3" y="14" width="7" height="7"></rect>
        </svg>
      </div>
      <h3 class="empty-title">{{ t('compose.empty.noProjects') }}</h3>
      <p class="empty-desc">{{ t('compose.empty.createFirst') }}</p>
      <button class="btn btn-primary" @click="openCreateDialog">{{ t('compose.createProject') }}</button>
    </div>

    <!-- Create/Edit Dialog -->
    <CreateComposeDialog
      v-model="showCreateDialog"
      :edit-compose="editingCompose"
      @success="handleDialogSuccess"
    />

    <!-- Operation Progress Dialog -->
    <el-dialog
      v-model="showProgressDialog"
      :title="progressTitle"
      width="480px"
      :close-on-click-modal="false"
      :close-on-press-escape="false"
      :show-close="false"
    >
      <div class="progress-content">
        <!-- 日志列表 -->
        <div class="log-container" ref="logContainerRef">
          <div 
            v-for="(log, index) in progressLogs" 
            :key="index" 
            class="log-item"
            :class="{ 'log-success': log.type === 'success', 'log-error': log.type === 'error' }"
          >
            <span class="log-time">{{ log.time }}</span>
            <span class="log-message">{{ log.message }}</span>
          </div>
        </div>
        
        <!-- 完成状态 -->
        <div v-if="progressComplete" class="progress-complete">
          <div class="complete-icon" :class="{ 'is-error': progressError }">
            <svg v-if="!progressError" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
              <polyline points="22 4 12 14.01 9 11.01"></polyline>
            </svg>
            <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="15" y1="9" x2="9" y2="15"></line>
              <line x1="9" y1="9" x2="15" y2="15"></line>
            </svg>
          </div>
          <span class="complete-text">{{ progressError ? t('common.error') : t('common.success') }}</span>
        </div>
        
        <!-- 关闭按钮 -->
        <div v-if="progressComplete" class="progress-actions">
          <button class="btn btn-primary" @click="closeProgressDialog">
            {{ t('common.close') }}
          </button>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { composeApi } from '@/api/compose'
import { signalrService } from '@/services/signalr'
import CreateComposeDialog from '@/components/compose/CreateComposeDialog.vue'
import { useSettingsStore } from '@/stores/settings'
import type { ComposeFile } from '@/types/compose'
import { formatLocalizedDate, formatLocalizedTime } from '@/utils/date'

const { t } = useI18n()
const settingsStore = useSettingsStore()

const loading = ref(false)
const search = ref('')
const projects = ref<any[]>([])
const showCreateDialog = ref(false)
const editingCompose = ref<ComposeFile | undefined>(undefined)
const currentPage = ref(1)
const pageSize = computed(() => settingsStore.defaultPageSize)

// 进度弹窗状态
const showProgressDialog = ref(false)
const progressTitle = ref('')
const progressLogs = ref<{ time: string; message: string; type?: 'info' | 'success' | 'error' }[]>([])
const progressComplete = ref(false)
const progressError = ref(false)
const currentProjectName = ref('')
const logContainerRef = ref<HTMLElement | null>(null)
let unsubscribeOperationProgress: (() => void) | null = null

const filteredProjects = computed(() => 
  projects.value.filter(p => p.name.toLowerCase().includes(search.value.toLowerCase()))
)

const totalPages = computed(() => Math.ceil(filteredProjects.value.length / pageSize.value))
const paginatedProjects = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredProjects.value.slice(start, start + pageSize.value)
})

const runningCount = computed(() => projects.value.filter(p => p.status === 'Running').length)

watch(search, () => { currentPage.value = 1 })
watch(pageSize, () => { currentPage.value = 1 })

const formatDate = (str: string) => {
  if (!str) return '-'
  return formatLocalizedDate(str, '-')
}

const getStatusText = (status: string) => {
  const statusMap: Record<string, string> = {
    Running: t('compose.detail.statusRunning'),
    PartiallyRunning: t('compose.detail.statusPartiallyRunning'),
    Stopped: t('compose.detail.statusStopped'),
    Created: t('compose.detail.statusCreated'),
    Error: t('compose.detail.statusError'),
    Deploying: t('compose.detail.statusDeploying'),
    Removing: t('compose.detail.statusRemoving'),
    Unknown: t('compose.detail.statusUnknown')
  }
  return statusMap[status] || status || t('compose.detail.statusUnknown')
}

const openCreateDialog = () => {
  editingCompose.value = undefined
  showCreateDialog.value = true
}

const refreshData = async () => {
  loading.value = true
  try {
    const res = await composeApi.getComposeFiles()
    projects.value = Array.isArray(res) ? res : []
  } catch {
    ElMessage.error(t('compose.loadFailed'))
  } finally {
    loading.value = false
  }
}

const deployProject = async (row: any) => {
  try {
    await composeApi.deployCompose({ composeFileId: row.id })
    ElMessage.success(t('compose.detail.deploySuccess'))
    refreshData()
  } catch { ElMessage.error(t('compose.detail.deployFailed')) }
}

const stopProject = async (row: any) => {
  // 确保 SignalR 连接已建立
  if (!signalrService.isConnected()) {
    try {
      await signalrService.connect()
      await new Promise(resolve => setTimeout(resolve, 100))
    } catch (e) {
      console.warn('SignalR 连接失败:', e)
    }
  }
  
  // 重置并显示进度弹窗
  progressLogs.value = []
  progressTitle.value = t('compose.progress.stopping', { name: row.name })
  progressComplete.value = false
  progressError.value = false
  currentProjectName.value = row.name
  showProgressDialog.value = true

  // 添加初始日志
  addLog(t('compose.progress.preparing'))

  try {
    const result = await composeApi.stopCompose({ composeFileId: row.id, operation: 'stop' })
    // 如果操作很快完成，可能还没收到进度推送，直接显示结果
    if (result.success && !progressComplete.value) {
      addLog(result.message || t('compose.detail.stopSuccess'), 'success')
      progressComplete.value = true
      refreshData()
    } else if (!result.success) {
      addLog(result.message || t('compose.detail.stopFailed'), 'error')
      progressError.value = true
      progressComplete.value = true
    }
  } catch (e: any) {
    addLog(e.message || t('compose.detail.stopFailed'), 'error')
    progressError.value = true
    progressComplete.value = true
  }
}

const startProject = async (row: any) => {
  // 确保 SignalR 连接已建立
  if (!signalrService.isConnected()) {
    try {
      await signalrService.connect()
      await new Promise(resolve => setTimeout(resolve, 100))
    } catch (e) {
      console.warn('SignalR 连接失败:', e)
    }
  }
  
  // 重置并显示进度弹窗
  progressLogs.value = []
  progressTitle.value = t('compose.progress.starting', { name: row.name })
  progressComplete.value = false
  progressError.value = false
  currentProjectName.value = row.name
  showProgressDialog.value = true

  // 添加初始日志
  addLog(t('compose.progress.preparing'))

  try {
    const result = await composeApi.startCompose({ composeFileId: row.id })
    // 如果操作很快完成，可能还没收到进度推送，直接显示结果
    if (result.success && !progressComplete.value) {
      addLog(result.message || t('compose.detail.startSuccess'), 'success')
      progressComplete.value = true
      refreshData()
    } else if (!result.success) {
      addLog(result.message || t('compose.detail.startFailed'), 'error')
      progressError.value = true
      progressComplete.value = true
    }
  } catch (e: any) {
    addLog(e.message || t('compose.detail.startFailed'), 'error')
    progressError.value = true
    progressComplete.value = true
  }
}

const restartProject = async (row: any) => {
  try {
    await composeApi.restartCompose({ composeFileId: row.id })
    ElMessage.success(t('compose.detail.restartSuccess'))
    refreshData()
  } catch { ElMessage.error(t('compose.detail.restartFailed')) }
}

const editProject = async (row: any) => {
  try {
    // 先获取完整的 compose 文件内容（包括 content 字段）
    const composeFile = await composeApi.getComposeFile(row.id, true)
    editingCompose.value = composeFile
    showCreateDialog.value = true
  } catch {
    ElMessage.error(t('compose.loadFailed'))
  }
}

const handleDelete = (row: any) => {
  ElMessageBox.confirm(t('compose.detail.deleteConfirm', { name: row.name })).then(async () => {
    await composeApi.deleteComposeFile(row.id)
    ElMessage.success(t('common.deleted'))
    refreshData()
  })
}

const handleDialogSuccess = () => {
  showCreateDialog.value = false
  editingCompose.value = undefined
  refreshData()
}

// 添加日志并滚动到底部
const addLog = (message: string, type: 'info' | 'success' | 'error' = 'info') => {
  const now = new Date()
  const time = formatLocalizedTime(now, '--', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
  progressLogs.value.push({ time, message, type })
  
  // 滚动到底部
  nextTick(() => {
    if (logContainerRef.value) {
      logContainerRef.value.scrollTop = logContainerRef.value.scrollHeight
    }
  })
}

// 关闭进度弹窗
const closeProgressDialog = () => {
  showProgressDialog.value = false
  progressLogs.value = []
}

// 处理操作进度更新
const handleOperationProgress = (message: any) => {
  const data = message.data
  if (data.projectName === currentProjectName.value) {
    // 添加日志
    const logMessage = data.detail ? `${data.step}: ${data.detail}` : data.step
    addLog(logMessage, data.progress === 100 ? 'success' : data.progress < 0 ? 'error' : 'info')
    
    if (data.progress === 100) {
      progressComplete.value = true
      refreshData()
    } else if (data.progress < 0) {
      progressError.value = true
      progressComplete.value = true
    }
  }
}

onMounted(async () => {
  refreshData()
  // 确保 SignalR 连接已建立
  if (!signalrService.isConnected()) {
    try {
      await signalrService.connect()
    } catch (e) {
      console.warn('SignalR 连接失败:', e)
    }
  }
  // 订阅操作进度
  unsubscribeOperationProgress = signalrService.subscribe('compose-operation-progress', handleOperationProgress)
})

onUnmounted(() => {
  if (unsubscribeOperationProgress) {
    unsubscribeOperationProgress()
  }
})
</script>

<style scoped>
.compose-page {
  padding: 24px 32px;
  max-width: 1600px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
    align-items: flex-start;
    margin-bottom: 24px;
  }
  
  .page-subtitle { margin: 6px 0 0 0; color: var(--text-muted); font-size: 14px; }
.header-actions { display: flex; gap: 10px; }

.btn-icon { width: 16px; height: 16px; }

.toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
}

.search-box {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  max-width: 320px;
  padding: 8px 12px;
  background: var(--bg-glass-dark);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.search-icon { width: 16px; height: 16px; color: var(--text-muted); }
.search-input { flex: 1; border: none; background: transparent; outline: none; font-size: 13px; }

.stats { display: flex; gap: 16px; margin-left: auto; font-size: 13px; color: var(--text-muted); }
.stats strong { color: var(--text-main); }

.data-table {
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
  overflow: hidden;
  overflow-x: auto;
}

.table-header {
  display: grid;
  grid-template-columns: minmax(280px, 2fr) 100px 120px 140px 160px;
  gap: 8px;
  padding: 12px 16px;
  background: var(--bg-glass-dark);
  border-bottom: 1px solid var(--border-color);
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
}

.table-row {
  display: grid;
  grid-template-columns: minmax(280px, 2fr) 100px 120px 140px 160px;
  gap: 8px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border-color-light);
  align-items: center;
  transition: all 0.15s ease;
}

.table-row:last-child { border-bottom: none; }
.table-row:hover { background: var(--bg-glass-dark); }
.table-row.running { border-left: 3px solid #22c55e; padding-left: 13px; }

.td-name { display: flex; align-items: center; gap: 12px; }

.project-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: linear-gradient(135deg, #3b82f6, #1d4ed8);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.project-icon svg { width: 18px; height: 18px; color: #fff; }

.name-info { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.name { font-weight: 600; font-size: 14px; color: var(--text-main); }
.path { font-size: 10px; font-family: 'JetBrains Mono', monospace; color: var(--text-muted); }

.service-count {
  font-family: 'JetBrains Mono', monospace;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-secondary);
  background: var(--bg-subtle);
  padding: 4px 12px;
  border-radius: 6px;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 500;
}

.status-dot { width: 7px; height: 7px; border-radius: 50%; }
.status-badge.running { color: #16a34a; }
.status-badge.running .status-dot { background: #22c55e; box-shadow: 0 0 6px rgba(34, 197, 94, 0.5); }
.status-badge.partiallyrunning { color: #d97706; }
.status-badge.partiallyrunning .status-dot { background: #f59e0b; box-shadow: 0 0 6px rgba(245, 158, 11, 0.5); }
.status-badge.stopped { color: var(--text-muted); }
.status-badge.stopped .status-dot { background: var(--text-muted); }
.status-badge.created { color: #0ea5e9; }
.status-badge.created .status-dot { background: #0ea5e9; box-shadow: 0 0 6px rgba(14, 165, 233, 0.5); }
.status-badge.error { color: #dc2626; }
.status-badge.error .status-dot { background: #ef4444; box-shadow: 0 0 6px rgba(239, 68, 68, 0.5); }
.status-badge.deploying, .status-badge.removing { color: #8b5cf6; }
.status-badge.deploying .status-dot, .status-badge.removing .status-dot { background: #8b5cf6; box-shadow: 0 0 6px rgba(139, 92, 246, 0.5); }
.status-badge.unknown { color: #d97706; }
.status-badge.unknown .status-dot { background: #f59e0b; }
.status-badge.unknown .status-dot { background: #f59e0b; }

.time { font-size: 12px; color: var(--text-muted); }

.td-actions { display: flex; gap: 4px; justify-content: center; width: 100%; }
.th-actions { text-align: center; }

.action-btn {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  background: var(--bg-surface);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.15s ease;
  color: var(--text-muted);
}

.action-btn svg { width: 14px; height: 14px; }
.action-btn:hover { border-color: #3b82f6; color: #3b82f6; }
.action-btn.success:hover { border-color: #22c55e; color: #22c55e; background: rgba(34, 197, 94, 0.1); }
.action-btn.warning:hover { border-color: #f59e0b; color: #f59e0b; background: rgba(245, 158, 11, 0.1); }
.action-btn.danger:hover { border-color: #ef4444; color: #ef4444; background: rgba(239, 68, 68, 0.1); }
.action-btn.info:hover { border-color: #8b5cf6; color: #8b5cf6; background: rgba(139, 92, 246, 0.1); }

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 0;
  font-size: 13px;
  color: var(--text-muted);
}

.page-controls { display: flex; align-items: center; gap: 8px; }
.page-current { font-weight: 500; }

.page-btn {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  background: var(--bg-surface);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: var(--text-muted);
}

.page-btn svg { width: 16px; height: 16px; }
.page-btn:hover:not(:disabled) { border-color: #3b82f6; color: #3b82f6; }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 60px 40px;
  text-align: center;
}

.empty-icon { width: 64px; height: 64px; margin-bottom: 16px; color: var(--text-secondary); }
.empty-title { font-size: 18px; font-weight: 600; color: var(--text-secondary); margin: 0 0 8px 0; }
.empty-desc { font-size: 14px; color: var(--text-muted); margin: 0 0 24px 0; }

@media (max-width: 1024px) {
  .th-created, .td-created { display: none; }
  .table-header, .table-row { grid-template-columns: minmax(200px, 2fr) 80px 100px 140px; }
}

@media (max-width: 768px) {
  .compose-page { padding: 16px; }
  .page-header { flex-direction: column; gap: 12px; }
  .toolbar { flex-wrap: wrap; }
  .search-box { max-width: none; width: 100%; }
  .th-services, .td-services { display: none; }
  .table-header, .table-row { grid-template-columns: 1fr 100px 120px; }
}

@media (max-width: 480px) {
  .compose-page { padding: 12px; }
  /* Only name + actions */
  .th-services, .td-services, .th-created, .td-created, .th-status, .td-status { display: none; }
  .table-header, .table-row { grid-template-columns: 1fr 110px; }
  .project-icon { width: 30px; height: 30px; }
  .empty-state { padding: 40px 20px; }
}

/* 进度弹窗样式 */
.progress-content {
  padding: 0;
}

.log-container {
  max-height: 300px;
  overflow-y: auto;
  background: var(--bg-glass-dark);
  border-radius: 8px;
  border: 1px solid var(--border-color);
  padding: 12px;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  font-size: 13px;
}

.log-item {
  display: flex;
  gap: 12px;
  padding: 6px 0;
  border-bottom: 1px solid var(--border-color-light);
  color: var(--text-secondary);
}

.log-item:last-child {
  border-bottom: none;
}

.log-time {
  color: var(--text-muted);
  font-size: 12px;
  flex-shrink: 0;
}

.log-message {
  word-break: break-all;
}

.log-success .log-message {
  color: #22c55e;
}

.log-error .log-message {
  color: #ef4444;
}

.progress-complete {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  margin-top: 20px;
  padding: 16px;
  background: var(--bg-subtle);
  border-radius: 8px;
}

.complete-icon {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: #22c55e;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
}

.complete-icon.is-error {
  background: #ef4444;
}

.complete-icon svg {
  width: 18px;
  height: 18px;
}

.complete-text {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-main);
}

.progress-actions {
  display: flex;
  justify-content: center;
  margin-top: 20px;
}

</style>

<style>
/* === Dark Mode === */
html.dark .toolbar, html.dark .data-table { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-box { background: #0f172a; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-input { color: #f1f5f9; }
html.dark .table-header { background: #0f172a; color: #94a3b8; }
html.dark .table-row { border-color: rgba(255, 255, 255, 0.05); }
html.dark .table-row:hover { background: rgba(255, 255, 255, 0.03); }
html.dark .name { color: #f1f5f9; }
html.dark .stats strong { color: #f1f5f9; }
html.dark .service-count { background: rgba(255, 255, 255, 0.1); }
html.dark .action-btn, html.dark .page-btn { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
html.dark .log-container { background: #0f172a; border-color: rgba(255, 255, 255, 0.1); }
html.dark .log-item { border-color: rgba(255, 255, 255, 0.05); }
html.dark .progress-complete { background: rgba(255, 255, 255, 0.05); }
html.dark .complete-text { color: #f1f5f9; }
</style>
