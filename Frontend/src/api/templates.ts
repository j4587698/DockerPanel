import api from "./index"

// 模板类型
export type TemplateType = 'web' | 'database' | 'cache' | 'queue' | 'monitoring' | 'development' | 'custom'

// 端口映射
export interface TemplatePortMapping {
  hostIp?: string
  hostPort?: number
  containerPort: number
  protocol: string
}

// 卷映射
export interface TemplateVolumeMapping {
  hostPath?: string
  containerPath: string
  readOnly: boolean
}

// 重启策略
export interface TemplateRestartPolicy {
  name: string
  maximumRetryCount?: number
}

// 网络配置
export interface TemplateNetworkConfig {
  networkId: string
  networkName?: string
  aliases?: string[]
  ipAddress?: string
}

// 模板
export interface ContainerTemplate {
  id: string
  name: string
  type: TemplateType
  description?: string
  image: string
  command?: string[]
  workingDir?: string
  user?: string
  ports?: TemplatePortMapping[]
  volumes?: TemplateVolumeMapping[]
  environment?: Record<string, string>
  labels?: Record<string, string>
  restartPolicy?: TemplateRestartPolicy
  networkMode?: string
  networks?: TemplateNetworkConfig[]
  createdAt: string
  updatedAt: string
}

// 创建模板请求
export interface CreateTemplateRequest {
  name: string
  type: TemplateType
  description?: string
  image: string
  command?: string[]
  workingDir?: string
  user?: string
  ports?: TemplatePortMapping[]
  volumes?: TemplateVolumeMapping[]
  environment?: Record<string, string>
  labels?: Record<string, string>
  restartPolicy?: TemplateRestartPolicy
  networkMode?: string
  networks?: TemplateNetworkConfig[]
}

// 模板API服务
export const templateApi = {
  // 获取模板列表
  getTemplates: (type?: TemplateType) => 
    api.get<ContainerTemplate[]>("/templates", { params: { type } }),

  // 获取单个模板
  getTemplate: (id: string) => 
    api.get<ContainerTemplate>(`/templates/${id}`),

  // 创建模板
  createTemplate: (data: CreateTemplateRequest) => 
    api.post<ContainerTemplate>("/templates", data),

  // 更新模板
  updateTemplate: (id: string, data: CreateTemplateRequest) => 
    api.put<ContainerTemplate>(`/templates/${id}`, data),

  // 删除模板
  deleteTemplate: (id: string) => 
    api.delete(`/templates/${id}`),

  // 复制模板
  duplicateTemplate: (id: string) => 
    api.post<ContainerTemplate>(`/templates/${id}/duplicate`),

  // 导出模板
  exportTemplate: (id: string) => 
    api.get<ContainerTemplate>(`/templates/${id}/export`),

  // 导入模板
  importTemplate: (template: ContainerTemplate) => 
    api.post<ContainerTemplate>("/templates/import", template),
}

export default templateApi
