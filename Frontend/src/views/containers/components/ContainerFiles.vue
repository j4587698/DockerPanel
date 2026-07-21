<template>
  <div class="container-files">
    <!-- 挂载点信息提示 -->
    <div v-if="mounts.length > 0" class="mounts-info">
      <div class="mounts-header" @click="showMountsInfo = !showMountsInfo">
        <el-icon><InfoFilled /></el-icon>
        <span>{{ t('container.filesModule.mountPointsCount', { count: mounts.length }) }}</span>
        <el-icon class="toggle-icon" :class="{ expanded: showMountsInfo }">
          <ArrowDown />
        </el-icon>
      </div>
      <div v-show="showMountsInfo" class="mounts-list">
        <div v-for="mount in mounts" :key="mount.destination" class="mount-item">
          <el-tag :type="mount.isNamedVolume ? 'success' : mount.isBindMount ? 'warning' : 'info'" size="small">
            {{ mount.isNamedVolume ? 'Volume' : mount.isBindMount ? 'Bind' : mount.type }}
          </el-tag>
          <span class="mount-destination">{{ mount.destination }}</span>
          <span class="mount-source" v-if="mount.source || mount.name">
            ← {{ mount.source || mount.name }}
          </span>
          <el-tag v-if="!mount.rw" type="danger" size="small">{{ t('container.filesModule.readOnly') }}</el-tag>
        </div>
        <div class="mount-legend">
          <span><el-tag type="success" size="small">Volume</el-tag> {{ t('container.filesModule.persistentStorage') }}</span>
          <span><el-tag type="warning" size="small">Bind</el-tag> {{ t('container.filesModule.hostMount') }}</span>
          <span><el-tag type="info" size="small">Tmpfs</el-tag> {{ t('container.filesModule.tmpfsStorage') }}</span>
        </div>
      </div>
    </div>

    <!-- 文件路径导航 -->
    <div class="file-navigation">
      <el-breadcrumb separator="/">
        <el-breadcrumb-item @click="navigateToPath('/')">
          <el-icon><HomeFilled /></el-icon>
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

      <div class="nav-actions">
        <el-input
          v-model="inputPath"
          :placeholder="t('container.filesModule.inputPath')"
          @keyup.enter="navigateToPath(inputPath)"
          style="width: 300px"
        >
          <template #append>
            <el-button @click="navigateToPath(inputPath)">{{ t('container.filesModule.goTo') }}</el-button>
          </template>
        </el-input>
      </div>
    </div>

    <!-- 文件操作工具栏 -->
    <div class="file-toolbar">
      <div class="toolbar-left">
        <el-button @click="loadFiles" :loading="loading" :icon="Refresh">{{ t('fileManager.refresh') }}</el-button>
        <el-button @click="showUploadDialog = true" :disabled="!canWrite" :icon="Upload">{{ t('container.filesModule.uploadFile') }}</el-button>
        <el-button @click="showCreateFolderDialog = true" :disabled="!canWrite" :icon="FolderAdd">{{ t('container.filesModule.newFolder') }}</el-button>
        <el-button
          @click="downloadSelected"
          :disabled="selectedFiles.length === 0"
         :icon="Download">{{ t('container.filesModule.downloadSelected') }}</el-button>
      </div>

      <div class="toolbar-right">
        <el-input
          v-model="searchKeyword"
          :placeholder="t('container.filesModule.searchFiles')"
          clearable
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
        @row-contextmenu="showContextMenu"
        v-loading="loading"
        style="height: 100%"
      >
        <el-table-column type="selection" width="55" />

        <el-table-column :label="t('fileManager.name')" min-width="250">
          <template #default="{ row }">
            <div class="file-name">
              <el-icon v-if="row.type === 'directory'" class="file-icon directory">
                <Folder />
              </el-icon>
              <el-icon v-else class="file-icon file">
                <Document />
              </el-icon>
              <span class="name-text">{{ row.name }}</span>
              <el-tag v-if="row.isMount" :type="getMountTagType(row.mountType)" size="small" class="mount-tag">
                {{ row.mountType === 'volume' ? 'Volume' : row.mountType === 'bind' ? 'Bind' : row.mountType }}
              </el-tag>
            </div>
          </template>
        </el-table-column>

        <el-table-column prop="size" :label="t('fileManager.size')" width="100">
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

        <el-table-column :label="t('container.filesModule.storageType')" width="160">
          <template #default="{ row }">
            <div class="storage-info">
              <el-tag v-if="row.isMount" :type="getMountTagType(row.mountType)" size="small">
                {{ getMountTypeLabel(row.mountType) }}
              </el-tag>
              <el-tag v-if="row.changeStatus === 'A'" type="success" size="small" class="change-tag">
                {{ t('container.filesModule.newFile') }}
              </el-tag>
              <el-tag v-else-if="row.changeStatus === 'C'" type="warning" size="small" class="change-tag">
                {{ t('container.filesModule.modified') }}
              </el-tag>
              <el-tag v-else-if="row.changeStatus === 'D'" type="danger" size="small" class="change-tag">
                {{ t('container.filesModule.deleted') }}
              </el-tag>
              <span v-if="!row.isMount && !row.changeStatus" class="storage-placeholder">-</span>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('container.filesModule.action')" width="184" fixed="right" align="center">
          <template #default="{ row }">
            <el-button-group size="small">
              <el-button
                v-if="row.type === 'directory'"
                type="primary"
                @click="enterDirectory(row)"
              >
                {{ t('container.filesModule.enter') }}
              </el-button>
              <el-button
                v-if="row.type === 'file'"
                type="primary"
                @click="openEditDialog(row)"
                :disabled="!canWrite"
              >
                {{ t('container.filesModule.edit') }}
              </el-button>
              <el-button
                v-if="row.type === 'file'"
                type="success"
                @click="downloadFile(row)"
              >
                {{ t('fileManager.download') }}
              </el-button>
              <el-dropdown @command="(cmd) => handleFileAction(cmd, row)">
                <el-button size="small" :icon="More" />
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item command="permissions">{{ t('container.filesModule.modifyPermissions') }}</el-dropdown-item>
                    <el-dropdown-item command="rename">{{ t('fileManager.rename') }}</el-dropdown-item>
                    <el-dropdown-item command="copyPath">{{ t('container.filesModule.copyPath') }}</el-dropdown-item>
                    <el-dropdown-item command="delete" divided :disabled="!canWrite">{{ t('fileManager.delete') }}</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </el-button-group>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 上传文件对话框 -->
    <el-dialog
      v-model="showUploadDialog"
      :title="t('container.filesModule.uploadDialogTitle')"
      width="500px"
    >
      <el-upload
        ref="uploadRef"
        :auto-upload="false"
        :on-change="handleFileChange"
        :file-list="uploadFileList"
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
        <el-button type="primary" @click="uploadFiles" :loading="uploading">{{ t('container.filesModule.startUpload') }}</el-button>
      </template>
    </el-dialog>

    <!-- 新建文件夹对话框 -->
    <el-dialog
      v-model="showCreateFolderDialog"
      :title="t('container.filesModule.newFolderDialogTitle')"
      width="400px"
    >
      <el-form :model="folderForm" :rules="folderRules" ref="folderFormRef">
        <el-form-item :label="t('container.filesModule.folderName')" prop="name">
          <el-input v-model="folderForm.name" :placeholder="t('container.filesModule.folderNamePlaceholder')" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showCreateFolderDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="createFolder" :loading="creating">{{ t('container.filesModule.create') }}</el-button>
      </template>
    </el-dialog>

    <!-- 重命名对话框 -->
    <el-dialog
      v-model="showRenameDialog"
      :title="t('container.filesModule.renameDialogTitle')"
      width="400px"
    >
      <el-form :model="renameForm" :rules="renameRules" ref="renameFormRef">
        <el-form-item :label="t('container.filesModule.newName')" prop="name">
          <el-input v-model="renameForm.name" :placeholder="t('container.filesModule.newNamePlaceholder')" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showRenameDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="renameFile" :loading="renaming">{{ t('container.filesModule.confirmRename') }}</el-button>
      </template>
    </el-dialog>

    <!-- 编辑文件对话框 -->
    <el-dialog
      v-model="showEditDialog"
      :title="t('container.filesModule.editFileDialogTitle', { name: editingFileName })"
      width="90%"
      top="5vh"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <div class="edit-container">
        <div class="edit-toolbar">
          <span class="file-path">{{ editingFilePath }}</span>
          <div class="edit-info">
            <span>{{ t('container.filesModule.lines') }}: {{ fileContentLineCount }}</span>
            <span>{{ t('container.filesModule.size') }}: {{ formatFileSize(fileContentSize) }}</span>
          </div>
        </div>
        <el-input
          v-model="fileContent"
          type="textarea"
          :rows="25"
          :placeholder="t('container.filesModule.fileContent')"
          class="code-editor"
          @keydown.ctrl.s.prevent="saveFileContent"
        />
      </div>

      <template #footer>
        <span class="shortcut-hint">{{ t('container.filesModule.saveShortcut') }}</span>
        <el-button @click="showEditDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="saveFileContent" :loading="savingContent">{{ t('common.save') }}</el-button>
      </template>
    </el-dialog>

    <!-- 修改权限对话框 -->
    <el-dialog
      v-model="showPermissionsDialog"
      :title="t('container.filesModule.permissionsDialogTitle')"
      width="450px"
    >
      <el-form :model="permissionsForm" label-width="100px">
        <el-form-item :label="t('container.filesModule.file')">
          <span class="file-name-display">{{ permissionsForm.fileName }}</span>
        </el-form-item>
        <el-form-item :label="t('container.filesModule.currentPermissions')">
          <el-tag>{{ permissionsForm.currentPermissions }}</el-tag>
        </el-form-item>
        <el-form-item :label="t('container.filesModule.newPermissions')">
          <el-input
            v-model="permissionsForm.permissions"
            :placeholder="t('container.filesModule.permissionsPlaceholder')"
            style="width: 120px"
          >
            <template #append>
              <el-dropdown @command="setPermissionsPreset">
                <el-button>
                  {{ t('container.filesModule.preset') }} <el-icon class="el-icon--right"><ArrowDown /></el-icon>
                </el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item command="755">{{ t('container.filesModule.permission755') }}</el-dropdown-item>
                    <el-dropdown-item command="644">{{ t('container.filesModule.permission644') }}</el-dropdown-item>
                    <el-dropdown-item command="600">{{ t('container.filesModule.permission600') }}</el-dropdown-item>
                    <el-dropdown-item command="777">{{ t('container.filesModule.permission777') }}</el-dropdown-item>
                    <el-dropdown-item command="700">{{ t('container.filesModule.permission700') }}</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </template>
          </el-input>
        </el-form-item>
        <el-form-item :label="t('container.filesModule.permissionDescription')">
          <div class="permissions-explanation">
            <div v-if="parsePermissions(permissionsForm.permissions)">
              <span v-for="(perm, idx) in parsePermissions(permissionsForm.permissions)" :key="idx" class="perm-group">
                <strong>{{ perm.label }}:</strong> {{ perm.value }}
              </span>
            </div>
            <span v-else class="text-muted">{{ t('container.filesModule.validOctalPermission') }}</span>
          </div>
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showPermissionsDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="changePermissions" :loading="changingPermissions">{{ t('container.filesModule.modify') }}</el-button>
      </template>
    </el-dialog>

    <!-- 右键菜单 -->
    <div
      v-show="contextMenuVisible"
      class="context-menu"
      :style="{ left: contextMenuX + 'px', top: contextMenuY + 'px' }"
      @click.stop
    >
      <div class="context-menu-item" @click="handleContextAction('open')" v-if="contextMenuFile?.type === 'directory'">
        <el-icon><FolderOpened /></el-icon>
        {{ t('container.filesModule.enterDirectory') }}
      </div>
      <div class="context-menu-item" @click="handleContextAction('edit')" v-if="contextMenuFile?.type === 'file'">
        <el-icon><Edit /></el-icon>
        {{ t('container.filesModule.editFile') }}
      </div>
      <div class="context-menu-item" @click="handleContextAction('download')" v-if="contextMenuFile?.type === 'file'">
        <el-icon><Download /></el-icon>
        {{ t('fileManager.download') }}
      </div>
      <div class="context-menu-divider"></div>
      <div class="context-menu-item" @click="handleContextAction('permissions')">
        <el-icon><Key /></el-icon>
        {{ t('container.filesModule.modifyPermissions') }}
      </div>
      <div class="context-menu-item" @click="handleContextAction('rename')">
        <el-icon><EditPen /></el-icon>
        {{ t('fileManager.rename') }}
      </div>
      <div class="context-menu-item" @click="handleContextAction('copyPath')">
        <el-icon><CopyDocument /></el-icon>
        {{ t('container.filesModule.copyPath') }}
      </div>
      <div class="context-menu-divider"></div>
      <div class="context-menu-item danger" @click="handleContextAction('delete')">
        <el-icon><Delete /></el-icon>
        {{ t('fileManager.delete') }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type UploadFile } from 'element-plus'
