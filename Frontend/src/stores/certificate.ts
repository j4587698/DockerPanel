import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import { ElMessage } from 'element-plus'
import { acmeApi } from '@/api/acme'
import { certificateApi } from '@/api/certificate'
import type {
  AcmeAccount,
  AcmeProvider,
  AcmeCertificateOrder,
  AcmeCertificateData,
  AcmeConnectionTestResult,
  AcmeOperationLog,
  WildcardCertificateDetails,
  WildcardCertificateSummary,
  AutoRenewalConfiguration,
  AcmeKeyInfo,
  AcmeKeyPair,
  DnsProviderInfo,
  CreateAcmeAccountRequest,
  AcmeCertificateRequest,
  CompleteChallengeRequest,
  RevokeCertificateRequest,
  WildcardCertificateRequest,
  WildcardAutoChallengeRequest,
  GenerateCsrRequest,
  ValidateCertificateRequest,
  ImportKeyRequest
} from '@/types/certificate'

// ACME证书管理状态接口
interface CertificateState {
  // 提供商管理
  providers: AcmeProvider[]
  selectedProvider: AcmeProvider | null
  providerTestResult: AcmeConnectionTestResult | null
  dnsProviders: DnsProviderInfo[]

  // 账户管理
  accounts: AcmeAccount[]
  selectedAccount: AcmeAccount | null
  accountKeyInfo: AcmeKeyInfo | null

  // 证书订单管理
  orders: AcmeCertificateOrder[]
  selectedOrder: AcmeCertificateOrder | null
  certificateData: AcmeCertificateData | null
  pendingChallenges: any[]

  // 通配符证书管理
  wildcardCertificates: WildcardCertificateSummary[]
  selectedWildcardCertificate: WildcardCertificateDetails | null

  // 自动续期配置
  autoRenewalConfigs: AutoRenewalConfiguration[]

  // 操作日志
  operationLogs: AcmeOperationLog[]
  selectedLog: AcmeOperationLog | null

  // 统计信息
  statistics: any

  // 密钥管理
  keyPairs: AcmeKeyPair[]

  // 加载状态
  loading: boolean
  error: string | null

  // 过滤器
  filter: {
    provider: string
    account: string
    status: string
    operation: string
    search: string
  }
}

