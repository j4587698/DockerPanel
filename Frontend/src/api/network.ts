/**
 * Networks API
 * 网络管理相关的API调用
 */

import api from "./index"
import type {
  NetworkInfo,
  NetworkDetailInfo,
  CreateNetworkRequest,
  UpdateNetworkRequest,
  NetworkPruneResult,
  NetworkStatistics,
  NetworkContainerInfo,
  NetworkConnectionConfig,
  NetworkIpamInfo,
  PruneNetworksRequest
} from "@/types/network"

// 网络API对象 - 对齐后端NetworkController
export const networkApi = {
  // 获取网络列表
  getNetworks: (params?: {
    nodeId?: string
    page?: number
    pageSize?: number
  }) => api.get<NetworkInfo[]>("/network", { params }),

  // 根据ID获取网络详情
  getNetwork: (networkId: string, params?: { nodeId?: string }) =>
    api.get<NetworkDetailInfo>(`/network/${networkId}`, { params }),

  // 创建网络
  createNetwork: (data: CreateNetworkRequest) =>
    api.post<NetworkInfo>("/network", data),

  // 删除网络
  deleteNetwork: (networkId: string, params?: { nodeId?: string }) =>
    api.delete(`/network/${networkId}`, { params }),

  // 更新网络配置
  updateNetwork: (networkId: string, data: UpdateNetworkRequest, params?: { nodeId?: string }) =>
    api.put<NetworkInfo>(`/network/${networkId}`, data, { params }),

  // 连接容器到网络
  connectContainerToNetwork: (
    networkId: string,
    containerId: string,
    config?: NetworkConnectionConfig,
    params?: { nodeId?: string }
  ) =>
    api.post(`/network/${networkId}/connect/${containerId}`, config, { params }),

  // 断开容器与网络的连接
  disconnectContainerFromNetwork: (networkId: string, containerId: string, params?: { nodeId?: string }) =>
    api.post(`/network/${networkId}/disconnect/${containerId}`, {}, { params }),

  // 获取网络中的容器列表
  getNetworkContainers: (networkId: string, params?: { nodeId?: string }) =>
    api.get<NetworkContainerInfo[]>(`/network/${networkId}/containers`, { params }),

  // 清理未使用的网络
  pruneNetworks: (data: PruneNetworksRequest) =>
    api.post<NetworkPruneResult>("/network/prune", data),

  // 获取网络统计信息
  getNetworkStatistics: (params?: { nodeId?: string }) =>
    api.get<NetworkStatistics>("/network/statistics", { params }),

  // 检查网络是否存在
  networkExists: (networkId: string, params?: { nodeId?: string }) =>
    api.get<boolean>(`/network/${networkId}/exists`, { params }),

  // 获取网络的IPAM信息
  getNetworkIpamInfo: (networkId: string, params?: { nodeId?: string }) =>
    api.get<NetworkIpamInfo>(`/network/${networkId}/ipam`, { params })
}

export default networkApi