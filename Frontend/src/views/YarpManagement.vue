<template>
  <div class="yarp-page" v-loading="loading">
    <!-- Page Header -->
    <header class="page-header">
      <div class="header-content">
        <h1 class="page-title">{{ t('proxy.yarpManagement.pageTitle') }}</h1>
        <p class="page-subtitle">{{ t('proxy.yarpManagement.pageSubtitle') }}</p>
      </div>
      <div class="header-actions">
        <button class="btn btn-primary" @click="openCreateDialog">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
          {{ t('proxy.addMapping') }}
        </button>
        <button class="btn btn-secondary" @click="fetchMappings">
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
        <input v-model="search" type="text" :placeholder="t('proxy.yarpManagement.searchPlaceholder')" class="search-input" />
      </div>
      <div class="stats">
        <span class="stat"><strong>{{ mappings.length }}</strong> {{ t('proxy.yarpManagement.mappingRules') }}</span>
        <span class="stat"><strong>{{ activeMappings }}</strong> {{ t('proxy.yarpManagement.activeCount') }}</span>
      </div>
    </div>

    <!-- Data Table -->
    <div class="data-table" v-if="filteredMappings.length > 0">
      <div class="table-header">
        <div class="th th-domain">{{ t('proxy.yarpManagement.accessDomain') }}</div>
        <div class="th th-target">{{ t('proxy.yarpManagement.backendTarget') }}</div>
        <div class="th th-ssl">{{ t('proxy.yarpManagement.ssl') }}</div>
        <div class="th th-status">{{ t('proxy.yarpManagement.status') }}</div>
        <div class="th th-actions">{{ t('proxy.yarpManagement.actions') }}</div>
      </div>

      <div v-for="mapping in paginatedMappings" :key="mapping.id" class="table-row" :class="{ active: mapping.enabled }">
        <div class="td td-domain">
          <div class="domain-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="2" y1="12" x2="22" y2="12"></line>
              <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
            </svg>
          </div>
          <div class="domain-info">
            <span class="domain-host">{{ mapping.domain }}</span>
            <code class="domain-path">{{ mapping.pathPrefix || '/' }}</code>
          </div>
        </div>

        <div class="td td-target">
          <code class="target-badge">
            {{ getContainerDisplayName(mapping) }}:{{ mapping.containerPort }}
          </code>
        </div>

        <div class="td td-ssl">
          <span class="ssl-badge" :class="{ enabled: mapping.enableSsl }">
            <svg viewBox="0 0 24 24" fill="currentColor" width="14" height="14">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
              <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
            </svg>
            {{ mapping.enableSsl ? 'HTTPS' : 'HTTP' }}
          </span>
        </div>

        <div class="td td-status">
          <el-switch
            v-model="mapping.enabled"
            :loading="mapping._toggling"
            @change="toggleEnabled(mapping)"
            active-color="#10b981"
            inactive-color="#374151"
          />
        </div>

        <div class="td td-actions">
          <button class="action-btn" @click="editMapping(mapping)" :title="t('proxy.yarpManagement.edit')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path></svg>
          </button>
          <button class="action-btn danger" @click="handleDelete(mapping)" :title="t('proxy.yarpManagement.delete')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
          </button>
        </div>
      </div>
    </div>

    <!-- Pagination -->
    <div class="pagination" v-if="totalPages > 1">
      <div class="page-info">{{ t('proxy.yarpManagement.totalItems', { count: filteredMappings.length }) }}</div>
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
    <div v-if="!loading && filteredMappings.length === 0" class="empty-state">
      <div class="empty-icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <circle cx="12" cy="12" r="10"></circle>
          <line x1="2" y1="12" x2="22" y2="12"></line>
          <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
        </svg>
      </div>
      <h3 class="empty-title">{{ t('proxy.yarpManagement.noMappings') }}</h3>
      <p class="empty-desc">{{ t('proxy.yarpManagement.noMappingsDesc') }}</p>
      <button class="btn btn-primary" @click="openCreateDialog">{{ t('proxy.addMapping') }}</button>
    </div>

    <!-- Create/Edit Dialog -->
    <el-dialog v-model="showCreateDialog" :title="dialogTitle" width="520px" class="modern-dialog" @close="resetForm">
      <el-form :model="form" label-position="top">
        <el-form-item :label="t('proxy.yarpManagement.domainHost')" required>
          <el-input v-model="form.domain" :placeholder="t('proxy.yarpManagement.domainPlaceholder')" />
        </el-form-item>

        <el-row :gutter="16">
          <el-col :span="16">
            <el-form-item :label="t('proxy.yarpManagement.targetContainer')" required>
              <el-select v-model="form.containerId" :placeholder="t('proxy.yarpManagement.selectContainer')" style="width: 100%" filterable>
                <el-option v-for="c in containers" :key="c.id" :label="c.name" :value="c.id" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item :label="t('proxy.yarpManagement.containerPortLabel')" required>
              <el-input-number v-model="form.containerPort" :min="1" :max="65535" style="width: 100%" :controls="false" />
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item :label="t('proxy.yarpManagement.pathPrefixLabel')">
          <el-input v-model="form.pathPrefix" placeholder="/" />
        </el-form-item>

        <el-form-item :label="t('proxy.yarpManagement.sslMode')">
          <el-radio-group v-model="sslMode">
            <el-radio-button value="none">{{ t('proxy.yarpManagement.sslModeNone') }}</el-radio-button>
            <el-radio-button value="existing">{{ t('proxy.yarpManagement.sslModeExisting') }}</el-radio-button>
            <el-radio-button value="auto">{{ t('proxy.yarpManagement.sslModeAuto') }}</el-radio-button>
          </el-radio-group>
        </el-form-item>
        <el-form-item :label="t('proxy.yarpManagement.bindCertificate')" v-if="sslMode === 'existing'">
          <el-select v-model="form.certificateId" :placeholder="t('proxy.yarpManagement.selectCertificate')" style="width: 100%" clearable filterable>
            <el-option v-for="cert in certificates" :key="cert.id" :label="getCertLabel(cert)" :value="cert.id" />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('proxy.yarpManagement.selectAcmeAccount')" v-if="sslMode === 'auto'" required>
          <el-select v-model="form.accountId" :placeholder="t('proxy.yarpManagement.selectAcmeAccountPlaceholder')" style="width: 100%">
            <el-option v-for="acc in accounts" :key="acc.id" :label="`${acc.email} (${acc.provider})`" :value="acc.id" />
          </el-select>
          <div v-if="accounts.length === 0" style="color: var(--el-color-warning); font-size: 12px; margin-top: 4px;">
            {{ t('proxy.yarpManagement.noAvailableAccount') }}
          </div>
        </el-form-item>

        <!-- 高级设置 -->
        <div class="advanced-settings-toggle" @click="showAdvanced = !showAdvanced">
          <span>{{ showAdvanced ? t('common.hideAdvanced', '隐藏高级设置') : t('common.showAdvanced', '显示高级设置') }}</span>
          <svg :class="{ 'rotate': showAdvanced }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="16" height="16">
            <polyline points="6 9 12 15 18 9"></polyline>
          </svg>
        </div>

        <div v-show="showAdvanced" class="advanced-settings">
          <el-form-item :label="t('proxy.yarpManagement.forceHttps', '强制 HTTPS 跳转')">
            <el-switch v-model="form.forceHttps" />
            <div class="form-help">{{ t('proxy.yarpManagement.forceHttpsHelp', '开启后，访问 HTTP 会自动跳转到 HTTPS (推荐绑证书后开启)') }}</div>
          </el-form-item>
          
          <el-row :gutter="16">
            <el-col :span="12">
              <el-form-item :label="t('proxy.yarpManagement.activityTimeout', '空闲超时 (秒)')">
                <el-input-number v-model="form.activityTimeoutSeconds" :min="0" :step="10" :placeholder="t('common.default', '默认') + ' 100'" style="width: 100%" :controls="false" />
                <div class="form-help">{{ t('proxy.yarpManagement.timeoutHelp', '0 = 不限时') }}</div>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item :label="t('proxy.yarpManagement.httpVersion', '后端 HTTP 版本')">
                <el-select v-model="form.httpVersion" :placeholder="t('common.default', '自动')" style="width: 100%" clearable>
                  <el-option label="HTTP/1.1" value="1.1" />
                  <el-option label="HTTP/2" value="2" />
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
        </div>
      </el-form>
      <template #footer>
        <div class="dialog-footer">
          <el-button @click="showCreateDialog = false">{{ t('proxy.yarpManagement.cancel') }}</el-button>
          <el-button type="primary" @click="handleSubmit">{{ isEditing ? t('proxy.yarpManagement.updateSubmit') : t('proxy.yarpManagement.submit') }}</el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox } from 'element-plus'