// 创建ACME证书管理状态管理
export const useCertificateStore = defineStore('certificate', () => {
  // 状态
  const state = ref<CertificateState>({
    providers: [],
    selectedProvider: null,
    providerTestResult: null,
    dnsProviders: [],
    accounts: [],
    selectedAccount: null,
    accountKeyInfo: null,
    orders: [],
    selectedOrder: null,
    certificateData: null,
    pendingChallenges: [],
    wildcardCertificates: [],
    selectedWildcardCertificate: null,
    autoRenewalConfigs: [],
    operationLogs: [],
    selectedLog: null,
    statistics: null,
    keyPairs: [],
    loading: false,
    error: null,
    filter: {
      provider: '',
      account: '',
      status: '',
      operation: '',
      search: ''
    }
  })

  // 计算属性
  const loading = computed(() => state.value.loading)
  const error = computed(() => state.value.error)
  const providers = computed(() => state.value.providers)
  const selectedProvider = computed(() => state.value.selectedProvider)
  const accounts = computed(() => state.value.accounts)
  const selectedAccount = computed(() => state.value.selectedAccount)
  const orders = computed(() => state.value.orders)
  const selectedOrder = computed(() => state.value.selectedOrder)
  const certificateData = computed(() => state.value.certificateData)
  const wildcardCertificates = computed(() => state.value.wildcardCertificates)
  const selectedWildcardCertificate = computed(() => state.value.selectedWildcardCertificate)
  const operationLogs = computed(() => state.value.operationLogs)
  const statistics = computed(() => state.value.statistics)
  const dnsProviders = computed(() => state.value.dnsProviders)

  // 过滤后的账户列表
  const filteredAccounts = computed(() => {
    let filtered = state.value.accounts

    if (state.value.filter.provider) {
      filtered = filtered.filter(account => account.provider === state.value.filter.provider)
    }

    if (state.value.filter.search) {
      const searchTerm = state.value.filter.search.toLowerCase()
      filtered = filtered.filter(account =>
        account.email.toLowerCase().includes(searchTerm) ||
        account.id.toLowerCase().includes(searchTerm)
      )
    }

    return filtered
  })

  // 过滤后的订单列表
  const filteredOrders = computed(() => {
    let filtered = state.value.orders

    if (state.value.filter.account) {
      filtered = filtered.filter(order => order.accountId === state.value.filter.account)
    }

    if (state.value.filter.status) {
      filtered = filtered.filter(order => order.status === state.value.filter.status)
    }

    if (state.value.filter.search) {
      const searchTerm = state.value.filter.search.toLowerCase()
      filtered = filtered.filter(order =>
        order.domains.some(domain => domain.toLowerCase().includes(searchTerm)) ||
        order.id.toLowerCase().includes(searchTerm)
      )
    }

    return filtered
  })

  // 过滤后的通配符证书列表
  const filteredWildcardCertificates = computed(() => {
    let filtered = state.value.wildcardCertificates

    if (state.value.filter.status) {
      filtered = filtered.filter(cert => cert.status === state.value.filter.status)
    }

    if (state.value.filter.search) {
      const searchTerm = state.value.filter.search.toLowerCase()
      filtered = filtered.filter(cert =>
        cert.baseDomain.toLowerCase().includes(searchTerm) ||
        cert.wildcardDomain.toLowerCase().includes(searchTerm)
      )
    }

    return filtered
  })

  // ==================== 提供商管理方法 ====================

  // 获取ACME提供商列表
  const fetchProviders = async () => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.getProviders()
      // 处理API响应结构
      let providers = []
      if (Array.isArray(response)) {
        providers = response
      } else {
        console.warn('Certificate Store - 意外的响应结构:', response)
        providers = []
      }
      state.value.providers = providers
    } catch (error: any) {
      state.value.error = error.message || '获取ACME提供商列表失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 测试提供商连接
  const testProviderConnection = async (provider: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.testProviderConnection(provider)
      state.value.providerTestResult = response

      if (response.success) {
        ElMessage.success(`提供商 ${provider} 连接测试成功`)
      } else {
        ElMessage.error(`提供商 ${provider} 连接测试失败: ${response.message}`)
      }

      return response
    } catch (error: any) {
      state.value.error = error.message || '测试提供商连接失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取DNS提供商列表
  const fetchDnsProviders = async () => {
    try {
      const response = await acmeApi.getDnsProviders()
      state.value.dnsProviders = response
    } catch (error: any) {
      state.value.error = error.message || '获取DNS提供商列表失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // ==================== 账户管理方法 ====================

  // 获取ACME账户列表
  const fetchAccounts = async (params?: { provider?: string }) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.getAccounts(params)
      state.value.accounts = response
    } catch (error: any) {
      state.value.error = error.message || '获取ACME账户列表失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 创建ACME账户
  const createAccount = async (data: CreateAcmeAccountRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.createAccount(data)
      state.value.accounts.push(response)
      ElMessage.success('ACME账户创建成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '创建ACME账户失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 删除ACME账户
  const deleteAccount = async (accountId: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      await acmeApi.deleteAccount(accountId)
      state.value.accounts = state.value.accounts.filter(account => account.id !== accountId)
      if (state.value.selectedAccount?.id === accountId) {
        state.value.selectedAccount = null
      }
      ElMessage.success('ACME账户删除成功')
    } catch (error: any) {
      state.value.error = error.message || '删除ACME账户失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取账户详情
  const fetchAccountDetails = async (accountId: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.getAccount(accountId)
      state.value.selectedAccount = response
    } catch (error: any) {
      state.value.error = error.message || '获取账户详情失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取账户密钥信息
  const fetchAccountKeyInfo = async (accountId: string) => {
    try {
      const response = await acmeApi.getAccountKeyInfo(accountId)
      state.value.accountKeyInfo = response
    } catch (error: any) {
      state.value.error = error.message || '获取账户密钥信息失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // ==================== 证书管理方法 ====================

  // 获取证书列表
  const fetchCertificates = async (params?: { accountId?: string; status?: string; domain?: string; page?: number; pageSize?: number }) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.getCertificates(params)
      // 处理API响应结构
      let certificates = []
      if (response.items) {
        certificates = response.items
      } else if (Array.isArray(response)) {
        certificates = response
      } else {
        console.warn('Certificate Store - 意外的响应结构:', response)
        certificates = []
      }
      // 更新订单列表以保持兼容性
      state.value.orders = certificates
      state.value.certificates = certificates
      return response
    } catch (error: any) {
      state.value.error = error.message || '获取证书列表失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== 证书订单管理方法 ====================

  // 申请证书
  const orderCertificate = async (data: AcmeCertificateRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.orderCertificate(data)
      state.value.orders.push(response)
      ElMessage.success('证书申请成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '申请证书失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 完成挑战
  const completeChallenge = async (orderId: string, authorizationId: string, data: CompleteChallengeRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.completeChallenge(orderId, authorizationId, data)

      // 更新订单状态
      const orderIndex = state.value.orders.findIndex(order => order.id === orderId)
      if (orderIndex !== -1) {
        // 这里需要根据实际的响应结构来更新订单
        // 可能需要重新获取订单详情
        await fetchOrderDetails(orderId)
      }

      ElMessage.success('挑战完成成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '完成挑战失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 下载证书（ZIP包）
  const downloadCertificate = async (certificateId: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      // 注意：api 拦截器返回的是 response.data，所以这里 response 就是 blob
      const blobData = await acmeApi.downloadCertificate(certificateId)

      // 创建下载链接
      const blob = new Blob([blobData], { type: 'application/zip' })
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url

      // 使用默认文件名
      const fileName = `certificate_${certificateId}.zip`
      link.setAttribute('download', fileName)

      document.body.appendChild(link)
      link.click()
      
      // 延迟清理，确保下载开始
      setTimeout(() => {
        document.body.removeChild(link)
        window.URL.revokeObjectURL(url)
      }, 100)

      ElMessage.success('证书下载成功')
    } catch (error: any) {
      state.value.error = error.message || '下载证书失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取待处理的挑战
  const fetchPendingChallenges = async (orderId: string) => {
    try {
      const response = await acmeApi.getPendingChallenges(orderId)
      state.value.pendingChallenges = response
    } catch (error: any) {
      state.value.error = error.message || '获取待处理挑战失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // 续期证书
  const renewCertificate = async (certificateId: string, data?: AcmeCertificateRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.renewCertificate(certificateId, data)
      state.value.orders.push(response)
      ElMessage.success('证书续期成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '续期证书失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 取消证书申请
  const cancelCertificateOrder = async (orderId: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      await acmeApi.cancelCertificateOrder(orderId)
      await fetchCertificates() // 刷新证书列表
      ElMessage.success('证书申请已取消')
    } catch (error: any) {
      state.value.error = error.message || '取消申请失败'
      ElMessage.error(error.message || '取消申请失败')
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 撤销证书
  const revokeCertificate = async (certificateId: string, data: RevokeCertificateRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      await acmeApi.revokeCertificate(certificateId, data)
      ElMessage.success('证书撤销成功')
    } catch (error: any) {
      state.value.error = error.message || '撤销证书失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 删除证书
  const deleteCertificate = async (certificateId: string, force = false) => {
    state.value.loading = true
    state.value.error = null

    try {
      const result = await certificateApi.deleteCertificate(certificateId, force)

      // 检查删除结果
      if (result && typeof result === 'object' && 'success' in result) {
        if (!result.success) {
          // 处理业务逻辑错误，如证书不存在
          ElMessage.warning(result.message || '证书删除失败')
          return
        }
      }

      // 从本地状态中移除证书
      const index = state.value.orders.findIndex(order => order.id === certificateId)
      if (index > -1) {
        state.value.orders.splice(index, 1)
      }

      const message = force ? '证书强制删除成功' : '证书删除成功'
      ElMessage.success(message)
    } catch (error: any) {
      state.value.error = error.message || '证书删除失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 重试证书
  const retryCertificate = async (certificateId: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const result = await certificateApi.retryCertificate(certificateId)

      if (result) {
        ElMessage.success('证书重试已启动，正在重新申请...')
        // 刷新证书列表以获取最新状态
        await fetchCertificates()
      }

      return result
    } catch (error: any) {
      state.value.error = error.message || '证书重试失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 自动续期证书
  const autoRenewCertificates = async () => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.autoRenewCertificates()
      ElMessage.success(`成功处理 ${response.length} 个续期操作`)
      return response
    } catch (error: any) {
      state.value.error = error.message || '自动续期证书失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 启用/禁用自动续期
  const toggleAutoRenewal = async (certificateId: string, enabled: boolean) => {
    state.value.loading = true
    state.value.error = null

    try {
      await acmeApi.toggleAutoRenewal(certificateId, enabled)
      // 更新本地状态
      const index = state.value.orders.findIndex(o => o.id === certificateId)
      if (index !== -1) {
        state.value.orders[index].autoRenew = enabled
      }
      return true
    } catch (error: any) {
      state.value.error = error.message || '更新自动续期设置失败'
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== 通配符证书管理方法 ====================

  // 申请通配符证书
  const orderWildcardCertificate = async (data: WildcardCertificateRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.orderWildcardCertificate(data)
      ElMessage.success('通配符证书申请成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '申请通配符证书失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取通配符证书列表
  const fetchWildcardCertificates = async (params?: { baseDomain?: string; status?: string }) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.getWildcardCertificates(params)
      // 处理API响应结构
      let certificates = []
      if (Array.isArray(response)) {
        certificates = response
      } else {
        console.warn('Certificate Store - 意外的响应结构:', response)
        certificates = []
      }
      state.value.wildcardCertificates = certificates
    } catch (error: any) {
      state.value.error = error.message || '获取通配符证书列表失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取通配符证书详情
  const fetchWildcardCertificateDetails = async (certificateId: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.getWildcardCertificate(certificateId)
      state.value.selectedWildcardCertificate = response
    } catch (error: any) {
      state.value.error = error.message || '获取通配符证书详情失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 自动完成通配符证书挑战
  const autoCompleteWildcardChallenge = async (data: WildcardAutoChallengeRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.autoCompleteWildcardChallenge(data)
      ElMessage.success('通配符证书挑战自动完成成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '自动完成通配符证书挑战失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== CSR和密钥管理方法 ====================

  // 生成CSR
  const generateCsr = async (data: GenerateCsrRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.generateCsr(data)
      ElMessage.success('CSR生成成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '生成CSR失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 验证证书
  const validateCertificate = async (data: ValidateCertificateRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.validateCertificate(data)
      ElMessage.success('证书验证成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '验证证书失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 生成密钥对
  const generateKeyPair = async (keyType: string) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.generateKeyPair(keyType)
      state.value.keyPairs.push(response)
      ElMessage.success('密钥对生成成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '生成密钥对失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 导入密钥
  const importKey = async (data: ImportKeyRequest) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.importKey(data)
      state.value.keyPairs.push(response)
      ElMessage.success('密钥导入成功')
      return response
    } catch (error: any) {
      state.value.error = error.message || '导入密钥失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // ==================== 操作日志管理方法 ====================

  // 获取操作日志
  const fetchOperationLogs = async (params?: any) => {
    state.value.loading = true
    state.value.error = null

    try {
      const response = await acmeApi.getOperationLogs(params)
      state.value.operationLogs = response
    } catch (error: any) {
      state.value.error = error.message || '获取操作日志失败'
      ElMessage.error(state.value.error)
      throw error
    } finally {
      state.value.loading = false
    }
  }

  // 获取统计信息
  const fetchStatistics = async (params?: { provider?: string }) => {
    try {
      const response = await acmeApi.getStatistics(params)
      // API interceptor already returns data, so response IS the data
      state.value.statistics = response
    } catch (error: any) {
      state.value.error = error.message || '获取统计信息失败'
      ElMessage.error(state.value.error)
      throw error
    }
  }

  // ==================== 辅助方法 ====================

  // 获取订单详情（内部方法）
  const fetchOrderDetails = async (orderId: string) => {
    const response = await acmeApi.getCertificateOrder(orderId)
    const order = ((response as any).data ?? response) as AcmeCertificateOrder
    state.value.selectedOrder = order

    const index = state.value.orders.findIndex(item => item.id === orderId)
    if (index >= 0) {
      state.value.orders.splice(index, 1, order)
    } else {
      state.value.orders.push(order)
    }
  }

  // 选择提供商
  const selectProvider = (provider: AcmeProvider) => {
    state.value.selectedProvider = provider
  }

  // 选择账户
  const selectAccount = (account: AcmeAccount) => {
    fetchAccountDetails(account.id)
  }

  // 选择订单
  const selectOrder = (order: AcmeCertificateOrder) => {
    state.value.selectedOrder = order
    fetchPendingChallenges(order.id)
  }

  // 选择通配符证书
  const selectWildcardCertificate = (certificate: WildcardCertificateSummary) => {
    fetchWildcardCertificateDetails(certificate.certificateId)
  }

  // 更新过滤器
  const updateFilter = (filter: Partial<CertificateState['filter']>) => {
    state.value.filter = { ...state.value.filter, ...filter }
  }

  // 清除选择
  const clearSelections = () => {
    state.value.selectedProvider = null
    state.value.selectedAccount = null
    state.value.selectedOrder = null
    state.value.selectedWildcardCertificate = null
    state.value.certificateData = null
    state.value.providerTestResult = null
  }

  // 清除错误
  const clearError = () => {
    state.value.error = null
  }

  // 重置过滤器
  const resetFilter = () => {
    state.value.filter = {
      provider: '',
      account: '',
      status: '',
      operation: '',
      search: ''
    }
  }

  return {
    // 状态
    state,
    loading,
    error,

    // 计算属性
    providers,
    selectedProvider,
    accounts,
    selectedAccount,
    orders,
    selectedOrder,
    certificateData,
    wildcardCertificates,
    selectedWildcardCertificate,
    operationLogs,
    statistics,
    dnsProviders,
    filteredAccounts,
    filteredOrders,
    filteredWildcardCertificates,

    // 提供商管理方法
    fetchProviders,
    testProviderConnection,
    fetchDnsProviders,

    // 账户管理方法
    fetchAccounts,
    createAccount,
    deleteAccount,
    fetchAccountDetails,
    fetchAccountKeyInfo,

    // 证书管理方法
    fetchCertificates,

    // 证书订单管理方法
    orderCertificate,
    completeChallenge,
    downloadCertificate,
    fetchPendingChallenges,
    renewCertificate,
    cancelCertificateOrder,
    revokeCertificate,
    deleteCertificate,
    retryCertificate,
    autoRenewCertificates,
    toggleAutoRenewal,

    // 通配符证书管理方法
    orderWildcardCertificate,
    fetchWildcardCertificates,
    fetchWildcardCertificateDetails,
    autoCompleteWildcardChallenge,

    // CSR和密钥管理方法
    generateCsr,
    validateCertificate,
    generateKeyPair,
    importKey,

    // 操作日志管理方法
    fetchOperationLogs,
    fetchStatistics,

    // 辅助方法
    selectProvider,
    selectAccount,
    selectOrder,
    selectWildcardCertificate,
    updateFilter,
    clearSelections,
    clearError,
    resetFilter
  }
})

export default useCertificateStore