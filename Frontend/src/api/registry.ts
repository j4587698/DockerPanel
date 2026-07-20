import api from "@/api"
import type {
  ImageRegistry,
  RegistryTestResult,
  RegistryAuthResult,
  CreateRegistryRequest,
  UpdateRegistryRequest,
  RegistryLoginRequest,
  PushImageRequest,
  PullImageRequest,
  SearchRegistryRequest,
  RegistrySearchResult,
  RegistryImageDetail,
  RegistryImageTag,
  RegistryOperationResult,
  RegistryHealthCheckResult,
  RegistryImageScanResult,
  RegistryAccessLog,
  RegistryCredential,
  RegistrySyncResult,
  RegistryConfigTemplate,
  BatchRegistryOperationRequest,
  BatchRegistryOperationResult,
  RegistryUsageStats,
  RegistryCleanupResult,
  RegistryCleanupPolicy,
  RegistrySyncConfig,
  RegistryScanConfig
} from "@/types/registry"

export const registryApi = {
  // 基础仓库管理
  getRegistries() {
    return api.get<ImageRegistry[]>("registries")
  },

  getRegistriesByType(type: string) {
    return api.get<ImageRegistry[]>(`registries/by-type/${type}`)
  },

  getMirrors() {
    return api.get<ImageRegistry[]>("registries/mirrors")
  },

  getPrivateRegistries() {
    return api.get<ImageRegistry[]>("registries/private")
  },

  getRegistryById(id: string) {
    return api.get<ImageRegistry>(`registries/${id}`)
  },

  createRegistry(data: CreateRegistryRequest) {
    return api.post<ImageRegistry>("registries", data)
  },

  updateRegistry(id: string, data: UpdateRegistryRequest) {
    return api.put<ImageRegistry>(`registries/${id}`, data)
  },

  deleteRegistry(id: string) {
    return api.delete(`registries/${id}`)
  },

  testRegistryConnection(id: string) {
    return api.post<RegistryTestResult>(`registries/${id}/test`)
  },

  setDefaultRegistry(id: string) {
    return api.post(`registries/${id}/set-default`)
  },

  // 测试仓库配置（无需保存）
  testRegistryConfig(data: { domain: string; username?: string; password?: string; isSecure?: boolean }) {
    return api.post<RegistryTestResult>('registries/test-config', data)
  },

  getRegistryImages(registryId: string, search?: string) {
    return api.get<any[]>(`registries/${registryId}/images`, {
      params: { search }
    })
  },

  // 认证相关
  loginToRegistry(data: RegistryLoginRequest) {
    return api.post<RegistryAuthResult>("registries/login", data)
  },

  logoutFromRegistry(registryId: string) {
    return api.post(`registries/${registryId}/logout`)
  },

  validateRegistryAuth(registryId: string) {
    return api.get<RegistryAuthResult>(`registries/${registryId}/validate-auth`)
  },

  syncRegistryImages(registryId: string) {
    return api.post<RegistrySyncResult>(`registries/${registryId}/sync`)
  },

  getRegistryStatistics(registryId?: string) {
    return api.get<any>("registries/statistics", {
      params: { registryId }
    })
  },

  // 增强功能
  pushImage(data: PushImageRequest) {
    return api.post<RegistryOperationResult>("registries/push", data)
  },

  searchRegistryImages(data: SearchRegistryRequest) {
    return api.post<RegistrySearchResult>("registries/search", data)
  },

  // 搜索仓库镜像（简化版）
  searchImages(registryId: string, params: { keyword: string; page?: number; pageSize?: number }) {
    return api.post<{ results: any[]; total: number }>(`registries/${registryId}/search`, {
      query: params.keyword,
      limit: params.pageSize || 20,
      offset: ((params.page || 1) - 1) * (params.pageSize || 20)
    })
  },

  getImageDetail(registryId: string, imageName: string, tag = "latest") {
    return api.get<RegistryImageDetail>(`registries/${registryId}/images/${imageName}/details`, {
      params: { tag }
    })
  },

  getImageTags(registryId: string, imageName: string) {
    return api.get<RegistryImageTag[]>(`registries/${registryId}/images/${imageName}/tags`)
  },

  deleteImageTag(registryId: string, imageName: string, tag: string) {
    return api.delete<RegistryOperationResult>(`registries/${registryId}/images/${imageName}/tags/${tag}`)
  },

  // 健康检查
  getRegistryHealth(registryId: string) {
    return api.get<RegistryHealthCheckResult>(`registries/${registryId}/health`)
  },

  checkAllRegistriesHealth() {
    return api.get<Record<string, RegistryHealthCheckResult>>("registries/health/all")
  },

  // 安全扫描
  scanImageSecurity(registryId: string, imageName: string, tag = "latest") {
    return api.post<RegistryImageScanResult>(`registries/${registryId}/images/${imageName}/scan`, {}, {
      params: { tag }
    })
  },

  // 访问日志
  getRegistryAccessLogs(
    registryId: string,
    startTime?: string,
    endTime?: string,
    action?: string,
    limit = 100
  ) {
    return api.get<RegistryAccessLog[]>(`registries/${registryId}/logs`, {
      params: { startTime, endTime, action, limit }
    })
  },

  // 凭证管理
  saveRegistryCredential(registryId: string, credential: RegistryCredential) {
    return api.post<RegistryCredential>(`registries/${registryId}/credentials`, credential)
  },

  deleteRegistryCredential(credentialId: string) {
    return api.delete(`registries/credentials/${credentialId}`)
  },

  getRegistryCredentials(registryId: string) {
    return api.get<RegistryCredential[]>(`registries/${registryId}/credentials`)
  },

  refreshRegistryToken(registryId: string) {
    return api.post<RegistryAuthResult>(`registries/${registryId}/refresh-token`)
  },

  // 元数据同步
  syncRegistryMetadata(registryId: string, forceSync = false) {
    return api.post<RegistrySyncResult>(`registries/${registryId}/sync-metadata`, {}, {
      params: { forceSync }
    })
  },

  // 配置模板
  getRegistryConfigTemplate(registryType: string) {
    return api.get<RegistryConfigTemplate>(`registries/config-templates/${registryType}`)
  },

  // 镜像复制
  copyImageBetweenRegistries(data: {
    sourceRegistryId: string
    targetRegistryId: string
    imageName: string
    tag?: string
  }) {
    return api.post<RegistryOperationResult>("registries/copy", data)
  },

  // 批量操作
  batchRegistryOperation(data: BatchRegistryOperationRequest) {
    return api.post<BatchRegistryOperationResult>("registries/batch-operation", data)
  },

  // 使用统计
  getRegistryUsageStats(registryId: string, period = "7d") {
    return api.get<RegistryUsageStats>(`registries/${registryId}/stats`, {
      params: { period }
    })
  },

  // 清理操作
  cleanupRegistry(registryId: string, policy: RegistryCleanupPolicy) {
    return api.post<RegistryCleanupResult>(`registries/${registryId}/cleanup`, policy)
  },

  // 自动同步配置
  setRegistryAutoSync(registryId: string, config: RegistrySyncConfig) {
    return api.post<RegistrySyncConfig>(`registries/${registryId}/auto-sync`, config)
  },

  getRegistryAutoSyncConfig(registryId: string) {
    return api.get<RegistrySyncConfig>(`registries/${registryId}/auto-sync`)
  },

  // 安全扫描配置
  setRegistryScanConfig(registryId: string, config: RegistryScanConfig) {
    return api.post<RegistryScanConfig>(`registries/${registryId}/scan-config`, config)
  },

  getRegistryScanConfig(registryId: string) {
    return api.get<RegistryScanConfig>(`registries/${registryId}/scan-config`)
  }
}