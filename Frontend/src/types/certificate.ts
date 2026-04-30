/**
 * ACME证书管理类型定义
 * 对应后端DockerPanel.API.Models.Acme命名空间下的所有类型
 */

// ==================== 基础ACME类型 ====================

/**
 * ACME账户信息
 */
export interface AcmeAccount {
  id: string
  email: string
  provider: string
  accountKey: string
  accountUri: string
  isActive: boolean
  createdAt: string
  lastUsedAt?: string
  metadata: Record<string, any>
}

/**
 * ACME提供商信息
 */
export interface AcmeProvider {
  name: string
  displayName: string
  directoryUrl: string
  isProduction: boolean
  isStaging: boolean
  description: string
  supportedChallengeTypes: string[]
  configuration: Record<string, any>
}

/**
 * 证书申请请求
 */
export interface AcmeCertificateRequest {
  accountId: string
  domains: string[]
  keyType: string // RSA2048, RSA4096, ECDSA256, ECDSA384
  useWildcard: boolean
  useStaging: boolean
  challengeTypes: string[] // http-01, dns-01, tls-alpn-01
  certificateValidityDays?: number
  metadata: Record<string, any>
}

/**
 * 证书订单信息
 */
export interface AcmeCertificateOrder {
  id: string
  accountId: string
  orderUri: string
  domains: string[]
  status: string // pending, ready, processing, valid, invalid
  createdAt: string
  expiresAt?: string
  completedAt?: string
  certificateId?: string
  authorizations: AcmeAuthorization[]
  finalizeUri?: string
  certificateUri?: string
  error?: string
  metadata: Record<string, any>
  // 以下字段由 API 返回
  name?: string
  domain?: string
  issuer?: string
  subject?: string
  acmeProvider?: string
  provider?: string
  challengeType?: string
  dnsProvider?: string
  dnsConfigFields?: string[] // 只返回字段名，不返回敏感值
  autoRenew?: boolean
  isAutoRenewal?: boolean
  updatedAt?: string
  issuedAt?: string
  daysUntilExpiry?: number
  serialNumber?: string
  fingerprint?: string
  email?: string
  description?: string
  logs?: any[]
}

/**
 * ACME授权信息
 */
export interface AcmeAuthorization {
  id: string
  domain: string
  status: string // pending, valid, invalid, deactivated, expired
  expiresAt: string
  isWildcard: boolean
  challenges: AcmeChallenge[]
  error?: string
}

/**
 * ACME挑战信息
 */
export interface AcmeChallenge {
  type: string // http-01, dns-01, tls-alpn-01
  status: string // pending, processing, valid, invalid
  url: string
  token: string
  keyAuthorization?: string
  validatedAt?: string
  error?: string
  challengeData: Record<string, any>
}

/**
 * 挑战结果
 */
export interface AcmeChallengeResult {
  success: boolean
  message: string
  challengeType: string
  status: string
  validatedAt?: string
  error?: string
  details: Record<string, any>
}

/**
 * 证书数据
 */
export interface AcmeCertificateData {
  certificate: string // PEM格式证书
  certificateChain?: string // PEM格式证书链
  privateKey?: string // PEM格式私钥
  certificateFingerprint: string
  serialNumber: string
  issuedAt: string
  expiresAt: string
  domains: string[]
  issuer: string
  subject: string
  metadata: Record<string, any>
}

/**
 * 连接测试结果
 */
export interface AcmeConnectionTestResult {
  success: boolean
  message: string
  provider: string
  directoryUrl: string
  responseTime: string // TimeSpan
  version: string
  supportedChallengeTypes: string[]
  additionalInfo: Record<string, any>
}

/**
 * 证书续期配置
 */
export interface AcmeRenewalConfiguration {
  certificateId: string
  accountId: string
  autoRenewalEnabled: boolean
  renewalDaysBeforeExpiry: number
  renewalSchedule: string // Cron表达式
  notificationEmails: string[]
  settings: Record<string, any>
  lastRenewalAttempt?: string
  nextRenewalAttempt?: string
  renewalAttempts: number
  renewalInProgress: boolean
}

