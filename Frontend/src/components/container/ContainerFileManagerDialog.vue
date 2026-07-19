<template>
  <el-dialog
    v-model="visible"
    :title="`${t('container.filesModule.fileManager')} - ${containerName || ''}`"
    width="1000px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <!-- 文件路径导航 -->
    <div class="file-navigation">
      <el-breadcrumb separator="/">
        <el-breadcrumb-item @click="navigateToPath('')">
          <el-icon><House /></el-icon>
          {{ t('container.filesModule.rootDirectory') }}
        </el-breadcrumb-item>
        <el-breadcrumb-item
          v-for="(segment, index) in pathSegments"
          :key="index"
          @click="navigateToSegment(index)"
        >
          {{ segment }}
        </el-breadcrumb-item>
      </el-breadcrumb>

      <div class="path-input">
        <el-input
          v-model="currentPath"
          :placeholder="t('container.filesModule.inputPath')"
          @keyup.enter="loadFiles"
        >
          <template #append>
            <el-button @click="loadFiles">{{ t('container.filesModule.goTo') }}</el-button>
          </template>
        </el-input>
      </div>
    </div>

    <!-- 文件操作工具栏 -->
    <div class="file-toolbar">
      <div class="toolbar-left">
        <el-button @click="loadFiles" :loading="loading" :icon="Refresh">{{ t('container.filesModule.refresh') }}</el-button>
        <el-button @click="showUploadDialog = true" :icon="Upload">{{ t('container.filesModule.uploadFile') }}</el-button>
        <el-button @click="showCreateFolderDialog = true" :icon="FolderAdd">{{ t('container.filesModule.newFolder') }}</el-button>
        <el-button @click="downloadSelected" :disabled="selectedFiles.length === 0" :icon="Download">{{ t('container.filesModule.downloadSelected') }}</el-button>
      </div>

      <div class="toolbar-right">
        <el-input
          v-model="searchKeyword"
          :placeholder="t('container.filesModule.searchFiles')"
          clearable
          @input="handleSearch"
          style="width: 200px"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
      </div>
    </div>

    <!-- 文件列表 -->
    <div class="file-list">
      <el-table
        :data="filteredFiles"
        @selection-change="handleSelectionChange"
        @row-dblclick="handleRowDoubleClick"
        v-loading="loading"
        height="400"
      >
        <el-table-column type="selection" width="55" />

        <el-table-column :label="t('container.filesModule.name')" min-width="200">
          <template #default="{ row }">
            <div class="file-name">
              <el-icon v-if="row.type === 'directory'" class="file-icon directory">
                <Folder />
              </el-icon>
              <el-icon v-else class="file-icon file">
                <Document />
              </el-icon>
              <span class="name-text">{{ row.name }}</span>
            </div>
          </template>
        </el-table-column>

        <el-table-column prop="size" :label="t('container.filesModule.size')" width="100">
          <template #default="{ row }">
            {{ row.type === 'directory' ? '-' : formatFileSize(row.size) }}
          </template>
        </el-table-column>

        <el-table-column prop="modified" :label="t('container.filesModule.modifiedTime')" width="180">
          <template #default="{ row }">
            {{ formatDateTime(row.modified) }}
          </template>
        </el-table-column>

        <el-table-column prop="permissions" :label="t('container.filesModule.permissions')" width="120" />

        <el-table-column :label="t('container.filesModule.action')" width="200">
          <template #default="{ row }">
            <el-button
              v-if="row.type === 'directory'"
              size="small"
              @click="enterDirectory(row)"
            >
              {{ t('container.filesModule.enter') }}
            </el-button>
            <el-button
              v-if="row.type === 'file'"
              size="small"
              @click="downloadFile(row)"
            >
              {{ t('container.filesModule.download') }}
            </el-button>
            <el-button
              size="small"
              @click="openRenameDialog(row)"
            >
              {{ t('container.filesModule.rename') }}
            </el-button>
            <el-button
              size="small"
              type="danger"
              @click="showDeleteConfirm(row)"
            >
              {{ t('container.filesModule.delete') }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 统计信息 -->
    <div class="file-stats">
      <el-descriptions :column="3" border size="small">
        <el-descriptions-item :label="t('container.filesModule.fileCount')">
          {{ fileStats.fileCount }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('container.filesModule.folderCount')">
          {{ fileStats.folderCount }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('container.filesModule.totalSize')">
          {{ formatFileSize(fileStats.totalSize) }}
        </el-descriptions-item>
      </el-descriptions>
    </div>

    <!-- 上传文件对话框 -->
    <el-dialog
      v-model="showUploadDialog"
      :title="t('container.filesModule.uploadDialogTitle')"
      width="500px"
      append-to-body
    >
      <el-upload
        ref="uploadRef"
        :action="uploadUrl"
        :headers="uploadHeaders"
        :data="{ path: currentPath }"
        :on-success="handleUploadSuccess"
        :on-error="handleUploadError"
        :before-upload="beforeUpload"
        multiple
        drag
      >
        <el-icon class="el-icon--upload"><UploadFilled /></el-icon>
        <div class="el-upload__text">
          {{ t('container.filesModule.dragOrClick') }}
        </div>
        <template #tip>
          <div class="el-upload__tip">
            {{ t('container.filesModule.uploadMultipleHint') }}
          </div>
        </template>
      </el-upload>

      <template #footer>
        <el-button @click="showUploadDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="uploadFiles">{{ t('container.filesModule.startUpload') }}</el-button>
      </template>
    </el-dialog>

    <!-- 新建文件夹对话框 -->
    <el-dialog
      v-model="showCreateFolderDialog"
      :title="t('container.filesModule.newFolderDialogTitle')"
      width="400px"
      append-to-body
    >
      <el-form ref="folderFormRef" :model="folderForm" :rules="folderRules">
        <el-form-item :label="t('container.filesModule.folderName')" prop="name">
          <el-input v-model="folderForm.name" :placeholder="t('container.filesModule.folderNamePlaceholder')" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showCreateFolderDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="createFolder">{{ t('container.filesModule.create') }}</el-button>
      </template>
    </el-dialog>

    <!-- 重命名对话框 -->
    <el-dialog
      v-model="showRenameDialog"
      :title="t('container.filesModule.renameDialogTitle')"
      width="400px"
      append-to-body
    >
      <el-form ref="renameFormRef" :model="renameForm" :rules="renameRules">
        <el-form-item :label="t('container.filesModule.newName')" prop="name">
          <el-input v-model="renameForm.name" :placeholder="t('container.filesModule.newNamePlaceholder')" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showRenameDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="renameFile">{{ t('container.filesModule.confirmRename') }}</el-button>
      </template>
    </el-dialog>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('common.close') }}</el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance } from 'element-plus'
