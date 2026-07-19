<template>
  <el-dialog
    v-model="visible"
    :title="t('fileManager.title')"
    width="1000px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <!-- 提示信息 -->
    <el-alert
      type="info"
      :closable="false"
      show-icon
      style="margin-bottom: 16px;"
    >
      <template #title>
        {{ t('volume.fileManager.info', { name: volumeName || '' }) }}
      </template>
    </el-alert>

    <!-- 文件路径导航 -->
    <div class="file-navigation">
      <el-breadcrumb separator="/">
        <el-breadcrumb-item @click="navigateToPath('')">
          <el-icon><House /></el-icon>
          {{ t('fileManager.rootDirectory') }}
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
          :placeholder="t('fileManager.inputPath')"
          @keyup.enter="loadFiles"
        >
          <template #append>
            <el-button @click="loadFiles">{{ t('fileManager.goTo') }}</el-button>
          </template>
        </el-input>
      </div>
    </div>

    <!-- 文件操作工具栏 -->
    <div class="file-toolbar">
      <div class="toolbar-left">
        <el-button @click="loadFiles" :loading="loading" :icon="Refresh">{{ t('fileManager.refresh') }}</el-button>
        <el-button @click="showUploadDialog = true" :icon="Upload">{{ t('fileManager.upload') }}</el-button>
        <el-button @click="showCreateFolderDialog = true" :icon="FolderAdd">{{ t('fileManager.newFolder') }}</el-button>
        <el-button @click="downloadSelected" :disabled="selectedFiles.length === 0" :icon="Download">{{ t('fileManager.downloadSelected') }}</el-button>
        <el-button type="primary" @click="downloadAllAsArchive" :loading="archiveLoading" :icon="Download">{{ t('fileManager.downloadAll') }}</el-button>
      </div>

      <div class="toolbar-right">
        <el-input
          v-model="searchKeyword"
          :placeholder="t('fileManager.searchFiles')"
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

        <el-table-column :label="t('fileManager.name')" min-width="200">
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

        <el-table-column prop="size" :label="t('fileManager.size')" width="100">
          <template #default="{ row }">
            {{ row.type === 'directory' ? '-' : formatFileSize(row.size) }}
          </template>
        </el-table-column>

        <el-table-column prop="modified" :label="t('fileManager.modifiedTime')" width="180">
          <template #default="{ row }">
            {{ formatDateTime(row.modified) }}
          </template>
        </el-table-column>

        <el-table-column prop="permissions" :label="t('fileManager.permissions')" width="120" />

        <el-table-column :label="t('common.actions')" width="200" fixed="right" align="center">
          <template #default="{ row }">
            <el-button-group size="small">
              <el-button
                v-if="row.type === 'directory'"
                type="primary"
                @click="enterDirectory(row)"
              >
                {{ t('fileManager.enter') }}
              </el-button>
              <el-button
                v-if="row.type === 'file'"
                type="primary"
                @click="downloadFile(row)"
              >
                {{ t('fileManager.download') }}
              </el-button>
              <el-dropdown @command="(cmd: string) => handleFileAction(cmd, row)">
                <el-button size="small" :icon="More" />
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item command="rename">{{ t('fileManager.rename') }}</el-dropdown-item>
                    <el-dropdown-item command="delete" divided>{{ t('common.delete') }}</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </el-button-group>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 统计信息 -->
    <div class="file-stats">
      <el-descriptions :column="3" border size="small">
        <el-descriptions-item :label="t('fileManager.fileCount')">
          {{ fileStats.fileCount }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('fileManager.folderCount')">
          {{ fileStats.folderCount }}
        </el-descriptions-item>
        <el-descriptions-item :label="t('fileManager.totalSize')">
          {{ formatFileSize(fileStats.totalSize) }}
        </el-descriptions-item>
      </el-descriptions>
    </div>

    <!-- 上传文件对话框 -->
    <el-dialog
      v-model="showUploadDialog"
      :title="t('fileManager.uploadFile')"
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
          {{ t('fileManager.dragOrClick') }}
        </div>
        <template #tip>
          <div class="el-upload__tip">
            {{ t('fileManager.uploadMultipleHint') }}
          </div>
        </template>
      </el-upload>

      <template #footer>
        <el-button @click="showUploadDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="uploadFiles">{{ t('fileManager.upload') }}</el-button>
      </template>
    </el-dialog>

    <!-- 新建文件夹对话框 -->
    <el-dialog
      v-model="showCreateFolderDialog"
      :title="t('fileManager.newFolder')"
      width="400px"
      append-to-body
    >
      <el-form ref="folderFormRef" :model="folderForm" :rules="folderRules">
        <el-form-item :label="t('fileManager.folderName')" prop="name">
          <el-input v-model="folderForm.name" :placeholder="t('fileManager.folderNamePlaceholder')" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showCreateFolderDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="createFolder">{{ t('fileManager.createFolder') }}</el-button>
      </template>
    </el-dialog>

    <!-- 重命名对话框 -->
    <el-dialog
      v-model="showRenameDialog"
      :title="t('fileManager.newName')"
      width="400px"
      append-to-body
    >
      <el-form ref="renameFormRef" :model="renameForm" :rules="renameRules">
        <el-form-item :label="t('fileManager.newName')" prop="name">
          <el-input v-model="renameForm.name" :placeholder="t('fileManager.newNamePlaceholder')" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showRenameDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="renameFile">{{ t('fileManager.confirmRename') }}</el-button>
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
  UploadFilled,
  More
} from '@element-plus/icons-vue'
import { volumeApi } from '@/api/volumes'
import { useI18n } from 'vue-i18n'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()