// ==================== 请求类型 ====================

/**
 * 创建ACME账户请求
 */
export interface CreateAcmeAccountRequest {
  email: string
  provider: string
}

/**
 * 完成挑战请求
 */
export interface CompleteChallengeRequest {
  challengeType: string
}

/**
 * 撤销证书请求
 */
export interface RevokeCertificateRequest {
  reason: number // 0=unspecified, 1=keyCompromise, 2=affiliationChanged, etc.
}

/**
 * 自动续期配置
 */
export interface AutoRenewalConfiguration {
  id: string
  certificateId: string
  accountId: string
  autoRenewalEnabled: boolean
  renewalDaysBeforeExpiry: number
  renewalSchedule: string // Cron表达式
  notificationEmails: string[]
  settings: Record<string, any>
  lastRenewalAttempt?: string
  nextRenewalAttempt?: string
  renewalAttempts: number
  renewalInProgress: boolean
  createdAt: string
  updatedAt: string
  status: RenewalStatus
}

/**
 * 续期状态
 */
export interface RenewalStatus {
  state: string // pending, success, failed, disabled
  message?: string
  lastCheck?: string
  lastSuccess?: string
  lastFailure?: string
  consecutiveFailures: number
  totalRenewals: number
  nextAttempt?: string
}

/**
 * ACME操作日志
 */
export interface AcmeOperationLog {
  id: string
  operation: string // create, renew, revoke, challenge
  operationType: string // create, renew, revoke, challenge
  accountId: string
  certificateId?: string
  orderId?: string
  status: string // started, completed, failed
  success: boolean
  message?: string
  errorMessage?: string
  startedAt: string
  timestamp: string
  completedAt?: string
  duration: string // TimeSpan
  requestData: Record<string, any>
  responseData: Record<string, any>
  nodeId?: string
  userId?: string
}

/**
 * ACME证书验证结果
 */
export interface AcmeCertificateValidationResult {
  certificateId: string
  certificateChain: string
  privateKey: string
  certificateFingerprint: string
  serialNumber: string
  issuedAt: string
  expiresAt: string
  domains: string[]
  issuer: string
  subject: string
  validationStatus: ValidationResult
  validationErrors: string[]
  metadata: Record<string, any>

  // 额外属性
  subjectAlternativeNames: string[]
  domainMatch: boolean
  daysUntilExpiry: number
  warnings: string[]
  selfSigned: boolean
  valid: boolean
  status: string
  errors: string[]
  validatedAt: string
}

/**
 * 验证结果
 */
export interface ValidationResult {
  isValid: boolean
  status: string // valid, invalid, expired
  reason?: string
  validatedAt: string
  expiresAt?: string
  domainValidations: DomainValidation[]
}

/**
 * 域名验证
 */
export interface DomainValidation {
  domain: string
  isValid: boolean
  status: string
  errorMessage?: string
  validatedAt: string
}

/**
 * ACME密钥信息
 */
export interface AcmeKeyInfo {
  keyId: string
  keyType: string // RSA, ECDSA
  keySize: number
  publicKey: string
  privateKey: string
  keyAlgorithm: string
  keyFingerprint: string
  createdAt: string
  lastUsedAt?: string
  isActive: boolean
  associatedCertificates: string[]
  metadata: Record<string, any>

  // 额外属性
  publicKeyPem: string
}

/**
 * ACME密钥对
 */
export interface AcmeKeyPair {
  keyId: string
  publicKeyPem: string
  privateKeyPem: string
  keyType: string
  keySize: number
  keyAlgorithm: string
  createdAt: string
  expiresAt?: string
  isExpired: boolean
  certificateIds: string[]

  // 额外属性
  keyFingerprint: string
}

// ==================== DNS挑战相关类型 ====================

/**
 * DNS挑战配置结果
 */
export interface DnsChallengeConfigurationResult {
  success: boolean
  domain: string
  challengeType: string // dns-01
  challengeToken: string
  keyAuthorization: string
  dnsRecordName: string
  dnsRecordValue: string
  dnsProvider: string
  status: string // configured, failed, pending
  errorMessage?: string
  message: string
  errors: string[]
  configuredAt: string
  expiresAt?: string
  configurationData: Record<string, any>
}

