/**
 * ACME证书管理API
 * 对应后端DockerPanel.API.Controllers.AcmeController
 */

import api from "./index"
import type {
  AcmeAccount,
  AcmeProvider,
  AcmeCertificateRequest,
  AcmeCertificateOrder,
  AcmeChallenge,
  AcmeChallengeResult,
  AcmeCertificateData,
  AcmeConnectionTestResult,
  AcmeRenewalConfiguration,
  CreateAcmeAccountRequest,
  CompleteChallengeRequest,
  RevokeCertificateRequest,
  AutoRenewalConfiguration,
  AcmeOperationLog,
  AcmeCertificateValidationResult,
  AcmeKeyInfo,
  AcmeKeyPair,
  DnsChallengeConfigurationResult,
  DnsChallengeCleanupResult,
  DnsProviderInfo,
  WildcardCertificateDetails,
  WildcardCertificateSummary,
  WildcardCertificateBatchRequest,
  WildcardCertificateBatchResult,
  WildcardCertificateStatus,
  WildcardAutoChallengeRequest,
  WildcardAutoChallengeResult,
  WildcardCertificateRequest,
  WildcardCertificateResult,
  WildcardImportResult,
  GenerateCsrRequest,
  ValidateCertificateRequest,
  ImportKeyRequest
} from "@/types/certificate"

// ==================== ACME API对象 ====================