import {
  HomeFilled,
  Folder,
  Document,
  Refresh,
  Upload,
  Download,
  Search,
  FolderAdd,
  UploadFilled,
  InfoFilled,
  ArrowDown,
  More,
  Edit,
  EditPen,
  Key,
  FolderOpened,
  CopyDocument,
  Delete
} from '@element-plus/icons-vue'
import { containerApi, type ContainerFileInfo, type ContainerMountInfo, type ContainerFileListResponse } from '@/api/containers'
import { formatLocalizedDateTime } from '@/utils/date'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface Props {
  containerId: string
  containerState?: string
}

const props = defineProps<Props>()

// 响应式数据
const loading = ref(false)
const files = ref<ContainerFileInfo[]>([])
const mounts = ref<ContainerMountInfo[]>([])
const selectedFiles = ref<ContainerFileInfo[]>([])
const currentPath = ref('/')
const inputPath = ref('/')
const searchKeyword = ref('')
const showMountsInfo = ref(false)
const showUploadDialog = ref(false)
const showCreateFolderDialog = ref(false)
const showRenameDialog = ref(false)
const uploadRef = ref()
const uploadFileList = ref<UploadFile[]>([])
const uploading = ref(false)
const creating = ref(false)
const renaming = ref(false)
const folderFormRef = ref<FormInstance>()
const renameFormRef = ref<FormInstance>()

