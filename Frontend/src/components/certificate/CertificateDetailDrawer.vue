<template>
  <el-drawer
    v-model="drawerVisible"
    :title="t('certificate.drawerTitle')"
    direction="rtl"
    size="60%"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <div v-if="loading" class="loading-container">
      <el-skeleton :rows="8" animated />
    </div>

    <div v-else-if="certificate" class="certificate-detail">
      <!-- 操作按钮 -->
      <div class="action-bar">
        <el-button size="small" @click="refreshCertificate">
          <el-icon><Refresh /></el-icon>
          {{ t('certificate.refresh') }}
        </el-button>
        <el-button
          v-if="certificate?.status === 'valid' || certificate?.status === 'expiring'"
          type="primary"
          size="small"
          @click="downloadCertificate"
        >
          <el-icon><Download /></el-icon>
          {{ t('certificate.downloadCertificate') }}
        </el-button>
        <el-button
          v-if="shouldShowRenewButton()"
          type="warning"
          size="small"
          @click="renewCertificate"
          :loading="renewing"
        >
          <el-icon><Refresh /></el-icon>
          {{ t('certificate.renewCertificate') }}
        </el-button>
        <el-button
          v-if="shouldShowCancelButton()"
          type="danger"
          size="small"
          @click="cancelCertificateOrder"
        >
          <el-icon><CircleClose /></el-icon>
          {{ t('certificate.cancelApplication') }}
        </el-button>
      </div>

      <!-- 基本信息 -->
      <el-card class="detail-card" shadow="never">
        <template #header>
          <div class="card-header">
            <h3>{{ t('certificate.basicInfo') }}</h3>
            <el-tag :type="getStatusType(certificate.status)" effect="dark">
              {{ getStatusLabel(certificate.status) }}
            </el-tag>
          </div>
        </template>

        <div class="info-grid">
          <div class="info-item">
            <label>{{ t('certificate.certificateName') }}</label>
            <div class="value">{{ certificate.name }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('certificate.acmeProviderLabel') }}</label>
            <div class="value">{{ getProviderLabel(certificate.provider) }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('certificate.email') }}</label>
            <div class="value">{{ certificate.email }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('certificate.challengeTypeLabel') }}</label>
            <div class="value">
              <el-tag size="small" effect="plain">{{ getChallengeTypeLabel(certificate.challengeType) }}</el-tag>
            </div>
          </div>

          <div class="info-item">
            <label>{{ t('certificate.autoRenewLabel') }}</label>
            <div class="value">
              <el-tag :type="certificate.autoRenew ? 'success' : 'info'" size="small" effect="plain">
                {{ certificate.autoRenew ? t('certificate.enabled') : t('certificate.disabled') }}
              </el-tag>
            </div>
          </div>

          <div class="info-item" v-if="certificate.description">
            <label>{{ t('certificate.description') }}</label>
            <div class="value">{{ certificate.description }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('certificate.createdAt') }}</label>
            <div class="value">{{ formatDateTime(certificate.createdAt) }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('certificate.updatedAt') }}</label>
            <div class="value">{{ formatDateTime(certificate.updatedAt) }}</div>
          </div>
        </div>
      </el-card>

      <!-- 域名信息 -->
      <el-card class="detail-card" shadow="never">
        <template #header>
          <div class="card-header">
            <h3>{{ t('certificate.domainInfo') }}</h3>
            <el-tag type="info" size="small" effect="plain">
              {{ t('certificate.domainCount', { count: certificate.domains?.length || 0 }) }}
            </el-tag>
          </div>
        </template>

        <div class="domains-section">
          <div class="domain-list">
            <div
              v-for="domain in certificate.domains"
              :key="domain"
              class="domain-item"
            >
              <el-icon class="domain-icon"><Link /></el-icon>
              <span class="domain-name">{{ domain }}</span>
              <el-tag
                v-if="domain.startsWith('*.')"
                type="warning"
                size="small"
                effect="plain"
              >
                {{ t('certificate.wildcard') }}
              </el-tag>
            </div>
          </div>
        </div>
      </el-card>

      <!-- 证书信息 -->
      <el-card class="detail-card" shadow="never" v-if="certificate.issuedAt">
        <template #header>
          <div class="card-header">
            <h3>{{ t('certificate.certificateInfo') }}</h3>
          </div>
        </template>

        <div class="cert-info">
          <div class="info-row">
            <label>{{ t('certificate.issuedAt') }}</label>
            <span>{{ formatDateTime(certificate.issuedAt) }}</span>
          </div>

          <div class="info-row">
            <label>{{ t('certificate.expiresAt') }}</label>
            <div class="expiry-info">
              <span>{{ formatDateTime(certificate.expiresAt) }}</span>
              <el-text
                v-if="certificate.status === 'valid'"
                :type="getExpiryWarningType(certificate.expiresAt)"
                size="small"
              >
                {{ getExpiryDays(certificate.expiresAt) }}
              </el-text>
            </div>
          </div>

          <div class="info-row" v-if="certificate.serialNumber">
            <label>{{ t('certificate.serialNumber') }}</label>
            <el-text class="mono-text">{{ certificate.serialNumber }}</el-text>
          </div>

          <div class="info-row" v-if="certificate.fingerprint">
            <label>{{ t('certificate.fingerprint') }}</label>
            <el-text class="mono-text">{{ certificate.fingerprint }}</el-text>
          </div>

          <div class="info-row" v-if="certificate.issuer">
            <label>{{ t('certificate.issuer') }}</label>
            <span>{{ certificate.issuer }}</span>
          </div>

          <div class="info-row" v-if="certificate.subject">
            <label>{{ t('certificate.subject') }}</label>
            <span>{{ certificate.subject }}</span>
          </div>
        </div>
      </el-card>

      <!-- DNS验证配置 -->
      <el-card class="detail-card" shadow="never" v-if="certificate.challengeType === 'dns-01'">
        <template #header>
          <div class="card-header">
            <h3>{{ t('certificate.dnsValidationConfig') }}</h3>
          </div>
        </template>

        <div class="dns-config">
          <div class="info-row">
            <label>{{ t('certificate.dnsProviderLabel') }}</label>
            <el-tag v-if="certificate.dnsProvider" size="small" effect="plain">
              {{ getDnsProviderLabel(certificate.dnsProvider) }}
            </el-tag>
            <span v-else style="color: var(--text-muted);">--</span>
          </div>

          <div class="config-fields" v-if="certificate.dnsConfigFields && certificate.dnsConfigFields.length > 0">
            <div
              v-for="field in certificate.dnsConfigFields"
              :key="field"
              class="config-field"
            >
              <label>{{ getConfigFieldLabel(field) }}</label>
              <el-tag type="success" size="small" effect="plain">
                <el-icon><Lock /></el-icon>
                {{ t('certificate.configured') }}
              </el-tag>
            </div>
          </div>
        </div>
      </el-card>

      <!-- HTTP验证配置 -->
      <el-card class="detail-card" shadow="never" v-if="certificate.challengeType === 'http-01'">
        <template #header>
          <div class="card-header">
            <h3>{{ t('certificate.httpValidationConfig') }}</h3>
          </div>
        </template>

        <div class="http-config">
          <div class="info-row">
            <label>{{ t('certificate.webRoot') }}</label>
            <span>{{ certificate.webRoot || '--' }}</span>
          </div>
        </div>
      </el-card>

      <!-- 申请日志 -->
      <el-card class="detail-card" shadow="never" v-if="certificate.logs?.length">
        <template #header>
          <div class="card-header">
            <h3>{{ t('certificate.applicationLog') }}</h3>
          </div>
        </template>

        <div class="logs-section">
          <div
            v-for="(log, index) in certificate.logs"
            :key="index"
            class="log-item"
          >
            <div class="log-time">{{ formatDateTime(log.timestamp) }}</div>
            <div class="log-level">
              <el-tag :type="getLogLevelType(log.level)" size="small" effect="plain">
                {{ log.level?.toUpperCase() }}
              </el-tag>
            </div>
            <div class="log-message">{{ log.message }}</div>
          </div>
        </div>
      </el-card>

      <!-- 详细进度跟踪 -->
      <el-card class="detail-card" shadow="never" v-if="showProgress">
        <template #header>
          <div class="card-header">
            <h3>{{ t('certificate.applicationProgress') }}</h3>
            <el-button
              @click="toggleProgress"
              size="small"
              text
            >
              {{ progressExpanded ? t('certificate.collapse') : t('certificate.expand') }}
              <el-icon><component :is="progressExpanded ? ArrowUp : ArrowDown" /></el-icon>
            </el-button>
          </div>
        </template>

        <div v-if="progressExpanded">
          <div v-if="loadingProgress" class="loading-progress">
            <el-skeleton :rows="3" animated />
          </div>

          <div v-else-if="!progressData" class="no-progress-data">
            <el-empty :description="t('certificate.noProgressData')" :image-size="100">
              <template #description>
                <p>{{ t('certificate.progressDataCleaned') }}</p>
                <p v-if="certificate.status === 'pending'" class="warning-tip">
                  <el-icon><Warning /></el-icon>
                  <span>{{ t('certificate.certificateStillPending') }}</span>
                </p>
                <p v-if="certificate.error" class="simple-error">
                  <strong>{{ t('certificate.errorMessage') }}</strong>{{ certificate.error }}
                </p>
              </template>
            </el-empty>
          </div>

          <div v-else class="detailed-progress">
            <!-- 进度概览 -->
            <div class="progress-overview">
              <div class="overview-item">
                <label>{{ t('certificate.currentStatus') }}</label>
                <el-tag :type="getProgressStatusType(progressData.status)" size="small" effect="dark">
                  {{ getProgressStatusLabel(progressData.status) }}
                </el-tag>
              </div>
              <div class="overview-item">
                <label>{{ t('certificate.currentStep') }}</label>
                <span>{{ progressData.currentStepDescription }}</span>
              </div>
              <div class="overview-item full-width">
                <label>{{ t('certificate.progress') }}</label>
                <el-progress
                  :percentage="progressData.progressPercentage"
                  :status="getProgressStatus(progressData.status)"
                  :stroke-width="8"
                />
              </div>
              <div class="overview-item">
                <label>{{ t('certificate.startTime') }}</label>
                <span>{{ formatDateTime(progressData.startedAt) }}</span>
              </div>
              <div class="overview-item" v-if="progressData.completedAt">
                <label>{{ t('certificate.completedTime') }}</label>
                <span>{{ formatDateTime(progressData.completedAt) }}</span>
              </div>
            </div>

            <!-- 错误和警告 -->
            <div v-if="progressData.errors.length > 0 || progressData.warnings.length > 0" class="messages-section">
              <h4>{{ t('certificate.errorsAndWarnings') }}</h4>
              <div v-if="progressData.errors.length > 0" class="error-messages">
                <div v-for="(error, index) in progressData.errors" :key="'error-' + index" class="message-item error">
                  <el-icon><CircleClose /></el-icon>
                  <span>{{ error }}</span>
                </div>
              </div>
              <div v-if="progressData.warnings.length > 0" class="warning-messages">
                <div v-for="(warning, index) in progressData.warnings" :key="'warning-' + index" class="message-item warning">
                  <el-icon><Warning /></el-icon>
                  <span>{{ warning }}</span>
                </div>
              </div>
            </div>

            <!-- 详细步骤 -->
            <div class="steps-section">
              <h4>{{ t('certificate.detailedSteps') }}</h4>
              <div class="steps-timeline">
                <div
                  v-for="(step, index) in progressData.steps"
                  :key="'step-' + index"
                  class="step-item"
                  :class="{
                    'step-completed': step.isCompleted,
                    'step-current': step.step === progressData.currentStep,
                    'step-error': !step.isSuccess && step.isCompleted
                  }"
                >
                  <div class="step-marker">
                    <el-icon v-if="step.isCompleted && step.isSuccess">
                      <CircleCheck />
                    </el-icon>
                    <el-icon v-else-if="step.isCompleted && !step.isSuccess">
                      <CircleClose />
                    </el-icon>
                    <el-icon v-else class="spinning">
                      <Loading />
                    </el-icon>
                  </div>
                  <div class="step-content">
                    <div class="step-header">
                      <span class="step-title">{{ step.description }}</span>
                      <span class="step-time" v-if="step.completedAt">
                        {{ formatDateTime(step.completedAt) }}
                      </span>
                    </div>
                    <div class="step-message" v-if="step.message">
                      {{ step.message }}
                    </div>
                    <div v-if="step.durationSeconds" class="step-duration">
                      {{ t('certificate.duration', { seconds: step.durationSeconds }) }}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </el-card>
    </div>

    <div v-else class="empty-state">
      <el-empty :description="t('certificate.notFound')" />
    </div>
  </el-drawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Link, Download, Refresh, ArrowUp, ArrowDown, CircleCheck, CircleClose, Warning, Loading, Lock } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { useCertificateStore } from '@/stores/certificate'
