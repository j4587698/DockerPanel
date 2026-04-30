/**
 * Images API
 * 镜像管理相关的API调用，兼容旧版 `image.ts` 和 `images.ts` 调用签名。
 */

import api from "./index"
import type {
  ImageInfo,
  ImageDetailInfo,
  ImageSearchResult,
  ImageHistoryEntry,
  ImageLayersInfo,
  ImageStatistics,
  BuildImageRequest,
  BatchRemoveImagesRequest,
  BatchOperationResult,
  PruneResult,
  PruneImagesRequest
} from "@/types/image"

export type {
  ImageInfo,
  ImageDetailInfo,
  ImageSearchResult,
  ImageHistoryEntry,
  ImageLayersInfo,
  ImageStatistics,
  BuildImageRequest,
  TagImageRequest,
  PushImageRequest,
  BatchRemoveImagesRequest,
  BatchOperationResult,
  PruneResult,
  PruneImagesRequest
} from "@/types/image"

type NodeParams = string | { nodeId?: string } | undefined
type ImageSearchParams = string | {
  term: string
  limit?: number
  page?: number
  pageSize?: number
  nodeId?: string
}

const normalizeNodeParams = (params?: NodeParams) =>
  typeof params === 'string' ? { nodeId: params } : params

const normalizeSearchParams = (params: ImageSearchParams, limit = 25) =>
  typeof params === 'string' ? { term: params, limit } : params

// 镜像API对象 - 对齐后端ImageController
export const imageApi = {
  // 获取镜像列表
  getImages: (params?: {
    nodeId?: string
    all?: boolean
    filters?: Record<string, string[]>
    page?: number
    pageSize?: number
  }) => api.get<ImageInfo[]>("/images", { params }),

  // 根据ID获取镜像详情
  getImage: (imageId: string, params?: NodeParams) =>
    api.get<ImageDetailInfo>(`/images/${encodeURIComponent(imageId)}`, { params: normalizeNodeParams(params) }),

  // 获取镜像 inspect 详情
  inspectImage: (imageId: string, params?: NodeParams) =>
    api.get(`/images/${encodeURIComponent(imageId)}/inspect`, { params: normalizeNodeParams(params) }),

  // 拉取镜像
  pullImage: (data: {
    imageName: string
    tag?: string
    registry?: string | null
    nodeId?: string
    connectionId?: string
  }) => api.post("/images/pull", data, { timeout: 600000 }),

  // 删除镜像
  removeImage: (imageId: string, params?: { force?: boolean; noprune?: boolean; nodeId?: string }) =>
    api.delete(`/images/${encodeURIComponent(imageId)}`, { params }),

  // 标记镜像
  tagImage: (sourceImageId: string, data: {
    targetRepository: string
    targetTag?: string
    nodeId?: string
  }) => api.post(`/images/${encodeURIComponent(sourceImageId)}/tag`, data),

  // 推送镜像
  pushImage: (imageName: string, data?: { tag?: string; nodeId?: string }) =>
    api.post(`/images/${encodeURIComponent(imageName)}/push`, data),

  // 搜索镜像
  searchImages: (params: ImageSearchParams, limit = 25) =>
    api.get<ImageSearchResult[]>("/images/search", {
      params: normalizeSearchParams(params, limit),
      paramsSerializer: { indexes: null }
    }),

  // 获取镜像构建历史
  getImageHistory: (imageId: string, params?: NodeParams) =>
    api.get<ImageHistoryEntry[]>(`/images/${encodeURIComponent(imageId)}/history`, { params: normalizeNodeParams(params) }),

  // 构建镜像
  buildImage: (data: BuildImageRequest | FormData) => {
    if (data instanceof FormData) {
      return api.post("/images/build", data, {
        headers: { 'Content-Type': undefined },
        timeout: 0
      })
    }

    return api.post("/images/build", data)
  },

  // 获取镜像分层信息
  getImageLayers: (imageId: string, params?: NodeParams) =>
    api.get<ImageLayersInfo>(`/images/${encodeURIComponent(imageId)}/layers`, { params: normalizeNodeParams(params) }),

  // 导出镜像 URL
  exportImage: (imageId: string, nodeId?: string) =>
    `/api/images/${encodeURIComponent(imageId)}/export${nodeId ? `?nodeId=${encodeURIComponent(nodeId)}` : ''}`,

  // 导入镜像
  importImage: (file: File, nodeId?: string) => {
    const formData = new FormData()
    formData.append('file', file)
    return api.post("/images/import", formData, {
      params: { nodeId },
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 0
    })
  },

  // 批量删除镜像
  batchRemoveImages: (data: BatchRemoveImagesRequest) =>
    api.delete<BatchOperationResult>("/images/batch", { data }),

  // 兼容旧名称
  batchRemove: (data: { imageIds: string[]; force?: boolean; nodeId?: string }) =>
    api.delete<BatchOperationResult>("/images/batch", { data }),

  // 获取镜像统计信息
  getImageStatistics: (params?: { nodeId?: string }) =>
    api.get<ImageStatistics>("/images/statistics", { params }),

  // 兼容旧名称
  getStatistics: (nodeId?: string) =>
    api.get<ImageStatistics>("/images/statistics", { params: { nodeId } }),

  // 清理未使用的镜像
  pruneImages: (data?: PruneImagesRequest | {
    filters?: Record<string, string[]>
    all?: boolean
    dangling?: boolean
    nodeId?: string
  }) => api.post<PruneResult>("/images/prune", data)
}

export default imageApi
