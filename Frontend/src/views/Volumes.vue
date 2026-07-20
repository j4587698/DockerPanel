<template>
  <div class="volumes-page" v-loading="loading">
    <!-- Page Header -->
    <header class="page-header">
      <div class="header-content">
        <h1 class="page-title">{{ t('volume.title') }}</h1>
        <p class="page-subtitle">{{ t('volume.subtitle') }}</p>
      </div>
      <div class="header-actions">
        <button class="btn btn-primary" @click="showCreateDialog = true">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
          {{ t('volume.create') }}
        </button>
        <button class="btn btn-secondary" @click="showRestoreDialog = true">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
            <polyline points="17 8 12 3 7 8"></polyline>
            <line x1="12" y1="3" x2="12" y2="15"></line>
          </svg>
          {{ t('volume.restoreFromArchive') }}
        </button>
        <button class="btn btn-secondary" @click="refreshData" :disabled="loading">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="23 4 23 10 17 10"></polyline>
            <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path>
          </svg>
          {{ t('common.refresh') }}
        </button>
      </div>
    </header>

    <!-- Toolbar -->
    <div class="toolbar">
      <div class="search-box">
        <svg class="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="11" cy="11" r="8"></circle>
          <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
        </svg>
        <input v-model="searchQuery" type="text" :placeholder="t('volume.searchPlaceholder')" class="search-input" />
      </div>
      <div class="stats">
        <span class="stat"><strong>{{ volumes.length }}</strong> {{ t('volume.volumesCount') }}</span>
      </div>
      <button class="btn btn-danger-outline" @click="handlePruneVolumes">{{ t('volume.pruneUnused') }}</button>
    </div>

    <!-- Data Table -->
    <el-table
      v-if="paginatedVolumes.length > 0"
      :data="paginatedVolumes"
      style="width: 100%"
      v-loading="loading"
    >
      <el-table-column :label="t('common.name')" min-width="280" sortable :sort-method="(a: any, b: any) => toggleSortCompare('name', a, b)">
        <template #default="{ row }">
          <div class="td-name" @click="openDetail(row)" style="cursor: pointer;">
            <div class="volume-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path>
              </svg>
            </div>
            <div class="name-info">
              <span class="name clickable" @click="openDetail(row)">{{ row.name }}</span>
              <code class="id">{{ row.id?.substring(0, 12) || '-' }}</code>
            </div>
          </div>
        </template>
      </el-table-column>

      <el-table-column :label="t('volume.driver')" width="120" align="center">
        <template #default="{ row }">
          <span class="driver-badge">{{ row.driver || 'local' }}</span>
        </template>
      </el-table-column>

      <el-table-column :label="t('common.created')" min-width="160" sortable :sort-method="(a: any, b: any) => toggleSortCompare('created', a, b)">
        <template #default="{ row }">
          <span class="time">{{ formatDate(row.created) }}</span>
        </template>
      </el-table-column>

      <el-table-column :label="t('volume.usage')" width="110" align="center" sortable :sort-method="(a: any, b: any) => toggleSortCompare('usageCount', a, b)">
        <template #default="{ row }">
          <span class="usage-badge" :class="getUsageClass(row)">
            {{ row.usageCount ?? '-' }}
          </span>
        </template>
      </el-table-column>

      <el-table-column :label="t('common.actions')" width="200" align="center" fixed="right">
        <template #default="{ row }">
          <div class="actions-cell">
              <el-button class="table-action-btn browse" :icon="Folder" :title="t('volume.browseFiles')" @click="openFileManager(row)" />
              <el-button class="table-action-btn download" :icon="Download" :title="t('volume.downloadVolume')" @click="downloadVolume(row)" />
              <el-button class="table-action-btn detail" :icon="View" :title="t('common.details')" @click="openDetail(row)" />
              <el-button class="table-action-btn danger" :icon="Delete" :title="t('common.delete')" @click="handleDeleteVolume(row)" />
          </div>
        </template>
      </el-table-column>
    </el-table>

    <!-- Pagination -->
    <div class="pagination" v-if="totalPages > 1">
      <div class="page-info">共 {{ filteredVolumes.length }} 条</div>
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
    <div v-if="!loading && filteredVolumes.length === 0" class="empty-state">
      <div class="empty-icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path>
        </svg>
      </div>
      <h3 class="empty-title">{{ t('volume.empty.noVolumes') }}</h3>
      <p class="empty-desc">{{ t('volume.empty.createFirst') }}</p>
      <button class="btn btn-primary" @click="showCreateDialog = true">{{ t('volume.create') }}</button>
    </div>

    <!-- Create Dialog -->
    <el-dialog v-model="showCreateDialog" :title="t('volume.create')" width="480px" class="modern-dialog">
      <el-form :model="createForm" label-position="top">
        <el-form-item :label="t('common.name')">
          <el-input v-model="createForm.name" :placeholder="t('volume.createForm.namePlaceholder')" />
        </el-form-item>
        <el-form-item :label="t('volume.driver')">
          <el-select v-model="createForm.driver" style="width: 100%">
            <el-option label="local" value="local" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <div class="dialog-footer">
          <el-button @click="showCreateDialog = false">{{ t('common.cancel') }}</el-button>
          <el-button type="primary" @click="createVolume">{{ t('common.confirm') }}</el-button>
        </div>
      </template>
    </el-dialog>

    <!-- Restore from Archive Dialog -->
    <el-dialog v-model="showRestoreDialog" :title="t('volume.restoreFromArchive')" width="480px" class="modern-dialog">
      <el-form :model="restoreForm" label-position="top">
        <el-form-item :label="t('volume.volumeName')">
          <el-input v-model="restoreForm.volumeName" :placeholder="t('volume.restoreVolumeNamePlaceholder')" />
        </el-form-item>
        <el-form-item :label="t('volume.archiveFile')">
          <el-upload
            ref="restoreUploadRef"
            :auto-upload="false"
            :limit="1"
            accept=".tar.gz,.tgz"
            :on-change="handleRestoreFileChange"
            :on-remove="handleRestoreFileRemove"
          >
            <template #trigger>
              <el-button type="primary">{{ t('volume.selectArchive') }}</el-button>
            </template>
            <template #tip>
              <div class="el-upload__tip">{{ t('volume.archiveFileTip') }}</div>
            </template>
          </el-upload>
        </el-form-item>
      </el-form>
      <template #footer>
        <div class="dialog-footer">
          <el-button @click="showRestoreDialog = false">{{ t('common.cancel') }}</el-button>
          <el-button type="primary" @click="restoreFromArchive" :loading="restoreLoading">{{ t('common.confirm') }}</el-button>
        </div>
      </template>
    </el-dialog>

    <!-- Volume Detail Drawer -->
    <el-drawer v-model="showDetailDrawer" :title="t('volume.detailTitle')" size="480px" class="detail-drawer">
      <div v-if="detailLoading" class="detail-loading">
        <el-icon class="is-loading"><Loading /></el-icon>
        <span>{{ t('common.loading') }}</span>
      </div>
      <div v-else-if="volumeDetail" class="detail-content">
        <!-- 文件管理按钮 -->
        <div class="detail-actions">
          <button class="btn btn-primary" @click="openFileManagerFromDrawer" style="width: 100%;">
            <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width: 16px; height: 16px;">
              <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path>
            </svg>
            {{ t('volume.browseFiles') }}
          </button>
        </div>

        <div class="detail-section">
          <div class="detail-row">
            <span class="label">{{ t('common.name') }}</span>
            <span class="value">{{ volumeDetail.name }}</span>
          </div>
          <div class="detail-row">
            <span class="label">{{ t('volume.driver') }}</span>
            <span class="value">{{ volumeDetail.driver || 'local' }}</span>
          </div>
          <div class="detail-row">
            <span class="label">{{ t('volume.mountpoint') }}</span>
            <code class="value mono">{{ volumeDetail.mountpoint || '-' }}</code>
          </div>
          <div class="detail-row">
            <span class="label">{{ t('common.created') }}</span>
            <span class="value">{{ formatDateTime(volumeDetail.createdAt) }}</span>
          </div>
          <div class="detail-row">
            <span class="label">{{ t('volume.scope') }}</span>
            <span class="value">{{ volumeDetail.scope || 'local' }}</span>
          </div>
          <div class="detail-row">
            <span class="label">{{ t('common.size') }}</span>
            <span class="value">{{ formatSize(volumeDetail.usage?.size) }}</span>
          </div>
        </div>

        <div class="detail-section" v-if="volumeDetail.labels && Object.keys(volumeDetail.labels).length > 0">
          <h4>{{ t('volume.labels') }}</h4>
          <div class="label-list">
            <div v-for="(value, key) in volumeDetail.labels" :key="key" class="label-item">
              <span class="label-key">{{ key }}</span>
              <span class="label-value">{{ value }}</span>
            </div>
          </div>
        </div>

        <div class="detail-section">
          <h4>{{ t('volume.usageInfo') }} <span class="usage-count">({{ volumeDetail.mounts?.length || 0 }} {{ t('volume.containers') }})</span></h4>
          <div v-if="volumeDetail.mounts && volumeDetail.mounts.length > 0" class="mounts-list">
            <div v-for="mount in volumeDetail.mounts" :key="mount.containerId" class="mount-item">
              <div class="mount-header">
                <span class="container-name">{{ mount.containerName }}</span>
                <span class="mount-mode" :class="{ readonly: mount.mode === 'ro' }">{{ mount.mode || 'rw' }}</span>
              </div>
              <div class="mount-paths">
                <code>{{ mount.destination }}</code>
              </div>
            </div>
          </div>
          <div v-else class="no-mounts">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="12" y1="8" x2="12" y2="12"></line>
              <line x1="12" y1="16" x2="12.01" y2="16"></line>
            </svg>
            <span>{{ t('volume.noContainersUsing') }}</span>
          </div>
        </div>
      </div>
    </el-drawer>

    <!-- Volume File Manager Dialog -->
    <VolumeFileManagerDialog 
      v-model="showFileManager" 
      :volume-id="currentVolumeId" 
      :volume-name="currentVolumeName" 
    />

    <!-- Cleanup Volumes Dialog -->
    <CleanupVolumesDialog 
      v-model="showCleanupDialog"
      @success="refreshData"
    />

  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useVolumesStore } from '@/stores/volumes'
