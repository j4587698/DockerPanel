<template>
  <div class="certs-page" v-loading="loading">
    <!-- Page Header -->
    <header class="page-header">
      <div class="header-content">
        <h1 class="page-title">{{ t('certificate.pageTitle') }}</h1>
        <p class="page-subtitle">{{ t('certificate.pageSubtitle') }}</p>
      </div>
      <div class="header-actions">
        <button v-if="activeTab === 'certificates'" class="btn btn-primary" @click="showCreateDialog = true">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
          {{ t('certificate.requestCertificate') }}
        </button>
        <button v-if="activeTab === 'accounts'" class="btn btn-primary" @click="showAccountDialog = true">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
          {{ t('certificate.addAccount') }}
        </button>
        <button class="btn btn-secondary" @click="refreshAll">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="23 4 23 10 17 10"></polyline>
            <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path>
          </svg>
          {{ t('common.refresh') }}
        </button>
      </div>
    </header>

    <!-- Tab Navigation -->
    <div class="tab-nav">
      <button 
        class="tab-btn" 
        :class="{ active: activeTab === 'certificates' }"
        @click="activeTab = 'certificates'"
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
          <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
        </svg>
        {{ t('certificate.certificates') }}
        <span class="tab-count">{{ stats.totalCertificates }}</span>
      </button>
      <button 
        class="tab-btn" 
        :class="{ active: activeTab === 'accounts' }"
        @click="activeTab = 'accounts'"
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
          <circle cx="12" cy="7" r="4"></circle>
        </svg>
        {{ t('certificate.acmeAccounts') }}
        <span class="tab-count">{{ accounts.length }}</span>
      </button>
    </div>

    <!-- Certificates Tab -->
    <template v-if="activeTab === 'certificates'">
      <!-- Stats Cards -->
      <div class="stats-row">
        <div class="stat-card">
          <div class="stat-icon total">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
            </svg>
          </div>
          <div class="stat-info">
            <span class="stat-value">{{ stats.totalCertificates }}</span>
            <span class="stat-label">{{ t('certificate.totalCertificates') }}</span>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon valid">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
              <polyline points="22 4 12 14.01 9 11.01"></polyline>
            </svg>
          </div>
          <div class="stat-info">
            <span class="stat-value">{{ stats.validCertificates }}</span>
            <span class="stat-label">{{ t('certificate.valid') }}</span>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon expiring">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <polyline points="12 6 12 12 16 14"></polyline>
            </svg>
          </div>
          <div class="stat-info">
            <span class="stat-value">{{ stats.expiringCertificates }}</span>
            <span class="stat-label">{{ t('certificate.expiring') }}</span>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon expired">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="15" y1="9" x2="9" y2="15"></line>
              <line x1="9" y1="9" x2="15" y2="15"></line>
            </svg>
          </div>
          <div class="stat-info">
            <span class="stat-value">{{ stats.expiredCertificates }}</span>
            <span class="stat-label">{{ t('certificate.expired') }}</span>
          </div>
        </div>
      </div>

      <!-- Toolbar -->
      <div class="toolbar">
        <div class="search-box">
          <svg class="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="11" cy="11" r="8"></circle>
            <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
          </svg>
          <input v-model="filter.domain" type="text" :placeholder="t('certificate.searchDomain')" class="search-input" />
        </div>
        <div class="filter-tabs">
          <button 
            v-for="tab in statusTabs"
            :key="tab.value"
            class="filter-tab"
            :class="{ active: filter.status === tab.value }"
            @click="filter.status = tab.value"
          >
            <span class="tab-dot" :class="tab.color"></span>
            {{ tab.label }}
          </button>
        </div>
      </div>

      <!-- Data Table -->
      <el-table
        v-if="filteredCertificates.length > 0"
        :data="paginatedCertificates"
        style="width: 100%"
        v-loading="loading"
      >
        <el-table-column :label="t('certificate.domain')" min-width="280">
          <template #default="{ row }">
            <div class="td-domain" @click="viewCertificate(row)">
              <div class="cert-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                  <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                </svg>
              </div>
              <div class="domain-list">
                <span v-for="domain in row.domains?.slice(0, 2)" :key="domain" class="domain-tag">
                  {{ domain }}
                </span>
                <span v-if="row.domains?.length > 2" class="domain-more">+{{ row.domains.length - 2 }}</span>
              </div>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('certificate.status')" width="110" align="center">
          <template #default="{ row }">
            <span class="status-badge" :class="row.status">
              {{ getStatusLabel(row.status) }}
            </span>
          </template>
        </el-table-column>

        <el-table-column :label="t('certificate.provider')" width="130" align="center">
          <template #default="{ row }">
            <span class="provider-text">{{ getProviderLabel(row.provider) }}</span>
          </template>
        </el-table-column>

        <el-table-column :label="t('certificate.expiresAt')" min-width="170">
          <template #default="{ row }">
            <div class="expiry-info">
              <span class="expiry-date">{{ formatDate(row.expiresAt) }}</span>
              <span v-if="row.status === 'valid'" class="expiry-days" :class="getExpiryClass(row.expiresAt)">
                {{ getExpiryDays(row.expiresAt) }}
              </span>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('certificate.autoRenew')" width="110" align="center">
          <template #default="{ row }">
            <el-switch 
              v-model="row.autoRenew" 
              @change="handleAutoRenewChange(row)"
              :disabled="row.status !== 'valid'"
              size="small"
            />
          </template>
        </el-table-column>

        <el-table-column :label="t('common.actions')" width="200" align="center" fixed="right">
          <template #default="{ row }">
            <div class="actions-cell">
              <el-button class="table-action-btn detail" :icon="View" :title="t('common.details')" @click="viewCertificate(row)" />
              <el-button v-if="row.status === 'valid' || row.status === 'expiring'" class="table-action-btn download" :icon="Download" :title="t('common.download')" @click="downloadCertificate(row)" />
              <el-button v-if="shouldShowRenewButton(row)" class="table-action-btn renew" :icon="Refresh" :title="t('certificate.renewButton')" @click="renewCertificate(row)" />
              <el-button class="table-action-btn danger" :icon="Delete" :title="t('common.delete')" @click="deleteCertificate(row)" />
            </div>
          </template>
        </el-table-column>
      </el-table>

      <!-- Pagination -->
      <div class="pagination" v-if="totalPages > 1">
        <div class="page-info">
          {{ t('certificate.showingItems', { start: (currentPage - 1) * pageSize + 1, end: Math.min(currentPage * pageSize, filteredCertificates.length), total: filteredCertificates.length }) }}
        </div>
        <div class="page-controls">
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
            >{{ page }}</button>
          </div>
          <button class="page-btn" :disabled="currentPage === totalPages" @click="currentPage++">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
          </button>
        </div>
        <select v-model.number="pageSize" class="page-size">
          <option :value="10">{{ t('certificate.itemsPerPage', { count: 10 }) }}</option>
          <option :value="20">{{ t('certificate.itemsPerPage', { count: 20 }) }}</option>
          <option :value="50">{{ t('certificate.itemsPerPage', { count: 50 }) }}</option>
          <option :value="100">{{ t('certificate.itemsPerPage', { count: 100 }) }}</option>
        </select>
      </div>

      <!-- Empty State -->
      <div v-if="!loading && filteredCertificates.length === 0" class="empty-state">
        <div class="empty-icon">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
            <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
            <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
          </svg>
        </div>
        <h3 class="empty-title">{{ t('certificate.noCertificates') }}</h3>
        <p class="empty-desc">{{ t('certificate.applyFirstCertificate') }}</p>
        <button class="btn btn-primary" @click="showCreateDialog = true">{{ t('certificate.requestCertificate') }}</button>
      </div>
    </template>

    <!-- Accounts Tab -->
    <template v-if="activeTab === 'accounts'">
      <div class="accounts-info">
        <el-alert 
          type="info" 
          :closable="false"
          show-icon
        >
          <template #title>
            {{ t('certificate.accountInfo') }}
          </template>
        </el-alert>
      </div>

      <el-table
        v-if="accounts.length > 0"
        :data="accounts"
        style="width: 100%"
        v-loading="loading"
      >
        <el-table-column :label="t('certificate.email')" min-width="240">
          <template #default="{ row }">
            <div class="account-email">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="email-icon">
                <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"></path>
                <polyline points="22,6 12,13 2,6"></polyline>
              </svg>
              {{ row.email }}
            </div>
          </template>
        </el-table-column>
        <el-table-column :label="t('certificate.provider')" width="150" align="center">
          <template #default="{ row }">
            <span class="provider-badge">{{ getProviderLabel(row.provider) }}</span>
          </template>
        </el-table-column>
        <el-table-column :label="t('certificate.status')" width="120" align="center">
          <template #default="{ row }">
            <span class="status-badge" :class="row.isActive ? 'valid' : 'inactive'">
              {{ row.isActive ? t('certificate.active') : t('certificate.inactive') }}
            </span>
          </template>
        </el-table-column>
        <el-table-column :label="t('certificate.createdAt')" min-width="160">
          <template #default="{ row }">
            <span class="date-text">{{ formatDate(row.createdAt) }}</span>
          </template>
        </el-table-column>
        <el-table-column :label="t('common.actions')" width="100" align="center" fixed="right">
          <template #default="{ row }">
            <div class="actions-cell">
              <el-button class="table-action-btn danger" :icon="Delete" :title="t('common.delete')" @click="deleteAccount(row)" />
            </div>
          </template>
        </el-table-column>
      </el-table>

      <!-- Empty State for Accounts -->
      <div v-if="!loading && accounts.length === 0" class="empty-state">
        <div class="empty-icon">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
            <circle cx="12" cy="7" r="4"></circle>
          </svg>
        </div>
        <h3 class="empty-title">{{ t('certificate.noAccounts') }}</h3>
        <p class="empty-desc">{{ t('certificate.addAccountHint') }}</p>
        <button class="btn btn-primary" @click="showAccountDialog = true">{{ t('certificate.addAccount') }}</button>
      </div>
    </template>

    <!-- Dialogs -->
    <CreateCertificateDialog ref="createCertDialogRef" v-model="showCreateDialog" @success="handleCreateSuccess" @addAccount="handleAddAccount" />
    <CertificateDetailDrawer v-model="showDetailDialog" :certificate="selectedCertificate" @refresh="refreshCertificates" />

    <!-- Account Dialog -->
    <el-dialog 
      v-model="showAccountDialog" 
      :title="t('certificate.addAccountTitle')"
      width="500px"
      :close-on-click-modal="false"
    >
      <el-form ref="accountFormRef" :model="accountForm" :rules="accountRules" :label-width="t('certificate.formLabelWidth')">
        <el-form-item :label="t('certificate.email')" prop="email">
          <el-input v-model="accountForm.email" :placeholder="t('certificate.emailPlaceholder')" />
        </el-form-item>
        <el-form-item :label="t('certificate.provider')" prop="provider">
          <el-select v-model="accountForm.provider" :placeholder="t('certificate.selectProvider')" style="width: 100%" @change="handleProviderChange">
            <el-option :label="t('certificate.letsEncrypt')" value="letsencrypt" />
            <el-option :label="t('certificate.letsEncryptStaging')" value="letsencrypt-staging" />
            <el-option :label="t('certificate.zeroSsl')" value="zerossl" />
            <el-option :label="t('certificate.buypass')" value="buypass" />
          </el-select>
        </el-form-item>
        
        <!-- EAB 配置 - 仅当选择需要 EAB 的提供商时显示 -->
        <template v-if="requiresEab">
          <el-alert type="warning" :closable="false" style="margin-bottom: 16px;">
            <template #title>
              {{ accountForm.provider === 'zerossl' ? t('certificate.zerosslEabHint') : t('certificate.eabRequired') }}
            </template>
          </el-alert>
          <el-form-item label="EAB Key ID" prop="eabKid">
            <el-input v-model="accountForm.eabKid" :placeholder="t('certificate.eabKidPlaceholder')" />
          </el-form-item>
          <el-form-item label="EAB HMAC Key" prop="eabHmacKey">
            <el-input v-model="accountForm.eabHmacKey" type="password" show-password :placeholder="t('certificate.eabHmacKeyPlaceholder')" />
          </el-form-item>
        </template>
      </el-form>
      <template #footer>
        <el-button @click="showAccountDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="createAccount" :loading="creatingAccount">{{ t('common.create') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { View, Download, Refresh, Delete } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { useCertificateStore } from '@/stores/certificate'