import { certificateProgressApi } from '@/api/certificate'
import type { AcmeCertificateOrder } from '@/types/certificate'
import type { ProgressTrackResponse } from '@/api/certificate'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()

interface Props {
  modelValue: boolean
  certificate?: AcmeCertificateOrder | null
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'refresh'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const certificatesStore = useCertificateStore()

// 响应式数据
const loading = ref(false)
const renewing = ref(false)
const progressExpanded = ref(true)
const progressData = ref<ProgressTrackResponse | null>(null)
const loadingProgress = ref(false)

const drawerVisible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

// 是否显示进度跟踪
const showProgress = computed(() => {
  return props.certificate && (
    props.certificate.status === 'pending' ||
    props.certificate.status === '申请中' ||
    props.certificate.status === 'failed' ||
    props.certificate.error
  )
})

// 获取状态类型
const getStatusType = (status: string) => {
  const typeMap: Record<string, string> = {
    valid: 'success',
    expiring: 'warning',
    expired: 'danger',
    pending: 'info',
    failed: 'danger',
    cancelled: 'warning'
  }
  return typeMap[status] || 'info'
}

// 获取状态标签
const getStatusLabel = (status: string) => {
  const labelMap: Record<string, string> = {
    valid: t('certificate.valid'),
    expiring: t('certificate.expiring'),
    expired: t('certificate.expired'),
    pending: t('certificate.pending'),
    failed: t('certificate.failed'),
    cancelled: t('certificate.cancelled')
  }
  return labelMap[status] || status
}

// 获取服务商标签
const getProviderLabel = (provider?: string) => {
  const providerMap: Record<string, string> = {
    letsencrypt: t('certificate.letsEncrypt'),
    'letsencrypt-staging': t('certificate.letsEncryptStaging'),
    zerossl: t('certificate.zeroSsl'),
    buypass: t('certificate.buypass')
  }
  return providerMap[provider || ''] || provider
}

// 获取验证方式标签
const getChallengeTypeLabel = (type?: string) => {
  const typeMap: Record<string, string> = {
    'http-01': t('certificate.http01'),
    'dns-01': t('certificate.dns01'),
    'tls-alpn-01': t('certificate.tlsAlpn01')
  }
  return typeMap[type || ''] || type
}

// 获取DNS服务商标签
const getDnsProviderLabel = (provider?: string) => {
  const providerMap: Record<string, string> = {
    cloudflare: t('certificate.cloudflare'),
    aliyun: t('certificate.aliyunDns'),
    tencent: t('certificate.tencentDns'),
    dnspod: t('certificate.dnsPod'),
    aws: t('certificate.awsRoute53'),
    azure: t('certificate.azureDns'),
    godaddy: t('certificate.goDaddy')
  }
  return providerMap[provider || ''] || provider
}

// 获取配置字段标签
const getConfigFieldLabel = (key: string | number) => {
  const fieldMap: Record<string, string> = {
    apiKey: t('certificate.apiKey'),
    email: t('certificate.email'),
    accessKeyId: t('certificate.accessKeyId'),
    accessKeySecret: t('certificate.accessKeySecret'),
    secretId: t('certificate.secretId'),
    secretKey: t('certificate.secretKey'),
    clientId: t('certificate.clientId'),
    clientSecret: t('certificate.clientSecret'),
    tenantId: t('certificate.tenantId'),
    subscriptionId: t('certificate.subscriptionId'),
    resourceGroup: t('certificate.resourceGroup'),
    region: t('certificate.region'),
    apiSecret: t('certificate.apiSecret')
  }
  return fieldMap[key] || key
}

// 获取日志级别类型
const getLogLevelType = (level: string) => {
  const levelMap: Record<string, string> = {
    error: 'danger',
    warning: 'warning',
    info: 'info',
    debug: 'info'
  }
  return levelMap[level.toLowerCase()] || 'info'
}

// 获取过期警告类型
const getExpiryWarningType = (expiresAt?: string) => {
  const days = getDaysUntilExpiry(expiresAt)
  if (days <= 7) return 'danger'
  if (days <= 30) return 'warning'
  return 'info'
}

// 获取过期天数
const getExpiryDays = (expiresAt?: string) => {
  const days = getDaysUntilExpiry(expiresAt)
  if (days < 0) return t('certificate.expiredDaysAgo', { days: Math.abs(days) })
  if (days === 0) return t('certificate.expiresToday')
  if (days === 1) return t('certificate.expiresTomorrow')
  return t('certificate.expiresInDays', { days })
}

// 计算距离过期天数
const getDaysUntilExpiry = (expiresAt?: string) => {
  if (!expiresAt) return 0
  const expiry = new Date(expiresAt)
  const now = new Date()
  const diffTime = expiry.getTime() - now.getTime()
  return Math.ceil(diffTime / (1000 * 60 * 60 * 24))
}

// 是否显示续期按钮
const shouldShowRenewButton = () => {
  if (!props.certificate || props.certificate.status !== 'valid') return false
  const days = getDaysUntilExpiry(props.certificate.expiresAt)
  return days <= 30 || props.certificate.autoRenew
}

// 是否显示取消申请按钮
const shouldShowCancelButton = () => {
  if (!props.certificate) return false
  return props.certificate.status === 'pending' || props.certificate.status === 'failed'
}

// 格式化日期时间
const formatDateTime = (dateString?: string) => {
  if (!dateString) return '--'
  return formatLocalizedDateTime(dateString, '--')
}

// 刷新证书
const refreshCertificate = () => {
  emit('refresh')
}

// 下载证书
const downloadCertificate = async () => {
  if (!props.certificate) return

  try {
    await certificatesStore.downloadCertificate(props.certificate.id)
  } catch (error: any) {
    ElMessage.error(error.message || t('certificate.downloadFailed'))
  }
}

// 续期证书
const renewCertificate = async () => {
  if (!props.certificate) return

  try {
    await ElMessageBox.confirm(
      t('certificate.renewConfirmMessage', { name: props.certificate.name }),
      t('certificate.renewConfirmTitle'),
      {
        confirmButtonText: t('certificate.renewButton'),
        cancelButtonText: t('certificate.cancelRenewButton'),
        type: 'warning'
      }
    )

    renewing.value = true
    await certificatesStore.renewCertificate(props.certificate.id)
    ElMessage.success(t('certificate.renewalStarted'))
    emit('refresh')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.message || t('certificate.renewalFailed'))
    }
  } finally {
    renewing.value = false
  }
}