import { useTasksStore } from '@/stores/tasks'
import { useSettingsStore } from '@/stores/settings'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Loading, Folder, Download, View, Delete } from '@element-plus/icons-vue'
import { volumeApi } from '@/api/volumes'
import VolumeFileManagerDialog from '@/components/volume/VolumeFileManagerDialog.vue'
import CleanupVolumesDialog from '@/components/volume/CleanupVolumesDialog.vue'
import { formatLocalizedDate, formatLocalizedDateTime as formatLocaleDateTime } from '@/utils/date'

const { t } = useI18n()
const store = useVolumesStore()
const tasksStore = useTasksStore()
const settingsStore = useSettingsStore()
const loading = ref(false)
const searchQuery = ref('')
const showCreateDialog = ref(false)
const createForm = ref({ name: '', driver: 'local' })
const currentPage = ref(1)
const pageSize = computed(() => settingsStore.defaultPageSize)

// Detail drawer
const showDetailDrawer = ref(false)
const detailLoading = ref(false)
const volumeDetail = ref<any>(null)

// File manager
const showFileManager = ref(false)
const currentVolumeId = ref('')
const currentVolumeName = ref('')

// Cleanup dialog
const showCleanupDialog = ref(false)

// Restore from archive
const showRestoreDialog = ref(false)
const restoreForm = ref({ volumeName: '', archiveFile: null as File | null })
const restoreLoading = ref(false)

