// 容器模板相关类型定义

export interface ContainerTemplate {
  id: string
  name: string
  description: string
  category: TemplateCategory
  image: string
  ports: TemplatePortMapping[]
  volumes: TemplateVolumeMapping[]
  environment: Record<string, string>
  labels: Record<string, string>
  restartPolicy: RestartPolicy
  command?: string[]
  workingDir?: string
  user?: string
  hostConfig: TemplateHostConfig
  tags: string[]
  createdAt: string
  updatedAt: string
}

export interface TemplatePortMapping {
  containerPort: number
  hostPort?: number
  protocol: string
  hostIp?: string
  description?: string
}

export interface TemplateVolumeMapping {
  hostPath: string
  containerPath: string
  readOnly: boolean
  description?: string
}

export interface RestartPolicy {
  name: string
  maximumRetryCount: number
}

export interface TemplateHostConfig {
  autoRemove?: boolean
  privileged?: boolean
  publishAllPorts?: boolean
  readonlyRootfs?: boolean
  dns?: string[]
  dnsSearch?: string[]
  extraHosts?: string[]
  networkMode?: string
  ipcMode?: string
  pidMode?: string
  utsMode?: string
  logConfig?: LogConfig
  securityOpt?: string[]
  storageOpt?: Record<string, string>
  cgroupParent?: string
  cgroupnsMode?: string
  deviceRequests?: DeviceRequest[]
  kernelMemory?: number
  kernelMemoryTCP?: number
  memoryReservation?: number
  memorySwap?: number
  memorySwappiness?: number
  nanoCpus?: number
  blkioWeight?: number
  blkioWeightDevice?: BlkioWeightDevice[]
  deviceReadBps?: BlkioDeviceRate[]
  deviceWriteBps?: BlkioDeviceRate[]
  deviceReadIOps?: BlkioDeviceRate[]
  deviceWriteIOps?: BlkioDeviceRate[]
  oomKillDisable?: boolean
  ulimits?: Ulimit[]
  pidsLimit?: number
  cpuCount?: number
  cpuPercent?: number
  ioMaximumIOps?: number
  ioMaximumBandwidth?: number
}

export interface DeviceRequest {
  driver: string
  count: number
  deviceIDs: string[]
  capabilities?: string[]
  options?: Record<string, string>
}

export interface BlkioWeightDevice {
  path: string
  weight: number
}

export interface BlkioDeviceRate {
  path: string
  rate: number
}

export interface Ulimit {
  name: string
  soft: number
  hard: number
}

export interface LogConfig {
  type: string
  config?: Record<string, string>
}

export type TemplateCategory =
  | 'Web应用'
  | '数据库'
  | '缓存服务'
  | '消息队列'
  | '监控工具'
  | '开发环境'
  | '自定义'

export interface TemplateCreateRequest {
  name: string
  description: string
  category: TemplateCategory
  image: string
  ports: TemplatePortMapping[]
  volumes: TemplateVolumeMapping[]
  environment: Record<string, string>
  labels: Record<string, string>
  restartPolicy: RestartPolicy
  command?: string[]
  workingDir?: string
  user?: string
  hostConfig: TemplateHostConfig
  tags: string[]
}

export interface TemplateUpdateRequest {
  name?: string
  description?: string
  category?: TemplateCategory
  image?: string
  ports?: TemplatePortMapping[]
  volumes?: TemplateVolumeMapping[]
  environment?: Record<string, string>
  labels?: Record<string, string>
  restartPolicy?: RestartPolicy
  command?: string[]
  workingDir?: string
  user?: string
  hostConfig?: TemplateHostConfig
  tags?: string[]
}

export interface TemplateImportExport {
  version: string
  templates: ContainerTemplate[]
  exportedAt: string
  exportedBy?: string
}