import {
  House,
  Folder,
  Document,
  Refresh,
  Upload,
  Download,
  Search,
  FolderAdd,
  UploadFilled
} from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { containerApi, type FileInfo } from '@/api/containers'
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
const loading = ref(false)
const files = ref<FileInfo[]>([])
const selectedFiles = ref<FileInfo[]>([])
const currentPath = ref('/')
const searchKeyword = ref('')
const showUploadDialog = ref(false)
const showCreateFolderDialog = ref(false)
const showRenameDialog = ref(false)
const uploadRef = ref()
const folderFormRef = ref<FormInstance>()
const renameFormRef = ref<FormInstance>()

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const containerId = computed(() => props.containerId)
const containerName = computed(() => containerId.value?.slice(0, 12) || '')

// 路径段
const pathSegments = computed(() => {
  return currentPath.value
    .split('/')
    .filter(segment => segment.length > 0)
})

// 过滤后的文件
const filteredFiles = computed(() => {
  if (!searchKeyword.value) return files.value

  const keyword = searchKeyword.value.toLowerCase()
  return files.value.filter(file =>
    file.name.toLowerCase().includes(keyword)
  )
})

// 文件统计
const fileStats = computed(() => {
  const fileCount = files.value.filter(f => f.type === 'file').length
  const folderCount = files.value.filter(f => f.type === 'directory').length
  const totalSize = files.value
    .filter(f => f.type === 'file')
    .reduce((sum, f) => sum + (f.size || 0), 0)

  return { fileCount, folderCount, totalSize }
})

