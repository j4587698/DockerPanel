import api from "./index"
import type {
  Container as ContainerInfo,
  ContainerStats,
  ContainerLogsRequest,
  ContainerLogsResponse,
  ContainerExecRequest,
  ContainerExecResponse,
  ContainerFileRequest,
  ContainerFileResponse,
  ContainerProcess,
  ContainerChange,
  ContainerCreateRequest,
  ContainerUpdateRequest,
  ContainerBatchOperation,
  ContainerPauseRequest,
  ContainerRenameRequest,
  ContainerExportRequest
} from "@/types/container"

// 重新导出类型以保持向后兼容
export type {
  ContainerInfo,
  ContainerStats,
  ContainerLog,
  ContainerExec,
  ContainerProcess,
  ContainerChange
}

// 执行命令请求 - 对齐后端模型
export interface ExecCommandRequest {
  command: string[]
  tty?: boolean
  attachStdout?: boolean
  attachStderr?: boolean
  workingDir?: string
  user?: string
  env?: Record<string, string>
}

// 执行结果 - 对齐后端模型
export interface ExecResult {
  exitCode: number
  stdout: string
  stderr: string
  startTime: string
  endTime: string
}

// 容器日志 - 对齐后端模型
export interface ContainerLogs {
  containerId: string
  logs: LogEntry[]
  hasMore: boolean
  since?: string
  until?: string
}

export interface LogEntry {
  timestamp: string
  message: string
  stream: string
  level?: string
}

// 批量操作请求 - 对齐后端模型
export interface BatchContainerOperationRequest {
  containerIds: string[]
  operation: "start" | "stop" | "restart" | "remove"
  force?: boolean
  timeout?: number
}

// 批量操作结果 - 对齐后端模型
export interface BatchOperationResult {
  success: boolean
  total: number
  successful: number
  failed: number
  errors: Array<{
    containerId: string
    error: string
  }>
}

// 容器进程 - 对齐后端模型
export interface ContainerProcess {
  pid: number
  user: string
  time: string
  command: string
}

// 文件系统变更 - 对齐后端模型
export interface FileSystemChange {
  kind: string
  path: string
}

// 使用中心化类型定义系统中的类型
export type { CreateContainerRequest } from "@/types/container"

// 使用中心化类型定义系统中的容器相关类型
export type {
  HostConfig,
  DeviceRequest,
  BlkioWeightDevice,
  BlkioDeviceRate,
  Ulimit,
  LogConfig,
  ContainerStats,
  CpuStats,
  MemoryStats,
  NetworkStats,
  BlockIoStats,
  ContainerLogs,
  LogEntry,
  ExecCommandRequest,
  ExecResult
} from "@/types/container"

// 使用中心化类型定义系统中的文件管理相关类型
export type {
  FileInfo,
  FileListResponse,
  FileUploadRequest,
  FileDownloadRequest,
  FileCreateFolderRequest,
  FileRenameRequest,
  FileDeleteRequest
} from "@/types/container"

// 使用中心化类型定义系统中的其他容器相关类型
export type {
  ContainerProcess,
  FileSystemChange,
  UpdateContainerRequest,
  ContainerResourceUsage,
  BatchContainerOperationRequest,
  BatchOperationResult,
  HealthCheckConfig,
  HealthCheckStatus,
  HealthCheckLog,
  HealthCheckStats,
  ResourceLimits,
  BlkioDeviceLimit,
  UpdateContainerResourcesRequest
} from "@/types/container"

