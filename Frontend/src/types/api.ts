// ==================== API相关类型定义 ====================

import { PaginationParams, PaginationResponse, OperationResult } from './common'

// API客户端配置
export interface ApiClientConfig {
  baseURL?: string
  timeout?: number
  headers?: Record<string, string>
  interceptors?: {
    request?: (config: any) => any
    response?: (response: any) => any
    error?: (error: any) => any
  }
}

// HTTP方法枚举
export enum HttpMethod {
  GET = 'GET',
  POST = 'POST',
  PUT = 'PUT',
  DELETE = 'DELETE',
  PATCH = 'PATCH',
  HEAD = 'HEAD',
  OPTIONS = 'OPTIONS'
}

// API请求配置
export interface ApiRequestConfig {
  method?: HttpMethod
  url?: string
  data?: any
  params?: Record<string, any>
  headers?: Record<string, string>
  timeout?: number
  responseType?: 'json' | 'blob' | 'text' | 'arraybuffer'
  onUploadProgress?: (progressEvent: any) => void
  onDownloadProgress?: (progressEvent: any) => void
}

// API响应类型
export interface ApiResponse<T = any> {
  data?: T
  status: number
  statusText: string
  headers: Record<string, string>
  config: ApiRequestConfig
  request?: any
}

// 错误响应类型
export interface ApiErrorResponse {
  message: string
  code?: string | number
  details?: any
  stack?: string
  timestamp?: string
  path?: string
  method?: string
  params?: any
}

// WebSocket消息类型
export interface WebSocketMessage<T = any> {
  type: string
  data: T
  id?: string
  timestamp?: string
}

// 实时数据更新类型
export interface RealtimeUpdate<T = any> {
  type: 'create' | 'update' | 'delete' | 'status'
  resource: string
  data: T
  timestamp: string
}

// 批量操作API类型
export interface BatchApiRequest<T = any> {
  operation: string
  items: T[]
  options?: Record<string, any>
}

export interface BatchApiResponse<T = any> {
  totalCount: number
  successCount: number
  failureCount: number
  results: Array<{
    item: T
    success: boolean
    error?: string
  }>
}

// 文件上传API类型
export interface FileUploadRequest {
  file: File
  field?: string
  metadata?: Record<string, any>
  onProgress?: (progress: number) => void
}

export interface FileUploadResponse {
  id: string
  filename: string
  size: number
  url?: string
  metadata?: Record<string, any>
}

// 导入导出API类型
export interface ExportRequest {
  format: 'json' | 'csv' | 'xlsx' | 'yaml'
  filters?: Record<string, any>
  fields?: string[]
}

export interface ImportRequest {
  file: File
  format: 'json' | 'csv' | 'xlsx' | 'yaml'
  options?: Record<string, any>
  preview?: boolean
}

export interface ImportPreviewResponse {
  headers: string[]
  data: any[]
  errors: string[]
  totalRows: number
}

// 搜索API类型
export interface SearchRequest {
  query?: string
  filters?: Record<string, any>
  sort?: {
    field: string
    order: 'asc' | 'desc'
  }
  pagination?: PaginationParams
}

export interface SearchResponse<T> {
  items: T[]
  total: number
  pagination: PaginationResponse<T>
  suggestions?: string[]
  facets?: Record<string, Array<{
    value: string
    count: number
  }>>
}

// 统计API类型
export interface StatisticsRequest {
  period?: string
  filters?: Record<string, any>
  groupBy?: string[]
  metrics?: string[]
}

export interface StatisticsResponse {
  period: string
  metrics: Record<string, number | string>
  charts: Array<{
    type: string
    title: string
    data: any
  }>
  summary: Record<string, any>
}

// 健康检查API类型
export interface HealthCheckRequest {
  detailed?: boolean
  components?: string[]
}

export interface HealthCheckResponse {
  status: 'healthy' | 'unhealthy' | 'degraded'
  timestamp: string
  uptime: number
  version?: string
  components?: Array<{
    name: string
    status: 'healthy' | 'unhealthy' | 'degraded'
    details?: any
    lastCheck: string
  }>
  checks?: Record<string, boolean>
}