const folderForm = ref({ name: '' })
const renameForm = ref({ name: '', oldName: '', path: '' })

const folderRules = {
  name: [{ required: true, message: t('container.filesModule.folderNameRequired'), trigger: 'blur' }]
}

const renameRules = {
  name: [{ required: true, message: t('container.filesModule.newNameRequired'), trigger: 'blur' }]
}

// 编辑文件相关
const showEditDialog = ref(false)
const editingFileName = ref('')
const editingFilePath = ref('')
const fileContent = ref('')
const savingContent = ref(false)

// 编辑器计算属性
const fileContentLineCount = computed(() => fileContent.value.split('\n').length)
const fileContentSize = computed(() => new Blob([fileContent.value]).size)

// 修改权限相关
const showPermissionsDialog = ref(false)
const permissionsForm = ref({
  path: '',
  fileName: '',
  currentPermissions: '',
  permissions: ''
})
const changingPermissions = ref(false)

// 右键菜单相关
const contextMenuVisible = ref(false)
const contextMenuX = ref(0)
const contextMenuY = ref(0)
const contextMenuFile = ref<ContainerFileInfo | null>(null)

// 计算属性
const pathSegments = computed(() => {
  return currentPath.value
    .split('/')
    .filter(segment => segment.length > 0)
})