// 处理关闭
const handleClose = () => {
  drawerVisible.value = false
}

// 切换进度显示
const toggleProgress = () => {
  progressExpanded.value = !progressExpanded.value
}

// 取消证书申请
const cancelCertificateOrder = async () => {
  if (!props.certificate) return

  try {
    await ElMessageBox.confirm(
      t('certificate.cancelApplicationMessage'),
      t('certificate.cancelApplicationTitle'),
      {
        confirmButtonText: t('certificate.confirmCancelButton'),
        cancelButtonText: t('certificate.keepApplicationButton'),
        type: 'warning',
      }
    )

    await certificatesStore.cancelCertificateOrder(props.certificate.id)
    ElMessage.success(t('certificate.applicationCancelled'))
    drawerVisible.value = false
    emit('refresh')
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('取消证书申请失败:', error)
    }
  }
}

// 加载证书进度数据
const loadProgressData = async () => {
  if (!props.certificate?.id) return

  try {
    loadingProgress.value = true
    const progress = await certificateProgressApi.getProgressByCertificateId(props.certificate.id)
    progressData.value = progress
  } catch (error: any) {
    progressData.value = null
  } finally {
    loadingProgress.value = false
  }
}

// 监听证书变化
watch(() => props.certificate, (newCertificate) => {
  if (newCertificate && showProgress.value) {
    loadProgressData()
  }
}, { immediate: true })