import { useSettingsStore } from '@/stores/settings'
import { acmeApi } from '@/api/acme'
import type { Certificate } from '@/api/certificate'
import type { AcmeAccount, AcmeCertificateOrder } from '@/types/certificate'
import CreateCertificateDialog from '@/components/certificate/CreateCertificateDialog.vue'
import CertificateDetailDrawer from '@/components/certificate/CertificateDetailDrawer.vue'
import { formatLocalizedDate } from '@/utils/date'

const { t } = useI18n()
const certificatesStore = useCertificateStore()
const settingsStore = useSettingsStore()

const loading = ref(false)
const activeTab = ref<'certificates' | 'accounts'>('certificates')
const showCreateDialog = ref(false)
const createCertDialogRef = ref()
const isAddingAccountFromDialog = ref(false)
const showDetailDialog = ref(false)
const showAccountDialog = ref(false)
const selectedCertificate = ref<AcmeCertificateOrder | null>(null)
const currentPage = ref(1)
const pageSize = ref(settingsStore.defaultPageSize)

// Accounts
const accounts = ref<AcmeAccount[]>([])
const creatingAccount = ref(false)
const accountFormRef = ref()
const accountForm = ref({
  email: '',
  provider: 'letsencrypt',
  eabKid: '',
  eabHmacKey: ''
})

