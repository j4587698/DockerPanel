<template>
  <div class="page-container" v-loading="loading">
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ t('registry.title') }}</h1>
        <p class="page-subtitle">{{ t('registry.pageSubtitle') }}</p>
      </div>
      <div class="header-actions">
        <el-button type="primary" :icon="Plus" @click="showCreateDialog = true">{{ t('registry.addRegistry') }}</el-button>
        <el-button :icon="Refresh" @click="refreshData">{{ t('common.refresh') }}</el-button>
      </div>
    </div>

    <div class="saas-card">
      <div class="toolbar">
        <el-input v-model="search" :placeholder="t('registry.searchPlaceholder')" :prefix-icon="Search" class="toolbar-search" clearable />
      </div>

      <el-table :data="filteredRegistries" style="width: 100%">
        <el-table-column :label="t('registry.registryName')" min-width="200">
          <template #default="{ row }">
            <div class="name-cell">
              <el-icon class="icon"><Connection /></el-icon>
              <div class="text">
                <span class="main">{{ row.name }}</span>
                <el-tag v-if="row.isDefault" size="small" effect="dark">{{ t('registry.defaultTag') }}</el-tag>
              </div>
            </div>
          </template>
        </el-table-column>

        <el-table-column prop="url" :label="t('registry.address')" min-width="250" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="font-mono text-secondary">{{ row.url }}</span>
          </template>
        </el-table-column>

        <el-table-column prop="registryType" :label="t('registry.registryType')" width="120" />

        <el-table-column :label="t('common.status')" width="120">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status)" effect="light">
              {{ row.status }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column :label="t('common.actions')" width="180" align="center" fixed="right">
          <template #default="{ row }">
            <div class="actions-cell">
              <el-tooltip :content="t('registry.testConnection')">
                <el-button circle size="small" :icon="Refresh" @click="testConnection(row)" />
              </el-tooltip>
              <el-tooltip :content="t('common.edit')">
                <el-button circle size="small" type="primary" plain :icon="Edit" @click="editRegistry(row)" />
              </el-tooltip>
              <el-tooltip :content="t('common.delete')">
                <el-button circle size="small" type="danger" plain :icon="Delete" @click="deleteRegistry(row)" />
              </el-tooltip>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- Create/Edit Dialog -->
    <el-dialog v-model="showCreateDialog" :title="dialogTitle" width="560px" @close="resetForm">
      <el-form label-position="top">
        <el-form-item :label="t('registry.name')" required>
          <el-input v-model.trim="form.name" :placeholder="t('registry.registryNamePlaceholder')" />
        </el-form-item>
        <el-form-item :label="t('registry.registryType')">
          <el-select v-model="form.registryType" style="width: 100%">
            <el-option :label="t('registry.typePrivate')" :value="RegistryType.Private" />
            <el-option :label="t('registry.typeMirror')" :value="RegistryType.Mirror" />
            <el-option :label="t('registry.typeDockerHub')" :value="RegistryType.DockerHub" />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('registry.address')" required>
          <el-input v-model.trim="form.domain" :placeholder="t('registry.domainPlaceholder')" />
          <div class="form-hint">{{ t('registry.domainHint') }}</div>
        </el-form-item>
        <el-form-item :label="t('registry.username')">
          <el-input v-model.trim="form.username" :placeholder="t('registry.usernamePlaceholder')" autocomplete="username" />
        </el-form-item>
        <el-form-item :label="t('registry.passwordToken')">
          <el-input v-model="form.password" type="password" show-password autocomplete="new-password" />
          <div v-if="editingId" class="form-hint">{{ t('registry.passwordEditHint') }}</div>
        </el-form-item>
        <el-form-item>
          <el-checkbox v-model="form.isSecure">HTTPS</el-checkbox>
          <el-checkbox v-model="form.isPublic">{{ t('registry.anonymous') }}</el-checkbox>
          <el-checkbox v-model="form.isDefault">{{ t('registry.setAsDefault') }}</el-checkbox>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showCreateDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button @click="testFormConnection" :loading="testing">{{ t('registry.testConnection') }}</el-button>
        <el-button type="primary" @click="saveRegistry" :loading="saving">{{ t('common.save') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Search, Delete, Connection, Edit } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { registryApi } from '@/api/registry'
import { RegistryType } from '@/types/registry'

const { t } = useI18n()

const loading = ref(false)
const search = ref('')
const registries = ref<any[]>([])
const showCreateDialog = ref(false)
const saving = ref(false)
const testing = ref(false)
const editingId = ref<string | null>(null)
const form = ref({
  name: '',
  domain: '',
  username: '',
  password: '',
  isPublic: true,
  isSecure: true,
  isDefault: false,
  registryType: RegistryType.Private
})

const dialogTitle = computed(() => editingId.value ? t('registry.editRegistry') : t('registry.addRegistry'))

const filteredRegistries = computed(() => {
  if (!search.value) return registries.value
  return registries.value.filter(r => r.name.toLowerCase().includes(search.value.toLowerCase()))
})

const getStatusType = (status: string) => {
  return status === 'Online' ? 'success' : 'danger'
}

const refreshData = async () => {
  loading.value = true
  try {
    const res = await registryApi.getRegistries()
    registries.value = Array.isArray(res) ? res : []
  } catch {
    ElMessage.error(t('registry.loadFailed'))
  } finally {
    loading.value = false
  }
}

const testConnection = async (row: any) => {
  try {
    await registryApi.testRegistryConnection(row.id)
    ElMessage.success(t('registry.connectionSuccess'))
    refreshData()
  } catch { ElMessage.error(t('registry.connectionFailed')) }
}

const normalizeDomain = (domain: string) => domain.replace(/^https?:\/\//i, '').replace(/\/+$/, '')

const validateForm = () => {
  if (!form.value.name.trim()) {
    ElMessage.warning(t('registry.nameRequired'))
    return false
  }

  if (!form.value.domain.trim()) {
    ElMessage.warning(t('registry.addressRequired'))
    return false
  }

  return true
}

const resetForm = () => {
  editingId.value = null
  form.value = {
    name: '',
    domain: '',
    username: '',
    password: '',
    isPublic: true,
    isSecure: true,
    isDefault: false,
    registryType: RegistryType.Private
  }
}

const editRegistry = (row: any) => {
  editingId.value = row.id
  form.value = {
    name: row.name || '',
    domain: row.domain || normalizeDomain(row.url || ''),
    username: row.username || '',
    password: '',
    isPublic: Boolean(row.isPublic),
    isSecure: row.isSecure !== false,
    isDefault: Boolean(row.isDefault),
    registryType: row.registryType ?? RegistryType.Private
  }
  showCreateDialog.value = true
}

const buildPayload = () => ({
  name: form.value.name.trim(),
  domain: normalizeDomain(form.value.domain.trim()),
  username: form.value.username.trim() || undefined,
  password: form.value.password || undefined,
  isPublic: form.value.isPublic,
  isSecure: form.value.isSecure,
  isDefault: form.value.isDefault,
  registryType: form.value.registryType
})

const saveRegistry = async () => {
  if (!validateForm()) return

  saving.value = true
  try {
    const payload = buildPayload()
    if (editingId.value) {
      await registryApi.updateRegistry(editingId.value, payload)
      ElMessage.success(t('registry.updateSuccess'))
    } else {
      await registryApi.createRegistry(payload)
      ElMessage.success(t('registry.addSuccess'))
    }

    showCreateDialog.value = false
    await refreshData()
  } catch (error: any) {
    ElMessage.error(error?.message || t('registry.saveFailed'))
  } finally {
    saving.value = false
  }
}

const testFormConnection = async () => {
  if (!validateForm()) return

  testing.value = true
  try {
    const payload = buildPayload()
    const result = await registryApi.testRegistryConfig(payload)
    if (result.isConnected) {
      ElMessage.success(result.message || t('registry.connectionSuccess'))
    } else {
      ElMessage.error(result.message || t('registry.connectionFailed'))
    }
  } catch (error: any) {
    ElMessage.error(error?.message || t('registry.connectionFailed'))
  } finally {
    testing.value = false
  }
}

const deleteRegistry = (row: any) => {
  ElMessageBox.confirm(t('registry.deleteConfirmSimple', { name: row.name })).then(async () => {
    await registryApi.deleteRegistry(row.id)
    ElMessage.success(t('common.deleted'))
    refreshData()
  })
}

onMounted(() => refreshData())
</script>

<style scoped>
.page-container { padding: 32px; max-width: 1600px; margin: 0 auto; }
.toolbar { padding: 16px 24px; border-bottom: 1px solid var(--border-color); }
.toolbar-search { width: 300px; }
.name-cell { display: flex; align-items: center; gap: 12px; }
.name-cell .icon { font-size: 18px; color: var(--color-primary); background: #eff6ff; padding: 8px; border-radius: 8px; }
.name-cell .text { display: flex; align-items: center; gap: 8px; }
.name-cell .main { font-weight: 600; color: var(--text-main); }
.font-mono { font-family: monospace; }
.actions-cell { display: flex; justify-content: center !important; gap: 8px; width: 100%; }
.form-hint { margin-top: 6px; font-size: 12px; color: var(--text-muted); }

@media (max-width: 768px) {
  .page-container { padding: 16px; }
  .toolbar-search { width: 100%; }
  .toolbar { padding: 12px 16px; }
}
</style>