interface FileInfo {
  name: string
  path: string
  type: string
  size: number
  modified?: string
  permissions?: string
}

interface Props {
  modelValue: boolean
  volumeId: string
  volumeName: string
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// 响应式数据
const loading = ref(false)
const archiveLoading = ref(false)
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
const folderRules = computed(() => ({
  name: [
    { required: true, message: t('fileManager.validation.folderNameRequired'), trigger: 'blur' },
    { pattern: /^[^/\\:*?"<>|]+$/, message: t('fileManager.validation.folderNameInvalid'), trigger: 'blur' }
  ]
}))

const renameRules = computed(() => ({
  name: [
    { required: true, message: t('fileManager.validation.newNameRequired'), trigger: 'blur' },
    { pattern: /^[^/\\:*?"<>|]+$/, message: t('fileManager.validation.fileNameInvalid'), trigger: 'blur' }
  ]
}))

// 上传配置
const uploadUrl = computed(() => {
  return `/api/volumes/${props.volumeId}/files/upload?path=${encodeURIComponent(currentPath.value)}`
})

const uploadHeaders = computed(() => {
  return {
    'Authorization': `Bearer ${localStorage.getItem('token') || ''}`
  }
})

// 监听对话框打开
watch(visible, (newValue) => {
  if (newValue && props.volumeId) {
    currentPath.value = '/'
    loadFiles()
  }
})

// 方法
const loadFiles = async () => {
  if (!props.volumeId) return

  loading.value = true
  try {
    const response = await volumeApi.getVolumeFiles(props.volumeId, currentPath.value)
    // axios 拦截器已经解包了 response.data
    const data = response as any
    files.value = data.files || []
  } catch (error: any) {
    console.error(t('fileManager.messages.loadFailed'), error)
    ElMessage.error(error.response?.data?.message || t('fileManager.messages.loadFailed'))
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
  try {
    const filePath = `${currentPath.value.replace(/\/$/, '')}/${file.name}`
    const response = await volumeApi.downloadVolumeFile(props.volumeId, filePath)
    // axios 拦截器返回 response.data，对于 blob 响应，response 就是 Blob
    const blob = response as unknown as Blob
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = file.name
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)

    ElMessage.success(t('fileManager.messages.downloadSuccess'))
  } catch (error: any) {
    console.error(t('fileManager.messages.downloadFailed'), error)
    ElMessage.error(t('fileManager.messages.downloadFailed'))
  }
}

const downloadSelected = async () => {
  for (const file of selectedFiles.value) {
    if (file.type === 'file') {
      await downloadFile(file)
    }
  }
}

// 打包下载整个卷
const downloadAllAsArchive = async () => {
  archiveLoading.value = true
  try {
    const response = await volumeApi.downloadVolumeFile(props.volumeId, '/', true)
    // axios 拦截器返回 response.data，对于 blob 响应，response 就是 Blob
    const blob = response as unknown as Blob
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${props.volumeName || props.volumeId}_${new Date().toISOString().slice(0, 10)}.tar.gz`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    window.URL.revokeObjectURL(url)

    ElMessage.success(t('fileManager.messages.downloadSuccess'))
  } catch (error: any) {
    console.error(t('fileManager.messages.downloadFailed'), error)
    ElMessage.error(t('fileManager.messages.downloadFailed'))
  } finally {
    archiveLoading.value = false
  }
}

const beforeUpload = () => {
  return true
}

const handleUploadSuccess = () => {
  ElMessage.success(t('fileManager.messages.uploadSuccess'))
  loadFiles()
  showUploadDialog.value = false
}

const handleUploadError = () => {
  ElMessage.error(t('fileManager.messages.uploadFailed'))
}

const uploadFiles = () => {
  uploadRef.value?.submit()
}

const createFolder = async () => {
  if (!folderFormRef.value) return

  try {
    await folderFormRef.value.validate()

    await volumeApi.createVolumeFolder(props.volumeId, currentPath.value, folderForm.name)

    ElMessage.success(t('fileManager.messages.folderCreateSuccess'))
    showCreateFolderDialog.value = false
    folderForm.name = ''
    loadFiles()
  } catch (error: any) {
    console.error(t('fileManager.messages.folderCreateFailed'), error)
    ElMessage.error(error.response?.data?.message || t('fileManager.messages.folderCreateFailed'))
  }
}

const handleFileAction = (command: string, file: FileInfo) => {
  switch (command) {
    case 'rename':
      openRenameDialog(file)
      break
    case 'delete':
      showDeleteConfirm(file)
      break
  }
}

const openRenameDialog = (file: FileInfo) => {
  currentRenameFile = file
  renameForm.name = file.name
  showRenameDialog.value = true
}

const renameFile = async () => {
  if (!renameFormRef.value || !currentRenameFile) return

  try {
    await renameFormRef.value.validate()

    await volumeApi.renameVolumeFile(
      props.volumeId,
      currentPath.value,
      currentRenameFile.name,
      renameForm.name
    )

    ElMessage.success(t('fileManager.messages.renameSuccess'))
    showRenameDialog.value = false
    loadFiles()
  } catch (error: any) {
    console.error(t('fileManager.messages.renameFailed'), error)
    ElMessage.error(error.response?.data?.message || t('fileManager.messages.renameFailed'))
  }
}

const showDeleteConfirm = (file: FileInfo) => {
  ElMessageBox.confirm(
    t('fileManager.messages.deleteConfirm', {
      type: file.type === 'directory' ? t('fileManager.folderType') : t('fileManager.fileType'),
      name: file.name
    }),
    t('common.deleteConfirm'),
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
  try {
    const filePath = `${currentPath.value.replace(/\/$/, '')}/${file.name}`
    await volumeApi.deleteVolumeFile(props.volumeId, filePath, file.type === 'directory')

    ElMessage.success(t('fileManager.messages.deleteSuccess'))
    loadFiles()
  } catch (error: any) {
    console.error(t('fileManager.messages.deleteFailed'), error)
    ElMessage.error(error.response?.data?.message || t('fileManager.messages.deleteFailed'))
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
  if (!dateString) return '-'
  return formatLocalizedDateTime(dateString, '-')
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
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: var(--bg-app);
  border-radius: 8px;
}

.file-navigation :deep(.el-breadcrumb__item) {
  cursor: pointer;
}

.file-navigation :deep(.el-breadcrumb__inner:hover) {
  color: var(--color-primary);
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
}

.toolbar-left, .toolbar-right {
  display: flex;
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
  font-size: 18px;
}

.file-icon.directory {
  color: var(--color-warning);
}

.file-icon.file {
  color: var(--text-muted);
}

.name-text {
  word-break: break-all;
}

.file-stats {
  padding: 16px;
  background: var(--bg-app);
  border-radius: 8px;
}

.dialog-footer {
  text-align: right;
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