/**
 * DNS挑战清理结果
 */
export interface DnsChallengeCleanupResult {
  success: boolean
  domain: string
  dnsRecordName: string
  dnsProvider: string
  status: string // cleaned, failed
  errorMessage?: string
  message: string
  errors: string[]
  cleanedAt: string
  cleanupData: Record<string, any>
}

/**
 * DNS提供商信息
 */
export interface DnsProviderInfo {
  providerId: string
  name: string
  displayName: string
  description: string
  supportedChallengeTypes: string[]
  configurationFields: Record<string, DnsProviderField>
  isEnabled: boolean
  documentationUrl: string
  defaultSettings: Record<string, any>
}

/**
 * DNS提供商配置字段
 */
export interface DnsProviderField {
  key: string
  label: string
  type: string // string, number, boolean, secret
  required: boolean
  defaultValue?: string
  description?: string
  options: string[]
  validationPattern?: string
}

// ==================== 通配符证书相关类型 ====================

/**
 * 通配符证书详情
 */
export interface WildcardCertificateDetails {
  certificateId: string
  baseDomain: string
  wildcardDomain: string // *.example.com
  additionalDomains: string[]
  certificateChain: string
  privateKey: string
  issuedAt: string
  expiresAt: string
  issuer: string
  status: string
  accountId: string
  renewalConfiguration?: AutoRenewalConfiguration
  appliedServices: string[]
  metadata: Record<string, any>
}

/**
 * 通配符证书摘要
 */
export interface WildcardCertificateSummary {
  certificateId: string
  baseDomain: string
  wildcardDomain: string
  expiresAt: string
  daysUntilExpiry: number
  status: string
  autoRenewalEnabled: boolean
  lastRenewed?: string
  serviceCount: number
  isExpiringSoon: boolean
}

/**
 * 通配符证书批量请求
 */
export interface WildcardCertificateBatchRequest {
  certificates: WildcardCertificateRequestItem[]
  accountId: string
  skipExisting: boolean
  globalSettings: Record<string, any>
  operation: string
  certificateIds: string[]
}

/**
 * 通配符证书请求项
 */
export interface WildcardCertificateRequestItem {
  baseDomain: string
  additionalDomains: string[]
  dnsProvider: string
  dnsProviderConfig: Record<string, string>
  certificateSettings: Record<string, any>
  enableAutoRenewal: boolean
  notificationEmails: string[]
}

/**
 * 通配符证书批量结果
 */
export interface WildcardCertificateBatchResult {
  totalCertificates: number
  successCount: number
  failureCount: number
  results: WildcardCertificateResultItem[]
  startedAt: string
  completedAt: string
  duration: string // TimeSpan
  success: boolean
  message: string
  errors: string[]
  batchStartedAt: string
  batchCompletedAt: string
}

/**
 * 通配符证书结果项
 */
export interface WildcardCertificateResultItem {
  baseDomain: string
  success: boolean
  certificateId?: string
  errorMessage?: string
  status: string
  completedAt?: string
  metadata: Record<string, any>
}

/**
 * 通配符证书状态
 */
export interface WildcardCertificateStatus {
  certificateId: string
  baseDomain: string
  status: string // pending, valid, invalid, expired, revoked
  issuedAt?: string
  expiresAt?: string
  daysUntilExpiry: number
  isExpiringSoon: boolean
  isExpired: boolean
  lastError?: string
  lastChecked?: string
  appliedServices: string[]
  renewalStatus?: RenewalStatus
}

/**
 * 通配符自动挑战请求
 */
export interface WildcardAutoChallengeRequest {
  certificateId: string
  domain: string
  dnsProvider: string
  dnsProviderConfig: Record<string, string>
  challengeTypes: string[] // dns-01
  autoCleanup: boolean
  challengeTimeoutMinutes: number
  challengeSettings: Record<string, any>
}

/**
 * 通配符自动挑战结果
 */
