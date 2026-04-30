<template>
  <el-dialog
    v-model="visible"
    :title="t('registry.dialogTitle')"
    width="950px"
    :close-on-click-modal="false"
    @close="handleClose"
    top="6vh"
  >
    <div class="registry-manager">
      <!-- 标签页 -->
      <el-tabs v-model="activeTab" class="registry-tabs">
        <!-- 私有仓库标签页 -->
        <el-tab-pane :label="t('registry.privateRegistry')" name="private">
          <el-alert
            :title="t('registry.privateRegistry')"
            type="info"
            :description="t('registry.privateRegistryDesc')"
            show-icon
            :closable="false"
            style="margin-bottom: 16px;"
          />
          
          <!-- 工具栏 -->
          <div class="toolbar">
            <el-button type="primary" :icon="Plus" @click="openAddDialog('private')">{{ t('registry.addPrivateRegistry') }}</el-button>
            <el-button :icon="Refresh" @click="loadRegistries" :loading="loading">{{ t('registry.refreshList') }}</el-button>
          </div>

          <!-- 私有仓库列表 -->
          <el-table 
            :data="privateRegistries" 
            border 
            v-loading="loading" 
            style="width: 100%; margin-top: 12px;"
            :empty-text="t('registry.noPrivateRegistry')"
          >
            <el-table-column prop="name" :label="t('registry.name')" width="180">
              <template #default="{ row }">
                <span style="font-weight: 600;">{{ row.name }}</span>
                <el-tag v-if="row.isDefault" size="small" type="success" style="margin-left: 5px">{{ t('registry.defaultTag') }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="domain" :label="t('registry.address')" min-width="250">
              <template #default="{ row }">
                <span>{{ row.domain }}</span>
              </template>
            </el-table-column>
            <el-table-column prop="username" :label="t('registry.username')" width="150">
              <template #default="{ row }">
                <el-tag v-if="row.username" type="info" size="small">{{ row.username }}</el-tag>
                <span v-else class="text-gray">{{ t('registry.anonymous') }}</span>
              </template>
            </el-table-column>
            <el-table-column :label="t('common.actions')" width="160" align="center" fixed="right">
              <template #default="{ row }">
                <div class="action-btns">
                  <button class="action-btn" @click="testConnection(row)" :loading="testingId === row.id" :title="t('registry.testConnection')">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/>
                      <polyline points="22 4 12 14.01 9 11.01"/>
                    </svg>
                  </button>
                  <button class="action-btn" @click="handleEdit(row)" :title="t('common.edit')">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                  </button>
                  <button class="action-btn" @click="handleSearch(row)" v-if="row.username" :title="t('registry.searchImage')">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <circle cx="11" cy="11" r="8"/>
                      <line x1="21" y1="21" x2="16.65" y2="16.65"/>
                    </svg>
                  </button>
                  <button class="action-btn danger" @click="confirmDelete(row)" :title="t('common.delete')">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <polyline points="3 6 5 6 21 6"/>
                      <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                    </svg>
                  </button>
                </div>
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>

        <!-- 镜像加速器标签页 -->
        <el-tab-pane :label="t('registry.mirrorAccelerator')" name="mirror">
          <el-alert
            :title="t('registry.mirrorAccelerator')"
            type="warning"
            show-icon
            :closable="false"
            style="margin-bottom: 16px;"
          >
            <template #default>
              <div class="mirror-notice">
                <p><strong>{{ t('registry.mirrorNoticeTitle') }}</strong>{{ t('registry.mirrorNoticeDesc') }}</p>
                <p>• {{ t('registry.mirrorNotice1') }}</p>
                <p>• {{ t('registry.mirrorNotice2') }}</p>
                <p>• {{ t('registry.mirrorNotice3') }}</p>
              </div>
            </template>
          </el-alert>
          
          <!-- 工具栏 -->
          <div class="toolbar">
            <el-button type="primary" :icon="Plus" @click="openAddDialog('mirror')">{{ t('registry.addMirror') }}</el-button>
            <el-button :icon="Refresh" @click="loadRegistries" :loading="loading">{{ t('registry.refreshList') }}</el-button>
          </div>

          <!-- 加速器列表 -->
          <el-table 
            :data="mirrorRegistries" 
            border 
            v-loading="loading" 
            style="width: 100%; margin-top: 12px;"
            :empty-text="t('registry.noMirrorRegistry')"
          >
            <el-table-column prop="name" :label="t('registry.name')" width="180">
              <template #default="{ row }">
                <div class="mirror-name">
                  <svg class="mirror-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/>
                  </svg>
                  <span style="font-weight: 600;">{{ row.name }}</span>
                </div>
                <el-tag v-if="row.isDefault" size="small" type="success" style="margin-left: 22px">{{ t('registry.defaultInUse') }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="domain" :label="t('registry.mirrorAddress')" min-width="280">
              <template #default="{ row }">
                <code class="mirror-url">{{ row.domain }}</code>
              </template>
            </el-table-column>
            <el-table-column :label="t('common.status')" width="100" align="center">
              <template #default="{ row }">
                <el-tag :type="row.status === 'Active' ? 'success' : 'danger'" size="small">
                  {{ row.status === 'Active' ? t('registry.statusNormal') : t('registry.statusError') }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column :label="t('common.actions')" width="180" align="center" fixed="right">
              <template #default="{ row }">
                <div class="action-btns">
                  <button class="action-btn" @click="testConnection(row)" :loading="testingId === row.id" :title="t('registry.testConnection')">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/>
                      <polyline points="22 4 12 14.01 9 11.01"/>
                    </svg>
                  </button>
                  <button class="action-btn" @click="setAsDefault(row)" v-if="!row.isDefault" :title="t('registry.setAsDefault')">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/>
                    </svg>
                  </button>
                  <button class="action-btn" @click="handleEdit(row)" :title="t('common.edit')">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                  </button>
                  <button class="action-btn danger" @click="confirmDelete(row)" :title="t('common.delete')">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <polyline points="3 6 5 6 21 6"/>
                      <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                    </svg>
                  </button>
                </div>
              </template>
            </el-table-column>
          </el-table>


        </el-tab-pane>
      </el-tabs>
    </div>

    <!-- 添加/编辑弹窗 -->
    <el-dialog
      v-model="showFormDialog"
      :title="isEdit ? t('registry.editRegistry') : (form.registryType === 1 ? t('registry.addMirror') : t('registry.addPrivateRegistry'))"
      width="550px"
      append-to-body
      :close-on-click-modal="false"
      @closed="resetForm"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="100px" label-position="left">
        <el-form-item :label="t('registry.name')" prop="name">
          <el-input v-model="form.name" :placeholder="form.registryType === 1 ? t('registry.mirrorNamePlaceholder') : t('registry.registryNamePlaceholder')" />
        </el-form-item>
        <el-form-item :label="form.registryType === 1 ? t('registry.mirrorAddress') : t('registry.domain')" prop="domain">
          <el-input v-model="form.domain" :placeholder="form.registryType === 1 ? t('registry.mirrorDomainPlaceholder') : t('registry.domainPlaceholder')" />
          <div class="form-tip">{{ form.registryType === 1 ? t('registry.mirrorDomainHint') : t('registry.domainHint') }}</div>
        </el-form-item>
        <template v-if="form.registryType === 0">
          <el-divider content-position="left">{{ t('registry.authInfo') }}</el-divider>
          <el-form-item :label="t('registry.username')" prop="username">
            <el-input v-model="form.username" :placeholder="t('registry.usernamePlaceholder')" />
          </el-form-item>
          <el-form-item :label="t('registry.passwordToken')" prop="password">
            <el-input v-model="form.password" type="password" show-password :placeholder="t('registry.passwordEditHint')" />
          </el-form-item>
        </template>
        <el-form-item :label="t('registry.setAsDefault')">
          <el-switch v-model="form.isDefault" />
          <div class="form-tip">{{ form.registryType === 1 ? t('registry.defaultMirrorHint') : t('registry.defaultRegistryHint') }}</div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showFormDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button @click="handleTestConfig" :loading="testingConfig">{{ t('registry.testConnection') }}</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="submitting">{{ t('registry.saveConfig') }}</el-button>
      </template>
    </el-dialog>

    <!-- 搜索弹窗 -->
    <SearchRegistryDialog 
      v-model="showSearchDialog" 
      :registry="searchingRegistry" 
      @pull="handlePullFromRegistry"
    />
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import { registryApi } from '@/api/registry'
import { RegistryType } from '@/types/registry'
import type { ImageRegistry } from '@/types/registry'
import SearchRegistryDialog from '@/components/registry/SearchRegistryDialog.vue'

const { t } = useI18n()

const props = defineProps<{ modelValue: boolean }>()
const emit = defineEmits(['update:modelValue'])

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

const activeTab = ref('private')
const loading = ref(false)
const registries = ref<ImageRegistry[]>([])
const showFormDialog = ref(false)
const isEdit = ref(false)
const submitting = ref(false)
const testingConfig = ref(false)
const testingId = ref<string | null>(null)
const formRef = ref()

// 搜索相关
const showSearchDialog = ref(false)
const searchingRegistry = ref<ImageRegistry | null>(null)

const form = reactive({
  id: '',
  name: '',
  domain: '',
  username: '',
  password: '',
  registryType: RegistryType.Private,
  isDefault: false
})

const rules = computed(() => ({
  name: [{ required: true, message: t('registry.nameRequired'), trigger: 'blur' }],
  domain: [{ required: true, message: t('registry.addressRequired'), trigger: 'blur' }]
}))

// 计算私有仓库和加速器列表
const privateRegistries = computed(() => 
  registries.value.filter(r => r.registryType === RegistryType.Private)
)

const mirrorRegistries = computed(() => 
  registries.value.filter(r => r.registryType === RegistryType.Mirror)
)

const loadRegistries = async () => {
  loading.value = true
  try {
    const res = await registryApi.getRegistries()
    registries.value = (res as any).data || res
  } catch (error) {
    ElMessage.error(t('registry.loadFailed'))
  } finally {
    loading.value = false
  }
}

const openAddDialog = (type: 'private' | 'mirror') => {
  isEdit.value = false
  form.registryType = type === 'mirror' ? RegistryType.Mirror : RegistryType.Private
  showFormDialog.value = true
}

const handleEdit = (row: ImageRegistry) => {
  isEdit.value = true
  form.id = row.id
  form.name = row.name
  form.domain = row.domain || ''
  form.username = row.username || ''
  form.password = '' // 密码不回显
  form.registryType = row.registryType
  form.isDefault = row.isDefault
  showFormDialog.value = true
}

const handleDelete = async (id: string) => {
  try {
    await registryApi.deleteRegistry(id)
    ElMessage.success(t('common.deleteSuccess'))
    loadRegistries()
  } catch (error: any) {
    ElMessage.error(error.message || t('common.error'))
  }
}

const confirmDelete = (row: ImageRegistry) => {
  ElMessageBox.confirm(
    t('registry.deleteConfirm', { name: row.name }),
    t('common.deleteConfirm'),
    {
      confirmButtonText: t('common.confirm'),
      cancelButtonText: t('common.cancel'),
      type: 'warning'
    }
  ).then(() => {
    handleDelete(row.id)
  }).catch(() => {})
}

// 测试表单中的配置（无需保存）
const handleTestConfig = async () => {
  if (!form.domain) {
    ElMessage.warning(t('registry.addressRequired'))
    return
  }
  
  testingConfig.value = true
  try {
    const res = await registryApi.testRegistryConfig({
      domain: form.domain,
      username: form.username || undefined,
      password: form.password || undefined,
      isSecure: true
    })
    const result = (res as any).data || res
    if (result.isConnected) {
      ElMessage.success(`${result.message} (${result.responseTimeMs}ms)`)
    } else {
      ElMessage.error(result.message || t('registry.connectionFailed'))
    }
  } catch (error: any) {
    ElMessage.error(error.message || t('registry.connectionFailed'))
  } finally {
    testingConfig.value = false
  }
}

const testConnection = async (row: ImageRegistry) => {
  testingId.value = row.id
  try {
    const res = await registryApi.testRegistryConnection(row.id)
    const result = (res as any).data || res
    if (result.isConnected) {
      ElMessage.success(`${t('registry.connectionSuccess')} (${result.responseTimeMs}ms)`)
    } else {
      ElMessage.error(result.message || t('registry.connectionFailed'))
    }
  } catch (error: any) {
    const msg = error.response?.data?.message || error.message || t('registry.connectionCheckHint')
    ElMessage.error(msg)
  } finally {
    testingId.value = null
  }
}

const setAsDefault = async (row: ImageRegistry) => {
  try {
    await registryApi.setDefaultRegistry(row.id)
    ElMessage.success(t('registry.setAsDefaultSuccess'))
    loadRegistries()
  } catch (error: any) {
    ElMessage.error(error.message || t('common.error'))
  }
}

const handleSubmit = async () => {
  if (!formRef.value) return
  await formRef.value.validate()
  
  submitting.value = true
  try {
    if (isEdit.value) {
      await registryApi.updateRegistry(form.id, {
        name: form.name,
        domain: form.domain,
        username: form.username || undefined,
        password: form.password || undefined,
        registryType: form.registryType,
        isDefault: form.isDefault
      })
      ElMessage.success(t('registry.updateSuccess'))
    } else {
      await registryApi.createRegistry({
        name: form.name,
        domain: form.domain,
        username: form.username || undefined,
        password: form.password || undefined,
        registryType: form.registryType,
        isDefault: form.isDefault
      })
      ElMessage.success(t('registry.addSuccess'))
    }
    showFormDialog.value = false
    loadRegistries()
  } catch (error: any) {
    ElMessage.error(error.message || t('registry.saveFailed'))
  } finally {
    submitting.value = false
  }
}

const resetForm = () => {
  if (formRef.value) formRef.value.resetFields()
  form.id = ''
  form.name = ''
  form.domain = ''
  form.username = ''
  form.password = ''
  form.registryType = RegistryType.Private
  form.isDefault = false
  isEdit.value = false
}

const handleSearch = (row: ImageRegistry) => {
  searchingRegistry.value = row
  showSearchDialog.value = true
}

const handlePullFromRegistry = (image: any) => {
  ElMessage.success(t('registry.pullImageStart', { name: image.name }))
}

const handleClose = () => {
  visible.value = false
}

watch(visible, (val) => {
  if (val) loadRegistries()
})
</script>

<style scoped>
.registry-manager { padding: 0; }
.registry-tabs { margin-bottom: 0; }

.toolbar { 
  display: flex; 
  justify-content: space-between; 
  margin-bottom: 12px; 
}

.form-tip { 
  font-size: 12px; 
  color: #909399; 
  margin-top: 5px; 
  line-height: 1.4; 
}

.text-gray { 
  color: #ccc; 
  font-style: italic; 
}

.mirror-name {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mirror-icon {
  width: 18px;
  height: 18px;
  color: #f59e0b;
}

.mirror-url {
  font-family: 'JetBrains Mono', monospace;
  font-size: 12px;
  background: var(--bg-subtle, #f5f5f5);
  padding: 2px 6px;
  border-radius: 4px;
}

.mirror-notice {
  font-size: 13px;
  line-height: 1.6;
}
.mirror-notice p {
  margin: 0 0 4px 0;
}
.mirror-notice p:last-child {
  margin-bottom: 0;
}
.mirror-notice strong {
  color: #e6a23c;
}



/* 深色模式 */
html.dark .mirror-url {
  background: rgba(255, 255, 255, 0.1);
}

.action-btns {
  display: flex;
  gap: 6px;
  justify-content: center;
}

.action-btn {
  width: 28px;
  height: 28px;
  border: none;
  border-radius: 6px;
  background: rgba(255, 255, 255, 0.05);
  color: #94a3b8;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.action-btn:hover {
  background: rgba(59, 130, 246, 0.15);
  color: #60a5fa;
  transform: translateY(-1px);
}

.action-btn:active {
  transform: translateY(0);
}

.action-btn.danger:hover {
  background: rgba(239, 68, 68, 0.15);
  color: #f87171;
}

.action-btn svg {
  width: 14px;
  height: 14px;
}


</style>