const filteredFiles = computed(() => {
  if (!searchKeyword.value) return files.value
  const keyword = searchKeyword.value.toLowerCase()
  return files.value.filter(f => f.name.toLowerCase().includes(keyword))
})

const canWrite = computed(() => {
  // 检查当前路径是否在只读挂载点内
  const mountInPath = mounts.value.find(m => 
    currentPath.value.startsWith(m.destination) && !m.rw
  )
  return !mountInPath && props.containerState === 'running'
})

// 方法
const loadFiles = async () => {
  if (!props.containerId) return
  
  loading.value = true
  try {
    const response = await containerApi.getContainerFiles(props.containerId, currentPath.value)
    files.value = response.files || []
    if (response.mounts) {
      mounts.value = response.mounts
    }
    inputPath.value = currentPath.value
  } catch (error: any) {
    console.error('loadFiles error:', error)
    ElMessage.error(error.message || t('container.filesModule.getFilesFailed'))
  } finally {
    loading.value = false
  }
}

const navigateToPath = (path: string) => {
  // 标准化路径
  let normalizedPath = path.replace(/\/+/g, '/')
  if (!normalizedPath.startsWith('/')) {
    normalizedPath = '/' + normalizedPath
  }
  currentPath.value = normalizedPath || '/'
  loadFiles()
}