import { yarpApi } from '@/api/yarp'
import { certificateApi, type Certificate } from '@/api/certificate'
import { acmeApi } from '@/api/acme'
import type { AcmeAccount } from '@/api/certificate'
import { useContainersStore } from '@/stores/containers'
import { useSettingsStore } from '@/stores/settings'

const { t } = useI18n()

const containerStore = useContainersStore()
const settingsStore = useSettingsStore()
const loading = ref(false)
const search = ref('')
const mappings = ref<any[]>([])
const certificates = ref<Certificate[]>([])
const accounts = ref<AcmeAccount[]>([])
const showCreateDialog = ref(false)
const editingMappingId = ref<string | null>(null)
const currentPage = ref(1)
const pageSize = computed(() => settingsStore.defaultPageSize)
const sslMode = ref<'none' | 'existing' | 'auto'>('none')
const showAdvanced = ref(false)

const form = ref({
  domain: '',
  containerId: '',
  destinationAddress: '',
  containerPort: 80,
  pathPrefix: '/',
  enableSsl: false,
  certificateId: '',
  accountId: '',
  autoRequestCertificate: false,
  enabled: true,
  protocol: 'http',
  priority: 100,
  forceHttps: false,
  activityTimeoutSeconds: undefined as number | undefined,
  httpVersion: ''
})

