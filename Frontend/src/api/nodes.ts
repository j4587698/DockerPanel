import api from "./index"

// 连接类型枚举
export enum DockerConnectionType {
  Local = 'Local',
  Tcp = 'Tcp',
  Tls = 'Tls',
  SshTunnel = 'SshTunnel'
}

// 节点相关的类型定义
export interface NodeInfo {
  id: string
  name?: string
  host?: string
  port: number
  engineType?: string
  version?: string
  status?: string
  createdAt: Date
  lastSeen?: Date
  labels: Record<string, string>
  useSsh: boolean
  username?: string
  password?: string
  privateKeyPath?: string
  engineInfo?: EngineInfo
  // 新增字段
  connectionType?: DockerConnectionType
  dockerEndpoint?: string
  groupId?: string
  groupName?: string
  tags?: string[]
  isDefault?: boolean
  sortOrder?: number
  description?: string
  tlsConfig?: NodeTlsConfig
  sshTunnelConfig?: NodeSshTunnelConfig
  connectionTimeout?: number
  enableHealthCheck?: boolean
  healthCheckInterval?: number
  isOnline?: boolean
}

export interface NodeTlsConfig {
  enabled: boolean
  caCertPath?: string
  clientCertPath?: string
  clientKeyPath?: string
  skipVerify?: boolean
  serverName?: string
}

export interface NodeSshTunnelConfig {
  sshHost: string
  sshPort: number
  sshUsername: string
  sshPassword?: string
  sshPrivateKeyPath?: string
  sshPrivateKeyPassphrase?: string
  remoteDockerSocket: string
  localForwardPort?: number
  sshConnectionId?: string
}

export interface EngineInfo {
  engineVersion?: string
  apiVersion?: string
  buildTime?: string
  gitCommit?: string
  goVersion?: string
  osType?: string
  architecture?: string
  kernelVersion?: string
  containers: number
  containersRunning: number
  containersPaused: number
  containersStopped: number
  images: number
}

export interface AddNodeRequest {
  name: string
  host: string
  port: number
  engineType?: string
  connectionType?: DockerConnectionType
  dockerEndpoint?: string
  groupId?: string
  tags?: string[]
  labels?: Record<string, string>
  isDefault?: boolean
  description?: string
  connectionTimeout?: number
  enableHealthCheck?: boolean
  healthCheckInterval?: number
  username?: string
  password?: string
  tlsConfig?: NodeTlsConfig
  useSsh?: boolean
  sshPort?: number
  sshUsername?: string
  sshPassword?: string
  sshPrivateKeyPath?: string
  sshPrivateKeyPassphrase?: string
  remoteDockerSocket?: string
  sshConnectionId?: string
}

export interface UpdateNodeRequest {
  name?: string
  host?: string
  port?: number
  engineType?: string
  connectionType?: DockerConnectionType
  dockerEndpoint?: string
  groupId?: string
  tags?: string[]
  labels?: Record<string, string>
  isDefault?: boolean
  description?: string
  connectionTimeout?: number
  enableHealthCheck?: boolean
  healthCheckInterval?: number
  username?: string
  password?: string
  tlsConfig?: NodeTlsConfig
  useSsh?: boolean
  sshPort?: number
  sshUsername?: string
  sshPassword?: string
  sshPrivateKeyPath?: string
  sshPrivateKeyPassphrase?: string
  remoteDockerSocket?: string
  sshConnectionId?: string
}

export interface TestNodeConnectionRequest {
  host?: string
  port?: number
  connectionType?: DockerConnectionType
  dockerEndpoint?: string
  connectionTimeout?: number
  username?: string
  password?: string
  tlsConfig?: NodeTlsConfig
  useSsh?: boolean
  sshPort?: number
  sshUsername?: string
  sshPassword?: string
  sshPrivateKeyPath?: string
  remoteDockerSocket?: string
}

