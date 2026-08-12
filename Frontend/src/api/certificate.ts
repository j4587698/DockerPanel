import api from "./index"

// ==================== ACME证书相关类型定义 ====================

// 证书基础信息
export interface Certificate {
  id: string
  domain: string
  alternativeNames: string[]
  issuer: string
  status: "pending" | "valid" | "invalid" | "revoked" | "expired"
  keyAlgorithm: string
  signatureAlgorithm: string
  createdAt: string
  expiresAt: string
  renewedAt?: string
  autoRenew: boolean
  acmeProvider: string
  challengeType: "http-01" | "dns-01" | "tls-alpn-01"
  nodeId?: string
  certificateContent?: string
  privateKeyContent?: string
  chainContent?: string
  // 兼容certificates.ts的字段
  name?: string
  domains?: string[]
  subject?: string
  serialNumber?: string
  fingerprint?: string
  validFrom?: string
  validTo?: string
  issuedAt?: string
  daysUntilExpiry?: number
  isAutoRenewal?: boolean
  dnsProvider?: string
  dnsConfig?: Record<string, any>
  challenges?: Record<string, any>
  webRoot?: string
  provider?: string
  email?: string
  description?: string
  logs?: any[]
  error?: string
  updatedAt?: string
  lastRenewedAt?: string
}

export interface CertificateRequest {
  domain: string
  alternativeNames?: string[]
  challengeType: "http-01" | "dns-01" | "tls-alpn-01"
  acmeProvider: string
  email?: string
  keySize?: number
  keyAlgorithm?: "RSA" | "ECDSA"
  autoRenew?: boolean
  nodeId?: string
  dnsProvider?: string
  dnsCredentials?: Record<string, string>
}

export interface CertificateRenewalRequest {
  certificateId: string
  forceRenew?: boolean
}

export interface CertificateValidationResult {
  isValid: boolean
  errors: string[]
  warnings: string[]
  expiresAt?: string
  daysUntilExpiry?: number
}

export interface ChallengeResponse {
  challengeId: string
  type: string
  status: "pending" | "processing" | "valid" | "invalid"
  token?: string
  keyAuthorization?: string
  recordName?: string
  recordValue?: string
  recordType?: string
  validationUrl?: string
}

export interface AcmeAccount {
  id: string
  email: string
  provider: string
  status: "valid" | "deactivated" | "revoked"
  createdAt: string
  keyType: string
  kid?: string
}

export interface AcmeAccountRequest {
  email: string
  provider: string
  keyType?: "RSA" | "ECDSA"
  keySize?: number
  eabKid?: string
  eabHmacKey?: string
}

export interface AcmeProvider {
  id: string
  name: string
  directoryUrl: string
  isProduction: boolean
  supportsEab: boolean
  supportsWildcard: boolean
  documentationUrl?: string
  tosUrl?: string
}

export interface DnsProviderConfig {
  id: string
  name: string
  type: "cloudflare" | "route53" | "azure" | "google" | "digitalocean" | "custom"
  credentials: Record<string, string>
  isDefault: boolean
  isActive: boolean
  createdAt: string
}

export interface CertificateStatistics {
  totalCertificates: number
  validCertificates: number
  expiredCertificates: number
  expiringSoonCertificates: number
  pendingCertificates: number
  invalidCertificates: number
  revokedCertificates: number
  autoRenewEnabled: number
  certificatesByProvider: Record<string, number>
  certificatesByNode: Record<string, number>
  averageExpiryDays: number
  lastUpdated: string
}

export interface CertificateExportRequest {
  certificateIds: string[]
  format: "pem" | "pfx" | "jks"
  includePrivateKey: boolean
  includeChain: boolean
  password?: string
}

export interface CertificateImportRequest {
  certificateContent: string
  privateKeyContent?: string
  chainContent?: string
  domain: string
  alternativeNames?: string[]
  nodeId?: string
}

