// ==================== 通用类型定义 ====================

// API响应基础类型
export interface ApiResponse<T = any> {
  success: boolean
  data?: T
  message?: string
  error?: string
  code?: number
  timestamp?: string
}

// 分页相关类型
export interface PaginationParams {
  page?: number
  pageSize?: number
  offset?: number
  limit?: number
}

export interface PaginationResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
  totalPages?: number
  hasNext?: boolean
  hasPrev?: boolean
}

// 排序类型
export interface SortParams {
  sortBy?: string
  sortOrder?: 'asc' | 'desc'
}

// 过滤类型
export interface FilterParams {
  search?: string
  filters?: Record<string, any>
}

// 时间范围类型
export interface DateRange {
  startDate?: string | Date
  endDate?: string | Date
}

// 操作结果类型
export interface OperationResult {
  success: boolean
  message?: string
  data?: any
  errors?: string[]
}

// 批量操作类型
export interface BatchOperation<T> {
  ids: string[]
  operation: string
  data?: T
  options?: Record<string, any>
}

export interface BatchResult<T> {
  totalCount: number
  successCount: number
  failureCount: number
  results: Array<{
    id: string
    success: boolean
    data?: T
    error?: string
  }>
}

// 错误类型
export interface ApiError {
  code: string
  message: string
  details?: any
  timestamp?: string
  path?: string
}

// 通用状态枚举
export enum Status {
  ACTIVE = 'active',
  INACTIVE = 'inactive',
  PENDING = 'pending',
  RUNNING = 'running',
  STOPPED = 'stopped',
  ERROR = 'error',
  UNKNOWN = 'unknown'
}

// 优先级枚举
export enum Priority {
  LOW = 'low',
  MEDIUM = 'medium',
  HIGH = 'high',
  CRITICAL = 'critical'
}

// 日志级别枚举
export enum LogLevel {
  DEBUG = 'debug',
  INFO = 'info',
  WARN = 'warn',
  ERROR = 'error',
  FATAL = 'fatal'
}

// 通用配置类型
export interface BaseConfig {
  id: string
  name: string
  description?: string
  enabled: boolean
  createdAt: string
  updatedAt: string
  createdBy?: string
  updatedBy?: string
}

// 统计信息基础类型
export interface BaseStatistics {
  total: number
  active: number
  inactive: number
  lastUpdated: string
  nodeId?: string
}

// 健康检查类型
export interface HealthCheck {
  status: 'healthy' | 'unhealthy' | 'degraded'
  message?: string
  details?: Record<string, any>
  timestamp: string
}

// 标签类型
export interface Tag {
  key: string
  value: string
  color?: string
}

// 元数据类型
export interface Metadata {
  labels?: Record<string, string>
  annotations?: Record<string, string>
  tags?: Tag[]
}

// 资源限制类型
export interface ResourceLimits {
  memory?: string | number
  cpu?: string | number
  disk?: string | number
  bandwidth?: string | number
}

// 环境变量类型
export interface EnvironmentVariable {
  key: string
  value: string
  description?: string
  sensitive?: boolean
}

// 端口映射类型
export interface PortMapping {
  hostPort: number
  containerPort: number
  protocol: 'tcp' | 'udp'
  hostIp?: string
}

// 卷映射类型
export interface VolumeMapping {
  hostPath: string
  containerPath: string
  readOnly?: boolean
  type?: 'bind' | 'volume'
}

// 网络配置类型
export interface NetworkConfig {
  name: string
  driver?: string
  subnet?: string
  gateway?: string
  ipRange?: string
  labels?: Record<string, string>
}

// 权限类型
export interface Permission {
  resource: string
  actions: string[]
  effect: 'allow' | 'deny'
}

// 用户信息类型
export interface User {
  id: string
  username: string
  email?: string
  roles?: string[]
  permissions?: Permission[]
  lastLogin?: string
  isActive: boolean
}

// 会话信息类型
export interface Session {
  id: string
  userId: string
  token: string
  expiresAt: string
  createdAt: string
  lastActivity?: string
  userAgent?: string
  ipAddress?: string
}

// 通知类型
export interface Notification {
  id: string
  type: 'info' | 'warning' | 'error' | 'success'
  title: string
  message: string
  data?: any
  read: boolean
  createdAt: string
  userId?: string
}

// 文件上传类型
export interface FileUpload {
  file: File
  name?: string
  description?: string
  tags?: string[]
  metadata?: Record<string, any>
}

// 导入导出类型
export interface ImportExport {
  format: 'json' | 'csv' | 'xlsx' | 'yaml'
  data?: any
  filename?: string
  mimeType?: string
}

// 验证规则类型
export interface ValidationRule {
  field: string
  rules: Array<{
    type: 'required' | 'min' | 'max' | 'pattern' | 'email' | 'url' | 'custom'
  value?: any
    message?: string
  validator?: (value: any) => boolean | string
  }>
}

// 表单字段类型
export interface FormField {
  name: string
  label: string
  type: 'text' | 'number' | 'email' | 'password' | 'select' | 'checkbox' | 'radio' | 'textarea' | 'date' | 'file'
  required?: boolean
  disabled?: boolean
  placeholder?: string
  options?: Array<{ label: string; value: any }>
  validation?: ValidationRule[]
  defaultValue?: any
  description?: string
}

// 表单配置类型
export interface FormConfig {
  title: string
  description?: string
  fields: FormField[]
  layout?: 'horizontal' | 'vertical' | 'inline'
  submitText?: string
  cancelText?: string
  showReset?: boolean
  validation?: {
    validateOnChange?: boolean
    showErrors?: boolean
  }
}

// 图表数据类型
export interface ChartData {
  labels: string[]
  datasets: Array<{
    label: string
    data: number[]
    backgroundColor?: string | string[]
    borderColor?: string | string[]
    borderWidth?: number
    fill?: boolean
  }>
}

// 图表配置类型
export interface ChartConfig {
  type: 'line' | 'bar' | 'pie' | 'doughnut' | 'radar' | 'scatter'
  title?: string
  responsive?: boolean
  legend?: {
    display: boolean
    position?: 'top' | 'bottom' | 'left' | 'right'
  }
  scales?: Record<string, any>
  plugins?: Record<string, any>
}

// 表格列配置类型
export interface TableColumn {
  key: string
  title: string
  dataIndex?: string
  width?: number | string
  sortable?: boolean
  filterable?: boolean
  render?: (value: any, record: any) => any
  align?: 'left' | 'center' | 'right'
  fixed?: 'left' | 'right'
}

// 表格配置类型
export interface TableConfig {
  columns: TableColumn[]
  dataSource: any[]
  loading?: boolean
  pagination?: PaginationParams
  selection?: {
    enabled: boolean
    type: 'checkbox' | 'radio'
  }
  actions?: Array<{
    label: string
    key: string
    icon?: string
    handler: (record: any) => void
    disabled?: (record: any) => boolean
  }>
  scroll?: {
    x?: string | number
    y?: string | number
  }
}

// 搜索配置类型
export interface SearchConfig {
  placeholder?: string
  fields: Array<{
    key: string
    label: string
    type: 'text' | 'select' | 'date' | 'daterange'
    options?: Array<{ label: string; value: any }>
  }>
  filters?: Record<string, any>
  sort?: SortParams
}