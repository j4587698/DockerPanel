<template>
  <div class="images-page" v-loading="loading">
    <!-- Page Header -->
    <header class="page-header">
      <div class="header-content">
        <h1 class="page-title">{{ t('image.title') }}</h1>
        <p class="page-subtitle">{{ t('image.subtitle') }}</p>
      </div>
      <div class="header-actions">
        <button class="btn btn-primary" @click="showPull = true">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
            <polyline points="7 10 12 15 17 10"></polyline>
            <line x1="12" y1="15" x2="12" y2="3"></line>
          </svg>
          {{ t('image.pull') }}
        </button>
        <button class="btn btn-secondary" @click="showImport = true">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
            <polyline points="17 8 12 3 7 8"></polyline>
            <line x1="12" y1="3" x2="12" y2="15"></line>
          </svg>
          {{ t('image.import') }}
        </button>
        <button class="btn btn-secondary" @click="showBuild = true">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polygon points="12 2 2 7 12 12 22 7 12 2"></polygon>
            <polyline points="2 17 12 22 22 17"></polyline>
            <polyline points="2 12 12 17 22 12"></polyline>
          </svg>
          {{ t('image.build') }}
        </button>
        <button class="btn btn-secondary" @click="showRegistry = true">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
            <line x1="3" y1="9" x2="21" y2="9"></line>
            <line x1="9" y1="21" x2="9" y2="9"></line>
          </svg>
          {{ t('image.registryManager') }}
        </button>
        <button class="btn btn-secondary" @click="refreshData">
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
        <input 
          v-model="search" 
          type="text" 
          :placeholder="t('image.searchPlaceholder')" 
          class="search-input"
        />
      </div>
      <div class="actions">
        <button 
          v-show="selectedIds.length > 0"
          class="btn btn-danger btn-sm"
          @click="handleBatchDelete"
        >
          {{ t('image.batchDelete') }} ({{ selectedIds.length }})
        </button>
        <button class="btn btn-secondary btn-sm" @click="handlePrune">
          {{ t('image.cleanupUnused') }}
        </button>
      </div>
      <div class="stats">
        {{ t('image.totalCount') }} <strong>{{ filteredImages.length }}</strong> {{ t('image.imagesUnit') }}，{{ t('image.totalSize') }} <strong>{{ formatSize(totalSize) }}</strong>
      </div>
    </div>

    <!-- Data Table -->
    <div class="data-table" v-if="paginatedImages.length > 0">
      <!-- Table Header -->
      <div class="table-header">
        <div class="th th-checkbox">
          <input type="checkbox" @change="toggleSelectAll" :checked="isAllSelected" />
        </div>
        <div class="th th-repo">{{ t('image.image') }}</div>
        <div class="th th-id">{{ t('common.id') }}</div>
        <div class="th th-size">{{ t('common.size') }}</div>
        <div class="th th-created">{{ t('common.created') }}</div>
        <div class="th th-actions">{{ t('common.actions') }}</div>
      </div>

      <!-- Table Rows -->
      <div 
        v-for="image in paginatedImages"
        :key="image.id"
        class="table-row"
        :class="{ selected: selectedIds.includes(image.id) }"
      >
        <div class="td td-checkbox">
          <input type="checkbox" :checked="selectedIds.includes(image.id)" @change="toggleSelect(image.id)" />
        </div>
        
        <div class="td td-repo">
          <div class="repo-icon" :class="{ 'is-none': image.repository === '<none>', 'is-unused': !image.isUsed }">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path>
            </svg>
          </div>
          <div class="repo-info">
            <span class="repo-name clickable" @click="handleDetail(image)" :title="image.repository === '<none>' ? t('image.noTag') : `${image.repository}:${image.tag}`">
              {{ image.repository === '<none>' ? t('image.noTag') : image.repository }}
            </span>
            <div class="repo-meta">
              <span v-if="image.tag && image.repository !== '<none>'" class="tag">{{ image.tag }}</span>
              <span v-if="!image.isUsed" class="unused-badge">{{ t('image.unused') }}</span>
              <span v-else-if="image.containersCount > 0" class="used-badge">{{ image.containersCount }} {{ t('image.containersCount') }}</span>
            </div>
          </div>
        </div>

        <div class="td td-id">
          <code>{{ image.id?.substring(7, 19) || '-' }}</code>
        </div>

        <div class="td td-size">
          <span class="size">{{ formatSize(image.size) }}</span>
        </div>

        <div class="td td-created">
          <span class="time">{{ formatDate(image.created) }}</span>
        </div>

        <div class="td td-actions">
          <button class="action-btn" @click="handleDetail(image)" :title="t('image.viewDetails')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
              <polyline points="10 9 9 9 8 9"></polyline>
            </svg>
          </button>
          <button class="action-btn" @click="handleRun(image)" :title="t('image.runContainer')">
            <svg viewBox="0 0 24 24" fill="currentColor"><polygon points="5 3 19 12 5 21 5 3"></polygon></svg>
          </button>
          <button class="action-btn" @click="handleExport(image)" :title="t('image.exportImage')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
          </button>
          <button class="action-btn" @click="openTagDialog(image)" :title="t('image.tagImage')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"></path>
              <line x1="7" y1="7" x2="7.01" y2="7"></line>
            </svg>
          </button>
          <button class="action-btn" @click="openPushDialog(image)" :title="t('image.push')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M12 19V5"></path>
              <polyline points="5 12 12 5 19 12"></polyline>
            </svg>
          </button>
          <button class="action-btn danger" @click="handleDelete(image)" :title="t('image.removeImage')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
          </button>
        </div>
      </div>
    </div>

    <!-- Pagination -->
    <div class="pagination" v-if="totalPages > 1">
      <div class="page-info">
        {{ t('pagination.showing') }} {{ (currentPage - 1) * pageSize + 1 }} - {{ Math.min(currentPage * pageSize, filteredImages.length) }} 
        {{ t('pagination.totalItems') }} {{ filteredImages.length }} {{ t('pagination.itemsUnit') }}
      </div>
      <div class="page-controls">
        <button class="page-btn" :disabled="currentPage === 1" @click="currentPage = 1">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="11 17 6 12 11 7"></polyline><polyline points="18 17 13 12 18 7"></polyline></svg>
        </button>
        <button class="page-btn" :disabled="currentPage === 1" @click="currentPage--">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="15 18 9 12 15 6"></polyline></svg>
        </button>
        <div class="page-numbers">
          <button 
            v-for="page in visiblePages" 
            :key="page"
            class="page-num"
            :class="{ active: page === currentPage }"
            @click="currentPage = page"
          >
            {{ page }}
          </button>
        </div>
        <button class="page-btn" :disabled="currentPage === totalPages" @click="currentPage++">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
        </button>
        <button class="page-btn" :disabled="currentPage === totalPages" @click="currentPage = totalPages">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="13 17 18 12 13 7"></polyline><polyline points="6 17 11 12 6 7"></polyline></svg>
        </button>
      </div>
      <select v-model.number="pageSize" class="page-size">
        <option :value="10">10 {{ t('pagination.itemsPerPage') }}</option>
        <option :value="20">20 {{ t('pagination.itemsPerPage') }}</option>
        <option :value="50">50 {{ t('pagination.itemsPerPage') }}</option>
        <option :value="100">100 {{ t('pagination.itemsPerPage') }}</option>
      </select>
    </div>

    <!-- Empty State -->
    <div v-if="!loading && filteredImages.length === 0" class="empty-state">
      <div class="empty-icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path>
        </svg>
      </div>
      <h3 class="empty-title">{{ t('image.empty.noImages') }}</h3>
      <p class="empty-desc">{{ t('image.empty.pullFirst') }}</p>
      <button class="btn btn-primary" @click="showPull = true">{{ t('image.pull') }}</button>
    </div>

    <PullImageDialog v-model="showPull" @success="refreshData" />
    <RegistryManagerDialog v-model="showRegistry" />
    <ImageDetailDrawer v-model="showDetail" :image-id="selectedImageId" />
    <CreateContainerDialog
      v-model="showCreateContainer"
      :preselected-image="selectedImage"
      @success="refreshData"
    />

    <!-- 导入镜像对话框 -->
    <el-dialog v-model="showImport" :title="t('image.importTitle')" width="480px" class="modern-dialog">
      <div class="import-form">
        <div class="form-item">
          <label class="form-label">{{ t('image.importFile') }}</label>
          <el-upload
            drag
            :auto-upload="false"
            :limit="1"
            accept=".tar"
            :on-change="(file: any) => importFile = file.raw"
            :on-remove="() => importFile = null"
          >
            <el-icon class="el-icon--upload"><upload-filled /></el-icon>
            <div class="el-upload__text">{{ t('image.selectFile') }}</div>
            <template #tip>
              <div class="el-upload__tip">{{ t('image.importFileTip') }}</div>
            </template>
          </el-upload>
        </div>
      </div>
      <template #footer>
        <el-button @click="showImport = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="handleImport" :loading="importLoading">{{ t('common.confirm') }}</el-button>
      </template>
    </el-dialog>

    <!-- 标记镜像对话框 -->
    <el-dialog v-model="showTag" :title="t('image.tagTitle')" width="480px" class="modern-dialog">
      <div class="tag-form">
        <div class="form-item">
          <label class="form-label">{{ t('image.tagSource') }}</label>
          <el-input :value="selectedImage?.repository !== '<none>' ? `${selectedImage?.repository}:${selectedImage?.tag}` : selectedImage?.id?.substring(0, 19)" disabled />
        </div>
        <div class="form-item">
          <label class="form-label">{{ t('image.tagTargetRepository') }}</label>
          <el-input v-model="tagForm.targetRepository" :placeholder="t('image.tagTargetPlaceholder')" />
        </div>
        <div class="form-item">
          <label class="form-label">{{ t('image.tagTargetTag') }}</label>
          <el-input v-model="tagForm.targetTag" placeholder="latest" />
        </div>
      </div>
      <template #footer>
        <el-button @click="showTag = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="handleTag" :loading="tagLoading">{{ t('common.confirm') }}</el-button>
      </template>
    </el-dialog>

    <!-- 推送镜像对话框 -->
    <el-dialog v-model="showPush" :title="t('image.pushTitle')" width="480px" class="modern-dialog">
      <div class="push-form">
        <div class="form-item">
          <label class="form-label">{{ t('image.image') }}</label>
          <el-input :value="selectedImage?.repository !== '<none>' ? `${selectedImage?.repository}:${selectedImage?.tag || 'latest'}` : selectedImage?.id?.substring(0, 19)" disabled />
        </div>
        <p class="push-tip">{{ t('image.pushToRegistry') }}</p>
      </div>
      <template #footer>
        <el-button @click="showPush = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="handlePush">{{ t('image.push') }}</el-button>
      </template>
    </el-dialog>

    <!-- 构建镜像对话框 -->
    <el-dialog v-model="showBuild" :title="t('image.buildTitle')" width="640px" class="modern-dialog">
      <div class="build-form">
        <!-- 构建模式选择 -->
        <div class="form-item">
          <label class="form-label">{{ t('image.buildMode') }}</label>
          <el-radio-group v-model="buildForm.mode" class="build-mode-radio">
            <el-radio-button value="dockerfile">
              <div class="mode-option">
                <span class="mode-title">{{ t('image.buildModeDockerfile') }}</span>
                <span class="mode-desc">{{ t('image.buildModeDockerfileDesc') }}</span>
              </div>
            </el-radio-button>
            <el-radio-button value="archive">
              <div class="mode-option">
                <span class="mode-title">{{ t('image.buildModeArchive') }}</span>
                <span class="mode-desc">{{ t('image.buildModeArchiveDesc') }}</span>
              </div>
            </el-radio-button>
          </el-radio-group>
        </div>

        <!-- Dockerfile 模式：只编辑 Dockerfile -->
        <template v-if="buildForm.mode === 'dockerfile'">
          <div class="form-item">
            <label class="form-label">{{ t('image.buildDockerfileContent') }}</label>
            <DockerfileEditor
              v-model="buildForm.dockerfileContent"
              :height="240"
            />
          </div>
        </template>

        <!-- 压缩包模式：上传压缩包 -->
        <template v-else>
          <div class="form-item">
            <label class="form-label">{{ t('image.buildArchive') }}</label>
            <el-upload
              drag
              :auto-upload="false"
              :limit="1"
              accept=".tar,.tar.gz,.tgz,.zip"
              :on-change="(file: any) => buildForm.archiveFile = file.raw"
              :on-remove="() => buildForm.archiveFile = null"
            >
              <el-icon class="el-icon--upload"><upload-filled /></el-icon>
              <div class="el-upload__text">{{ t('image.selectFile') }}</div>
              <template #tip>
                <div class="el-upload__tip">{{ t('image.buildArchiveTip') }}</div>
              </template>
            </el-upload>
          </div>
          <div class="form-item">
            <label class="form-label">{{ t('image.buildDockerfilePath') }}</label>
            <el-input v-model="buildForm.dockerfilePath" :placeholder="t('image.buildDockerfilePathTip')" />
          </div>
        </template>

        <!-- 通用配置 -->
        <div class="form-row">
          <div class="form-item flex-1">
            <label class="form-label">{{ t('image.buildRepository') }}</label>
            <el-input v-model="buildForm.repository" :placeholder="t('image.buildRepositoryTip')" />
          </div>
          <div class="form-item" style="width: 120px;">
            <label class="form-label">{{ t('image.buildTag') }}</label>
            <el-input v-model="buildForm.tag" placeholder="latest" />
          </div>
        </div>
        <div class="form-item">
          <label class="form-label">{{ t('image.buildArgs') }}</label>
          <el-input
            v-model="buildForm.buildArgsText"
            type="textarea"
            :rows="3"
            :placeholder="t('image.buildArgsTip')"
          />
        </div>
        <div class="form-item">
          <el-checkbox v-model="buildForm.noCache">{{ t('image.buildNoCache') }}</el-checkbox>
        </div>
      </div>
      <template #footer>
        <el-button @click="showBuild = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="handleBuild" :loading="buildLoading">{{ t('common.confirm') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, defineAsyncComponent, onMounted, onUnmounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useImagesStore } from '@/stores/images'