// 监听抽屉打开
watch(drawerVisible, (isOpen) => {
  if (isOpen && props.certificate && showProgress.value) {
    loadProgressData()
  }
})

// 获取进度状态类型
const getProgressStatusType = (status: number) => {
  const statusMap: Record<number, string> = {
    0: 'info',
    1: 'warning',
    2: 'success',
    3: 'danger',
    4: 'info'
  }
  return statusMap[status] || 'info'
}

// 获取进度状态标签
const getProgressStatusLabel = (status: number) => {
  const labelMap: Record<number, string> = {
    0: t('certificate.statusWaiting'),
    1: t('certificate.statusInProgress'),
    2: t('certificate.statusCompleted'),
    3: t('certificate.statusFailed'),
    4: t('certificate.statusCancelled')
  }
  return labelMap[status] || t('certificate.statusUnknown')
}

// 获取进度条状态
const getProgressStatus = (status: number) => {
  if (status === 2) return 'success'
  if (status === 3) return 'exception'
  return undefined
}
</script>

<style scoped>
.certificate-detail {
  display: flex;
  flex-direction: column;
  gap: 20px;
  padding: 0 4px;
}

.loading-container {
  padding: 40px;
}

.action-bar {
  display: flex;
  gap: 12px;
  padding: 16px 20px;
  background: linear-gradient(135deg, var(--primary-gradient-start) 0%, var(--primary-gradient-end) 100%);
  border-radius: 12px;
  margin-bottom: 8px;
}