// 检查当前选择的提供商是否需要 EAB
const requiresEab = computed(() => {
  return ['zerossl'].includes(accountForm.value.provider)
})

const accountRules = {
  email: [
    { required: true, message: t('certificate.validation.emailRequired'), trigger: 'blur' },
    { type: 'email', message: t('certificate.validation.emailInvalid'), trigger: 'blur' }
  ],
  provider: [{ required: true, message: t('certificate.validation.providerRequired'), trigger: 'change' }],
  eabKid: [
    {
      validator: (_rule: any, value: string, callback: Function) => {
        if (requiresEab.value && !value) {
          callback(new Error(t('certificate.validation.eabKidRequired')))
        } else {
          callback()
        }
      },
      trigger: 'blur'
    }
  ],
  eabHmacKey: [
    {
      validator: (_rule: any, value: string, callback: Function) => {
        if (requiresEab.value && !value) {
          callback(new Error(t('certificate.validation.eabHmacKeyRequired')))
        } else {
          callback()
        }
      },
      trigger: 'blur'
    }
  ]
}

// 处理提供商变更
const handleProviderChange = () => {
  // 清空 EAB 字段
  accountForm.value.eabKid = ''
  accountForm.value.eabHmacKey = ''
}

const filter = ref({ domain: '', status: '' })