// 排序状态
const sortField = ref('created')
const sortOrder = ref<'asc' | 'desc'>('desc')

const volumes = computed(() => store.volumes)
const filteredVolumes = computed(() => {
  let result = volumes.value
  
  // 搜索过滤
  if (searchQuery.value) {
    result = result.filter((v: any) => 
      v.name.toLowerCase().includes(searchQuery.value.toLowerCase())
    )
  }
  
  // 排序
  result = [...result].sort((a: any, b: any) => {
    let aVal: any, bVal: any
    
    switch (sortField.value) {
      case 'name':
        aVal = a.name || ''
        bVal = b.name || ''
        break
      case 'created':
        aVal = new Date(a.created || 0).getTime()
        bVal = new Date(b.created || 0).getTime()
        break
      case 'usageCount':
        aVal = a.usageCount ?? 0
        bVal = b.usageCount ?? 0
        break
      default:
        aVal = a.name || ''
        bVal = b.name || ''
    }
    
    if (typeof aVal === 'string') {
      return sortOrder.value === 'asc' 
        ? aVal.localeCompare(bVal) 
        : bVal.localeCompare(aVal)
    }
    return sortOrder.value === 'asc' ? aVal - bVal : bVal - aVal
  })
  
  return result
})

const totalPages = computed(() => Math.ceil(filteredVolumes.value.length / pageSize.value))
const paginatedVolumes = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredVolumes.value.slice(start, start + pageSize.value)
})

