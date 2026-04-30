import api from './index'

export interface OperationAuditLog {
  id: string
  timestamp: string
  operationType: string
  resourceType: string
  resourceId?: string | null
  method: string
  path: string
  controller?: string | null
  action?: string | null
  nodeId?: string | null
  status: string
  statusCode: number
  durationMs: number
  clientIp?: string | null
  userAgent?: string | null
  errorMessage?: string | null
  routeValues: Record<string, string>
  query: Record<string, string>
}

export interface OperationAuditLogFilter {
  search?: string
  operationType?: string
  resourceType?: string
  resourceId?: string
  status?: string
  nodeId?: string
  startDate?: string
  endDate?: string
  page?: number
  pageSize?: number
}

export interface OperationAuditLogPage {
  items: OperationAuditLog[]
  total: number
  page: number
  pageSize: number
}

export const auditApi = {
  getLogs(params: OperationAuditLogFilter = {}) {
    return api.get<OperationAuditLogPage>('/audit/logs', { params }) as unknown as Promise<OperationAuditLogPage>
  },

  getLog(id: string) {
    return api.get<OperationAuditLog>(`/audit/logs/${id}`) as unknown as Promise<OperationAuditLog>
  }
}

export default auditApi