const statusTabs = computed(() => [
  { value: '', label: t('common.all'), color: 'gray' },
  { value: 'valid', label: t('certificate.valid'), color: 'green' },
  { value: 'expiring', label: t('certificate.expiring'), color: 'yellow' },
  { value: 'expired', label: t('certificate.expired'), color: 'red' }
])

const stats = computed(() => {
  const s = certificatesStore.statistics
  return {
    totalCertificates: s?.totalCertificates || 0,
    validCertificates: s?.validCertificates || 0,
    expiringCertificates: s?.expiringCertificates || 0,
    expiredCertificates: s?.expiredCertificates || 0
  }
})

const certificates = computed(() => certificatesStore.orders || [])

const filteredCertificates = computed(() => {
  let result = certificates.value
  if (filter.value.domain) {
    result = result.filter(cert => 
      cert.domains?.some(d => d.toLowerCase().includes(filter.value.domain.toLowerCase()))
    )
  }
  if (filter.value.status) {
    result = result.filter(cert => cert.status === filter.value.status)
  }
  return result
})

const totalPages = computed(() => Math.ceil(filteredCertificates.value.length / pageSize.value))

const paginatedCertificates = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredCertificates.value.slice(start, start + pageSize.value)
})

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

watch([() => filter.value.domain, () => filter.value.status, pageSize], () => { currentPage.value = 1 })
watch(() => settingsStore.defaultPageSize, (size) => {
  pageSize.value = size
  currentPage.value = 1
})

const getStatusLabel = (status: string) => {
  const labels: Record<string, string> = {
    valid: t('certificate.valid'),
    expiring: t('certificate.expiring'),
    expired: t('certificate.expired'),
    pending: t('certificate.pending'),
    failed: t('certificate.failed')
  }
  return labels[status] || status
}
const getProviderLabel = (provider: string) => {
  const labels: Record<string, string> = {
    letsencrypt: t('certificate.letsEncrypt'),
    'letsencrypt-staging': t('certificate.letsEncryptStaging'),
    zerossl: t('certificate.zeroSsl'),
    buypass: t('certificate.buypass')
  }
  return labels[provider] || provider
}

const getDaysUntilExpiry = (expiresAt: string) => {
  const expiry = new Date(expiresAt)
  const now = new Date()
  return Math.ceil((expiry.getTime() - now.getTime()) / (1000 * 60 * 60 * 24))
}