export interface WildcardAutoChallengeResult {
  success: boolean
  certificateId: string
  domain: string
  dnsProvider: string
  challengeResults: ChallengeResult[]
  startedAt: string
  completedAt?: string
  duration: string // TimeSpan
  errorMessage?: string
  cleanupCompleted: boolean
  message: string
  errors: string[]
  configuredAt: string
  metadata: Record<string, any>
}

/**
 * 挑战结果
 */
export interface ChallengeResult {
  challengeType: string
  domain: string
  success: boolean
  status: string
  errorMessage?: string
  startedAt: string
  completedAt?: string
  duration: string // TimeSpan
  challengeData: Record<string, any>
}

/**
 * 通配符证书申请请求
 */
export interface WildcardCertificateRequest {
  // 申请的域名列表
  domains: string[]

  // 基础域名
  baseDomain: string

  // 子域名列表
  subdomains: string[]

  // ACME账户ID
  accountId: string

  // 密钥类型
  keyType: string

  // DNS提供商
  dnsProvider: string

  // DNS提供商配置
  dnsProviderConfig: Record<string, string>

  // DNS凭证
  dnsCredentials: Record<string, Record<string, any>>

  // 首选DNS提供商列表
  preferredDnsProviders: string[]

  // 是否使用测试环境
  useStaging: boolean

  // 挑战类型列表
  challengeTypes: string[]

  // 证书有效期（天）
  certificateValidityDays?: number

  // 是否启用自动续期
  enableAutoRenewal: boolean

  // 通知邮箱列表
  notificationEmails: string[]

  // 续期天数（过期前多少天续期）
  renewalDaysBeforeExpiry: number

  // 续期计划（Cron表达式）
  renewalSchedule?: string

  // 元数据
  metadata: Record<string, any>
}

/**
 * 通配符证书申请结果
 */
export interface WildcardCertificateResult {
  // 是否成功
  success: boolean

  // 证书ID
  certificateId?: string

  // 基础域名
  baseDomain: string

  // 子域名列表
  subdomains: string[]

  // 完整域名列表
  fullDomains: string[]

  // 证书指纹
  certificateFingerprint: string

  // 签发时间
  issuedAt: string

  // 过期时间
  expiresAt: string

  // 错误信息
  errorMessage?: string

  // 消息
  message: string

  // 错误列表
  errors: string[]

  // 警告列表
  warnings: string[]

  // 状态
  status: string

  // 订单ID
  orderId?: string

  // 申请时间
  requestedAt: string

  // 完成时间
  completedAt?: string

  // 处理时长
  duration: string // TimeSpan

  // 验证步骤
  validationSteps: string[]

  // 预验证结果
  preValidationResult?: AcmeCertificateValidationResult

  // 元数据
  metadata: Record<string, any>
}

/**
 * 通配符DNS结果
 */
export interface WildcardDnsResult {
  success: boolean
  message: string
  domain: string
  dnsProvider: string
  recordName: string
  recordValue: string
  recordType: string // TXT
  createdAt: string
  validatedAt?: string
  validationSteps: string[]
  errors: string[]
  warnings: string[]
  dnsDetails: Record<string, any>
  dnsPropagationTime?: string // TimeSpan
  isWildcard: boolean
  wildcardPattern: string
}

/**
 * 通配符导入结果
 */
export interface WildcardImportResult {
  success: boolean
  message: string
  certificateId?: string
  baseDomain: string
  importedDomains: string[]
  importedAt: string
  certificateFingerprint: string
  issuedAt: string
  expiresAt: string
  issuer: string
  importSteps: string[]
  errors: string[]
  warnings: string[]
  importDetails: Record<string, any>
  isWildcard: boolean
  importSource: string
  format: string
  fileSize: number
}

// ==================== API请求/响应类型 ====================

/**
 * 生成CSR请求
 */
export interface GenerateCsrRequest {
  domains: string[]
  keyType: string
}

/**
 * 验证证书请求
 */
export interface ValidateCertificateRequest {
  certificateData: string
}

/**
 * 导入密钥请求
 */
export interface ImportKeyRequest {
  keyData: string
  format: string
}