import { useSettingsStore } from '@/stores/settings'
import { ElMessage, ElMessageBox } from 'element-plus'
import PullImageDialog from '@/components/image/PullImageDialog.vue'
import RegistryManagerDialog from '@/components/image/RegistryManagerDialog.vue'
import ImageDetailDrawer from '@/components/image/ImageDetailDrawer.vue'
import CreateContainerDialog from '@/components/container/CreateContainerDialog.vue'
import { imageApi } from '@/api/images'
import { UploadFilled } from '@element-plus/icons-vue'
import { useTasksStore } from '@/stores/tasks'
import { formatLocalizedDateTime } from '@/utils/date'

const DockerfileEditor = defineAsyncComponent(() => import('@/components/common/DockerfileEditor.vue'))

const { t } = useI18n()
const store = useImagesStore()
const tasksStore = useTasksStore()
const settingsStore = useSettingsStore()
const loading = computed(() => store.loading)
const search = ref('')
const showPull = ref(false)
const showRegistry = ref(false)
const showDetail = ref(false)
const showImport = ref(false)
const showTag = ref(false)
const showPush = ref(false)
const showBuild = ref(false)
const selectedImageId = ref<string | null>(null)
const selectedIds = ref<string[]>([])
const showCreateContainer = ref(false)
const selectedImage = ref<any>(null)
const importFile = ref<File | null>(null)
const importLoading = ref(false)
const tagForm = ref({ targetRepository: '', targetTag: '' })
const tagLoading = ref(false)
const buildForm = ref({
  mode: 'dockerfile' as 'dockerfile' | 'archive',
  // Dockerfile 模式
  dockerfileContent: '',
  // 压缩包模式
  archiveFile: null as File | null,
  dockerfilePath: './Dockerfile',
  // 通用
  repository: '',
  tag: 'latest',
  buildArgsText: '',
  noCache: false
})
const buildLoading = ref(false)