// 配置API类型
export interface ConfigRequest {
  scope?: string
  keys?: string[]
}

export interface ConfigResponse {
  config: Record<string, any>
  metadata?: {
    version: string
    lastModified: string
    environment: string
  }
}

// 通知API类型
export interface NotificationRequest {
  type: 'info' | 'warning' | 'error' | 'success'
  title: string
  message: string
  data?: any
  channels?: string[]
  priority?: 'low' | 'medium' | 'high' | 'critical'
  scheduled?: string
}

export interface NotificationResponse {
  id: string
  status: 'sent' | 'failed' | 'pending'
  channels: Array<{
    channel: string
    status: 'sent' | 'failed'
    error?: string
  }>
  timestamp: string
}

// 缓存API类型
export interface CacheRequest {
  key?: string
  pattern?: string
}

export interface CacheResponse {
  keys: string[]
  stats: {
    totalKeys: number
    totalSize: number
    hitRate: number
    missRate: number
  }
}

// 日志API类型
export interface LogRequest {
  level?: string
  source?: string
  startTime?: string
  endTime?: string
  limit?: number
  offset?: number
  search?: string
}

export interface LogResponse {
  logs: Array<{
    timestamp: string
    level: string
    source: string
    message: string
    metadata?: Record<string, any>
  }>
  total: number
  hasMore: boolean
}

// 用户认证API类型
export interface LoginRequest {
  username: string
  password: string
  remember?: boolean
  captcha?: string
}

export interface LoginResponse {
  user: {
    id: string
    username: string
    email?: string
    roles: string[]
    permissions: string[]
  }
  token: string
  refreshToken: string
  expiresAt: string
}

export interface RefreshTokenRequest {
  refreshToken: string
}

export interface RefreshTokenResponse {
  token: string
  refreshToken: string
  expiresAt: string
}

export interface LogoutRequest {
  allDevices?: boolean
}

// 权限验证API类型
export interface PermissionRequest {
  resource: string
  action: string
  context?: Record<string, any>
}

export interface PermissionResponse {
  allowed: boolean
  reason?: string
  conditions?: Array<{
    type: string
    value: any
    operator: string
  }>
}

// API端点类型
export interface ApiEndpoint {
  path: string
  method: HttpMethod
  summary?: string
  description?: string
  parameters?: Array<{
    name: string
    in: 'path' | 'query' | 'header' | 'cookie'
    required: boolean
    type: string
    description?: string
    schema?: any
  }>
  requestBody?: {
    content?: Record<string, any>
    description?: string
    required?: boolean
  }
  responses?: Record<string, any>
  tags?: string[]
  security?: Array<{
    type: string
    name: string
  }>
}

// API文档类型
export interface ApiDocumentation {
  title: string
  version: string
  description?: string
  baseUrl?: string
  endpoints: ApiEndpoint[]
  schemas?: Record<string, any>
  securityDefinitions?: Record<string, any>
}

// API测试类型
export interface ApiTestCase {
  name: string
  endpoint: string
  method: HttpMethod
  description?: string
  request?: {
    data?: any
    params?: Record<string, any>
    headers?: Record<string, string>
  }
  expectedResponse?: {
    status: number
    data?: any
    headers?: Record<string, string>
  }
  timeout?: number
}

export interface ApiTestResult {
  name: string
  status: 'passed' | 'failed' | 'error'
  duration: number
  response?: {
    status: number
    data?: any
    headers?: Record<string, string>
  }
  error?: string
  timestamp: string
}

// API监控类型
export interface ApiMetrics {
  endpoint: string
  method: HttpMethod
  requests: number
  responses: number
  errors: number
  averageResponseTime: number
  lastAccess: string
  status: 'healthy' | 'degraded' | 'unhealthy'
}

export interface ApiMetricsSummary {
  totalRequests: number
  totalErrors: number
  averageResponseTime: number
  endpoints: ApiMetrics[]
  timestamp: string
}