const getExpiryDays = (expiresAt: string) => {
  const days = getDaysUntilExpiry(expiresAt)
  if (days < 0) return t('certificate.expiredDaysAgo', { days: Math.abs(days) })
  if (days === 0) return t('certificate.expiresToday')
  return t('certificate.expiresInDays', { days })
}

const getExpiryClass = (expiresAt: string) => {
  const days = getDaysUntilExpiry(expiresAt)
  if (days <= 7) return 'danger'
  if (days <= 30) return 'warning'
  return 'normal'
}

const formatDate = (d?: string) => formatLocalizedDate(d, '--')

const shouldShowRenewButton = (cert: AcmeCertificateOrder) => {
  // 有效证书：即将过期（15天内）时显示续期按钮
  if (cert.status === 'valid') return getDaysUntilExpiry(cert.expiresAt) <= 15
  // 过期证书：可以续期
  if (cert.status === 'expired') return true
  // 即将过期证书：可以续期
  if (cert.status === 'expiring') return true
  // DNS-01 挑战待处理：可以重试
  if (cert.status === 'pending' && cert.challengeType === 'dns-01') return true
  return false
}

const refreshCertificates = async () => {
  loading.value = true
  try {
    await certificatesStore.fetchCertificates({ page: 1, pageSize: 50 })
    await certificatesStore.fetchStatistics()
  } finally { loading.value = false }
}

const refreshAccounts = async () => {
  try {
    const response = await acmeApi.getAccounts()
    // axios 拦截器已经解包了 response.data，所以这里直接使用 response
    accounts.value = Array.isArray(response) ? response : []
  } catch {
    accounts.value = []
  }
}

const refreshAll = async () => {
  loading.value = true
  try {
    await Promise.all([refreshCertificates(), refreshAccounts()])
  } finally {
    loading.value = false
  }
}

const viewCertificate = (cert: AcmeCertificateOrder) => {
  selectedCertificate.value = cert
  showDetailDialog.value = true
}

const downloadCertificate = async (cert: AcmeCertificateOrder) => {
  try {
    await certificatesStore.downloadCertificate(cert.id)
    ElMessage.success(t('common.downloadSuccess'))
  } catch { ElMessage.error(t('certificate.downloadFailed')) }
}

const renewCertificate = async (cert: AcmeCertificateOrder) => {
  try {
    await ElMessageBox.confirm(t('certificate.renewConfirmMessage', { name: cert.domains?.join(', ') }), t('certificate.renewConfirmTitle'))
    await certificatesStore.renewCertificate(cert.id)
    ElMessage.success(t('certificate.renewalStarted'))
  } catch {}
}

const deleteCertificate = async (cert: AcmeCertificateOrder) => {
  try {
    await ElMessageBox.confirm(
      t('certificate.deleteConfirmMessage', { name: cert.domains?.join(', ') }),
      t('common.deleteConfirm'),
      { type: 'warning' }
    )
    await certificatesStore.deleteCertificate(cert.id)
    ElMessage.success(t('common.deleted'))
    refreshCertificates()
  } catch (error: any) {
    // 用户取消不显示错误
    if (error === 'cancel' || error?.toString()?.includes('cancel')) return
    // 显示后端返回的错误信息
    if (error?.message || error?.response?.data?.message) {
      ElMessage.error(error.message || error.response.data.message)
    }
  }
}

const handleAutoRenewChange = async (cert: AcmeCertificateOrder) => {
  try {
    await certificatesStore.toggleAutoRenewal(cert.id, cert.autoRenew ?? false)
    ElMessage.success(cert.autoRenew ? t('certificate.autoRenewEnabled') : t('certificate.autoRenewDisabled'))
  } catch {
    cert.autoRenew = !cert.autoRenew
    ElMessage.error(t('common.error'))
  }
}

const handleCreateSuccess = () => {
  showCreateDialog.value = false
  refreshCertificates()
}

// 从证书对话框中添加账户
const handleAddAccount = () => {
  isAddingAccountFromDialog.value = true
  showAccountDialog.value = true
}