const containers = computed(() => containerStore.containers)
const activeMappings = computed(() => mappings.value.filter(m => m.enabled).length)
const isEditing = computed(() => Boolean(editingMappingId.value))
const dialogTitle = computed(() => isEditing.value ? t('proxy.yarpManagement.editDialogTitle') : t('proxy.yarpManagement.createDialogTitle'))

const filteredMappings = computed(() => {
  if (!search.value) return mappings.value
  return mappings.value.filter(m => 
    m.domain.toLowerCase().includes(search.value.toLowerCase()) ||
    (m.containerName || '').toLowerCase().includes(search.value.toLowerCase())
  )
})

const totalPages = computed(() => Math.ceil(filteredMappings.value.length / pageSize.value))
const paginatedMappings = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredMappings.value.slice(start, start + pageSize.value)
})

watch(search, () => { currentPage.value = 1 })
watch(pageSize, () => { currentPage.value = 1 })

const getContainerDisplayName = (mapping: any) => {
  // 优先使用 containerName
  if (mapping.containerName) return mapping.containerName
  
  const containerId = String(mapping.containerId || '')
  
  // 从容器列表中查找（支持长短ID匹配）
  const container = containers.value.find(c => {
    const cId = String(c.id || '')
    return cId === containerId || 
           cId.startsWith(containerId) ||
           containerId.startsWith(cId)
  })
  if (container && container.name) return container.name
  
  // 最后显示 ID 前12位（短ID格式）
  return containerId.length > 12 ? containerId.substring(0, 12) : containerId || 'unknown'
}

const fetchMappings = async () => {
  loading.value = true
  try {
    const res = await yarpApi.getDomainMappings()
    mappings.value = Array.isArray(res) ? res : []
  } finally { loading.value = false }
}

const fetchCertificates = async () => {
  try {
    const [certsRes, accountsRes] = await Promise.all([
      certificateApi.getCertificates({ status: 'valid' }),
      acmeApi.getAccounts()
    ])
    // Handle both AxiosResponse and raw data formats
    const data = (certsRes as any)?.data || certsRes
    certificates.value = data?.items || []
    accounts.value = (accountsRes as any)?.data || accountsRes || []
  } catch (e) {
    console.error('Failed to fetch certificates:', e)
    certificates.value = []
    accounts.value = []
  }
}

