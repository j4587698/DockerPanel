import api from "./index"

export interface YarpStatus {
  totalRoutes: number
  totalClusters: number
  totalDomainMappings: number
  activeMappings: number
  sslEnabledMappings: number
  lastUpdated?: string
  isHealthy: boolean
}

export interface DomainMapping {
  id: string
  containerId: string
  containerName: string
  domain: string
  destinationAddress: string
  containerPort: number
  pathPrefix?: string
  enableSsl: boolean
  certificateId?: string
  autoRequestCertificate?: boolean
  protocol: string
  priority: number
  createdAt: string
  enabled: boolean
}

export interface CreateDomainMappingRequest {
  domain: string
  containerId: string
  containerName?: string
  destinationAddress: string
  containerPort?: number
  pathPrefix?: string
  enableSsl: boolean
  certificateId?: string
  autoRequestCertificate?: boolean
  protocol: string
  priority: number
}

export interface UpdateCertificateRequest {
  certificateId?: string
}

/**
 * YARP网关API (对齐后端 ProxyController)
 */
export const yarpApi = {
  /**
   * 获取所有域名映射
   */
  getDomainMappings: async (): Promise<DomainMapping[]> => {
    return await api.get("/proxy/mappings")
  },

  /**
   * 创建域名映射
   */
  createDomainMapping: async (data: CreateDomainMappingRequest): Promise<any> => {
    return await api.post("/proxy/mappings", data)
  },

  /**
   * 删除域名映射
   */
  deleteDomainMapping: async (id: string): Promise<void> => {
    await api.delete(`/proxy/mappings/${id}`)
  },

  /**
   * 更新域名映射
   */
  updateDomainMapping: async (id: string, data: Partial<DomainMapping>): Promise<any> => {
    return await api.put(`/proxy/mappings/${id}`, data)
  },

  /**
   * 切换域名映射启用状态
   */
  toggleMappingEnabled: async (id: string, enabled: boolean): Promise<any> => {
    return await api.put(`/proxy/mappings/${id}`, { enabled })
  },

  /**
   * 更新域名映射的证书绑定
   */
  updateMappingCertificate: async (mappingId: string, certificateId?: string): Promise<any> => {
    return await api.put(`/proxy/mappings/${mappingId}/certificate`, { certificateId })
  },

  /**
   * 获取YARP配置
   */
  getYarpConfig: async (): Promise<any> => {
    return await api.get("/proxy/config")
  },

  /**
   * 获取YARP代理状态
   */
  getYarpStatus: async (): Promise<YarpStatus> => {
    return await api.get("/proxy/status")
  },

  /**
   * 重新加载代理配置
   */
  reloadConfig: async (): Promise<any> => {
    return await api.post("/proxy/reload")
  }
}

export default yarpApi