// 构建进度相关
interface BuildProgress {
  buildId: string
  step: string
  progress: number
  detail?: string
  stream?: string
  isError: boolean
  timestamp: string
}

const currentPage = ref(1)
const pageSize = ref(settingsStore.defaultPageSize)

const filteredImages = computed(() => 
  store.images.filter((i: any) => 
    i.repository.toLowerCase().includes(search.value.toLowerCase()) ||
    i.id.toLowerCase().includes(search.value.toLowerCase())
  )
)

const totalPages = computed(() => Math.ceil(filteredImages.value.length / pageSize.value))
const paginatedImages = computed(() => filteredImages.value.slice((currentPage.value - 1) * pageSize.value, currentPage.value * pageSize.value))

const visiblePages = computed(() => {
  const pages = []
  const total = totalPages.value
  const current = currentPage.value
  let start = Math.max(1, current - 2)
  let end = Math.min(total, start + 4)
  if (end - start < 4) start = Math.max(1, end - 4)
  for (let i = start; i <= end; i++) pages.push(i)
  return pages
})

const totalSize = computed(() => store.images.reduce((sum: number, i: any) => sum + (i.size || 0), 0))

const isAllSelected = computed(() => paginatedImages.value.length > 0 && paginatedImages.value.every(i => selectedIds.value.includes(i.id)))