watch(searchQuery, () => { currentPage.value = 1 })
watch(pageSize, () => { currentPage.value = 1 })

const toggleSort = (field: string) => {
  if (sortField.value === field) {
    sortOrder.value = sortOrder.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortField.value = field
    sortOrder.value = 'desc'
  }
}

const toggleSortCompare = (field: string, a: any, b: any) => {
  let aVal: any, bVal: any
  switch (field) {
    case 'name':
      aVal = a.name || ''
      bVal = b.name || ''
      break
    case 'created':
      aVal = new Date(a.created || 0).getTime()
      bVal = new Date(b.created || 0).getTime()
      break
    case 'usageCount':
      aVal = a.usageCount ?? 0
      bVal = b.usageCount ?? 0
      break
    default:
      aVal = a.name || ''
      bVal = b.name || ''
  }
  const base = typeof aVal === 'string' ? aVal.localeCompare(bVal) : aVal - bVal
  return sortOrder.value === 'asc' ? base : -base
}

const formatDate = (str: string) => {
  if (!str) return '-'
  const date = new Date(str)
  if (isNaN(date.getTime())) return '-'
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))
  if (days === 0) return t('volume.date.today')
  if (days === 1) return t('volume.date.yesterday')
  if (days < 7) return t('volume.date.daysAgo', { count: days })
  return formatLocalizedDate(date, '-')
}

const formatDateTime = (str: string) => {
  if (!str) return '-'
  return formatLocaleDateTime(str, '-')
}

const formatSize = (bytes: number) => {
  if (!bytes || bytes === 0) return '-'
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  if (bytes < 1024 * 1024 * 1024) return (bytes / 1024 / 1024).toFixed(1) + ' MB'
  return (bytes / 1024 / 1024 / 1024).toFixed(2) + ' GB'
}

const getUsageClass = (volume: any) => {
  const count = volume.usageCount ?? 0
  if (count === 0) return 'unused'
  if (count < 3) return 'low'
  return 'high'
}

const refreshData = async () => {
  loading.value = true
  try { await store.fetchVolumes() } finally { loading.value = false }
}

const openDetail = async (volume: any) => {
  showDetailDrawer.value = true
  detailLoading.value = true
  volumeDetail.value = null
  currentVolumeId.value = volume.name
  currentVolumeName.value = volume.name
  
  try {
    const response = await volumeApi.getVolume(volume.name)
    volumeDetail.value = response as any
  } catch (error: any) {
    ElMessage.error(t('volume.loadDetailFailed') + ': ' + error.message)
    showDetailDrawer.value = false
  } finally {
    detailLoading.value = false
  }
}

const openFileManager = (volume: any) => {
  currentVolumeId.value = volume.name
  currentVolumeName.value = volume.name
  showFileManager.value = true
}

// 下载用的 iframe 引用
let downloadIframe: HTMLIFrameElement | null = null

// 移除下载 iframe
const removeDownloadIframe = () => {
  if (downloadIframe && downloadIframe.parentNode) {
    downloadIframe.parentNode.removeChild(downloadIframe)
    downloadIframe = null
  }
}