// 容器API服务
export const containerApi = {
  // 获取容器列表
  getContainers: (params?: {
    nodeId?: string
    all?: boolean
    limit?: number
    page?: number
    pageSize?: number
  }) => api.get<ContainerInfo[]>("/containers", { params }),

  // 获取容器详情
  getContainer: (id: string, nodeId?: string) => api.get<ContainerInfo>(`/containers/${id}`, { params: { nodeId } }),

  // 创建容器
  createContainer: (data: CreateContainerRequest) =>
    api.post<string>("/containers", data),

  // 启动容器
  startContainer: (id: string, nodeId?: string) =>
    api.post(`/containers/${id}/start`, null, { params: { nodeId } }),

  // 停止容器
  stopContainer: (id: string, timeout = 30, nodeId?: string) =>
    api.post(`/containers/${id}/stop`, null, { params: { timeout, nodeId } }),

  // 重启容器
  restartContainer: (id: string, timeout = 30, nodeId?: string) =>
    api.post(`/containers/${id}/restart`, null, { params: { timeout, nodeId } }),

  // 删除容器
  removeContainer: (id: string, force = false, nodeId?: string) =>
    api.delete(`/containers/${id}`, { params: { force, nodeId } }),

  // 重命名容器
  renameContainer: (id: string, newName: string) =>
    api.post(`/containers/${id}/rename`, { newName }),

  // 更新容器配置（可热更新的配置：重启策略、资源限制）
  updateContainer: (id: string, data: {
    restartPolicy?: { name: string; maximumRetryCount?: number }
    memory?: number
    memoryReservation?: number
    memorySwap?: number
    cpuShares?: number
    cpuQuota?: number
    cpuPeriod?: number
    cpusetCpus?: string
  }) => api.patch(`/containers/${id}`, data),

  // 获取容器日志
  getContainerLogs: (
    id: string,
    params?: {
      nodeId?: string
      since?: string
      until?: string
      tail?: number
      follow?: boolean
    }
  ) => api.get<ContainerLogs>(`/containers/${id}/logs`, { params }),

  // 获取容器统计信息
  getContainerStats: (id: string, nodeId?: string) =>
    api.get<ContainerStats>(`/containers/${id}/stats`, { params: { nodeId } }),

  // 在容器中执行命令
  executeCommand: (id: string, data: ExecCommandRequest, nodeId?: string) =>
    api.post<ExecResult>(`/containers/${id}/exec`, data, { params: { nodeId } }),

  // 导出容器
  exportContainer: (id: string) =>
    api.get(`/containers/${id}/export`, { responseType: 'blob' }),

  // 重建容器
  recreateContainer: (id: string, options?: { pullLatest?: boolean; autoStart?: boolean }) =>
    api.post<{ message: string; oldId: string; newId: string; name: string }>(`/containers/${id}/recreate`, options, { timeout: 0 }),
  
  // 批量操作容器
  batchOperation: (data: BatchContainerOperationRequest) =>
    api.post<BatchOperationResult>("/containers/batch", data),

  // 文件管理 API
  // 获取容器文件列表
  getContainerFiles: (id: string, path: string = "/", nodeId?: string) =>
    api.get<ContainerFileListResponse>(`/containers/${id}/files`, { params: { path, nodeId } }),

  // 获取容器挂载点信息
  getContainerMounts: (id: string, nodeId?: string) =>
    api.get<ContainerMountInfo[]>(`/containers/${id}/mounts`, { params: { nodeId } }),

  // 下载容器文件
  downloadContainerFile: (id: string, path: string, nodeId?: string) =>
    api.get(`/containers/${id}/files/download`, { params: { path, nodeId }, responseType: 'blob' }),

  // 上传文件到容器
  uploadContainerFile: (id: string, path: string, file: File, nodeId?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('path', path)
    return api.post(`/containers/${id}/files/upload`, formData, {
      params: { nodeId },
      headers: { 'Content-Type': 'multipart/form-data' }
    })
  },

  // 创建文件夹
  createContainerFolder: (id: string, path: string, name: string, nodeId?: string) =>
    api.post(`/containers/${id}/files/folder`, { path, name }, { params: { nodeId } }),

  // 重命名文件
  renameContainerFile: (id: string, path: string, oldName: string, newName: string, nodeId?: string) =>
    api.put(`/containers/${id}/files/rename`, { path, oldName, newName }, { params: { nodeId } }),

  // 删除文件
  deleteContainerFile: (id: string, path: string, recursive: boolean = false, nodeId?: string) =>
    api.delete(`/containers/${id}/files`, { params: { path, recursive, nodeId } }),

  // 获取文件内容（用于编辑）
  getContainerFileContent: (id: string, path: string, nodeId?: string) =>
    api.get<{ content: string; path: string }>(`/containers/${id}/files/content`, { params: { path, nodeId }, skipErrorHandler: true } as any),

  // 写入文件内容
  writeContainerFileContent: (id: string, path: string, content: string, nodeId?: string) =>
    api.put(`/containers/${id}/files/content`, { path, content }, { params: { nodeId }, skipErrorHandler: true } as any),

  // 修改文件权限
  changeContainerFilePermissions: (id: string, path: string, permissions: string, nodeId?: string) =>
    api.put(`/containers/${id}/files/permissions`, { path, permissions }, { params: { nodeId }, skipErrorHandler: true } as any),
  }

// 文件管理相关类型
export interface ContainerFileInfo {
  name: string
  path: string
  type: 'file' | 'directory'
  size: number
  modified?: string
  permissions: string
  owner?: string
  group?: string
  isMount: boolean
  mountSource?: string
  mountType?: string
  /** 文件变更状态：null=原始文件, "A"=新增, "C"=修改, "D"=已删除 */
  changeStatus?: 'A' | 'C' | 'D'
}

export interface ContainerMountInfo {
  destination: string
  source?: string
  type: string // "bind", "volume", "tmpfs"
  name?: string
  rw: boolean
  driver?: string
  isNamedVolume: boolean
  isBindMount: boolean
}

export interface ContainerFileListResponse {
  containerId: string
  currentPath: string
  files: ContainerFileInfo[]
  mounts: ContainerMountInfo[]
}

export default containerApi