/**
 * SSH管理API
 * 对应后端DockerPanel.API.Controllers.SshController
 */

import api from "./index"
import type {
  SshConnectionTestRequest,
  SshConnectionInfo,
  SshKeyPair,
  SshCommandResult,
  GenerateKeyPairRequest,
  ValidatePrivateKeyRequest,
  ExecuteCommandRequest,
  UploadFileRequest,
  DownloadFileRequest,
  BatchSshTestRequest,
  SshConnectionConfig,
  SshSessionInfo,
  SshOperationLog,
  SshSettings,
  SshStatistics,
  RemoteFileInfo,
  SshHostKey,
  SshTerminalSession
} from "@/types/ssh"

// ==================== SSH API对象 ====================

// SSH管理API - 对齐后端SshController
export const sshApi = {
  // ==================== 基础SSH操作 ====================

  // 测试SSH连接
  testConnection: (data: SshConnectionTestRequest) =>
    api.post<boolean>("/ssh/test-connection", data),

  // 批量测试SSH连接
  batchTestConnection: (data: BatchSshTestRequest) =>
    api.post<{ Results: Array<{ Host: string; Port: number; Username: string; Success: boolean; Error?: string }> }>("/ssh/batch-test-connection", data),

  // 生成SSH密钥对
  generateKeyPair: (data: GenerateKeyPairRequest) =>
    api.post<SshKeyPair>("/ssh/generate-keypair", data),

  // 验证SSH私钥
  validatePrivateKey: (data: ValidatePrivateKeyRequest) =>
    api.post<boolean>("/ssh/validate-privatekey", data),

  // 执行SSH命令
  executeCommand: (data: ExecuteCommandRequest) =>
    api.post<SshCommandResult>("/ssh/execute-command", data),

  // 上传文件到SSH服务器
  uploadFile: (data: UploadFileRequest) =>
    api.post<boolean>("/ssh/upload-file", data),

  // 从SSH服务器下载文件
  downloadFile: (data: DownloadFileRequest) =>
    api.post<boolean>("/ssh/download-file", data),

  // ==================== 连接配置管理 ====================

  // 获取连接配置列表
  getConnectionConfigs: (params?: { page?: number; pageSize?: number; search?: string }) =>
    api.get<{ items: SshConnectionConfig[]; total: number; page: number; pageSize: number }>("/ssh/connections", { params }),

  // 获取单个连接配置
  getConnectionConfig: (id: string) =>
    api.get<SshConnectionConfig>(`/ssh/connections/${id}`),

  // 创建连接配置
  createConnectionConfig: (data: Partial<SshConnectionConfig>) =>
    api.post<SshConnectionConfig>("/ssh/connections", data),

  // 更新连接配置
  updateConnectionConfig: (id: string, data: Partial<SshConnectionConfig>) =>
    api.put<SshConnectionConfig>(`/ssh/connections/${id}`, data),

  // 删除连接配置
  deleteConnectionConfig: (id: string) =>
    api.delete(`/ssh/connections/${id}`),

  // ==================== 密钥对管理 ====================

  // 获取密钥对列表
  getKeyPairs: (params?: { page?: number; pageSize?: number }) =>
    api.get<{ items: SshKeyPair[]; total: number; page: number; pageSize: number }>("/ssh/keypairs", { params }),

  // 导入密钥对
  importKeyPair: (data: { name: string; publicKey: string; privateKey?: string; passphrase?: string }) =>
    api.post<SshKeyPair>("/ssh/keypairs/import", data),

  // 删除密钥对
  deleteKeyPair: (id: string) =>
    api.delete(`/ssh/keypairs/${id}`),

  // ==================== 会话管理 ====================

  // 获取活跃会话列表
  getSessions: (params?: { page?: number; pageSize?: number }) =>
    api.get<{ items: SshSessionInfo[]; total: number; page: number; pageSize: number }>("/ssh/sessions", { params }),

  // 终止会话
  terminateSession: (id: string) =>
    api.delete(`/ssh/sessions/${id}`),

  // 重连会话
  reconnectSession: (id: string) =>
    api.post<SshSessionInfo>(`/ssh/sessions/${id}/reconnect`),

  // ==================== 目录操作 ====================

  // 列出远程目录
  listDirectory: (data: { connectionId: string; path: string }) =>
    api.post<RemoteFileInfo[]>("/ssh/list-directory", data),

  // 删除远程文件
  deleteRemoteFile: (data: { connectionId: string; path: string }) =>
    api.post<boolean>("/ssh/delete-remote-file", data),

  // ==================== 统计和日志 ====================

  // 获取统计信息
  getStatistics: () =>
    api.get<SshStatistics>("/ssh/statistics"),

  // 获取操作日志
  getOperationLogs: (params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    operation?: string;
    host?: string;
    status?: string;
    startDate?: string;
    endDate?: string;
  }) =>
    api.get<{ items: SshOperationLog[]; total: number; page: number; pageSize: number }>("/ssh/logs", { params }),

  // ==================== 设置管理 ====================

  // 获取SSH设置
  getSettings: () =>
    api.get<SshSettings>("/ssh/settings"),

  // 更新SSH设置
  updateSettings: (data: SshSettings) =>
    api.put<SshSettings>("/ssh/settings", data),

  // ==================== 连接指标 ====================

  // 获取连接指标
  getConnectionMetrics: (_host?: string, _port?: number) =>
    api.get<SshStatistics>("/ssh/statistics"),

  // ==================== 命令历史 ====================

  // 获取命令历史 (使用日志接口)
  getCommandHistory: (params?: { page?: number; pageSize?: number }) =>
    api.get<{ items: SshOperationLog[]; total: number; page: number; pageSize: number }>("/ssh/logs", {
      params: { ...params, operation: 'command' }
    }),

  // ==================== 文件传输 ====================

  // 获取文件传输列表 (使用日志接口)
  getFileTransfers: (params?: { page?: number; pageSize?: number }) =>
    api.get<{ items: SshOperationLog[]; total: number; page: number; pageSize: number }>("/ssh/logs", {
      params: { ...params, operation: 'upload,download' }
    }),

  // ==================== 主机密钥 ====================

  // 获取主机密钥
  getHostKeys: (params?: { page?: number; pageSize?: number; search?: string; trusted?: boolean }) =>
    api.get<{ items: SshHostKey[]; total: number; page: number; pageSize: number }>("/ssh/host-keys", { params }),

  // 添加或更新主机密钥
  addHostKey: (data: Partial<SshHostKey> & { host: string; keyData?: string }) =>
    api.post<SshHostKey>("/ssh/host-keys", {
      ...data,
      publicKey: data.publicKey ?? data.keyData ?? '',
      trusted: data.trusted ?? true
    }),

  // 删除主机密钥
  deleteHostKey: (id: string) =>
    api.delete(`/ssh/host-keys/${encodeURIComponent(id)}`),

  // ==================== 终端会话 ====================

  // 创建终端会话描述；实际连接由 SshTerminalHub 建立
  createTerminalSession: (data: string | Record<string, any>) =>
    api.post<SshTerminalSession>("/ssh/terminal-sessions", typeof data === 'string' ? { connectionId: data } : data),

  // ==================== 用户偏好设置 ====================

  // 获取用户偏好
  getUserPreferences: () =>
    api.get<SshSettings>("/ssh/settings"),

  // 更新用户偏好
  updateUserPreferences: (data: Partial<SshSettings>) =>
    api.put<SshSettings>("/ssh/settings", data),

  // ==================== 安全设置 ====================

  // 获取安全设置
  getSecuritySettings: () =>
    api.get<SshSettings>("/ssh/settings"),

  // 更新安全设置
  updateSecuritySettings: (data: Partial<SshSettings>) =>
    api.put<SshSettings>("/ssh/settings", data),

  // ==================== 全局配置 ====================

  // 获取全局配置
  getGlobalConfig: () =>
    api.get<SshSettings>("/ssh/settings")
}

export default sshApi