const createAccount = async () => {
  try {
    await accountFormRef.value?.validate()
    creatingAccount.value = true
    
    // 构建请求数据
    const requestData: any = {
      email: accountForm.value.email,
      provider: accountForm.value.provider
    }
    
    // 如果需要 EAB，添加 EAB 字段
    if (requiresEab.value) {
      requestData.eabKid = accountForm.value.eabKid
      requestData.eabHmacKey = accountForm.value.eabHmacKey
    }
    
    const response = await acmeApi.createAccount(requestData)
    ElMessage.success(t('certificate.accountCreated'))
    showAccountDialog.value = false
    accountForm.value = { email: '', provider: 'letsencrypt', eabKid: '', eabHmacKey: '' }

    // 刷新账户列表
    await refreshAccounts()

    // 如果是从证书对话框触发的，选中新账户
    if (isAddingAccountFromDialog.value && response?.id) {
      createCertDialogRef.value?.selectNewAccount(response.id)
      isAddingAccountFromDialog.value = false
    }
  } catch (error: any) {
    if (error.message) {
      ElMessage.error(error.message)
    }
  } finally {
    creatingAccount.value = false
  }
}

const deleteAccount = async (account: AcmeAccount) => {
  try {
    await ElMessageBox.confirm(
      t('certificate.deleteAccountConfirm', { email: account.email }), 
      t('common.deleteConfirm'),
      { type: 'warning' }
    )
    await acmeApi.deleteAccount(account.id)
    ElMessage.success(t('common.deleted'))
    refreshAccounts()
  } catch (error: any) {
    // 用户取消不显示错误
    if (error === 'cancel' || error?.toString()?.includes('cancel')) return
    // 显示后端返回的错误信息
    if (error?.message || error?.response?.data?.message) {
      ElMessage.error(error.message || error.response.data.message)
    }
  }
}

onMounted(() => refreshAll())
</script>