const toggleSelect = (id: string) => {
  const index = selectedIds.value.indexOf(id)
  if (index > -1) selectedIds.value.splice(index, 1)
  else selectedIds.value.push(id)
}

const toggleSelectAll = () => {
  if (isAllSelected.value) {
    const pageIds = paginatedImages.value.map(i => i.id)
    selectedIds.value = selectedIds.value.filter(id => !pageIds.includes(id))
  } else {
    paginatedImages.value.forEach(i => {
      if (!selectedIds.value.includes(i.id)) selectedIds.value.push(i.id)
    })
  }
}

const formatSize = (bytes: number) => {
  if (bytes === null || bytes === undefined || isNaN(bytes) || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const absBytes = Math.abs(bytes)
  if (absBytes < 1) return absBytes.toFixed(2) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const formatDate = (date: any) => {
  return formatLocalizedDateTime(date, '-', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

const refreshData = async () => {
  await store.fetchImages()
  selectedIds.value = []
}

const handleDelete = async (image: any) => {
  const name = image.repository === '<none>' ? image.id.substring(7, 19) : `${image.repository}:${image.tag}`
  
  try {
    await ElMessageBox.confirm(t('image.deleteConfirm', { name }), t('common.deleteConfirm'), { type: 'warning', confirmButtonText: t('common.delete'), cancelButtonText: t('common.cancel') })
    await store.removeImage(image.id)
    ElMessage.success(t('image.deleteSuccess'))
    refreshData()
  } catch (e: any) {
    ElMessage.error(e.message || t('image.deleteFailed'))
  }
}

const handleBatchDelete = async () => {
  if (selectedIds.value.length === 0) return
  
  try {
    await ElMessageBox.confirm(t('image.batchDeleteConfirm', { count: selectedIds.value.length }), t('image.batchDeleteTitle'), { type: 'danger', confirmButtonText: t('common.delete'), cancelButtonText: t('common.cancel') })
    await store.batchRemoveImages({ imageIds: selectedIds.value, force: false })
    ElMessage.success(t('image.batchDeleteSuccess'))
    selectedIds.value = []
    refreshData()
  } catch (e: any) {
    ElMessage.error(`${t('image.batchDeleteFailed')}: ${e.message}`)
  }
}

const handlePrune = () => {
  ElMessageBox.confirm(t('image.pruneConfirm'), t('image.cleanup'), { type: 'warning' }).then(async () => {
    await store.pruneImages({ all: true })
    refreshData()
  })
}

const handleRun = (image: any) => {
  selectedImage.value = image
  showCreateContainer.value = true
}

const handleDetail = (image: any) => {
  selectedImageId.value = image.id
  showDetail.value = true
}

// 导入镜像
const handleImport = async () => {
  if (!importFile.value) {
    ElMessage.warning(t('image.importFileRequired'))
    return
  }
  
  importLoading.value = true
  try {
    const result = await imageApi.importImage(importFile.value)
    ElMessage.success(t('image.importSuccess') + ': ' + (result as any).images?.join(', '))
    showImport.value = false
    importFile.value = null
    refreshData()
  } catch (e: any) {
    ElMessage.error(t('image.importFailed') + ': ' + e.message)
  } finally {
    importLoading.value = false
  }
}

// 导出镜像
const handleExport = async (image: any) => {
  const name = image.repository === '<none>' ? image.id : `${image.repository}:${image.tag || 'latest'}`
  try {
    ElMessage.info(t('image.exporting'))
    const url = imageApi.exportImage(name)
    window.open(url, '_blank')
  } catch (e: any) {
    ElMessage.error(t('image.exportFailed') + ': ' + e.message)
  }
}

// 打开标记对话框
const openTagDialog = (image: any) => {
  selectedImage.value = image
  tagForm.value = {
    targetRepository: image.repository !== '<none>' ? image.repository : '',
    targetTag: image.tag || 'latest'
  }
  showTag.value = true
}

// 标记镜像
const handleTag = async () => {
  if (!tagForm.value.targetRepository) {
    ElMessage.warning(t('image.tagTargetPlaceholder'))
    return
  }
  
  tagLoading.value = true
  try {
    const tagName = tagForm.value.targetTag 
      ? `${tagForm.value.targetRepository}:${tagForm.value.targetTag}`
      : tagForm.value.targetRepository
    await imageApi.tagImage(selectedImage.value.id, {
      targetRepository: tagForm.value.targetRepository,
      targetTag: tagForm.value.targetTag || 'latest'
    })
    ElMessage.success(t('image.tagSuccess'))
    showTag.value = false
    refreshData()
  } catch (e: any) {
    ElMessage.error(t('image.tagFailed') + ': ' + e.message)
  } finally {
    tagLoading.value = false
  }
}

// 打开推送对话框
const openPushDialog = (image: any) => {
  selectedImage.value = image
  showPush.value = true
}

// 推送镜像
const handlePush = () => {
  if (!selectedImage.value) return
  
  const name = selectedImage.value.repository !== '<none>' 
    ? selectedImage.value.repository 
    : selectedImage.value.id
  const tag = selectedImage.value.tag || 'latest'
  const fullImageName = `${name}:${tag}`
  
  const taskTitle = `推送镜像: ${fullImageName}`
  
  // 发送请求
  imageApi.pushImage(name, { tag }).then((data) => {
    // 注意：axios 拦截器已经返回 response.data，所以 data 就是响应数据本身
    
    if (data?.pushId) {
      // 添加任务到本地 store（后端已经创建了任务）
      tasksStore.addTask({
        id: data.pushId,
        type: 'image-push',
        title: taskTitle,
        status: 'running',
        progress: 0,
        detail: '推送任务已启动...'
      })
    }
    
    ElMessage.success(t('common.tasksPushSubmitted'))
    showPush.value = false
  }).catch((error: any) => {
    const errorMsg = error.response?.data?.error || error.message || '推送失败'
    ElMessage.error(errorMsg)
  })
}

// 构建镜像
const handleBuild = async () => {
  // 解析构建参数
  const buildArgs: Record<string, string> = {}
  if (buildForm.value.buildArgsText) {
    buildForm.value.buildArgsText.split('\n').forEach(line => {
      const trimmed = line.trim()
      if (trimmed && trimmed.includes('=')) {
        const [key, ...values] = trimmed.split('=')
        buildArgs[key.trim()] = values.join('=').trim()
      }
    })
  }

  // 验证
  if (!buildForm.value.repository) {
    ElMessage.warning(t('image.buildRepositoryRequired'))
    return
  }

  if (buildForm.value.mode === 'dockerfile') {
    // Dockerfile 模式验证：只需要 Dockerfile 内容
    if (!buildForm.value.dockerfileContent.trim()) {
      ElMessage.warning(t('image.buildDockerfileRequired'))
      return
    }
  } else {
    // 压缩包模式验证
    if (!buildForm.value.archiveFile) {
      ElMessage.warning(t('image.buildArchiveRequired'))
      return
    }
  }

  buildLoading.value = true
  // 显示上传中提示
  const uploadMessage = buildForm.value.mode === 'dockerfile' 
    ? '正在提交构建任务...' 
    : '正在上传文件，请稍候...'
  ElMessage.info(uploadMessage)
  
  const formData = new FormData()

    if (buildForm.value.mode === 'dockerfile') {
      // Dockerfile 模式：只发送 Dockerfile 内容
      formData.append('mode', 'dockerfile')
      formData.append('dockerfileContent', buildForm.value.dockerfileContent)
    } else {
      // 压缩包模式
      formData.append('mode', 'archive')
      formData.append('file', buildForm.value.archiveFile!)
      formData.append('dockerfilePath', buildForm.value.dockerfilePath || './Dockerfile')
    }

    formData.append('tag', `${buildForm.value.repository}:${buildForm.value.tag || 'latest'}`)
    formData.append('buildArgs', Object.entries(buildArgs).map(([k, v]) => `${k}=${v}`).join('\n'))
    formData.append('noCache', String(buildForm.value.noCache))

    const taskTitle = `构建镜像: ${buildForm.value.repository}:${buildForm.value.tag || 'latest'}`

    // 发送请求
    imageApi.buildImage(formData).then((data) => {
      // 注意：axios 拦截器已经返回 response.data，所以 data 就是响应数据本身
      
      if (data?.buildId) {
        // 添加任务到本地 store（后端已经创建了任务）
        tasksStore.addTask({
          id: data.buildId,
          type: 'image-build',
          title: taskTitle,
          status: 'running',
          progress: 0,
          detail: t('common.tasksBuildSubmitted')
        })
      }
      
      ElMessage.success(t('common.tasksBuildSubmitted'))
      showBuild.value = false
      
      // 重置表单
      buildForm.value = {
        mode: 'dockerfile',
        dockerfileContent: '',
        archiveFile: null,
        dockerfilePath: './Dockerfile',
        repository: '',
        tag: 'latest',
        buildArgsText: '',
        noCache: false
      }
    }).catch((apiError: any) => {
      ElMessage.error(t('image.buildFailed') + ': ' + (apiError.message || '未知错误'))
    }).finally(() => {
      buildLoading.value = false
    })
}

onMounted(() => {
  refreshData()
})

watch(search, () => { currentPage.value = 1 })
watch(() => settingsStore.defaultPageSize, (size) => {
  pageSize.value = size
  currentPage.value = 1
})
</script>

<style scoped>
.images-page {
  padding: 24px 32px;
  max-width: 1600px;
  margin: 0 auto;
}

/* === Page Header === */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
}

.page-subtitle { 
  margin: 6px 0 0 0; 
  color: var(--text-muted); 
  font-size: 14px; 
}

.header-actions { 
  display: flex; 
  gap: 10px; 
}

/* === Buttons === */
.btn-icon { 
  width: 16px; 
  height: 16px; 
}

.btn-sm {
  padding: 4px 10px;
  font-size: 12px;
  font-weight: 500;
  height: 28px;
  line-height: 1;
}

/* === Toolbar === */
.toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
  flex-wrap: wrap;
}

.search-box {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  max-width: 320px;
  min-width: 200px;
  padding: 8px 12px;
  background: var(--bg-glass-dark);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.search-box:focus-within { border-color: #3b82f6; }

.search-icon { width: 16px; height: 16px; color: var(--text-muted); }

.search-input {
  flex: 1;
  border: none;
  background: transparent;
  outline: none;
  font-size: 13px;
  color: var(--text-main);
  min-width: 0;
}

.actions { 
  display: flex; 
  gap: 8px; 
}

.stats { 
  margin-left: auto; 
  font-size: 13px; 
  color: var(--text-muted); 
}

.stats strong { 
  color: var(--text-main); 
}

/* === Data Table === */
.data-table {
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
  overflow: hidden;
  overflow-x: auto;
}

.table-header {
  display: grid;
  grid-template-columns: 40px 2fr 1fr 1fr 1.5fr 240px;
  gap: 12px;
  padding: 14px 16px;
  background: var(--bg-glass-dark);
  border-bottom: 1px solid var(--border-color);
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.table-row {
  display: grid;
  grid-template-columns: 40px 2fr 1fr 1fr 1.5fr 240px;
  gap: 12px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border-color-light);
  align-items: center;
  transition: background 0.15s ease;
}

.table-row:last-child { border-bottom: none; }
.table-row:hover { background: var(--bg-glass-dark); }
.table-row.selected { background: rgba(59, 130, 246, 0.05); }

.th, .td { 
  min-width: 0;
  overflow: hidden;
}

.td { font-size: 13px; color: var(--text-secondary); }

.td-checkbox, .th-checkbox { 
  justify-content: center; 
  display: flex;
}

.td-repo { 
  display: flex;
  align-items: center;
  gap: 10px; 
  min-width: 0;
}

.repo-icon { 
  width: 36px; 
  height: 36px; 
  border-radius: 8px; 
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  color: #fff; 
  display: flex; 
  align-items: center; 
  justify-content: center; 
  flex-shrink: 0; 
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.25);
}

.repo-icon svg { width: 18px; height: 18px; }

.repo-icon.is-none { 
  background: linear-gradient(135deg, #94a3b8, #64748b);
  box-shadow: 0 2px 8px rgba(100, 116, 139, 0.25);
}

.repo-icon.is-unused { 
  opacity: 0.7;
}

.repo-info { 
  display: flex; 
  flex-direction: column; 
  min-width: 0; 
  overflow: hidden;
}

.repo-meta {
  display: flex;
  gap: 6px;
  margin-top: 2px;
}

.repo-name { 
  font-weight: 600; 
  color: var(--text-main); 
  font-size: 13px; 
  white-space: nowrap; 
  overflow: hidden; 
  text-overflow: ellipsis; 
}

.repo-name.clickable {
  cursor: pointer;
  color: var(--color-primary, #3b82f6);
  transition: color 0.2s;
}

.repo-name.clickable:hover {
  color: var(--color-primary-hover, #2563eb);
  text-decoration: underline;
}

.tag { 
  font-size: 10px; 
  color: #3b82f6; 
  background: rgba(59, 130, 246, 0.1); 
  padding: 1px 8px; 
  border-radius: 4px; 
  width: fit-content;
  font-weight: 500;
}

.unused-badge {
  font-size: 10px; 
  color: #f59e0b; 
  background: rgba(245, 158, 11, 0.1); 
  padding: 1px 8px; 
  border-radius: 4px; 
  font-weight: 500;
}

.used-badge {
  font-size: 10px; 
  color: #10b981; 
  background: rgba(16, 185, 129, 0.1); 
  padding: 1px 8px; 
  border-radius: 4px; 
  font-weight: 500;
}

.td-id code { 
  font-family: 'JetBrains Mono', monospace; 
  font-size: 11px; 
  color: var(--text-muted); 
}

.td-size .size { 
  font-family: 'JetBrains Mono', monospace; 
  font-size: 12px; 
  color: var(--text-secondary); 
}

.td-created .time { 
  font-size: 12px; 
  color: var(--text-muted); 
}

.td-actions { 
  display: flex;
  gap: 6px; 
  justify-content: center; 
  width: 100%;
}

.th-actions { 
  text-align: center; 
}

.action-btn { 
  width: 32px; 
  height: 32px; 
  border-radius: 8px; 
  border: 1px solid var(--border-color); 
  background: var(--bg-surface); 
  display: flex; 
  align-items: center; 
  justify-content: center; 
  cursor: pointer; 
  color: var(--text-muted); 
  transition: all 0.2s ease; 
}

.action-btn:hover { 
  border-color: #3b82f6; 
  color: #3b82f6; 
  background: rgba(59, 130, 246, 0.08); 
}

.action-btn.danger:hover { 
  border-color: #ef4444; 
  color: #ef4444; 
  background: rgba(239, 68, 68, 0.08); 
}

.action-btn svg { 
  width: 14px; 
  height: 14px; 
}

/* === Pagination === */
.pagination { 
  display: flex; 
  justify-content: space-between; 
  align-items: center; 
  margin-top: 16px; 
  font-size: 12px; 
  color: var(--text-muted); 
}

.page-info {
  font-size: 13px;
}

.page-controls { 
  display: flex; 
  align-items: center; 
  gap: 6px; 
}

.page-btn { 
  padding: 6px 10px; 
  border-radius: 6px; 
  border: 1px solid var(--border-color); 
  background: var(--bg-surface); 
  cursor: pointer; 
  font-size: 12px; 
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
}

.page-btn svg {
  width: 14px;
  height: 14px;
}

.page-btn:hover:not(:disabled) { 
  border-color: #3b82f6; 
  color: #3b82f6; 
  background: rgba(59, 130, 246, 0.08);
}

.page-btn:disabled { 
  opacity: 0.5; 
  cursor: not-allowed; 
}

.page-numbers {
  display: flex;
  gap: 4px;
}

.page-num { 
  padding: 6px 12px;
  border-radius: 6px; 
  border: 1px solid transparent;
  background: transparent; 
  cursor: pointer; 
  font-size: 12px; 
  color: var(--text-secondary);
  transition: all 0.2s ease;
}

.page-num:hover {
  background: var(--bg-subtle);
}

.page-num.active { 
  font-weight: 600; 
  color: #3b82f6;
  background: rgba(59, 130, 246, 0.1);
  border-color: rgba(59, 130, 246, 0.2);
}

.page-size {
  padding: 6px 10px;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  background: var(--bg-surface);
  font-size: 12px;
  color: var(--text-secondary);
  cursor: pointer;
}

/* === Empty State === */
.empty-state { 
  text-align: center; 
  padding: 80px 40px; 
  color: var(--text-muted); 
}

.empty-icon { 
  margin-bottom: 16px; 
}

.empty-icon svg { 
  width: 48px; 
  height: 48px; 
  opacity: 0.5;
}

.empty-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-main);
  margin: 0 0 8px 0;
}

.empty-desc {
  font-size: 14px;
  margin: 0 0 20px 0;
}

/* === Responsive === */
@media (max-width: 1024px) {
  /* Hide ID column */
  .table-header, .table-row { grid-template-columns: 40px minmax(180px, 2fr) 100px 160px 100px; }
  .th:nth-child(3), .td-id { display: none; }
}

@media (max-width: 768px) {
  .images-page { padding: 16px; }
  .page-header { flex-direction: column; gap: 12px; align-items: stretch; }
  .header-actions { flex-wrap: wrap; gap: 8px; }
  .toolbar { flex-wrap: wrap; }
  .search-box { max-width: none; width: 100%; }
  .stats { width: 100%; text-align: center; }
  /* Hide ID + Created columns */
  .table-header, .table-row { grid-template-columns: 40px minmax(140px, 2fr) 80px 90px; }
  .th:nth-child(3), .td-id,
  .th:nth-child(5), .td-created { display: none; }
  .pagination { flex-direction: column; gap: 8px; align-items: center; }
}

@media (max-width: 480px) {
  .images-page { padding: 12px; }
  /* Hide Size too, keep only checkbox + repo + actions */
  .table-header, .table-row { grid-template-columns: 36px 1fr 80px; }
  .th:nth-child(3), .td-id,
  .th:nth-child(4), .td-size,
  .th:nth-child(5), .td-created { display: none; }
  .btn { padding: 6px 8px; font-size: 11px; }
  .btn-icon { width: 12px; height: 12px; }
  .empty-state { padding: 40px 20px; }
}

/* === Build Form === */
.build-form .form-item,
.tag-form .form-item,
.push-form .form-item,
.import-form .form-item {
  margin-bottom: 16px;
}

.build-form .form-label,
.tag-form .form-label,
.push-form .form-label,
.import-form .form-label {
  display: block;
  margin-bottom: 6px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-main);
}

.form-row {
  display: flex;
  gap: 12px;
}

.flex-1 {
  flex: 1;
}

.push-tip {
  font-size: 12px;
  color: var(--text-muted);
  margin: 0;
}

/* === Build Mode Radio === */
.build-mode-radio {
  display: flex;
  gap: 12px;
  width: 100%;
}

.build-mode-radio :deep(.el-radio-button) {
  flex: 1;
}

.build-mode-radio :deep(.el-radio-button__inner) {
  width: 100%;
  padding: 12px 16px;
  border-radius: 8px !important;
  border: 1px solid var(--border-color) !important;
  background: var(--bg-surface);
}

.build-mode-radio :deep(.el-radio-button.is-active .el-radio-button__inner) {
  border-color: #3b82f6 !important;
  background: rgba(59, 130, 246, 0.08);
}

.mode-option {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
}

.mode-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-main);
}

.mode-desc {
  font-size: 11px;
  color: var(--text-muted);
  text-align: left;
}

</style>

<style>
/* === Dark Mode === */
html.dark .toolbar, html.dark .data-table { 
  background: rgba(30, 41, 59, 0.8); 
  border-color: rgba(255, 255, 255, 0.1); 
}

html.dark .search-box { 
  background: rgba(15, 23, 42, 0.6); 
  border-color: rgba(255, 255, 255, 0.1); 
}

html.dark .search-input { 
  color: #f1f5f9; 
}

html.dark .table-header { 
  background: rgba(15, 23, 42, 0.6); 
  color: #94a3b8; 
  border-bottom-color: rgba(255, 255, 255, 0.1); 
}

html.dark .table-row { 
  border-bottom-color: rgba(255, 255, 255, 0.05); 
}

html.dark .table-row:hover { 
  background: rgba(255, 255, 255, 0.03); 
}

html.dark .table-row.selected { 
  background: rgba(59, 130, 246, 0.1); 
}

html.dark .repo-name, html.dark .stats strong, html.dark .page-num.active { 
  color: #f1f5f9; 
}

html.dark .action-btn, html.dark .page-btn, html.dark .page-size { 
  background: rgba(30, 41, 59, 0.8); 
  border-color: rgba(255, 255, 255, 0.1); 
  color: #cbd5e1; 
}

html.dark .tag { 
  background: rgba(59, 130, 246, 0.2); 
}

html.dark .empty-title {
  color: #f1f5f9;
}

/* 构建进度样式 */
.build-progress-container {
  padding: 0 16px;
}

.build-progress-item {
  padding: 16px;
  margin-bottom: 16px;
  background: rgba(0, 0, 0, 0.05);
  border-radius: 8px;
}

html.dark .build-progress-item {
  background: rgba(255, 255, 255, 0.05);
}

.progress-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.build-id {
  font-family: monospace;
  font-size: 12px;
  color: #64748b;
}

.build-step {
  font-size: 13px;
  font-weight: 500;
  padding: 2px 8px;
  border-radius: 4px;
  background: rgba(59, 130, 246, 0.1);
  color: #3b82f6;
}

.build-step.is-error {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.build-step.is-success {
  background: rgba(34, 197, 94, 0.1);
  color: #22c55e;
}

.progress-detail {
  margin-top: 8px;
  font-size: 13px;
  color: #475569;
}

html.dark .progress-detail {
  color: #94a3b8;
}

.progress-stream {
  margin-top: 8px;
  max-height: 100px;
  overflow-y: auto;
  background: rgba(0, 0, 0, 0.05);
  border-radius: 4px;
  padding: 8px;
}

html.dark .progress-stream {
  background: rgba(0, 0, 0, 0.3);
}

.progress-stream pre {
  margin: 0;
  font-family: monospace;
  font-size: 11px;
  color: #64748b;
  white-space: pre-wrap;
  word-break: break-all;
}

.progress-time {
  margin-top: 8px;
  font-size: 11px;
  color: #94a3b8;
}

.no-progress {
  text-align: center;
  color: #94a3b8;
  padding: 40px;
}

/* 后台任务按钮 */
.task-btn {
  position: relative;
}

.task-badge {
  position: absolute;
  top: -4px;
  right: -4px;
  background: #3b82f6;
  color: white;
  font-size: 10px;
  font-weight: 600;
  min-width: 16px;
  height: 16px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 4px;
}

html.dark .task-badge {
  background: #60a5fa;
}

/* 抽屉头部 */
.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