const navigateToSegment = (index: number) => {
  const segments = pathSegments.value.slice(0, index + 1)
  currentPath.value = '/' + segments.join('/')
  loadFiles()
}

const handleSelectionChange = (selection: ContainerFileInfo[]) => {
  selectedFiles.value = selection
}

const handleRowDoubleClick = (row: ContainerFileInfo) => {
  if (row.type === 'directory') {
    enterDirectory(row)
  } else {
    downloadFile(row)
  }
}

const enterDirectory = (file: ContainerFileInfo) => {
  // 确保路径格式正确
  let newPath = file.path
  if (!newPath.startsWith('/')) {
    newPath = '/' + newPath
  }
  currentPath.value = newPath
  loadFiles()
}

const handleFileAction = async (command: string, file: ContainerFileInfo) => {
  switch (command) {
    case 'permissions':
      openPermissionsDialog(file)
      break
    case 'rename':
      openRenameDialog(file)
      break
    case 'copyPath':
      try {
        await navigator.clipboard.writeText(file.path)
        ElMessage.success(t('container.filesModule.copySuccess'))
      } catch {
        ElMessage.error(t('container.filesModule.copyFailed'))
      }
      break
    case 'delete':
      showDeleteConfirm(file)
      break
  }
}

const downloadFile = async (file: ContainerFileInfo) => {
  try {
    const response = await containerApi.downloadContainerFile(props.containerId, file.path)
    
    const blob = new Blob([response as any])
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = file.name
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
    
    ElMessage.success(t('container.filesModule.downloadSuccess'))
  } catch (error: any) {
    ElMessage.error(error.message || t('container.filesModule.downloadFailed'))
  }
}

const downloadSelected = async () => {
  for (const file of selectedFiles.value) {
    if (file.type === 'file') {
      await downloadFile(file)
    }
  }
}

const handleFileChange = (file: UploadFile, fileList: UploadFile[]) => {
  uploadFileList.value = fileList
}

const uploadFiles = async () => {
  if (uploadFileList.value.length === 0) {
    ElMessage.warning(t('container.filesModule.selectFileToUpload'))
    return
  }

  uploading.value = true
  try {
    for (const file of uploadFileList.value) {
      if (file.raw) {
        await containerApi.uploadContainerFile(props.containerId, currentPath.value, file.raw)
      }
    }
    
    ElMessage.success(t('container.filesModule.uploadSuccess'))
    showUploadDialog.value = false
    uploadFileList.value = []
    loadFiles()
  } catch (error: any) {
    ElMessage.error(error.message || t('container.filesModule.uploadFailed'))
  } finally {
    uploading.value = false
  }
}

const createFolder = async () => {
  await folderFormRef.value?.validate()
  
  creating.value = true
  try {
    await containerApi.createContainerFolder(props.containerId, currentPath.value, folderForm.value.name)
    ElMessage.success(t('container.filesModule.folderCreateSuccess'))
    showCreateFolderDialog.value = false
    folderForm.value.name = ''
    loadFiles()
  } catch (error: any) {
    ElMessage.error(error.message || t('container.filesModule.folderCreateFailed'))
  } finally {
    creating.value = false
  }
}