<style scoped>
.certs-page {
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

/* Tab Navigation */
.tab-nav {
  display: flex;
  gap: 8px;
  margin-bottom: 24px;
}

.tab-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  border-radius: 10px;
  border: 1px solid var(--border-color);
  background: var(--bg-surface);
  color: var(--text-secondary);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tab-btn svg {
  width: 18px;
  height: 18px;
}

.tab-btn:hover {
  border-color: var(--color-secondary);
  color: var(--color-secondary);
}

.tab-btn.active {
  background: var(--color-secondary);
  border-color: var(--color-secondary);
  color: #fff;
}

.tab-count {
  background: rgba(255, 255, 255, 0.2);
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 12px;
  font-weight: 600;
}

.tab-btn:not(.active) .tab-count {
  background: var(--bg-subtle);
  color: var(--text-muted);
}

/* Stats */
.stats-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 20px;
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stat-icon svg { width: 24px; height: 24px; color: #fff; }
.stat-icon.total { background: linear-gradient(135deg, #8b5cf6, #6d28d9); }
.stat-icon.valid { background: linear-gradient(135deg, #22c55e, #16a34a); }
.stat-icon.expiring { background: linear-gradient(135deg, #f59e0b, #d97706); }
.stat-icon.expired { background: linear-gradient(135deg, #ef4444, #dc2626); }

.stat-info { display: flex; flex-direction: column; }
.stat-value { font-size: 24px; font-weight: 700; color: var(--text-main); }
.stat-label { font-size: 12px; color: var(--text-muted); }

/* Toolbar */
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
  max-width: 300px;
  padding: 8px 12px;
  background: var(--bg-glass-dark);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.search-icon { width: 16px; height: 16px; color: var(--text-muted); }
.search-input { flex: 1; border: none; background: transparent; outline: none; font-size: 13px; }

.filter-tabs { display: flex; gap: 6px; }

.filter-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-radius: 6px;
  border: none;
  background: transparent;
  color: var(--text-muted);
  font-size: 12px;
  cursor: pointer;
}

.filter-tab:hover { background: var(--bg-subtle); }
.filter-tab.active { background: var(--color-secondary); color: #fff; }

.tab-dot { width: 6px; height: 6px; border-radius: 50%; }
.tab-dot.gray { background: var(--text-muted); }
.tab-dot.green { background: #22c55e; }
.tab-dot.yellow { background: #f59e0b; }
.tab-dot.red { background: #ef4444; }
.filter-tab.active .tab-dot { background: rgba(255, 255, 255, 0.8); }

/* Accounts Info */
.accounts-info {
  margin-bottom: 16px;
}

/* Table */
.data-table {
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
  overflow: hidden;
}

.td-domain { display: flex; align-items: center; gap: 12px; cursor: pointer; }
.td-domain:hover .domain-tag { background: var(--primary-color); color: #fff; }
.td-domain:hover .cert-icon { transform: scale(1.05); }

.cert-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: linear-gradient(135deg, #f59e0b, #d97706);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: transform 0.2s ease;
}

.cert-icon svg { width: 18px; height: 18px; color: #fff; }

.domain-list { display: flex; gap: 4px; flex-wrap: wrap; }
.domain-tag { font-size: 12px; font-weight: 500; color: var(--text-main); background: var(--bg-subtle); padding: 2px 8px; border-radius: 4px; transition: all 0.2s ease; }
.domain-more { font-size: 11px; color: var(--text-muted); }

.status-badge {
  font-size: 11px;
  font-weight: 600;
  padding: 4px 10px;
  border-radius: 12px;
}

.status-badge.valid { background: rgba(34, 197, 94, 0.1); color: #16a34a; }
.status-badge.expiring { background: rgba(245, 158, 11, 0.1); color: #d97706; }
.status-badge.expired { background: rgba(239, 68, 68, 0.1); color: #dc2626; }
.status-badge.pending { background: rgba(59, 130, 246, 0.1); color: #2563eb; }
.status-badge.failed { background: rgba(239, 68, 68, 0.1); color: #dc2626; }
.status-badge.inactive { background: rgba(156, 163, 175, 0.1); color: #6b7280; }

.provider-text { font-size: 12px; color: var(--text-muted); }
.provider-badge { font-size: 12px; font-weight: 500; color: var(--text-secondary); background: var(--bg-subtle); padding: 4px 10px; border-radius: 6px; }
.date-text { font-size: 12px; color: var(--text-muted); }

.expiry-info { display: flex; flex-direction: column; gap: 2px; }
.expiry-date { font-size: 12px; color: var(--text-secondary); }
.expiry-days { font-size: 10px; }
.expiry-days.danger { color: #dc2626; }
.expiry-days.warning { color: #d97706; }
.expiry-days.normal { color: var(--text-muted); }

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

.actions-cell :deep(.table-action-btn.detail:hover),
.actions-cell :deep(.table-action-btn.download:hover),
.actions-cell :deep(.table-action-btn.renew:hover) {
  color: var(--color-primary);
}

.actions-cell :deep(.table-action-btn.danger:hover) {
  color: var(--color-danger);
}

.actions-cell :deep(.el-button + .el-button) {
  margin-left: 0;
}

/* Account Email */
.account-email {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--text-main);
}

.email-icon {
  width: 16px;
  height: 16px;
  color: var(--text-muted);
}

/* Pagination */
.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 0;
  font-size: 13px;
  color: var(--text-muted);
}

.page-controls { display: flex; align-items: center; gap: 4px; }

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
.page-btn:hover:not(:disabled) { border-color: #f59e0b; color: #f59e0b; }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.page-numbers { display: flex; gap: 4px; margin: 0 8px; }

.page-num {
  min-width: 32px;
  height: 32px;
  border-radius: 6px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 13px;
  color: var(--text-muted);
}

.page-num:hover { background: var(--bg-subtle); }
.page-num.active { background: var(--color-secondary); color: #fff; }

.page-size {
  padding: 6px 10px;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  font-size: 12px;
  background: var(--bg-surface);
}

/* Empty */
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

/* Responsive */
@media (max-width: 1200px) {
  .stats-row { grid-template-columns: repeat(2, 1fr); }
}

@media (max-width: 768px) {
  .certs-page { padding: 16px; }
  .page-header { flex-direction: column; gap: 12px; }
  .stats-row { grid-template-columns: repeat(2, 1fr); gap: 12px; }
  .stat-card { padding: 16px; }
  .toolbar { flex-wrap: wrap; }
  .search-box { max-width: none; width: 100%; }
  .filter-tabs { width: 100%; overflow-x: auto; }
  .pagination { flex-direction: column; gap: 12px; }
  .tab-nav { flex-wrap: wrap; }
}

</style>

<style>
/* Dark Mode */
html.dark .stat-card, html.dark .toolbar, html.dark .data-table { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-box { background: #0f172a; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-input { color: #f1f5f9; }
html.dark .stat-value { color: #f1f5f9; }
html.dark .domain-tag { background: rgba(255, 255, 255, 0.1); color: #f1f5f9; }
html.dark .page-btn, html.dark .tab-btn { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
</style>
