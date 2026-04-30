<template>
  <div class="file-transfer">
    <!-- 工具栏 -->
    <div class="toolbar">
      <el-row :gutter="16" align="middle">
        <el-col :span="8">
          <el-select
            v-model="selectedConnectionId"
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
            />
          </el-select>
        </el-col>
        <el-col :span="16" class="toolbar-right">
          <el-button
            type="primary"
            @click="showUploadDialog = true"
            :disabled="!selectedConnectionId"
          >
            <el-icon><Upload /></el-icon>
            上传文件
          </el-button>
          <el-button @click="refreshFiles" :disabled="!selectedConnectionId">
            <el-icon><Refresh /></el-icon>
            刷新
          </el-button>
        </el-col>
      </el-row>
    </div>

    <!-- 远程文件浏览器 -->
    <el-card v-if="selectedConnectionId" class="file-browser">
      <template #header>
        <div class="browser-header">
          <div class="path-nav">
            <el-breadcrumb separator="/">
              <el-breadcrumb-item
                v-for="(segment, index) in pathSegments"
                :key="index"
                @click="navigateTo(index)"
              >
                <span class="path-segment">{{ segment || '/' }}</span>
              </el-breadcrumb-item>
            </el-breadcrumb>
          </div>
          <el-input
            v-model="currentPath"
            placeholder="输入路径"
            style="width: 300px"
            @keyup.enter="loadDirectory"
          >
            <template #append>
              <el-button @click="loadDirectory">
                <el-icon><Right /></el-icon>
              </el-button>
            </template>
          </el-input>
        </div>
      </template>

      <el-table
        v-loading="loading"
        :data="files"
        @row-dblclick="handleRowDblClick"
        highlight-current-row
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="55" />
        <el-table-column label="名称" min-width="250" align="center">
          <template #default="{ row }">
            <div class="file-name" @click.stop="handleFileClick(row)">
              <el-icon class="file-icon" :class="getFileClass(row)">
                <component :is="getFileIcon(row)" />
              </el-icon>
              <span>{{ row.name }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="size" label="大小" width="120" align="center">
          <template #default="{ row }">
            {{ row.isDirectory ? '-' : formatSize(row.size) }}
          </template>
        </el-table-column>
        <el-table-column prop="permissions" label="权限" width="120" align="center">
          <template #default="{ row }">
            <code class="permissions">{{ row.permissions }}</code>
          </template>
        </el-table-column>
        <el-table-column prop="owner" label="所有者" width="100" align="center" />
        <el-table-column prop="modifiedAt" label="修改时间" width="160" align="center">
          <template #default="{ row }">
            {{ formatDate(row.modifiedAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="180" fixed="right" align="center">
          <template #default="{ row }">
            <el-button
              v-if="!row.isDirectory"
              size="small"
              text
              @click="downloadFile(row)"
            >
              <el-icon><Download /></el-icon>
              下载
            </el-button>
            <el-popconfirm
              title="确定要删除吗？"
              @confirm="deleteFile(row)"
            >
              <template #reference>
                <el-button size="small" text type="danger">
                  <el-icon><Delete /></el-icon>
                  删除
                </el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>

      <!-- 批量操作 -->
      <div v-if="selectedFiles.length > 0" class="batch-actions">
        <span>已选择 {{ selectedFiles.length }} 个文件</span>
        <el-button size="small" @click="batchDownload">批量下载</el-button>
        <el-button size="small" type="danger" @click="batchDelete">批量删除</el-button>
      </div>
    </el-card>

    <!-- 传输任务列表 -->
    <el-card v-if="transfers.length > 0" class="transfer-list">
      <template #header>
        <div class="card-header">
          <span>传输任务</span>
          <el-button size="small" text @click="clearCompletedTransfers">
            清除已完成
          </el-button>
        </div>
      </template>

      <div class="transfer-items">
        <div
          v-for="transfer in transfers"
          :key="transfer.id"
          class="transfer-item"
        >
          <div class="transfer-info">
            <el-icon class="transfer-icon">
              <component :is="transfer.type === 'upload' ? Upload : Download" />
            </el-icon>
            <div class="transfer-details">
              <div class="transfer-name">{{ transfer.filename }}</div>
              <div class="transfer-meta">
                {{ transfer.type === 'upload' ? '上传到' : '下载自' }}
                {{ transfer.host }}
              </div>
            </div>
          </div>
          <div class="transfer-progress">
            <el-progress
              :percentage="transfer.progress"
              :status="getTransferStatus(transfer)"
              :stroke-width="6"
            />
          </div>
          <div class="transfer-actions">
            <el-button
              v-if="transfer.status === 'pending' || transfer.status === 'transferring'"
              size="small"
              text
              type="danger"
              @click="cancelTransfer(transfer)"
            >
              取消
            </el-button>
          </div>
        </div>
      </div>
    </el-card>

    <!-- 上传对话框 -->
    <el-dialog v-model="showUploadDialog" title="上传文件" width="500px">
      <el-form :model="uploadForm" label-width="100px">
        <el-form-item label="选择文件">
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :limit="10"
            :on-change="handleUploadChange"
            :file-list="uploadForm.files"
            drag
            multiple
          >
            <el-icon class="el-icon--upload"><UploadFilled /></el-icon>
            <div class="el-upload__text">
              拖拽文件到此处，或<em>点击选择</em>
            </div>
          </el-upload>
        </el-form-item>
        <el-form-item label="目标路径">
          <el-input v-model="uploadForm.remotePath" placeholder="默认为当前目录" />
        </el-form-item>
        <el-form-item label="覆盖已存在">
          <el-switch v-model="uploadForm.overwrite" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showUploadDialog = false">取消</el-button>
        <el-button
          type="primary"
          @click="startUpload"
          :disabled="uploadForm.files.length === 0"
        >
          开始上传
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, shallowRef } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  Upload, Download, Refresh, Delete, Right,
  Folder, Document, Picture, VideoPlay, UploadFilled
} from '@element-plus/icons-vue'
import { useSshStore } from '@/stores/ssh'
import type { SshConnectionConfig } from '@/types/ssh'
import type { UploadFile, UploadInstance } from 'element-plus'
import { formatLocalizedDateTime } from '@/utils/date'

interface RemoteFile {
  name: string
  path: string
  isDirectory: boolean
  size: number
  permissions: string
  owner: string
  group: string
  modifiedAt: string
}

interface Transfer {
  id: string
  type: 'upload' | 'download'
  filename: string
  host: string
  progress: number
  status: 'pending' | 'transferring' | 'completed' | 'error' | 'cancelled'
  error?: string
}

const sshStore = useSshStore()
const uploadRef = ref<UploadInstance>()

const loading = ref(false)
const selectedConnectionId = ref('')
const currentPath = ref('/home')
const connections = ref<SshConnectionConfig[]>([])
const files = ref<RemoteFile[]>([])
const selectedFiles = ref<RemoteFile[]>([])
const transfers = ref<Transfer[]>([])
const showUploadDialog = ref(false)

const uploadForm = reactive({
  files: [] as UploadFile[],
  remotePath: '',
  overwrite: false
})

const pathSegments = computed(() => {
  return currentPath.value.split('/').filter(Boolean)
})

onMounted(async () => {
  await fetchConnections()
})

const fetchConnections = async () => {
  try {
    const response = await sshStore.fetchConnectionConfigs({ page: 1, pageSize: 100 })
    connections.value = response.items || []
  } catch {
    ElMessage.error('获取连接列表失败')
  }
}

const handleConnectionChange = () => {
  currentPath.value = '/home'
  loadDirectory()
}

const loadDirectory = async () => {
  if (!selectedConnectionId.value) return

  loading.value = true
  try {
    const response = await sshStore.listDirectory({
      connectionId: selectedConnectionId.value,
      path: currentPath.value
    })
    files.value = response.files || []
  } catch (error: any) {
    ElMessage.error(error.message || '加载目录失败')
    files.value = []
  } finally {
    loading.value = false
  }
}

const refreshFiles = () => {
  loadDirectory()
}

const navigateTo = (index: number) => {
  const segments = pathSegments.value.slice(0, index + 1)
  currentPath.value = '/' + segments.join('/')
  loadDirectory()
}

const handleRowDblClick = (row: RemoteFile) => {
  if (row.isDirectory) {
    currentPath.value = row.path
    loadDirectory()
  }
}

const handleFileClick = (row: RemoteFile) => {
  if (row.isDirectory) {
    currentPath.value = row.path
    loadDirectory()
  }
}

const handleSelectionChange = (selection: RemoteFile[]) => {
  selectedFiles.value = selection
}

const getFileIcon = (file: RemoteFile) => {
  if (file.isDirectory) return Folder
  const ext = file.name.split('.').pop()?.toLowerCase()
  if (['jpg', 'jpeg', 'png', 'gif', 'webp', 'svg'].includes(ext || '')) return Picture
  if (['mp4', 'avi', 'mkv', 'mov'].includes(ext || '')) return VideoPlay
  return Document
}

const getFileClass = (file: RemoteFile) => {
  return {
    'folder-icon': file.isDirectory,
    'file-icon': !file.isDirectory
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

const formatDate = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

const downloadFile = async (file: RemoteFile) => {
  const connection = connections.value.find(c => c.id === selectedConnectionId.value)
  if (!connection) return

  const transfer: Transfer = {
    id: Date.now().toString(),
    type: 'download',
    filename: file.name,
    host: connection.host,
    progress: 0,
    status: 'pending'
  }
  transfers.value.unshift(transfer)

  try {
    transfer.status = 'transferring'
    await sshStore.downloadFile({
      connectionId: selectedConnectionId.value,
      remotePath: file.path,
      onProgress: (progress: number) => {
        transfer.progress = progress
      }
    })
    transfer.progress = 100
    transfer.status = 'completed'
    ElMessage.success(`${file.name} 下载完成`)
  } catch (error: any) {
    transfer.status = 'error'
    transfer.error = error.message
    ElMessage.error(`下载失败: ${error.message}`)
  }
}

const deleteFile = async (file: RemoteFile) => {
  try {
    await sshStore.deleteRemoteFile({
      connectionId: selectedConnectionId.value,
      path: file.path
    })
    ElMessage.success('删除成功')
    loadDirectory()
  } catch (error: any) {
    ElMessage.error(`删除失败: ${error.message}`)
  }
}

const batchDownload = async () => {
  for (const file of selectedFiles.value) {
    if (!file.isDirectory) {
      await downloadFile(file)
    }
  }
}

const batchDelete = async () => {
  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedFiles.value.length} 个文件吗？`,
      '批量删除',
      { type: 'warning' }
    )

    for (const file of selectedFiles.value) {
      await sshStore.deleteRemoteFile({
        connectionId: selectedConnectionId.value,
        path: file.path
      })
    }

    ElMessage.success('批量删除成功')
    selectedFiles.value = []
    loadDirectory()
  } catch {
    // 用户取消
  }
}

const handleUploadChange = (uploadFile: UploadFile, uploadFiles: UploadFile[]) => {
  uploadForm.files = uploadFiles
}

const startUpload = async () => {
  const connection = connections.value.find(c => c.id === selectedConnectionId.value)
  if (!connection || uploadForm.files.length === 0) return

  showUploadDialog.value = false
  const remotePath = uploadForm.remotePath || currentPath.value

  for (const file of uploadForm.files) {
    const transfer: Transfer = {
      id: Date.now().toString() + file.name,
      type: 'upload',
      filename: file.name,
      host: connection.host,
      progress: 0,
      status: 'pending'
    }
    transfers.value.unshift(transfer)

    try {
      transfer.status = 'transferring'
      await sshStore.uploadFile({
        connectionId: selectedConnectionId.value,
        file: file.raw!,
        remotePath,
        overwrite: uploadForm.overwrite,
        onProgress: (progress: number) => {
          transfer.progress = progress
        }
      })
      transfer.progress = 100
      transfer.status = 'completed'
    } catch (error: any) {
      transfer.status = 'error'
      transfer.error = error.message
    }
  }

  uploadForm.files = []
  loadDirectory()
}

const getTransferStatus = (transfer: Transfer) => {
  if (transfer.status === 'completed') return 'success'
  if (transfer.status === 'error') return 'exception'
  return ''
}

const cancelTransfer = (transfer: Transfer) => {
  transfer.status = 'cancelled'
}

const clearCompletedTransfers = () => {
  transfers.value = transfers.value.filter(
    t => t.status !== 'completed' && t.status !== 'cancelled'
  )
}
</script>

<style scoped>
.file-transfer {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.toolbar {
  margin-bottom: 0;
}

.toolbar-right {
  text-align: right;
}

.browser-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.path-nav {
  flex: 1;
}

.path-segment {
  cursor: pointer;
  color: #409eff;
}

.path-segment:hover {
  text-decoration: underline;
}

.file-name {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

.file-name:hover {
  color: #409eff;
}

.file-icon {
  font-size: 18px;
}

.folder-icon {
  color: #e6a23c;
}

.permissions {
  font-size: 12px;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  color: #606266;
}

.batch-actions {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-top: 16px;
  padding: 12px 16px;
  background-color: #f5f7fa;
  border-radius: 4px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.transfer-items {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.transfer-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 12px;
  background-color: #f8f9fa;
  border-radius: 6px;
}

.transfer-info {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
}

.transfer-icon {
  font-size: 24px;
  color: #409eff;
}

.transfer-name {
  font-weight: 500;
  color: #303133;
}

.transfer-meta {
  font-size: 12px;
  color: #909399;
}

.transfer-progress {
  width: 200px;
}

</style>

<style>
/* === Dark Mode === */
html.dark .batch-actions,
html.dark .transfer-item {
  background-color: #1a1a1a;
}

html.dark .transfer-name {
  color: #e5eaf3;
}

html.dark .permissions {
  color: #a3a6ad;
}
</style>