const openRenameDialog = (file: ContainerFileInfo) => {
  renameForm.value = {
    name: file.name,
    oldName: file.name,
    path: currentPath.value
  }
  showRenameDialog.value = true
}

const renameFile = async () => {
  await renameFormRef.value?.validate()
  
  renaming.value = true
  try {
    await containerApi.renameContainerFile(
      props.containerId,
      renameForm.value.path,
      renameForm.value.oldName,
      renameForm.value.name
    )
    ElMessage.success(t('container.filesModule.renameSuccess'))
    showRenameDialog.value = false
    loadFiles()
  } catch (error: any) {
    ElMessage.error(error.message || t('container.filesModule.renameFailed'))
  } finally {
    renaming.value = false
  }
}

const showDeleteConfirm = async (file: ContainerFileInfo) => {
  try {
    const fileType = file.type === 'directory' ? t('container.filesModule.folderName') : t('container.filesModule.file')
    const extra = file.type === 'directory' ? t('container.filesModule.folderDeleteExtra') : ''
    await ElMessageBox.confirm(
      t('container.filesModule.deleteConfirmMessage', { type: fileType, name: file.name, extra }),
      t('container.filesModule.deleteConfirm'),
      { type: 'warning' }
    )
    
    await containerApi.deleteContainerFile(props.containerId, file.path, file.type === 'directory')
    ElMessage.success(t('container.filesModule.deleteSuccess'))
    loadFiles()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.message || t('container.filesModule.deleteFailed'))
    }
  }
}

// 编辑文件
const openEditDialog = async (file: ContainerFileInfo) => {
  if (file.type !== 'file') return
  
  try {
    loading.value = true
    const response = await containerApi.getContainerFileContent(props.containerId, file.path)
    
    // API 返回的是 { content: string; path: string }
    let content = ''
    if (response && typeof response === 'object') {
      content = (response as any).content || ''
    } else if (typeof response === 'string') {
      content = response
    }
    
    if (!content) {
      ElMessage.warning(t('container.filesModule.fileEmpty'))
      return
    }
    
    // 检查是否为二进制文件（通过检查是否有不可打印字符）
    const hasBinaryChars = /[\x00-\x08\x0E-\x1F]/.test(content)
    
    if (hasBinaryChars) {
      ElMessage.warning(t('container.filesModule.binaryFileWarning'))
      return
    }
    
    // 检查文件大小（超过 1MB 不建议编辑）
    if (content.length > 1024 * 1024) {
      ElMessage.warning(t('container.filesModule.fileTooLargeWarning'))
      return
    }
    
    editingFileName.value = file.name
    editingFilePath.value = file.path
    fileContent.value = content
    showEditDialog.value = true
  } catch (error: any) {
    const errorMsg = error?.response?.data?.message || error?.response?.data?.error || error?.message || t('container.filesModule.getFilesFailed')
    ElMessage.error(errorMsg)
  } finally {
    loading.value = false
  }
}

const saveFileContent = async () => {
  if (!editingFilePath.value) return
  
  savingContent.value = true
  try {
    await containerApi.writeContainerFileContent(props.containerId, editingFilePath.value, fileContent.value)
    ElMessage.success(t('container.filesModule.fileSaveSuccess'))
    showEditDialog.value = false
    loadFiles()
  } catch (error: any) {
    const errorMsg = error.response?.data?.message || error.message || t('container.filesModule.fileSaveFailed')
    ElMessage.error(errorMsg)
  } finally {
    savingContent.value = false
  }
}

// 修改权限
const openPermissionsDialog = (file: ContainerFileInfo) => {
  permissionsForm.value = {
    path: file.path,
    fileName: file.name,
    currentPermissions: file.permissions || '-',
    permissions: ''
  }
  // 尝试从权限字符串提取数字权限
  const permMatch = file.permissions?.match(/[0-7]{3,4}/)
  if (permMatch) {
    permissionsForm.value.permissions = permMatch[0]
  }
  showPermissionsDialog.value = true
}