const getCertLabel = (cert: Certificate) => {
  const domains = cert.domains?.join(', ') || cert.id
  return domains.length > 40 ? domains.substring(0, 40) + '...' : domains
}

const handleSubmit = async () => {
  if (!form.value.domain || !form.value.containerId) return
  try {
    // 构造目标地址
    const container = containers.value.find(c => c.id === form.value.containerId)
    const destinationAddr = container ? `${container.name}:${form.value.containerPort}` : form.value.destinationAddress

    const payload = {
      ...form.value,
      containerName: container?.name || '',
      destinationAddress: destinationAddr,
      enableSsl: sslMode.value !== 'none',
      autoRequestCertificate: sslMode.value === 'auto'
    }

    const response = isEditing.value
      ? await yarpApi.updateDomainMapping(editingMappingId.value!, payload) as any
      : await yarpApi.createDomainMapping(payload) as any

    // 处理响应
    if (response?.certificateRequested) {
      ElMessage.warning(response.message || t('proxy.yarpManagement.certificateRequested'))
    } else {
      ElMessage.success(response?.message || (isEditing.value ? t('proxy.yarpManagement.updateSuccess') : t('proxy.yarpManagement.createSuccess')))
    }

    showCreateDialog.value = false
    resetForm()
    fetchMappings()
  } catch { ElMessage.error(isEditing.value ? t('proxy.yarpManagement.updateFailed') : t('proxy.yarpManagement.createFailed')) }
}

const resetForm = () => {
  editingMappingId.value = null
  form.value = { 
    domain: '', 
    containerId: '', 
    destinationAddress: '',
    containerPort: 80, 
    pathPrefix: '/', 
    enableSsl: false, 
    certificateId: '',
    accountId: '',
    autoRequestCertificate: false,
    enabled: true,
    protocol: 'http', 
    priority: 100,
    forceHttps: false,
    activityTimeoutSeconds: undefined,
    httpVersion: ''
  }
  sslMode.value = 'none'
  showAdvanced.value = false
}

const openCreateDialog = () => {
  resetForm()
  showCreateDialog.value = true
}

const editMapping = (row: any) => {
  editingMappingId.value = row.id
  form.value = {
    domain: row.domain || '',
    containerId: row.containerId || '',
    destinationAddress: row.destinationAddress || '',
    containerPort: row.containerPort || 80,
    pathPrefix: row.pathPrefix || '/',
    enableSsl: Boolean(row.enableSsl),
    certificateId: row.certificateId || '',
    accountId: row.accountId || '',
    autoRequestCertificate: Boolean(row.autoRequestCertificate),
    enabled: row.enabled ?? true,
    protocol: row.protocol || 'http',
    priority: row.priority ?? 100,
    forceHttps: row.forceHttps ?? false,
    activityTimeoutSeconds: row.activityTimeoutSeconds,
    httpVersion: row.httpVersion || ''
  }
  sslMode.value = row.autoRequestCertificate ? 'auto' : (row.enableSsl ? 'existing' : 'none')
  showAdvanced.value = form.value.forceHttps || !!form.value.activityTimeoutSeconds || !!form.value.httpVersion
  showCreateDialog.value = true
}

const handleDelete = (row: any) => {
  ElMessageBox.confirm(t('proxy.yarpManagement.deleteConfirm', { domain: row.domain })).then(async () => {
    await yarpApi.deleteDomainMapping(row.id)
    ElMessage.success(t('proxy.yarpManagement.deleted'))
    fetchMappings()
  })
}

const toggleEnabled = async (mapping: any) => {
  const previousValue = !mapping.enabled
  mapping._toggling = true
  try {
    await yarpApi.toggleMappingEnabled(mapping.id, mapping.enabled)
    ElMessage.success(t('proxy.yarpManagement.toggleSuccess'))
  } catch {
    mapping.enabled = previousValue
    ElMessage.error(t('proxy.yarpManagement.toggleFailed'))
  } finally {
    mapping._toggling = false
  }
}

onMounted(() => {
  fetchMappings()
  fetchCertificates()
  containerStore.fetchContainers({ all: true })
})
</script>