.action-bar .el-button {
  border-color: rgba(255, 255, 255, 0.3);
  color: white;
  background: rgba(255, 255, 255, 0.1);
  min-width: 104px;
}

.action-bar .el-button:hover {
  background: rgba(255, 255, 255, 0.2);
  border-color: rgba(255, 255, 255, 0.5);
}

.action-bar .el-button--primary {
  background: rgba(255, 255, 255, 0.25);
  border-color: rgba(255, 255, 255, 0.4);
}

.action-bar .el-button--warning {
  background: rgba(245, 158, 11, 0.3);
  border-color: rgba(245, 158, 11, 0.5);
}

.action-bar .el-button--danger {
  background: rgba(239, 68, 68, 0.3);
  border-color: rgba(239, 68, 68, 0.5);
}

.detail-card {
  border: 1px solid var(--border-color);
  border-radius: 12px;
  overflow: hidden;
}

.detail-card :deep(.el-card__header) {
  padding: 16px 20px;
  background: var(--bg-subtle);
  border-bottom: 1px solid var(--border-color);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-header h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
  color: var(--text-main);
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.info-item label {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
}

.info-item .value {
  font-size: 14px;
  color: var(--text-main);
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.domains-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.domain-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.domain-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-subtle);
  transition: all 0.2s ease;
}