export interface TestNodeConnectionResult {
  success: boolean
  message?: string
  dockerVersion?: string
  apiVersion?: string
  os?: string
  architecture?: string
  responseTimeMs?: number
  errorMessage?: string
}

export interface NodeStats {
  nodeId: string
  containerCount: number
  runningContainerCount: number
  stoppedContainerCount: number
  imageCount: number
  networkCount: number
  volumeCount: number
  cpuUsage: number
  memoryUsage: number
  memoryTotal: number
  diskUsage: number
  diskTotal: number
  timestamp: Date
}

export interface NodeHealthStatus {
  nodeId: string
  status: string
  message?: string
  lastCheck: Date
  isHealthy: boolean
  checks: Record<string, boolean>
}

export interface NodeGroup {
  id: string
  name: string
  description?: string
  nodeIds: string[]
  labels: Record<string, string>
  createdAt: Date
  updatedAt: Date
  isActive: boolean
  nodeCount: number
  onlineNodeCount: number
  color?: string
  icon?: string
  sortOrder?: number
  parentGroupId?: string
}

export interface CreateNodeGroupRequest {
  name: string
  description?: string
  nodeIds?: string[]
  labels?: Record<string, string>
}

export interface UpdateNodeGroupRequest {
  name?: string
  description?: string
  nodeIds?: string[]
  labels?: Record<string, string>
  isActive?: boolean
}

export interface BatchNodeOperationRequest {
  nodeIds: string[]
  operation: string
  parameters?: Record<string, any>
}

export interface BatchNodeOperationResult {
  nodeId: string
  success: boolean
  connected?: boolean
  error?: string
}

// 节点API服务
export const nodeApi = {
  // 获取节点列表
  getNodes: (page?: number, pageSize?: number) => api.get<NodeInfo[]>("/nodes", { params: { page, pageSize } }),

  // 根据ID获取节点
  getNode: (id: string) => api.get<NodeInfo>(`/nodes/${id}`),

  // 添加节点
  addNode: (data: AddNodeRequest) =>
    api.post<string>("/nodes", data),

  // 更新节点
  updateNode: (id: string, data: UpdateNodeRequest) =>
    api.put(`/nodes/${id}`, data),

  // 删除节点
  removeNode: (id: string) =>
    api.delete(`/nodes/${id}`),

  // 测试节点连接
  testNodeConnection: (id: string) =>
    api.post<boolean>(`/nodes/${id}/test-connection`),

  // 测试连接参数（不保存）
  testConnection: (data: TestNodeConnectionRequest) =>
    api.post<TestNodeConnectionResult>("/nodes/test-connection", data),

  // 获取节点统计信息
  getNodeStats: (id: string) =>
    api.get<NodeStats>(`/nodes/${id}/stats`),

  // 获取节点详细信息
  getNodeInfo: (id: string) =>
    api.get<NodeInfo>(`/nodes/${id}/info`),

  // 获取节点健康状态
  getNodeHealthStatus: (id: string) =>
    api.get<NodeHealthStatus>(`/nodes/${id}/health`),

  // 批量节点操作
  batchOperation: (data: BatchNodeOperationRequest) =>
    api.post<{ results: BatchNodeOperationResult[] }>("/nodes/batch", data),

  // 获取默认节点
  getDefaultNode: () =>
    api.get<NodeInfo>("/nodes/default"),

  // 设置默认节点
  setDefaultNode: (id: string) =>
    api.post(`/nodes/${id}/set-default`),

  // 分组管理
  getGroups: () =>
    api.get<NodeGroup[]>("/nodes/groups"),

  getGroup: (id: string) =>
    api.get<NodeGroup>(`/nodes/groups/${id}`),

  createGroup: (data: CreateNodeGroupRequest) =>
    api.post<NodeGroup>("/nodes/groups", data),

  updateGroup: (id: string, data: UpdateNodeGroupRequest) =>
    api.put(`/nodes/groups/${id}`, data),

  deleteGroup: (id: string) =>
    api.delete(`/nodes/groups/${id}`)
}

export default nodeApi