<style scoped>
.yarp-page {
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
  grid-template-columns: minmax(250px, 2fr) minmax(180px, 1.5fr) 100px 120px 100px;
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
  grid-template-columns: minmax(250px, 2fr) minmax(180px, 1.5fr) 100px 120px 100px;
  gap: 8px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border-color-light);
  align-items: center;
  transition: all 0.15s ease;
}

.table-row:last-child { border-bottom: none; }
.table-row:hover { background: var(--bg-glass-dark); }
.table-row.active { border-left: 3px solid #10b981; padding-left: 13px; }

.td-domain { display: flex; align-items: center; gap: 12px; }

.domain-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: linear-gradient(135deg, #10b981, #059669);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.domain-icon svg { width: 18px; height: 18px; color: #fff; }

.domain-info { display: flex; flex-direction: column; gap: 2px; }
.domain-host { font-weight: 600; font-size: 14px; color: var(--text-main); }
.domain-path { font-size: 11px; font-family: 'JetBrains Mono', monospace; color: var(--text-muted); }

.target-badge {
  font-family: 'JetBrains Mono', monospace;
  font-size: 12px;
  padding: 4px 10px;
  background: var(--bg-subtle);
  border-radius: 6px;
  color: var(--text-secondary);
}

.ssl-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  padding: 4px 8px;
  border-radius: 6px;
  background: var(--bg-subtle);
  color: var(--text-muted);
}

.ssl-badge.enabled {
  background: rgba(16, 185, 129, 0.1);
  color: #059669;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 500;
}

.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}

.status-badge.active .status-dot { background: #10b981; box-shadow: 0 0 6px rgba(16, 185, 129, 0.5); }
.status-badge.inactive .status-dot { background: var(--text-muted); }
.status-badge.active { color: #059669; }
.status-badge.inactive { color: var(--text-muted); }

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
.action-btn:hover { border-color: #10b981; color: #10b981; }
.action-btn.danger:hover { border-color: #ef4444; color: #ef4444; background: rgba(239, 68, 68, 0.1); }

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
.page-btn:hover:not(:disabled) { border-color: #10b981; color: #10b981; }
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

.advanced-settings-toggle {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--text-muted);
  font-size: 13px;
  cursor: pointer;
  padding: 8px 0;
  margin-top: 8px;
  user-select: none;
  transition: color 0.2s;
}

.advanced-settings-toggle:hover {
  color: var(--el-color-primary);
}

.advanced-settings-toggle svg {
  transition: transform 0.2s;
}

.advanced-settings-toggle svg.rotate {
  transform: rotate(180deg);
}

.advanced-settings {
  margin-top: 16px;
  padding: 16px;
  background: var(--bg-subtle);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.form-help {
  font-size: 12px;
  color: var(--text-muted);
  line-height: 1.4;
  margin-top: 4px;
}

@media (max-width: 1024px) {
  .th-ssl, .td-ssl { display: none; }
  .table-header, .table-row { grid-template-columns: minmax(200px, 2fr) minmax(150px, 1.5fr) 100px 90px; }
}

@media (max-width: 768px) {
  .yarp-page { padding: 16px; }
  .page-header { flex-direction: column; gap: 12px; }
  .toolbar { flex-wrap: wrap; }
  .search-box { max-width: none; width: 100%; }
  .stats { width: 100%; justify-content: center; }
  /* Simplify table grid: hide status column */
  .th-ssl, .td-ssl { display: none; }
  .table-header, .table-row { grid-template-columns: minmax(140px, 2fr) minmax(120px, 1.5fr) 80px; }
  .th:last-child, .td-actions { display: none; }
  .pagination { flex-direction: column; gap: 8px; align-items: center; }
}

@media (max-width: 480px) {
  .yarp-page { padding: 12px; }
  /* Only show domain column */
  .table-header, .table-row { grid-template-columns: 1fr 80px; }
  .th-ssl, .td-ssl, .th:nth-child(3), .td:nth-child(3) { display: none; }
  .domain-icon { width: 30px; height: 30px; }
  .domain-host { font-size: 13px; }
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
html.dark .domain-host { color: #f1f5f9; }
html.dark .stats strong { color: #f1f5f9; }
html.dark .target-badge, html.dark .ssl-badge { background: rgba(255, 255, 255, 0.1); }
html.dark .action-btn, html.dark .page-btn { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
</style>