// 表单数据
const folderForm = reactive({
  name: ''
})

const renameForm = reactive({
  name: ''
})

let currentRenameFile: FileInfo | null = null

// 表单验证规则
const folderRules = {
  name: [
    { required: true, message: t('container.filesModule.folderNameRequired'), trigger: 'blur' },
    { pattern: /^[^/\\:*?"<>|]+$/, message: t('container.filesModule.folderNameInvalid'), trigger: 'blur' }
  ]
}

const renameRules = {
  name: [
    { required: true, message: t('container.filesModule.newNameRequired'), trigger: 'blur' },
    { pattern: /^[^/\\:*?"<>|]+$/, message: t('container.filesModule.fileNameInvalid'), trigger: 'blur' }
  ]
}

// 上传配置
const uploadUrl = computed(() => {
  return `/api/containers/${containerId.value}/files/upload`
})

const uploadHeaders = computed(() => {
  return {
    'Authorization': `Bearer ${localStorage.getItem('token') || ''}`
  }
})

// 监听对话框打开
watch(visible, (newValue) => {
  if (newValue && containerId.value) {
    currentPath.value = '/'
    loadFiles()
  }
})

// 方法
const loadFiles = async () => {
  if (!containerId.value) return

  loading.value = true
  try {
    const response = await containerApi.getContainerFiles(containerId.value, {
      path: currentPath.value
    })
    files.value = response.data.files || []
  } catch (error: any) {
    console.error('获取文件列表失败:', error)
    ElMessage.error(error.response?.data?.message || t('container.filesModule.getFilesFailed'))
  } finally {
    loading.value = false
  }
}

const handleSelectionChange = (selection: FileInfo[]) => {
  selectedFiles.value = selection
}

const handleRowDoubleClick = (row: FileInfo) => {
  if (row.type === 'directory') {
    enterDirectory(row)
  } else {
    downloadFile(row)
  }
}

const enterDirectory = (directory: FileInfo) => {
  currentPath.value = `${currentPath.value.replace(/\/$/, '')}/${directory.name}`
  loadFiles()
}

const navigateToPath = (path: string) => {
  currentPath.value = path || '/'
  loadFiles()
}

const navigateToSegment = (index: number) => {
  const segments = pathSegments.value.slice(0, index + 1)
  currentPath.value = '/' + segments.join('/')
  loadFiles()
}

const handleSearch = () => {
  // 搜索逻辑在 computed 中处理
}

const downloadFile = async (file: FileInfo) => {
  if (!containerId.value) return

  try {
    const response = await containerApi.downloadContainerFile(containerId.value, {
      path: `${currentPath.value.replace(/\/$/, '')}/${file.name}`
    })

    // 创建下载链接
    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.download = file.name
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)

    ElMessage.success(t('container.filesModule.downloadSuccess'))
  } catch (error: any) {
    console.error('下载文件失败:', error)
    ElMessage.error(t('container.filesModule.downloadFailed'))
  }
}

const downloadSelected = async () => {
  for (const file of selectedFiles.value) {
    if (file.type === 'file') {
      await downloadFile(file)
    }
  }
}

const beforeUpload = () => {
  return true
}

const handleUploadSuccess = () => {
  ElMessage.success(t('container.filesModule.uploadSuccess'))
  loadFiles()
  showUploadDialog.value = false
}

const handleUploadError = () => {
  ElMessage.error(t('container.filesModule.uploadFailed'))
}

const uploadFiles = () => {
  uploadRef.value?.submit()
}

const createFolder = async () => {
  if (!folderFormRef.value || !containerId.value) return

  try {
    await folderFormRef.value.validate()

    await containerApi.createContainerFolder(containerId.value, {
      path: `${currentPath.value.replace(/\/$/, '')}/${folderForm.name}`
    })

    ElMessage.success(t('container.filesModule.folderCreateSuccess'))
    showCreateFolderDialog.value = false
    folderForm.name = ''
    loadFiles()
  } catch (error: any) {
    console.error('创建文件夹失败:', error)
    ElMessage.error(error.response?.data?.message || t('container.filesModule.folderCreateFailed'))
  }
}

const openRenameDialog = (file: FileInfo) => {
  currentRenameFile = file
  renameForm.name = file.name
  showRenameDialog.value = true
}

const renameFile = async () => {
  if (!renameFormRef.value || !currentRenameFile || !containerId.value) return

  try {
    await renameFormRef.value.validate()

    await containerApi.renameContainerFile(containerId.value, {
      oldPath: `${currentPath.value.replace(/\/$/, '')}/${currentRenameFile.name}`,
      newPath: `${currentPath.value.replace(/\/$/, '')}/${renameForm.name}`
    })

    ElMessage.success(t('container.filesModule.renameSuccess'))
    showRenameDialog.value = false
    loadFiles()
  } catch (error: any) {
    console.error('重命名失败:', error)
    ElMessage.error(error.response?.data?.message || t('container.filesModule.renameFailed'))
  }
}

const showDeleteConfirm = (file: FileInfo) => {
  ElMessageBox.confirm(
    t('container.filesModule.deleteConfirmMessage', {
      type: file.type === 'directory' ? t('container.filesModule.folderType') : t('container.filesModule.fileType'),
      name: file.name,
      extra: file.type === 'directory' ? t('container.filesModule.folderDeleteExtra') : ''
    }),
    t('container.filesModule.deleteConfirm'),
    {
      confirmButtonText: t('common.confirm'),
      cancelButtonText: t('common.cancel'),
      type: 'warning'
    }
  ).then(async () => {
    await deleteFile(file)
  })
}

const deleteFile = async (file: FileInfo) => {
  if (!containerId.value) return

  try {
    await containerApi.deleteContainerFile(containerId.value, {
      path: `${currentPath.value.replace(/\/$/, '')}/${file.name}`
    })

    ElMessage.success(t('container.filesModule.deleteSuccess'))
    loadFiles()
  } catch (error: any) {
    console.error('删除失败:', error)
    ElMessage.error(error.response?.data?.message || t('container.filesModule.deleteFailed'))
  }
}

const formatFileSize = (bytes: number) => {
  if (bytes === null || bytes === undefined || isNaN(bytes) || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const absBytes = Math.abs(bytes)
  if (absBytes < 1) return absBytes.toFixed(2) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const formatDateTime = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

const handleClose = () => {
  visible.value = false
  files.value = []
  selectedFiles.value = []
  currentPath.value = '/'
  searchKeyword.value = ''
}
</script>

<style scoped>
.file-navigation {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  padding: 12px;
  background-color: #f5f7fa;
  border-radius: 6px;
}

.path-input {
  flex: 1;
  max-width: 400px;
  margin-left: 16px;
}

.file-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  padding: 12px;
  background-color: var(--bg-surface);
  border: 1px solid #e4e7ed;
  border-radius: 6px;
}

.toolbar-left,
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.file-list {
  margin-bottom: 16px;
}

.file-name {
  display: flex;
  align-items: center;
  gap: 8px;
}

.file-icon {
  font-size: 16px;
}

.file-icon.directory {
  color: #e6a23c;
}

.file-icon.file {
  color: #409eff;
}

.name-text {
  word-break: break-all;
}

.file-stats {
  padding: 16px;
  background-color: #f5f7fa;
  border-radius: 6px;
}

.dialog-footer {
  text-align: right;
}

:deep(.el-breadcrumb__item) {
  cursor: pointer;
}

:deep(.el-breadcrumb__item:hover) {
  color: #409eff;
}

@media (max-width: 768px) {
  .file-navigation {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }

  .path-input {
    margin-left: 0;
    max-width: none;
  }

  .file-toolbar {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }

  .toolbar-left,
  .toolbar-right {
    justify-content: center;
  }
}
</style>