// ==================== 证书申请进度跟踪类型定义 ====================

// 证书申请步骤枚举
export const CertificateApplicationStep = {
  NotStarted: 0,
  InitializingAcmeClient: 1,
  CreatingAccount: 2,
  CreatingOrder: 3,
  GettingAuthorizations: 4,
  ConfiguringDnsChallenge: 5,
  WaitingForDnsPropagation: 6,
  ValidatingDomains: 7,
  CleaningDnsRecords: 8,
  DownloadingCertificate: 9,
  SavingCertificate: 10,
  Completed: 11
} as const

// 证书申请状态枚举
export const CertificateApplicationStatus = {
  Pending: 0,
  InProgress: 1,
  Completed: 2,
  Failed: 3,
  Cancelled: 4
} as const

// 类型定义
export type CertificateApplicationStep = typeof CertificateApplicationStep[keyof typeof CertificateApplicationStep]
export type CertificateApplicationStatus = typeof CertificateApplicationStatus[keyof typeof CertificateApplicationStatus]

// 证书申请步骤详情
export interface CertificateApplicationStepDetail {
  step: CertificateApplicationStep
  description: string
  message: string
  startedAt: string
  completedAt?: string
  isCompleted: boolean
  isSuccess: boolean
  errors: string[]
  warnings: string[]
  metadata: Record<string, any>
  durationSeconds?: number
}

// 进度跟踪响应
export interface ProgressTrackResponse {
  progressId: string
  certificateId: string
  applicationName: string
  currentStep: CertificateApplicationStep
  currentStepDescription: string
  progressPercentage: number
  status: CertificateApplicationStatus
  startedAt: string
  lastUpdatedAt: string
  completedAt?: string
  estimatedRemainingSeconds?: number
  steps: CertificateApplicationStepDetail[]
  errors: string[]
  warnings: string[]
  metadata: Record<string, any>
  isCompleted: boolean
  isSuccess: boolean
}

// 进度跟踪请求
export interface ProgressTrackRequest {
  certificateId: string
  applicationName: string
  domains: string[]
  provider: string
  challengeType: string
  metadata: Record<string, any>
}

// 进度更新通知
export interface ProgressUpdateNotification {
  progressId: string
  certificateId: string
  currentStep: CertificateApplicationStep
  currentStepDescription: string
  progressPercentage: number
  status: CertificateApplicationStatus
  message: string
  timestamp: string
  errors: string[]
  warnings: string[]
  estimatedRemainingSeconds?: number
}

// ==================== ACME证书管理API服务 ====================

