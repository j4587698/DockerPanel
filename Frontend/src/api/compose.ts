import api from "@/api"
import type {
  ComposeFile,
  ComposeProject,
  ComposeValidationResult,
  ComposeOperationResult,
  ComposeLogsResponse,
  ComposeProjectStats,
  ComposeTemplate,
  CreateComposeFileRequest,
  UpdateComposeFileRequest,
  DeployComposeRequest,
  ComposeOperationRequest,
  ComposeLogsRequest,
  CloneComposeFileRequest,
  BatchComposeOperationRequest
} from "@/types/compose"

export const composeApi = {
  // 获取 Compose 文件列表
  getComposeFiles(params?: {
    nodeId?: string
    includeContent?: boolean
    page?: number
    pageSize?: number
  }) {
    return api.get<ComposeFile[]>("compose", { params })
  },

  // 获取 Compose 文件详情
  getComposeFile(id: string, includeContent = true) {
    return api.get<ComposeFile>(`compose/${id}`, {
      params: { includeContent }
    })
  },

  // 创建 Compose 文件
  createComposeFile(data: CreateComposeFileRequest) {
    return api.post<ComposeFile>("compose", data)
  },

  // 更新 Compose 文件
  updateComposeFile(id: string, data: UpdateComposeFileRequest) {
    return api.put<ComposeFile>(`compose/${id}`, data)
  },

  // 删除 Compose 文件
  deleteComposeFile(id: string, force = false) {
    return api.delete(`compose/${id}`, {
      params: { force }
    })
  },

  // 验证 Compose 文件
  validateComposeFile(id: string, content?: string) {
    return api.post<ComposeValidationResult>(`compose/${id}/validate`, content)
  },

  // 验证 Compose 内容（无需保存）
  validateCompose(data: { content: string }) {
    return api.post<ComposeValidationResult>("compose/validate", data)
  },

  // 解析 Compose 内容
  parseComposeContent(content: string) {
    return api.post<ComposeFile>("compose/parse", { content })
  },

  // 部署 Compose 项目
  deployCompose(data: DeployComposeRequest) {
    return api.post<ComposeOperationResult>("compose/deploy", data)
  },

  // 停止 Compose 项目
  stopCompose(data: ComposeOperationRequest) {
    return api.post<ComposeOperationResult>("compose/stop", data)
  },

  // 启动 Compose 项目
  startCompose(data: ComposeOperationRequest) {
    return api.post<ComposeOperationResult>("compose/start", data)
  },

  // 重启 Compose 项目
  restartCompose(data: ComposeOperationRequest) {
    return api.post<ComposeOperationResult>("compose/restart", data)
  },

  // 删除 Compose 项目
  removeCompose(data: ComposeOperationRequest) {
    return api.post<ComposeOperationResult>("compose/remove", data)
  },

  // 获取 Compose 项目状态
  getComposeProjectStatus(composeFileId: string, nodeId?: string) {
    return api.get<ComposeProject>(`compose/projects/${composeFileId}/status`, {
      params: { nodeId }
    })
  },

  // 获取 Compose 项目列表
  getComposeProjects(nodeId?: string) {
    return api.get<ComposeProject[]>("compose/projects", {
      params: { nodeId }
    })
  },

  // 获取 Compose 日志
  getComposeLogs(data: ComposeLogsRequest) {
    return api.post<ComposeLogsResponse>("compose/logs", data)
  },

  // 获取 Compose 项目统计
  getComposeProjectStats(composeFileId: string, nodeId?: string) {
    return api.get<ComposeProjectStats>(`compose/projects/${composeFileId}/stats`, {
      params: { nodeId }
    })
  },

  // 导出 Compose 文件
  exportComposeFile(id: string, format = "yaml") {
    return api.get<string>(`compose/${id}/export`, {
      params: { format },
      responseType: "blob"
    })
  },

  // 导入 Compose 文件
  importComposeFile(data: {
    content: string
    name: string
    description?: string
    nodeId?: string
  }) {
    return api.post<ComposeFile>("compose/import", data)
  },

  // 克隆 Compose 文件
  cloneComposeFile(id: string, data: CloneComposeFileRequest) {
    return api.post<ComposeFile>(`compose/${id}/clone`, data)
  },

  // 获取 Compose 模板列表
  getComposeTemplates(category?: string, tags?: string[]) {
    return api.get<ComposeTemplate[]>("compose/templates", {
      params: { category, tags }
    })
  },

  // 从模板创建 Compose
  createFromTemplate(data: {
    templateId: string
    variables: Record<string, any>
    name: string
    description?: string
  }) {
    return api.post<ComposeFile>("compose/create-from-template", data)
  },

  // 批量操作
  batchOperation(data: BatchComposeOperationRequest) {
    return api.post<Record<string, ComposeOperationResult>>("compose/batch-operation", data)
  },

  // 获取 Compose 文件历史版本
  getComposeFileHistory(id: string) {
    return api.get<any[]>(`compose/${id}/history`)
  },

  // 恢复 Compose 文件版本
  restoreComposeFileVersion(id: string, versionId: string) {
    return api.post<ComposeFile>(`compose/${id}/restore`, { versionId })
  },

  // 同步 Compose 文件到节点
  syncComposeFileToNode(id: string, nodeId: string) {
    return api.post<ComposeOperationResult>(`compose/${id}/sync`, { nodeId })
  },

  // 检查 Compose 文件依赖
  checkComposeDependencies(id: string) {
    return api.post<any>(`compose/${id}/check-dependencies`)
  }
}