const downloadVolume = async (volume: any) => {
  // 使用隐藏 iframe 触发下载，进度显示在后台任务列表中
  const downloadUrl = `/api/volumes/${encodeURIComponent(volume.name)}/files/download?path=/&archive=true`
  downloadIframe = document.createElement('iframe')
  downloadIframe.style.display = 'none'
  downloadIframe.src = downloadUrl
  document.body.appendChild(downloadIframe)
  
  ElMessage.success(t('common.tasksDownloadStarted'))
}

const openFileManagerFromDrawer = () => {
  showDetailDrawer.value = false
  showFileManager.value = true
}

const createVolume = async () => {
  try {
    await store.createVolume(createForm.value)
    ElMessage.success(t('volume.createSuccess'))
    showCreateDialog.value = false
    createForm.value = { name: '', driver: 'local' }
    refreshData()
  } catch {
    ElMessage.error(t('volume.createFailed'))
  }
}

const handleDeleteVolume = (row: any) => {
  ElMessageBox.confirm(t('volume.deleteConfirm', { name: row.name })).then(async () => {
    await store.deleteVolume(row.id || row.name)
    ElMessage.success(t('common.deleted'))
    refreshData()
  })
}

const handlePruneVolumes = () => {
  showCleanupDialog.value = true
}

// 从归档恢复卷
const handleRestoreFileChange = (file: any) => {
  restoreForm.value.archiveFile = file.raw
}

const handleRestoreFileRemove = () => {
  restoreForm.value.archiveFile = null
}

const restoreFromArchive = async () => {
  if (!restoreForm.value.archiveFile) {
    ElMessage.warning(t('volume.archiveFileRequired'))
    return
  }

  const volumeName = restoreForm.value.volumeName || 'auto-generated'
  const taskId = `volume-restore-${Date.now()}`
  
  // 添加任务到全局列表
  tasksStore.addTask({
    id: taskId,
    type: 'volume-archive',
    title: `恢复卷: ${volumeName}`,
    status: 'running',
    progress: 0,
    detail: '正在上传归档文件...'
  })
  
  restoreLoading.value = true
  
  try {
    const result = await volumeApi.restoreFromArchive(restoreForm.value.volumeName, restoreForm.value.archiveFile)
    
    // 更新任务状态
    tasksStore.updateTask(taskId, {
      progress: 100,
      status: 'completed',
      detail: '恢复完成'
    })
    
    ElMessage.success(t('volume.restoreSuccess'))
    showRestoreDialog.value = false
    restoreForm.value = { volumeName: '', archiveFile: null }
    refreshData()
  } catch (error: any) {
    console.error('Restore volume from archive failed:', error)
    tasksStore.updateTask(taskId, {
      status: 'failed',
      detail: error.response?.data?.error || error.message || 'Unknown error'
    })
    ElMessage.error(t('volume.restoreFailed') + ': ' + (error.message || 'Unknown error'))
  } finally {
    restoreLoading.value = false
  }
}

onMounted(() => {
  refreshData()
})
</script>

<style scoped>
.volumes-page {
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
}

.td-name { display: flex; align-items: center; gap: 12px; }