export const certificateApi = {
  // 证书基础操作

  // 获取证书列表（优先ACME API，回退到证书管理API）
  getCertificates: (params?: {
    nodeId?: string
    status?: string
    domain?: string
    page?: number
    pageSize?: number
  }) => {
    // 优先使用ACME API（注意路径为小写 /acme，大写 /Acme 会 404）
    return api.get<{
      items: Certificate[]
      total: number
      page: number
      pageSize: number
    }>("/acme/certificates", { params })
      .catch(() => {
        // 回退到证书管理API（/certificatemanagement，参数名与返回结构与ACME不同，做映射）
        return api.get<{
          certificates: Certificate[]
          totalCount: number
          pageIndex: number
          pageSize: number
        }>("/certificatemanagement", {
          params: {
            statusFilter: params?.status,
            domainFilter: params?.domain,
            pageIndex: params?.page,
            pageSize: params?.pageSize,
          }
        })
          .then(response => ({
            items: response.certificates,
            total: response.totalCount,
            page: response.pageIndex,
            pageSize: response.pageSize
          }))
      })
  },

  // 根据ID获取证书详情（ACME订单详情；回退到证书管理API）
  getCertificate: (id: string) =>
    api.get<Certificate>(`/acme/certificates/orders/${id}`)
      .catch(() => api.get<Certificate>(`/certificatemanagement/${id}`)),

  // 申请证书（ACME下单）
  requestCertificate: (data: CertificateRequest) =>
    api.post<Certificate>("/acme/certificates/order", data),

  // 更新证书
  updateCertificate: (id: string, data: Partial<CertificateRequest>) =>
    api.put<Certificate>(`/acme/certificates/${id}`, data)
      .catch(() => api.put<Certificate>(`/certificates/${id}`, data)),

  // 删除证书
  deleteCertificate: (id: string, force = false) =>
    api.delete(`/acme/certificates/${id}`, { params: { force } })
      .catch((error) => {
        // 对于404错误，返回响应数据让业务层处理
        if (error.response?.status === 404) {
          return error.response.data
        }
        // 注意：error.response.data 是真实 AxiosError 的响应体，不是拦截器解包后的数据，此处保持不动
        // 其他错误继续抛出
        throw error
      }),

  // 续期证书（ACME；回退到证书管理API）
  renewCertificate: (data: CertificateRenewalRequest) =>
    api.post<Certificate>(`/acme/certificates/${data.certificateId}/renew`)
      .catch(() => api.post<Certificate>(`/certificatemanagement/${data.certificateId}/renew`)),

  // 重试证书
  retryCertificate: (id: string) =>
    api.post<Certificate>(`/acme/certificates/${id}/retry`),

  // 验证证书（证书管理API）
  validateCertificate: (id: string) =>
    api.post<CertificateValidationResult>(`/certificatemanagement/${id}/validate`),

  // 导出证书（ACME下载；回退到证书管理API）
  exportCertificate: (data: CertificateExportRequest) =>
    api.get(`/acme/certificates/${data.certificateIds[0]}/download`, {
      params: { format: data.format || "pem" },
      responseType: "blob"
    }).catch(() => {
      // 回退到证书管理API
      const certificateId = data.certificateIds[0]
      const format = data.format || "pem"
      return api.get(`/certificatemanagement/${certificateId}/download`, {
        params: { format },
        responseType: "blob"
      })
    }),

  // 导入证书（证书管理API）
  importCertificate: (data: CertificateImportRequest) =>
    api.post<Certificate>("/certificatemanagement/import", data),

  // 批量操作

  // 批量续期证书（ACME；回退到证书管理API）
  batchRenewCertificates: (certificateIds: string[], force = false) =>
    api.post("/acme/certificates/renew-batch", {
      certificateIds,
      daysBeforeExpiry: force ? 0 : 30
    }).catch(() => {
      // 回退到证书管理API
      return api.post("/certificatemanagement/batch", {
        operation: "renew",
        certificateIds,
        operationParameters: { force }
      })
    }),

  // 批量删除证书（证书管理API）
  batchDeleteCertificates: (certificateIds: string[], force = false) =>
    api.post("/certificatemanagement/batch", {
      operation: "delete",
      certificateIds,
      operationParameters: { force }
    }),

  // 批量更新自动续期设置（证书管理API）
  batchUpdateAutoRenew: (certificateIds: string[], autoRenew: boolean) =>
    api.post("/certificatemanagement/batch", {
      operation: autoRenew ? "enable-auto-renewal" : "disable-auto-renewal",
      certificateIds,
      operationParameters: {}
    }),

  // 吊销证书（ACME）
  revokeCertificate: (id: string, request?: { reason?: string }) =>
    api.post(`/acme/certificates/${id}/revoke`, request),

  // 设置自动续签（ACME）
  setAutoRenew: (id: string, autoRenew: boolean) =>
    api.post(`/acme/certificates/${id}/auto-renew/${autoRenew ? "enable" : "disable"}`),

  // 挑战验证相关

  // 获取挑战状态
  getChallengeStatus: (certificateId: string) =>
    api.get<ChallengeResponse[]>(`/acme/certificates/${certificateId}/challenges`),

  // 触发挑战验证
  triggerChallenge: (certificateId: string, challengeId: string) =>
    api.post<ChallengeResponse>(`/acme/certificates/${certificateId}/challenges/${challengeId}/trigger`),

  // 获取挑战配置信息
  getChallengeConfig: (certificateId: string) =>
    api.get<{
      recordName?: string
      recordValue?: string
      recordType?: string
      filePath?: string
      fileContent?: string
    }>(`/acme/certificates/${certificateId}/challenge-config`),

  // ACME账户管理

  // 获取ACME账户列表
  getAcmeAccounts: () =>
    api.get<AcmeAccount[]>("/acme/accounts"),

  // 创建ACME账户
  createAcmeAccount: (data: AcmeAccountRequest) =>
    api.post<AcmeAccount>("/acme/accounts", data),

  // 更新ACME账户
  updateAcmeAccount: (id: string, data: Partial<AcmeAccountRequest>) =>
    api.put<AcmeAccount>(`/acme/accounts/${id}`, data),

  // 删除ACME账户
  deleteAcmeAccount: (id: string) =>
    api.delete(`/acme/accounts/${id}`),

  // 激活ACME账户
  activateAcmeAccount: (id: string) =>
    api.post<AcmeAccount>(`/acme/accounts/${id}/activate`),

  // 停用ACME账户
  deactivateAcmeAccount: (id: string) =>
    api.post<AcmeAccount>(`/acme/accounts/${id}/deactivate`),

  // DNS提供商管理

  // 获取DNS提供商列表
  getDnsProviders: () =>
    api.get<DnsProviderConfig[]>("/acme/dns-providers"),

  // 创建DNS提供商配置
  createDnsProvider: (data: Omit<DnsProviderConfig, "id" | "createdAt">) =>
    api.post<DnsProviderConfig>("/acme/dns-providers", data),

  // 更新DNS提供商配置
  updateDnsProvider: (id: string, data: Partial<DnsProviderConfig>) =>
    api.put<DnsProviderConfig>(`/acme/dns-providers/${id}`, data),

  // 删除DNS提供商配置
  deleteDnsProvider: (id: string) =>
    api.delete(`/acme/dns-providers/${id}`),

  // 测试DNS提供商连接
  testDnsProvider: (id: string) =>
    api.post<{
      success: boolean
      message: string
      details?: any
    }>(`/acme/dns-providers/${id}/test`),

  // 设置默认DNS提供商
  setDefaultDnsProvider: (id: string) =>
    api.post<DnsProviderConfig>(`/acme/dns-providers/${id}/set-default`),

  // ACME提供商管理

  // 获取ACME提供商列表
  getAcmeProviders: () =>
    api.get<AcmeProvider[]>("/acme/providers"),

  // 获取ACME提供商详情
  getAcmeProvider: (id: string) =>
    api.get<AcmeProvider>(`/acme/providers/${id}`),

  // 添加自定义ACME提供商
  addCustomAcmeProvider: (data: Omit<AcmeProvider, "id">) =>
    api.post<AcmeProvider>("/acme/providers", data),

  // 更新ACME提供商
  updateAcmeProvider: (id: string, data: Partial<AcmeProvider>) =>
    api.put<AcmeProvider>(`/acme/providers/${id}`, data),

  // 删除自定义ACME提供商
  deleteAcmeProvider: (id: string) =>
    api.delete(`/acme/providers/${id}`),

  // 统计和监控

  // 获取证书统计信息（ACME；回退到证书管理API）
  getCertificateStatistics: (nodeId?: string) =>
    api.get<CertificateStatistics>("/acme/statistics", {
      params: { nodeId }
    }).catch(() => api.get<CertificateStatistics>("/certificatemanagement/statistics")),

  // 获取即将过期的证书（证书管理API）
  getExpiringCertificates: (days = 15, nodeId?: string) =>
    api.get<Certificate[]>("/certificatemanagement/expiring", {
      params: { daysBeforeExpiry: days }
    }),

  // 获取证书操作历史（证书管理API，返回数组后包装成分页结构）
  getRenewalHistory: (certificateId?: string, page = 1, pageSize = 20) =>
    api.get<any[]>(`/certificatemanagement/${certificateId}/history`, {
      params: { limit: pageSize, offset: (page - 1) * pageSize }
    }).then(items => ({
      items,
      total: items.length,
      page,
      pageSize
    })),

  // 自动续期管理

  // 启用自动续期
  enableAutoRenew: (certificateId: string) =>
    api.post(`/acme/certificates/${certificateId}/auto-renew/enable`),

  // 禁用自动续期
  disableAutoRenew: (certificateId: string) =>
    api.post(`/acme/certificates/${certificateId}/auto-renew/disable`),

  // 获取自动续期配置
  getAutoRenewConfig: (certificateId: string) =>
    api.get<{
      enabled: boolean
      renewBeforeDays: number
      retryAttempts: number
      retryInterval: number
      lastRenewalAt?: string
      nextRenewalAt?: string
    }>(`/acme/certificates/${certificateId}/auto-renew/config`),

  // 更新自动续期配置
  updateAutoRenewConfig: (certificateId: string, config: {
    renewBeforeDays?: number
    retryAttempts?: number
    retryInterval?: number
  }) =>
    api.put(`/acme/certificates/${certificateId}/auto-renew/config`, config),

  // 手动触发自动续期检查（ACME）
  triggerAutoRenewCheck: () =>
    api.post("/acme/certificates/auto-renew"),

  // 获取自动续期状态
  getAutoRenewStatus: () =>
    api.get<{
      isRunning: boolean
      lastCheckAt?: string
      nextCheckAt?: string
      pendingRenewals: number
      failedRenewals: number
      successfulRenewals: number
    }>("/acme/auto-renew/status")
}