// ACME证书管理API - 对齐后端AcmeController
export const acmeApi = {
  // ==================== ACME提供商管理 ====================

  // 获取ACME提供商列表
  getProviders: () => api.get<AcmeProvider[]>("/acme/providers"),

  // 测试ACME提供商连接
  testProviderConnection: (provider: string) =>
    api.post<AcmeConnectionTestResult>(`/acme/providers/${provider}/test`),

  // ==================== ACME账户管理 ====================

  // 获取ACME账户列表
  getAccounts: (params?: { provider?: string }) =>
    api.get<AcmeAccount[]>("/acme/accounts", { params }),

  // 创建ACME账户
  createAccount: (data: CreateAcmeAccountRequest) =>
    api.post<AcmeAccount>("/acme/accounts", data),

  // 删除ACME账户
  deleteAccount: (accountId: string) =>
    api.delete(`/acme/accounts/${accountId}`),

  // 获取ACME账户详情
  getAccount: (accountId: string) =>
    api.get<AcmeAccount>(`/acme/accounts/${accountId}`),

  // ==================== 证书管理 ====================

  // 获取证书列表
  getCertificates: (params?: { accountId?: string; status?: string; domain?: string; page?: number; pageSize?: number }) =>
    api.get<any>("/acme/certificates", { params }),

  // ==================== 证书订单管理 ====================

  // 申请证书
  orderCertificate: (data: AcmeCertificateRequest) =>
    api.post<AcmeCertificateOrder>("/acme/certificates/order", data),

  // 获取证书订单详情
  getCertificateOrder: (orderId: string) =>
    api.get<AcmeCertificateOrder>(`/acme/certificates/orders/${orderId}`),

  // 完成挑战
  completeChallenge: (orderId: string, authorizationId: string, data: CompleteChallengeRequest) =>
    api.post<AcmeChallengeResult>(`/acme/certificates/orders/${orderId}/challenges/${authorizationId}/complete`, data),

  // 下载证书（返回ZIP包）
  downloadCertificate: (certificateId: string) =>
    api.get(`/acme/certificates/${certificateId}/download`, { responseType: 'blob' }),

  // 取消证书申请
  cancelCertificateOrder: (orderId: string) =>
    api.post(`/acme/certificates/orders/${orderId}/cancel`),

  // 获取待处理的挑战
  getPendingChallenges: (orderId: string) =>
    api.get<AcmeChallenge[]>(`/acme/certificates/orders/${orderId}/challenges/pending`),

  // 续期证书
  renewCertificate: (certificateId: string, data?: AcmeCertificateRequest) =>
    api.post<AcmeCertificateOrder>(`/acme/certificates/${certificateId}/renew`, data),

  // 撤销证书
  revokeCertificate: (certificateId: string, data: RevokeCertificateRequest) =>
    api.post(`/acme/certificates/${certificateId}/revoke`, data),

  // 自动续期证书
  autoRenewCertificates: () =>
    api.post<AcmeOperationLog[]>("/acme/certificates/auto-renew"),

  // 验证域名所有权
  verifyDomainOwnership: (domain: string, challengeType = "http-01") =>
    api.post<AcmeChallengeResult>("/acme/domains/verify", null, { params: { domain, challengeType } }),

  // ==================== CSR和密钥管理 ====================

  // 生成CSR
  generateCsr: (data: GenerateCsrRequest) =>
    api.post<{ csr: string }>("/acme/csr/generate", data),

  // 验证证书
  validateCertificate: (data: ValidateCertificateRequest) =>
    api.post<AcmeCertificateValidationResult>("/acme/certificates/validate", data),

  // 获取账户密钥信息
  getAccountKeyInfo: (accountId: string) =>
    api.get<AcmeKeyInfo>(`/acme/accounts/${accountId}/key`),

  // 生成密钥对
  generateKeyPair: (keyType: string) =>
    api.post<AcmeKeyPair>("/acme/keys/generate", null, { params: { keyType } }),

  // 导出账户密钥
  exportAccountKey: (accountId: string, format?: string) =>
    api.get<{ keyData: string; format: string }>(`/acme/accounts/${accountId}/key/export`, {
      params: { format }
    }),

  // 导入密钥
  importKey: (data: ImportKeyRequest) =>
    api.post<AcmeKeyInfo>("/acme/keys/import", data),

  // ==================== DNS挑战管理 ====================

  // 配置DNS挑战
  configureDnsChallenge: (domain: string, provider: string, config: Record<string, string>) =>
    api.post<DnsChallengeConfigurationResult>("/acme/dns-challenge/configure", {
      domain,
      provider,
      config
    }),

  // 清理DNS挑战
  cleanupDnsChallenge: (domain: string, provider: string) =>
    api.post<DnsChallengeCleanupResult>("/acme/dns-challenge/cleanup", {
      domain,
      provider
    }),

  // 获取DNS提供商信息
  getDnsProviderInfo: (provider: string) =>
    api.get<DnsProviderInfo>(`/acme/dns-providers/${provider}`),

  // 获取所有DNS提供商
  getDnsProviders: () =>
    api.get<DnsProviderInfo[]>("/acme/dns-providers"),

  // ==================== 通配符证书管理 ====================

  // 申请通配符证书
  orderWildcardCertificate: (data: WildcardCertificateRequest) =>
    api.post<WildcardCertificateResult>("/acme/wildcard-certificates/order", data),

  // 获取通配符证书详情
  getWildcardCertificate: (certificateId: string) =>
    api.get<WildcardCertificateDetails>(`/acme/wildcard-certificates/${certificateId}`),

  // 获取通配符证书列表
  getWildcardCertificates: (params?: { baseDomain?: string; status?: string }) =>
    api.get<WildcardCertificateSummary[]>("/acme/wildcard-certificates", { params }),

  // 批量申请通配符证书
  batchOrderWildcardCertificates: (data: WildcardCertificateBatchRequest) =>
    api.post<WildcardCertificateBatchResult>("/acme/wildcard-certificates/batch-order", data),

  // 自动完成通配符证书挑战
  autoCompleteWildcardChallenge: (data: WildcardAutoChallengeRequest) =>
    api.post<WildcardAutoChallengeResult>("/acme/wildcard-certificates/auto-challenge", data),

  // 获取通配符证书状态
  getWildcardCertificateStatus: (certificateId: string) =>
    api.get<WildcardCertificateStatus>(`/acme/wildcard-certificates/${certificateId}/status`),

  // 导入通配符证书
  importWildcardCertificate: (data: {
    certificateData: string
    privateKey: string
    baseDomain: string
    format?: string
  }) => api.post<WildcardImportResult>("/acme/wildcard-certificates/import", data),

  // 删除通配符证书
  deleteWildcardCertificate: (certificateId: string) =>
    api.delete(`/acme/wildcard-certificates/${certificateId}`),

  // ==================== 续期配置管理 ====================

  // 获取证书续期配置
  getRenewalConfiguration: (certificateId: string) =>
    api.get<AcmeRenewalConfiguration>(`/acme/certificates/${certificateId}/renewal-config`),

  // 更新证书续期配置
  updateRenewalConfiguration: (certificateId: string, data: Partial<AcmeRenewalConfiguration>) =>
    api.put<AcmeRenewalConfiguration>(`/acme/certificates/${certificateId}/renewal-config`, data),

  // 获取自动续期配置列表
  getAutoRenewalConfigurations: () =>
    api.get<AutoRenewalConfiguration[]>("/acme/certificates/auto-renewal-configs"),

  // 启用/禁用自动续期
  toggleAutoRenewal: (certificateId: string, enabled: boolean) =>
    enabled 
      ? api.post(`/acme/certificates/${certificateId}/auto-renewal/enable`)
      : api.post(`/acme/certificates/${certificateId}/auto-renewal/disable`),

  // ==================== 操作日志管理 ====================

  // 获取ACME操作日志
  getOperationLogs: (params?: {
    accountId?: string
    certificateId?: string
    operation?: string
    status?: string
    startDate?: string
    endDate?: string
    page?: number
    pageSize?: number
  }) => api.get<AcmeOperationLog[]>("/acme/logs", { params }),

  // 获取操作日志详情
  getOperationLog: (logId: string) =>
    api.get<AcmeOperationLog>(`/acme/logs/${logId}`),

  // 清理操作日志
  cleanupOperationLogs: (params?: {
    olderThan?: string
    accountId?: string
    certificateId?: string
  }) => api.delete("/acme/logs/cleanup", { params }),

  // ==================== 统计信息 ====================

  // 获取ACME统计信息
  getStatistics: (params?: { provider?: string }) =>
    api.get<{
      totalAccounts: number
      totalCertificates: number
      activeCertificates: number
      expiredCertificates: number
      expiringSoonCertificates: number
      totalOrders: number
      successfulOrders: number
      failedOrders: number
      pendingChallenges: number
      providers: Array<{
        name: string
        accounts: number
        certificates: number
        successRate: number
      }>
    }>("/acme/statistics", { params }),

  // ==================== 证书验证工具 ====================

  // 验证证书链
  validateCertificateChain: (certificateData: string) =>
    api.post<{
      isValid: boolean
      chainLength: number
      rootCertificate: string
      intermediateCertificates: string[]
      errors: string[]
      warnings: string[]
    }>("/acme/certificates/validate-chain", { certificateData }),

  // 检查证书到期时间
  checkCertificateExpiry: (certificateId: string) =>
    api.get<{
      daysUntilExpiry: number
      certificateId: string
    }>(`/acme/certificates/${certificateId}/expiry`),

  // 验证域名匹配
  validateDomainMatch: (certificateData: string, domains: string[]) =>
    api.post<{
      isValid: boolean
      matchedDomains: string[]
      unmatchedDomains: string[]
      additionalDomains: string[]
      isWildcard: boolean
    }>("/acme/certificates/validate-domain-match", { certificateData, domains })
}

export default acmeApi