.volume-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: linear-gradient(135deg, #f97316, #ea580c);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.volume-icon svg { width: 18px; height: 18px; color: #fff; }

.name-info { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.name { font-weight: 600; font-size: 14px; color: var(--text-main); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.name.clickable { cursor: pointer; transition: color 0.15s ease; }
.name.clickable:hover { color: var(--color-primary); }
.id { font-size: 10px; font-family: 'JetBrains Mono', monospace; color: var(--text-muted); }

.driver-badge {
  font-family: 'JetBrains Mono', monospace;
  font-size: 11px;
  padding: 4px 10px;
  background: var(--bg-subtle);
  border-radius: 6px;
  color: var(--text-secondary);
}

.time { font-size: 12px; color: var(--text-muted); }

.td-usage { text-align: center; }
.th-usage { text-align: center; }

.usage-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 28px;
  height: 22px;
  padding: 0 8px;
  border-radius: 11px;
  font-size: 12px;
  font-weight: 600;
}

.usage-badge.unused {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.usage-badge.low {
  background: rgba(34, 197, 94, 0.1);
  color: #22c55e;
}

.usage-badge.high {
  background: rgba(59, 130, 246, 0.1);
  color: #3b82f6;
}

.td-actions { display: flex; gap: 4px; justify-content: center; width: 100%; }
.th-actions { text-align: center; }

.actions-cell {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 8px;
  width: 100%;
}

.actions-cell :deep(.table-action-btn) {
  width: 30px;
  height: 30px;
  min-width: 30px;
  padding: 0;
  border-radius: 6px;
  background: var(--bg-surface);
  border-color: var(--border-color);
  color: var(--text-secondary);
}

.actions-cell :deep(.table-action-btn:hover) {
  background: var(--bg-subtle);
  border-color: var(--border-color);
}

.actions-cell :deep(.table-action-btn.browse:hover),
.actions-cell :deep(.table-action-btn.download:hover),
.actions-cell :deep(.table-action-btn.detail:hover) {
  color: var(--color-primary);
}

.actions-cell :deep(.table-action-btn.danger:hover) {
  color: var(--color-danger);
}

.actions-cell :deep(.el-button + .el-button) {
  margin-left: 0;
}

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
.page-btn:hover:not(:disabled) { border-color: #f97316; color: #f97316; }
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

.modern-dialog :deep(.el-dialog__footer) { display: flex; gap: 10px; justify-content: flex-end; }

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

/* Detail Drawer */
.detail-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 40px;
  color: var(--text-muted);
}

.detail-loading .el-icon { font-size: 24px; }

.detail-content { padding: 0 16px; }

.detail-actions {
  margin-bottom: 20px;
}

.detail-section {
  margin-bottom: 24px;
  padding-bottom: 20px;
  border-bottom: 1px solid var(--border-color-light);
}

.detail-section:last-child {
  border-bottom: none;
  margin-bottom: 0;
  padding-bottom: 0;
}

.detail-section h4 {
  margin: 0 0 12px 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
}

.detail-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 8px 0;
}

.detail-row .label {
  font-size: 13px;
  color: var(--text-muted);
  flex-shrink: 0;
  min-width: 80px;
}

.detail-row .value {
  font-size: 13px;
  color: var(--text-main);
  text-align: right;
  word-break: break-all;
}

.detail-row .value.mono {
  font-family: 'JetBrains Mono', monospace;
  font-size: 11px;
  background: var(--bg-subtle);
  padding: 4px 8px;
  border-radius: 4px;
}

.label-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.label-item {
  display: flex;
  gap: 8px;
  font-size: 12px;
}

.label-key {
  font-weight: 500;
  color: var(--text-secondary);
}

.label-value {
  color: var(--text-muted);
}

.usage-count {
  font-weight: 400;
  color: var(--text-muted);
}

.mounts-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.mount-item {
  padding: 12px;
  background: var(--bg-subtle);
  border-radius: 8px;
}

.mount-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}

.container-name {
  font-weight: 600;
  font-size: 13px;
  color: var(--text-main);
}

.mount-mode {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 4px;
  background: rgba(34, 197, 94, 0.1);
  color: #22c55e;
}

.mount-mode.readonly {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.mount-paths code {
  font-size: 11px;
  color: var(--text-muted);
  font-family: 'JetBrains Mono', monospace;
}

.no-mounts {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 24px;
  color: var(--text-muted);
  font-size: 13px;
}

.no-mounts svg {
  width: 32px;
  height: 32px;
  opacity: 0.5;
}

@media (max-width: 768px) {
  .volumes-page { padding: 16px; }
  .page-header { flex-direction: column; gap: 12px; }
  .toolbar { flex-wrap: wrap; }
  .search-box { max-width: none; width: 100%; }
}

</style>

<style>
/* === Dark Mode === */
html.dark .toolbar, html.dark .data-table { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-box { background: #0f172a; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-input { color: #f1f5f9; }
html.dark .name { color: #f1f5f9; }
html.dark .stats strong { color: #f1f5f9; }
html.dark .driver-badge { background: rgba(255, 255, 255, 0.1); }
html.dark .page-btn { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
</style>