const setPermissionsPreset = (perm: string) => {
  permissionsForm.value.permissions = perm
}

const parsePermissions = (perm: string): { label: string; value: string }[] | null => {
  if (!perm || !/^[0-7]{3,4}$/.test(perm)) return null
  
  const permNum = perm.length === 4 ? perm.slice(-3) : perm
  const labels = [t('container.filesModule.owner'), t('container.filesModule.group'), t('container.filesModule.other')]
  const result: { label: string; value: string }[] = []
  
  for (let i = 0; i < 3; i++) {
    const n = parseInt(permNum[i])
    const r = n & 4 ? 'r' : '-'
    const w = n & 2 ? 'w' : '-'
    const x = n & 1 ? 'x' : '-'
    result.push({ label: labels[i], value: `${r}${w}${x}` })
  }
  
  return result
}

const changePermissions = async () => {
  if (!permissionsForm.value.permissions) {
    ElMessage.warning(t('container.filesModule.permissionEmpty'))
    return
  }
  
  if (!/^[0-7]{3,4}$/.test(permissionsForm.value.permissions)) {
    ElMessage.warning(t('container.filesModule.permissionInvalid'))
    return
  }
  
  changingPermissions.value = true
  try {
    await containerApi.changeContainerFilePermissions(
      props.containerId, 
      permissionsForm.value.path, 
      permissionsForm.value.permissions
    )
    ElMessage.success(t('container.filesModule.permissionModifySuccess'))
    showPermissionsDialog.value = false
    loadFiles()
  } catch (error: any) {
    ElMessage.error(error.message || t('container.filesModule.permissionModifyFailed'))
  } finally {
    changingPermissions.value = false
  }
}

// 右键菜单 - Element Plus el-table 的 row-contextmenu 事件参数顺序是 (row, column, event)
const showContextMenu = (row: ContainerFileInfo, column: any, event: MouseEvent) => {
  event.preventDefault()
  contextMenuFile.value = row
  contextMenuX.value = event.pageX
  contextMenuY.value = event.pageY
  contextMenuVisible.value = true
  
  // 点击其他地方关闭菜单
  setTimeout(() => {
    document.addEventListener('click', hideContextMenu, { once: true })
  }, 0)
}

const hideContextMenu = () => {
  contextMenuVisible.value = false
  contextMenuFile.value = null
}

const handleContextAction = async (action: string) => {
  if (!contextMenuFile.value) return
  
  const file = contextMenuFile.value
  hideContextMenu()
  
  switch (action) {
    case 'open':
      enterDirectory(file)
      break
    case 'edit':
      await openEditDialog(file)
      break
    case 'download':
      await downloadFile(file)
      break
    case 'permissions':
      openPermissionsDialog(file)
      break
    case 'rename':
      openRenameDialog(file)
      break
    case 'copyPath':
      try {
        await navigator.clipboard.writeText(file.path)
        ElMessage.success(t('container.filesModule.copySuccess'))
      } catch {
        ElMessage.error(t('container.filesModule.copyFailed'))
      }
      break
    case 'delete':
      await showDeleteConfirm(file)
      break
  }
}

