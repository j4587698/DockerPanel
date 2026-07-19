<template>
  <el-dialog
    v-model="dialogVisible"
    :title="t('certificate.requestCertificate')"
    width="700px"
    :close-on-click-modal="false"
    custom-class="cert-dialog"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="140px"
      @submit.prevent
    >
      <el-form-item :label="t('certificate.domain')" prop="domains" required>
        <div class="domains-input">
          <div
            v-for="(_, index) in form.domains"
            :key="index"
            class="domain-item"
          >
            <el-input
              v-model="form.domains[index]"
              :placeholder="t('certificate.createDialog.domainPlaceholder')"
              clearable
            />
            <el-button
              v-if="form.domains.length > 1"
              type="danger"
              size="small"
              text
              @click="removeDomain(index)"
            >
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
          <el-button type="primary" plain @click="addDomain" :icon="Plus">{{ t('certificate.createDialog.addDomain') }}</el-button>
        </div>
        <div class="form-help">
          {{ t('certificate.createDialog.domainHint') }}
        </div>
      </el-form-item>

      <el-form-item :label="t('certificate.createDialog.acmeAccount')" prop="accountId">
        <el-select v-model="form.accountId" :placeholder="t('certificate.createDialog.selectAcmeAccount')" style="width: 100%" @change="handleAccountChange">
          <el-option
            v-for="account in accounts"
            :key="account.id"
            :label="`${account.email} (${getProviderLabel(account.provider)})`"
            :value="account.id"
          >
            <div class="account-option">
              <span>{{ account.email }}</span>
              <el-tag size="small" type="info">{{ getProviderLabel(account.provider) }}</el-tag>
            </div>
          </el-option>
          <el-option value="__add_new__" class="add-account-option">
            <div class="add-account-option-content">
              <el-icon><Plus /></el-icon>
              <span>{{ t('certificate.createDialog.addNewAccount') }}</span>
            </div>
          </el-option>
        </el-select>
      </el-form-item>

      <el-form-item :label="t('certificate.challengeType')" prop="challengeType">
        <el-radio-group v-model="form.challengeType" @change="handleChallengeTypeChange">
          <el-radio value="http-01">HTTP-01</el-radio>
          <el-radio value="dns-01">DNS-01</el-radio>
          <el-radio value="tls-alpn-01">TLS-ALPN-01</el-radio>
        </el-radio-group>
        <div class="form-help">
          <div v-if="form.challengeType === 'http-01'">
            {{ t('certificate.createDialog.http01Hint') }}
          </div>
          <div v-if="form.challengeType === 'dns-01'">
            {{ t('certificate.createDialog.dns01Hint') }}
          </div>
          <div v-if="form.challengeType === 'tls-alpn-01'">
            {{ t('certificate.createDialog.tlsAlpn01Hint') }}
          </div>
        </div>
      </el-form-item>

      <!-- DNS-01 验证配置 -->
      <template v-if="form.challengeType === 'dns-01'">
        <el-form-item :label="t('certificate.dnsProvider')" prop="dnsProvider">
          <el-select v-model="form.dnsProvider" :placeholder="t('certificate.createDialog.selectDnsProvider')" style="width: 100%">
            <el-option
              v-for="provider in dnsProviders"
              :key="provider.value"
              :label="provider.label"
              :value="provider.value"
            />
          </el-select>
          <div class="form-help">
            {{ t('certificate.createDialog.dnsProviderHint') }}
          </div>
        </el-form-item>

        <template v-for="(field, key) in currentDnsFields" :key="key">
          <el-form-item
            v-if="field && field.label"
            :label="field.label"
            :prop="`dnsConfig.${key}`"
          >
          <el-input
              v-model="form.dnsConfig[key]"
              :placeholder="field.placeholder"
              :type="field.type === 'password' ? 'password' : 'text'"
              show-password
              clearable
            />
          </el-form-item>
        </template>
      </template>

      <!-- 自动续期选项 - 对所有验证方式可见 -->
      <el-form-item :label="t('certificate.autoRenew')">
        <el-switch v-model="form.autoRenew" />
        <div class="form-help">
          {{ t('certificate.createDialog.autoRenewHint') }}
        </div>
      </el-form-item>

      <el-form-item :label="t('certificate.description')">
        <el-input
          v-model="form.description"
          type="textarea"
          :rows="3"
          :placeholder="t('certificate.createDialog.descriptionPlaceholder')"
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="loading">
          {{ t('certificate.requestCertificate') }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { Plus, Delete } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { useCertificateStore } from '@/stores/certificate'
import { acmeApi } from '@/api/acme'
import type { AcmeAccount } from '@/types/certificate'

const { t } = useI18n()

interface Props {
  modelValue: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'success'): void
  (e: 'addAccount'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const certificatesStore = useCertificateStore()

// 响应式数据
const formRef = ref<FormInstance>()
const loading = ref(false)
const accounts = ref<AcmeAccount[]>([])

// 当前DNS提供商字段
const currentDnsFields = computed(() => {
  return dnsProviderFields.value || {}
})

const formInitialState = {
  domains: [''],
  accountId: '',
  challengeType: 'http-01',
  dnsProvider: '',
  dnsConfig: {} as Record<string, string>,
  autoRenew: true,
  description: ''
}

const form = reactive({ ...formInitialState })

const resetFormState = () => {
  Object.keys(formInitialState).forEach(key => {
    delete (form as any)[key]
  })
  Object.assign(form, JSON.parse(JSON.stringify(formInitialState)))
  nextTick(() => {
    formRef.value?.clearValidate()
  })
}

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

// 加载账户列表
const loadAccounts = async () => {
  try {
    const response = await acmeApi.getAccounts()
    accounts.value = Array.isArray(response) ? response : []
  } catch {
    accounts.value = []
  }
}

// 处理账户选择变化
const handleAccountChange = (value: string) => {
  if (value === '__add_new__') {
    form.accountId = '' // 清空选择
    emit('addAccount')
  }
}

// 选择新创建的账户（供父组件调用）
const selectNewAccount = (accountId: string) => {
  form.accountId = accountId
}

// 监听对话框打开，加载账户
watch(dialogVisible, (visible) => {
  if (visible) {
    loadAccounts()
  }
})

// 获取服务商显示名称
const getProviderLabel = (provider: string) => {
  const labels: Record<string, string> = {
    letsencrypt: "Let's Encrypt",
    'letsencrypt-staging': t('certificate.createDialog.letsEncryptStaging'),
    zerossl: 'ZeroSSL',
    buypass: 'Buypass'
  }
  return labels[provider] || provider
}

// DNS提供商列表
const dnsProviders = computed(() => [
  { value: 'cloudflare', label: 'Cloudflare' },
  { value: 'aliyun', label: t('certificate.aliyunDns') },
  { value: 'tencent', label: t('certificate.tencentDns') },
  { value: 'dnspod', label: t('certificate.dnsPod') },
  { value: 'aws', label: t('certificate.awsRoute53') },
  { value: 'azure', label: t('certificate.azureDns') },
  { value: 'godaddy', label: t('certificate.goDaddy') }
])

// DNS提供商配置字段
const dnsProviderFields = computed(() => {
  const fields: Record<string, Record<string, { label: string; placeholder: string; type?: string; help: string }>> = {
    cloudflare: {
      apiKey: {
        label: t('certificate.createDialog.dnsFields.cloudflare.apiKey'),
        placeholder: 'Cloudflare Global API Key',
        type: 'password',
        help: t('certificate.createDialog.dnsFields.cloudflare.apiKeyHelp')
      },
      email: {
        label: t('certificate.createDialog.dnsFields.cloudflare.email'),
        placeholder: t('certificate.createDialog.dnsFields.cloudflare.emailPlaceholder'),
        help: t('certificate.createDialog.dnsFields.cloudflare.emailHelp')
      }
    },
    aliyun: {
      accessKeyId: {
        label: t('certificate.createDialog.dnsFields.aliyun.accessKeyId'),
        placeholder: t('certificate.createDialog.dnsFields.aliyun.accessKeyIdPlaceholder'),
        help: t('certificate.createDialog.dnsFields.aliyun.accessKeyIdHelp')
      },
      accessKeySecret: {
        label: t('certificate.createDialog.dnsFields.aliyun.accessKeySecret'),
        placeholder: t('certificate.createDialog.dnsFields.aliyun.accessKeySecretPlaceholder'),
        type: 'password',
        help: t('certificate.createDialog.dnsFields.aliyun.accessKeySecretHelp')
      }
    },
    tencent: {
      secretId: {
        label: t('certificate.createDialog.dnsFields.tencent.secretId'),
        placeholder: t('certificate.createDialog.dnsFields.tencent.secretIdPlaceholder'),
        help: t('certificate.createDialog.dnsFields.tencent.secretIdHelp')
      },
      secretKey: {
        label: t('certificate.createDialog.dnsFields.tencent.secretKey'),
        placeholder: t('certificate.createDialog.dnsFields.tencent.secretKeyPlaceholder'),
        type: 'password',
        help: t('certificate.createDialog.dnsFields.tencent.secretKeyHelp')
      }
    },
    dnspod: {
      secretId: {
        label: t('certificate.createDialog.dnsFields.dnspod.secretId'),
        placeholder: t('certificate.createDialog.dnsFields.dnspod.secretIdPlaceholder'),
        help: t('certificate.createDialog.dnsFields.dnspod.secretIdHelp')
      },
      secretKey: {
        label: t('certificate.createDialog.dnsFields.dnspod.secretKey'),
        placeholder: t('certificate.createDialog.dnsFields.dnspod.secretKeyPlaceholder'),
        type: 'password',
        help: t('certificate.createDialog.dnsFields.dnspod.secretKeyHelp')
      }
    },
    aws: {
      accessKeyId: {
        label: t('certificate.createDialog.dnsFields.aws.accessKeyId'),
        placeholder: 'AWS Access Key ID',
        help: t('certificate.createDialog.dnsFields.aws.accessKeyIdHelp')
      },
      secretAccessKey: {
        label: t('certificate.createDialog.dnsFields.aws.secretAccessKey'),
        placeholder: 'AWS Secret Access Key',
        type: 'password',
        help: t('certificate.createDialog.dnsFields.aws.secretAccessKeyHelp')
      },
      region: {
        label: t('certificate.region'),
        placeholder: 'us-east-1',
        help: t('certificate.createDialog.dnsFields.aws.regionHelp')
      }
    },
    azure: {
      clientId: {
        label: t('certificate.createDialog.dnsFields.azure.clientId'),
        placeholder: 'Azure AD Client ID',
        help: t('certificate.createDialog.dnsFields.azure.clientIdHelp')
      },
      clientSecret: {
        label: t('certificate.createDialog.dnsFields.azure.clientSecret'),
        placeholder: 'Azure AD Client Secret',
        type: 'password',
        help: t('certificate.createDialog.dnsFields.azure.clientSecretHelp')
      },
      tenantId: {
        label: t('certificate.createDialog.dnsFields.azure.tenantId'),
        placeholder: 'Azure AD Tenant ID',
        help: t('certificate.createDialog.dnsFields.azure.tenantIdHelp')
      },
      subscriptionId: {
        label: t('certificate.createDialog.dnsFields.azure.subscriptionId'),
        placeholder: 'Azure Subscription ID',
        help: t('certificate.createDialog.dnsFields.azure.subscriptionIdHelp')
      },
      resourceGroup: {
        label: t('certificate.resourceGroup'),
        placeholder: t('certificate.createDialog.dnsFields.azure.resourceGroupPlaceholder'),
        help: t('certificate.createDialog.dnsFields.azure.resourceGroupHelp')
      }
    },
    godaddy: {
      apiKey: {
        label: t('certificate.createDialog.dnsFields.godaddy.apiKey'),
        placeholder: 'GoDaddy API Key',
        help: t('certificate.createDialog.dnsFields.godaddy.apiKeyHelp')
      },
      apiSecret: {
        label: t('certificate.createDialog.dnsFields.godaddy.apiSecret'),
        placeholder: 'GoDaddy API Secret',
        type: 'password',
        help: t('certificate.createDialog.dnsFields.godaddy.apiSecretHelp')
      }
    }
  }

  return fields[form.dnsProvider] || {}
})

// 表单验证规则
const rules: FormRules = {
  domains: [
    {
      validator: (_rule: any, value: string[], callback: Function) => {
        if (!value || !value.some(d => d.trim())) {
          callback(new Error(t('certificate.createDialog.validation.domainRequired')))
        } else {
          const domains = value.filter(d => d.trim())
          const invalidDomains = domains.filter(d => !isValidDomain(d.trim()))
          if (invalidDomains.length > 0) {
            callback(new Error(t('certificate.createDialog.validation.invalidDomain', { domains: invalidDomains.join(', ') })))
          } else {
            callback()
          }
        }
      },
      trigger: 'blur'
    }
  ],
  accountId: [
    { required: true, message: t('certificate.createDialog.validation.accountRequired'), trigger: 'change' }
  ],
  challengeType: [
    { required: true, message: t('certificate.createDialog.validation.challengeTypeRequired'), trigger: 'change' }
  ],
  dnsProvider: [
    {
      validator: (_rule: any, value: string, callback: Function) => {
        if (form.challengeType === 'dns-01' && !value) {
          callback(new Error(t('certificate.createDialog.validation.dnsProviderRequired')))
        } else {
          callback()
        }
      },
      trigger: 'change'
    }
  ],
  description: [
    { max: 200, message: t('certificate.createDialog.validation.descriptionMaxLength'), trigger: 'blur' }
  ]
}

// 验证域名格式
const isValidDomain = (domain: string) => {
  const domainRegex = /^(\*\.)?[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$/
  return domainRegex.test(domain)
}

// 添加域名
const addDomain = () => {
  form.domains.push('')
}

// 移除域名
const removeDomain = (index: number) => {
  form.domains.splice(index, 1)
}

// 处理验证方式变更
const handleChallengeTypeChange = (type: string) => {
  if (type !== 'dns-01') {
    form.dnsProvider = ''
    form.dnsConfig = {}
  }
}

// 监听DNS提供商变更，加载保存的配置
watch(() => form.dnsProvider, (newProvider) => {
  if (newProvider) {
    loadDnsKeyConfig(newProvider)
  } else {
    form.dnsConfig = {}
  }
})

// 处理提交
const handleSubmit = async () => {
  try {
    await formRef.value?.validate()

    loading.value = true

    // 获取选中的账户
    const selectedAccount = accounts.value.find(a => a.id === form.accountId)
    if (!selectedAccount) {
      ElMessage.error(t('certificate.createDialog.validation.selectValidAccount'))
      return
    }

    // 构建证书申请数据
    const validDomains = form.domains.filter(d => d.trim())

    // 确保 DNS 凭据被正确处理（按提供商标记）
    let dnsCredentials = undefined
    if (form.challengeType === 'dns-01' && form.dnsProvider && form.dnsConfig) {
      // 按提供商标记凭据
      dnsCredentials = {
        [form.dnsProvider]: {}
      }

      // 确保所有凭据字段都是字符串
      Object.keys(form.dnsConfig).forEach(key => {
        const value = form.dnsConfig[key]
        if (value !== null && value !== undefined) {
          dnsCredentials[form.dnsProvider][key] = String(value)
        }
      })
    }

    const certificateData = {
      domain: validDomains[0], // 主域名
      alternativeNames: validDomains.slice(1), // 别名
      challengeType: form.challengeType,
      acmeProvider: selectedAccount.provider,
      email: selectedAccount.email,
      accountId: selectedAccount.id,
      accountKey: selectedAccount.accountKey,
      dnsProvider: form.challengeType === 'dns-01' ? form.dnsProvider : undefined,
      dnsCredentials: dnsCredentials,
      autoRenew: form.autoRenew
    }

    await certificatesStore.orderCertificate(certificateData)

    ElMessage.success(t('certificate.createDialog.submitSuccess'))
    emit('success')
    handleClose()
  } catch (error: any) {
    console.error('证书申请失败:', error)
    if (error.message) {
      ElMessage.error(error.message)
    }
  } finally {
    loading.value = false
  }
}

// 加载保存的DNS密钥配置
const loadDnsKeyConfig = (provider: string) => {
  const storageKey = `dns_config_${provider}`
  const saved = localStorage.getItem(storageKey)

  if (saved) {
    try {
      const config = JSON.parse(saved)
      form.dnsConfig = { ...config }
    } catch (error) {
      console.warn('Failed to load saved DNS config:', error)
    }
  }
}

// 处理关闭
const handleClose = () => {
  dialogVisible.value = false
  resetFormState()
}

// 暴露方法给父组件
defineExpose({
  selectNewAccount,
  loadAccounts
})
</script>

<style scoped>
.domains-input {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.domain-item {
  display: flex;
  gap: 8px;
  align-items: center;
}

.domain-item .el-input {
  flex: 1;
}

.account-option {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}

.add-account-option-content {
  display: flex;
  align-items: center;
  gap: 6px;
  color: #409eff;
  font-weight: 500;
}

.form-help {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
  line-height: 1.4;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

:deep(.el-form-item) {
  margin-bottom: 18px;
}

:deep(.el-form-item__label) {
  font-weight: 500;
}

</style>

<!-- 非 scoped 样式，确保对话框样式正确应用 -->
<style>
/* 申请证书对话框关闭按钮样式 */
.cert-dialog .el-dialog__headerbtn {
  width: 28px;
  height: 28px;
  top: 14px;
  right: 16px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.4);
  transition: all 0.25s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

.cert-dialog .el-dialog__headerbtn:hover {
  background: linear-gradient(135deg, #5a67d8 0%, #6b46c1 100%);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.5);
  transform: scale(1.05);
}

.cert-dialog .el-dialog__headerbtn:active {
  transform: scale(0.95);
}

.cert-dialog .el-dialog__headerbtn .el-dialog__close {
  font-size: 14px;
  font-weight: bold;
  color: #ffffff;
}

.cert-dialog .el-dialog__headerbtn:hover .el-dialog__close {
  color: #ffffff;
}

/* 优化对话框标题样式 */
.cert-dialog .el-dialog__header {
  padding: 16px 20px;
  padding-right: 50px;
  border-bottom: 1px solid #ebeef5;
  margin-right: 0;
  background: linear-gradient(180deg, #fafbfc 0%, #ffffff 100%);
}

.cert-dialog .el-dialog__title {
  font-weight: 600;
  font-size: 18px;
  color: #303133;
}

.cert-dialog .el-dialog__body {
  padding: 20px;
  max-height: 60vh;
  overflow-y: auto;
}
</style>