.domain-item:hover {
  border-color: var(--primary-color);
  background: var(--bg-glass);
}

.domain-icon {
  color: var(--primary-color);
}

.domain-name {
  flex: 1;
  font-size: 14px;
  color: var(--text-main);
  font-family: monospace;
}

.cert-info,
.dns-config,
.http-config {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.info-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 0;
  border-bottom: 1px solid var(--border-color-light);
}

.info-row:last-child {
  border-bottom: none;
}

.info-row label {
  font-size: 13px;
  color: var(--text-secondary);
  font-weight: 500;
}

.info-row > span {
  font-size: 14px;
  color: var(--text-main);
}

.expiry-info {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
}

.mono-text {
  font-family: monospace;
  font-size: 12px;
  word-break: break-all;
}

.config-fields {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.config-field {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: var(--bg-subtle);
  border-radius: 6px;
}

.config-field label {
  font-size: 13px;
  color: var(--text-secondary);
}

.logs-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-height: 300px;
  overflow-y: auto;
}

.log-item {
  padding: 12px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-subtle);
}

.log-time {
  font-size: 11px;
  color: var(--text-muted);
  margin-bottom: 4px;
}

.log-level {
  margin-bottom: 6px;
}

.log-message {
  font-size: 13px;
  color: var(--text-main);
}