// ==================== 证书申请进度跟踪API服务 ====================

export const certificateProgressApi = {
  // 创建进度跟踪
  createProgress: (data: ProgressTrackRequest) =>
    api.post<{ progressId: string }>("/certificateprogress/create", data),

  // 获取进度信息
  getProgress: (progressId: string) =>
    api.get<ProgressTrackResponse>(`/certificateprogress/${progressId}`),

  // 根据证书ID获取进度信息
  getProgressByCertificateId: (certificateId: string) =>
    api.get<ProgressTrackResponse>(`/certificateprogress/by-certificate/${certificateId}`),

  // 获取所有进度列表
  getAllProgress: () =>
    api.get<ProgressTrackResponse[]>("/certificateprogress"),

  // 更新进度步骤
  updateProgressStep: (progressId: string, data: {
    step: CertificateApplicationStep
    message: string
    isCompleted?: boolean
  }) =>
    api.put(`/certificateprogress/${progressId}/step`, data),

  // 完成当前步骤
  completeCurrentStep: (progressId: string, data?: { message?: string }) =>
    api.put(`/certificateprogress/${progressId}/complete-current`, data || {}),

  // 添加错误信息
  addError: (progressId: string, data: { error: string }) =>
    api.post(`/certificateprogress/${progressId}/error`, data),

  // 添加警告信息
  addWarning: (progressId: string, data: { warning: string }) =>
    api.post(`/certificateprogress/${progressId}/warning`, data),

  // 标记进度完成
  markAsCompleted: (progressId: string) =>
    api.put(`/certificateprogress/${progressId}/complete`),

  // 标记进度失败
  markAsFailed: (progressId: string, data: { errorMessage: string }) =>
    api.put(`/certificateprogress/${progressId}/fail`, data),

  // 删除进度记录
  deleteProgress: (progressId: string) =>
    api.delete(`/certificateprogress/${progressId}`),

  // 清理过期的进度记录
  cleanupExpiredProgress: () =>
    api.post("/certificateprogress/cleanup")
}

export default certificateApi