/**
 * Volumes API
 * 卷管理相关的API调用
 */

import api from "./index"
import type {
  VolumeInfo,
  VolumeDetailInfo,
  CreateVolumeRequest,
  UpdateVolumeRequest,
  VolumePruneResult,
  VolumeStatistics,
  VolumeUsageInfo,
  VolumeBackupResult,
  VolumeRestoreResult,
  VolumeBackupInfo,
  PruneVolumesRequest
} from "@/types/volume"

type VolumeNodeParams = string | { nodeId?: string } | undefined
type VolumeListParams = string | { nodeId?: string; page?: number; pageSize?: number } | undefined

const normalizeNodeParams = (params?: VolumeNodeParams) =>
  typeof params === 'string' ? { nodeId: params } : params

const normalizeListParams = (params?: VolumeListParams) =>
  typeof params === 'string' ? { nodeId: params } : params

// 卷API对象 - 对齐后端VolumeController
export const volumeApi = {
  // 获取卷列表
  getVolumes: (params?: VolumeListParams) =>
    api.get<VolumeInfo[]>("/volumes", { params: normalizeListParams(params) }),

  // 根据ID获取卷详情
  getVolume: (volumeId: string, params?: VolumeNodeParams) =>
    api.get<VolumeDetailInfo>(`/volumes/${encodeURIComponent(volumeId)}`, { params: normalizeNodeParams(params) }),

  // 创建卷
  createVolume: (data: CreateVolumeRequest) =>
    api.post<VolumeInfo>("/volumes", data),

  // 删除卷
  deleteVolume: (volumeId: string, force = false, nodeId?: string) =>
    api.delete(`/volumes/${volumeId}`, { params: { force, nodeId } }),

  // 更新卷配置
  updateVolume: (volumeId: string, data: UpdateVolumeRequest, nodeId?: string) =>
    api.put<VolumeInfo>(`/volumes/${encodeURIComponent(volumeId)}`, data, { params: { nodeId } }),

  // 清理未使用的卷
  pruneVolumes: (data: PruneVolumesRequest) =>
    api.post<VolumePruneResult>("/volumes/prune", data),

  // 获取卷统计信息
  getVolumeStatistics: (nodeId?: string) =>
    api.get<VolumeStatistics>("/volumes/statistics", { params: { nodeId } }),

  // 检查卷是否存在
  volumeExists: (volumeId: string, nodeId?: string) =>
    api.get<boolean>(`/volumes/${encodeURIComponent(volumeId)}/exists`, { params: { nodeId } }),

  // 兼容旧名称
  checkVolumeExists: (volumeId: string, nodeId?: string) =>
    api.get<boolean>(`/volumes/${encodeURIComponent(volumeId)}/exists`, { params: { nodeId } }),

  // 获取卷使用情况
  getVolumeUsage: (volumeId: string, nodeId?: string) =>
    api.get<VolumeUsageInfo>(`/volumes/${encodeURIComponent(volumeId)}/usage`, { params: { nodeId } }),

  // 备份卷
  backupVolume: (volumeId: string, data: any) =>
    api.post<VolumeBackupResult>(`/volumes/${volumeId}/backup`, data),

  // 恢复卷
  restoreVolume: (data: any) =>
    api.post<VolumeRestoreResult>("/volumes/restore", data),

  // 从归档文件恢复创建新卷
  restoreFromArchive: (volumeName: string, file: File, nodeId?: string) => {
    const formData = new FormData()
    formData.append('volumeName', volumeName)
    formData.append('archive', file)
    return api.post<VolumeInfo>("/volumes/restore-from-archive", formData, {
      params: { nodeId },
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 0 // 禁用超时，大文件上传可能需要很长时间
    })
  },

  // 获取卷备份列表
  getVolumeBackups: (volumeId: string) =>
    api.get<VolumeBackupInfo[]>(`/volumes/${volumeId}/backups`),

  // 删除卷备份
  deleteVolumeBackup: (volumeId: string, backupId: string) =>
    api.delete(`/volumes/${volumeId}/backups/${backupId}`),

  // ========== 文件操作 API ==========

  // 获取卷文件列表
  getVolumeFiles: (volumeId: string, path: string = "/", nodeId?: string) =>
    api.get(`/volumes/${volumeId}/files`, { params: { path, nodeId } }),

  // 下载卷文件
  downloadVolumeFile: (volumeId: string, path: string, archive = false, nodeId?: string) =>
    api.get(`/volumes/${volumeId}/files/download`, { 
      params: { path, archive, nodeId }, 
      responseType: 'blob' 
    }),

  // 上传文件到卷
  uploadVolumeFile: (volumeId: string, path: string, file: File, nodeId?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    return api.post(`/volumes/${volumeId}/files/upload`, formData, {
      params: { path, nodeId },
      headers: { 'Content-Type': 'multipart/form-data' }
    })
  },

  // 在卷中创建文件夹
  createVolumeFolder: (volumeId: string, path: string, name: string, nodeId?: string) =>
    api.post(`/volumes/${volumeId}/files/folder`, { path, name }, { params: { nodeId } }),

  // 重命名卷文件
  renameVolumeFile: (volumeId: string, path: string, oldName: string, newName: string, nodeId?: string) =>
    api.put(`/volumes/${volumeId}/files/rename`, { path, oldName, newName }, { params: { nodeId } }),

  // 删除卷文件
  deleteVolumeFile: (volumeId: string, path: string, recursive = false, nodeId?: string) =>
    api.delete(`/volumes/${volumeId}/files`, { params: { path, recursive, nodeId } }),

  // 获取卷文件内容
  getVolumeFileContent: (volumeId: string, path: string, nodeId?: string) =>
    api.get<{ content: string; path: string }>(`/volumes/${volumeId}/files/content`, { params: { path, nodeId } }),

  // 保存卷文件内容
  saveVolumeFileContent: (volumeId: string, path: string, content: string, nodeId?: string) =>
    api.put(`/volumes/${volumeId}/files/content`, { path, content }, { params: { nodeId } })
}

export default volumeApi