.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 300px;
}

/* 进度跟踪样式 */
.loading-progress {
  padding: 20px;
}

.no-progress-data {
  padding: 20px;
  text-align: center;
}

.simple-error {
  color: var(--danger-color);
  font-size: 14px;
  margin-top: 12px;
  padding: 12px;
  background: var(--danger-bg);
  border-radius: 8px;
  border: 1px solid var(--danger-border);
}

.warning-tip {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--warning-color);
  font-size: 13px;
  margin: 8px 0;
  padding: 10px 14px;
  background: var(--warning-bg);
  border-radius: 6px;
  border-left: 3px solid var(--warning-color);
}

.detailed-progress {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.progress-overview {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  padding: 16px;
  background: var(--bg-subtle);
  border-radius: 10px;
  border: 1px solid var(--border-color);
}

.overview-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.overview-item.full-width {
  grid-column: span 2;
}

.overview-item label {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
}

.overview-item span {
  font-size: 14px;
  color: var(--text-main);
}

.messages-section {
  padding: 16px;
  background: var(--danger-bg);
  border-radius: 10px;
  border: 1px solid var(--danger-border);
}

.messages-section h4 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main);
}

.error-messages,
.warning-messages {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.message-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  border-radius: 6px;
  font-size: 13px;
}

.message-item.error {
  background: var(--danger-bg);
  color: var(--danger-color);
  border: 1px solid var(--danger-border);
}

.message-item.warning {
  background: var(--warning-bg);
  color: var(--warning-color);
  border: 1px solid var(--warning-border);
}

.steps-section {
  padding: 8px 0;
}

.steps-section h4 {
  margin: 0 0 16px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main);
}

.steps-timeline {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.step-item {
  display: flex;
  gap: 14px;
  padding: 14px;
  border: 1px solid var(--border-color);
  border-radius: 10px;
  background: var(--bg-subtle);
  transition: all 0.3s ease;
}

.step-item.step-completed {
  background: var(--success-bg);
  border-color: var(--success-border);
}

.step-item.step-current {
  background: var(--warning-bg);
  border-color: var(--warning-color);
  box-shadow: 0 2px 8px rgba(251, 191, 36, 0.15);
}

.step-item.step-error {
  background: var(--danger-bg);
  border-color: var(--danger-border);
}

.step-marker {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: var(--border-color);
  color: var(--text-muted);
  flex-shrink: 0;
}

.step-item.step-completed .step-marker {
  background: var(--success-color);
  color: white;
}

.step-item.step-current .step-marker {
  background: var(--warning-color);
  color: white;
}

.step-item.step-error .step-marker {
  background: var(--danger-color);
  color: white;
}

.step-marker .spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.step-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.step-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.step-title {
  font-weight: 600;
  color: var(--text-main);
  font-size: 14px;
}

.step-time {
  font-size: 12px;
  color: var(--text-muted);
}

.step-message {
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.4;
}

.step-duration {
  font-size: 12px;
  color: var(--text-muted);
}

@media (max-width: 768px) {
  .info-grid {
    grid-template-columns: 1fr;
  }

  .progress-overview {
    grid-template-columns: 1fr;
  }

  .overview-item.full-width {
    grid-column: span 1;
  }

  .step-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 4px;
  }
}
</style>
