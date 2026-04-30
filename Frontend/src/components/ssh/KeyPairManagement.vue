<template>
  <div class="keypair-management">
    <!-- 工具栏 -->
    <div class="toolbar">
      <el-row :gutter="16" align="middle">
        <el-col :span="8">
          <el-input
            v-model="searchKeyword"
            placeholder="搜索密钥名称"
            clearable
            @input="handleSearch"
          >
            <template #prefix>
              <el-icon><Search /></el-icon>
            </template>
          </el-input>
        </el-col>
        <el-col :span="4">
          <el-select v-model="filterType" placeholder="类型" clearable @change="handleFilter">
            <el-option label="全部" value="" />
            <el-option label="RSA" value="rsa" />
            <el-option label="ED25519" value="ed25519" />
            <el-option label="ECDSA" value="ecdsa" />
          </el-select>
        </el-col>
        <el-col :span="12" class="toolbar-right">
          <el-button type="primary" @click="showGenerateDialog = true">
            <el-icon><Plus /></el-icon>
            生成密钥对
          </el-button>
          <el-button @click="showImportDialog = true">
            <el-icon><Upload /></el-icon>
            导入密钥
          </el-button>
          <el-button @click="refreshList">
            <el-icon><Refresh /></el-icon>
            刷新
          </el-button>
        </el-col>
      </el-row>
    </div>

    <!-- 密钥列表 -->
    <el-table
      v-loading="loading"
      :data="filteredKeyPairs"
      @selection-change="handleSelectionChange"
    >
      <el-table-column type="selection" width="55" />
      <el-table-column prop="name" label="名称" min-width="150" align="center">
        <template #default="{ row }">
          <div class="key-name">
            <el-icon class="key-icon"><Key /></el-icon>
            <span>{{ row.name }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column prop="keyType" label="类型" width="100" align="center">
        <template #default="{ row }">
          <el-tag size="small" :type="getKeyTypeTag(row.keyType)">
            {{ row.keyType.toUpperCase() }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="fingerprint" label="指纹" width="200" align="center">
        <template #default="{ row }">
          <el-tooltip :content="row.fingerprint" placement="top">
            <code class="fingerprint">{{ row.fingerprint?.substring(0, 20) }}...</code>
          </el-tooltip>
        </template>
      </el-table-column>
      <el-table-column prop="hasPassphrase" label="密码保护" width="100" align="center">
        <template #default="{ row }">
          <el-icon v-if="row.hasPassphrase" color="#67c23a"><Lock /></el-icon>
          <el-icon v-else color="#909399"><Unlock /></el-icon>
        </template>
      </el-table-column>
      <el-table-column prop="usageCount" label="使用次数" width="100" align="center" />
      <el-table-column prop="createdAt" label="创建时间" width="160" align="center">
        <template #default="{ row }">
          {{ formatDate(row.createdAt) }}
        </template>
      </el-table-column>
      <el-table-column label="操作" width="200" fixed="right" align="center">
        <template #default="{ row }">
          <el-button size="small" text @click="viewPublicKey(row)">
            <el-icon><View /></el-icon>
            公钥
          </el-button>
          <el-button size="small" text @click="downloadPublicKey(row)">
            <el-icon><Download /></el-icon>
            下载
          </el-button>
          <el-popconfirm
            title="确定要删除此密钥吗？"
            confirm-button-text="删除"
            cancel-button-text="取消"
            @confirm="deleteKeyPair(row)"
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

    <!-- 分页 -->
    <div class="pagination-wrapper">
      <el-pagination
        v-model:current-page="pagination.page"
        v-model:page-size="pagination.pageSize"
        :total="pagination.total"
        :page-sizes="[10, 20, 50, 100]"
        layout="total, sizes, prev, pager, next"
        @size-change="handlePageSizeChange"
        @current-change="handlePageChange"
      />
    </div>

    <!-- 批量操作 -->
    <div v-if="selectedKeys.length > 0" class="batch-actions">
      <span>已选择 {{ selectedKeys.length }} 个密钥</span>
      <el-button size="small" type="danger" @click="batchDelete">
        批量删除
      </el-button>
    </div>

    <!-- 生成密钥对话框 -->
    <GenerateKeyPairDialog
      v-model="showGenerateDialog"
      @success="handleKeyPairGenerated"
    />

    <!-- 导入密钥对话框 -->
    <el-dialog v-model="showImportDialog" title="导入密钥" width="500px">
      <el-form :model="importForm" label-width="100px">
        <el-form-item label="密钥名称" required>
          <el-input v-model="importForm.name" placeholder="请输入密钥名称" />
        </el-form-item>
        <el-form-item label="公钥内容" required>
          <el-input
            v-model="importForm.publicKey"
            type="textarea"
            :rows="4"
            placeholder="粘贴公钥内容 (ssh-rsa ... 或 ssh-ed25519 ...)"
          />
        </el-form-item>
        <el-form-item label="私钥内容">
          <el-input
            v-model="importForm.privateKey"
            type="textarea"
            :rows="6"
            placeholder="可选，粘贴私钥内容"
          />
        </el-form-item>
        <el-form-item label="私钥密码">
          <el-input
            v-model="importForm.passphrase"
            type="password"
            show-password
            placeholder="如果私钥有密码保护"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showImportDialog = false">取消</el-button>
        <el-button type="primary" @click="importKeyPair" :loading="importing">
          导入
        </el-button>
      </template>
    </el-dialog>

    <!-- 查看公钥对话框 -->
    <el-dialog v-model="showPublicKeyDialog" title="公钥内容" width="600px">
      <el-input
        :model-value="currentPublicKey"
        type="textarea"
        :rows="8"
        readonly
        class="public-key-content"
      />
      <template #footer>
        <el-button @click="copyPublicKey">
          <el-icon><CopyDocument /></el-icon>
          复制
        </el-button>
        <el-button @click="showPublicKeyDialog = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  Search, Plus, Upload, Refresh, Key, Lock, Unlock,
  View, Download, Delete, CopyDocument
} from '@element-plus/icons-vue'
import { useSshStore } from '@/stores/ssh'
import { useSettingsStore } from '@/stores/settings'
import GenerateKeyPairDialog from './GenerateKeyPairDialog.vue'
import { formatLocalizedDateTime } from '@/utils/date'

interface KeyPair {
  id: string
  name: string
  keyType: string
  fingerprint: string
  publicKey: string
  hasPassphrase: boolean
  usageCount: number
  createdAt: string
}

const sshStore = useSshStore()
const settingsStore = useSettingsStore()
const loading = ref(false)
const importing = ref(false)
const searchKeyword = ref('')
const filterType = ref('')
const keyPairs = ref<KeyPair[]>([])
const selectedKeys = ref<KeyPair[]>([])

const showGenerateDialog = ref(false)
const showImportDialog = ref(false)
const showPublicKeyDialog = ref(false)
const currentPublicKey = ref('')

const pagination = reactive({
  page: 1,
  pageSize: settingsStore.defaultPageSize,
  total: 0
})

const importForm = reactive({
  name: '',
  publicKey: '',
  privateKey: '',
  passphrase: ''
})

const filteredKeyPairs = computed(() => {
  let result = keyPairs.value

  if (searchKeyword.value) {
    result = result.filter(k =>
      k.name.toLowerCase().includes(searchKeyword.value.toLowerCase())
    )
  }

  if (filterType.value) {
    result = result.filter(k => k.keyType === filterType.value)
  }

  return result
})

onMounted(() => {
  fetchKeyPairs()
})

const fetchKeyPairs = async () => {
  loading.value = true
  try {
    const response = await sshStore.fetchKeyPairs({
      page: pagination.page,
      pageSize: pagination.pageSize
    })
    keyPairs.value = response.items || []
    pagination.total = response.total || 0
  } catch (error) {
    ElMessage.error('获取密钥列表失败')
  } finally {
    loading.value = false
  }
}

watch(() => settingsStore.defaultPageSize, (size) => {
  pagination.pageSize = size
  pagination.page = 1
  fetchKeyPairs()
})

const handleSearch = () => {
  pagination.page = 1
}

const handleFilter = () => {
  pagination.page = 1
}

const refreshList = () => {
  fetchKeyPairs()
}

const handlePageChange = (page: number) => {
  pagination.page = page
  fetchKeyPairs()
}

const handlePageSizeChange = (size: number) => {
  pagination.pageSize = size
  pagination.page = 1
  fetchKeyPairs()
}

const handleSelectionChange = (selection: KeyPair[]) => {
  selectedKeys.value = selection
}

const getKeyTypeTag = (type: string) => {
  const typeMap: Record<string, string> = {
    rsa: 'primary',
    ed25519: 'success',
    ecdsa: 'warning'
  }
  return typeMap[type] || 'info'
}

const formatDate = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

const viewPublicKey = (keyPair: KeyPair) => {
  currentPublicKey.value = keyPair.publicKey
  showPublicKeyDialog.value = true
}

const copyPublicKey = async () => {
  try {
    await navigator.clipboard.writeText(currentPublicKey.value)
    ElMessage.success('公钥已复制到剪贴板')
  } catch {
    ElMessage.error('复制失败')
  }
}

const downloadPublicKey = (keyPair: KeyPair) => {
  const blob = new Blob([keyPair.publicKey], { type: 'text/plain' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${keyPair.name}.pub`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  ElMessage.success('公钥已下载')
}

const deleteKeyPair = async (keyPair: KeyPair) => {
  try {
    await sshStore.deleteKeyPair(keyPair.id)
    ElMessage.success('密钥已删除')
    fetchKeyPairs()
  } catch {
    ElMessage.error('删除失败')
  }
}

const batchDelete = async () => {
  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedKeys.value.length} 个密钥吗？`,
      '批量删除',
      { type: 'warning' }
    )

    for (const key of selectedKeys.value) {
      await sshStore.deleteKeyPair(key.id)
    }

    ElMessage.success('批量删除成功')
    selectedKeys.value = []
    fetchKeyPairs()
  } catch {
    // 用户取消
  }
}

const importKeyPair = async () => {
  if (!importForm.name || !importForm.publicKey) {
    ElMessage.warning('请填写密钥名称和公钥内容')
    return
  }

  importing.value = true
  try {
    await sshStore.importKeyPair({
      name: importForm.name,
      publicKey: importForm.publicKey,
      privateKey: importForm.privateKey || undefined,
      passphrase: importForm.passphrase || undefined
    })

    ElMessage.success('密钥导入成功')
    showImportDialog.value = false
    importForm.name = ''
    importForm.publicKey = ''
    importForm.privateKey = ''
    importForm.passphrase = ''
    fetchKeyPairs()
  } catch {
    ElMessage.error('密钥导入失败')
  } finally {
    importing.value = false
  }
}

const handleKeyPairGenerated = () => {
  fetchKeyPairs()
}
</script>

<style scoped>
.keypair-management {
  padding: 0;
}

.toolbar {
  margin-bottom: 20px;
}

.toolbar-right {
  text-align: right;
}

.key-name {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.key-icon {
  color: #409eff;
}

.fingerprint {
  font-size: 12px;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  color: #606266;
  background-color: #f5f7fa;
  padding: 2px 6px;
  border-radius: 3px;
}

.pagination-wrapper {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
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

.public-key-content :deep(.el-textarea__inner) {
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  font-size: 12px;
}

</style>

<style>
/* === Dark Mode === */
html.dark .fingerprint {
  background-color: #1a1a1a;
  color: #a3a6ad;
}

html.dark .batch-actions {
  background-color: #1a1a1a;
}
</style>