// 工具方法
const formatFileSize = (bytes: number): string => {
  if (bytes === null || bytes === undefined || isNaN(bytes) || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const absBytes = Math.abs(bytes)
  if (absBytes < 1) return absBytes.toFixed(2) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const formatDateTime = (date?: string): string => {
  return formatLocalizedDateTime(date, '-')
}

const getMountTagType = (type?: string): string => {
  switch (type) {
    case 'volume': return 'success'
    case 'bind': return 'warning'
    case 'tmpfs': return 'info'
    default: return 'info'
  }
}

const getMountTypeLabel = (type?: string): string => {
  switch (type) {
    case 'volume': return 'Volume'
    case 'bind': return t('container.filesModule.bindMount')
    case 'tmpfs': return t('container.filesModule.temporaryStorage')
    default: return t('container.filesModule.containerInternal')
  }
}

// 监听容器ID变化
watch(() => props.containerId, () => {
  if (props.containerId && props.containerState === 'running') {
    loadFiles()
  }
}, { immediate: true })

// 监听容器状态变化
watch(() => props.containerState, (newState) => {
  if (newState === 'running') {
    loadFiles()
  }
})

onMounted(() => {
  if (props.containerId && props.containerState === 'running') {
    loadFiles()
  }
})
</script>

<style scoped>

.container-files {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 20px;
  background: var(--bg-surface);
  border-radius: 12px;
}

.mounts-info {
  margin-bottom: 16px;
  background: var(--bg-app);
  border-radius: 8px;
  overflow: hidden;
}

.mounts-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  cursor: pointer;
  font-size: 14px;
  color: var(--text-muted);
}

.mounts-header:hover {
  background: var(--bg-surface-hover);
}

.toggle-icon {
  margin-left: auto;
  transition: transform 0.3s;
}

.toggle-icon.expanded {
  transform: rotate(180deg);
}

.mounts-list {
  padding: 0 16px 16px;
}

.mount-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: var(--bg-surface);
  border-radius: 6px;
  margin-bottom: 8px;
  font-size: 13px;
}

.mount-destination {
  font-family: monospace;
  color: var(--text-main);
}

.mount-source {
  color: var(--text-muted);
  font-size: 12px;
}

.mount-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid var(--border-color);
  font-size: 12px;
  color: var(--text-muted);
}

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
  flex: 1;
  min-height: 0;
  background: var(--bg-app);
  border-radius: 8px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.file-list :deep(.el-table) {
  flex: 1;
}

.file-list :deep(.el-table__inner-wrapper) {
  height: 100%;
}

.file-list :deep(.el-table__body-wrapper) {
  flex: 1;
  overflow-y: auto;
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

.mount-tag {
  margin-left: 8px;
}

.storage-info {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: center;
}

.storage-placeholder {
  color: #909399;
  font-size: 12px;
  min-width: 20px;
  text-align: center;
}

.change-tag {
  margin-left: 4px;
}

.name-text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

:deep(.el-table) {
  --el-table-bg-color: transparent;
  --el-table-tr-bg-color: transparent;
  --el-table-row-hover-bg-color: var(--bg-surface-hover);
}

:deep(.el-upload-dragger) {
  background: var(--bg-app);
  border-color: var(--border-color);
}

/* 编辑文件对话框 */
.edit-container {
  display: flex;
  flex-direction: column;
  height: 70vh;
}

.edit-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: var(--bg-app);
  border-radius: 6px;
  margin-bottom: 12px;
}

.edit-toolbar .file-path {
  font-family: monospace;
  color: var(--text-muted);
  font-size: 13px;
}

.edit-info {
  display: flex;
  gap: 16px;
  font-size: 12px;
  color: var(--text-muted);
}

.code-editor :deep(textarea) {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 13px;
  line-height: 1.5;
  background: var(--bg-app);
  border-radius: 6px;
}

.shortcut-hint {
  margin-right: auto;
  color: var(--text-muted);
  font-size: 12px;
}

/* 权限对话框 */
.file-name-display {
  font-family: monospace;
  color: var(--text-main);
}

.permissions-explanation {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.perm-group {
  margin-right: 16px;
  font-size: 13px;
}

.perm-group strong {
  color: var(--text-muted);
}

/* 右键菜单 */
.context-menu {
  position: fixed;
  z-index: 9999;
  background: var(--bg-surface);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 4px 0;
  min-width: 160px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.context-menu-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  cursor: pointer;
  font-size: 13px;
  color: var(--text-main);
  transition: background 0.15s;
}

.context-menu-item:hover {
  background: var(--bg-surface-hover);
}

.context-menu-item.danger {
  color: var(--color-danger);
}

.context-menu-item .el-icon {
  font-size: 16px;
  color: var(--text-muted);
}

.context-menu-item.danger .el-icon {
  color: var(--color-danger);
}

.context-menu-divider {
  height: 1px;
  background: var(--border-color);
  margin: 4px 8px;
}

.text-muted {
  color: var(--text-muted);
}

</style>
