import api from "./index"

export interface AlertThresholds {
  cpu: number
  memory: number
  disk: number
}

export interface PanelGeneralSettings {
  systemName: string
  systemDescription: string
  adminEmail: string
  defaultLanguage: string
  defaultTimezone: string
}

export interface PanelSecuritySettings {
  sessionTimeout: number
  sessionTimeoutMinutes: number
  maxLoginAttempts: number
  lockoutDurationMinutes: number
  passwordMinLength: number
  passwordRequireUppercase: boolean
  passwordRequireLowercase: boolean
  passwordRequireNumbers: boolean
  passwordRequireSpecialChars: boolean
  enableTwoFactorAuth: boolean
}

export interface PanelMonitoringSettings {
  metricsEnabled: boolean
  healthChecksEnabled: boolean
  alertsEnabled: boolean
  metricsRetentionDays: number
  metricsCollectionIntervalSeconds: number
  alertThresholds: AlertThresholds
}

export interface PanelUiSettings {
  theme: "light" | "dark" | "auto"
  refreshInterval: number
  defaultPageSize: number
}

export interface PanelLoggingSettings {
  logLevel: string
  logRetentionDays: number
}

export interface PanelSystemSettings {
  id: string
  general: PanelGeneralSettings
  security: PanelSecuritySettings
  monitoring: PanelMonitoringSettings
  ui: PanelUiSettings
  logging: PanelLoggingSettings
  updatedAt: string
}

export interface PublicSystemSettings {
  systemName: string
  systemDescription: string
  defaultLanguage: string
  defaultTimezone: string
  theme: "light" | "dark" | "auto"
  refreshInterval: number
  defaultPageSize: number
}

export const settingsApi = {
  getSystemSettings: () =>
    api.get<PanelSystemSettings>("/settings/system"),

  getPublicSettings: () =>
    api.get<PublicSystemSettings>("/settings/public", { skipErrorHandler: true } as any),

  updateSystemSettings: (settings: PanelSystemSettings) =>
    api.put<PanelSystemSettings>("/settings/system", settings),

  resetSystemSettings: () =>
    api.post<PanelSystemSettings>("/settings/system/reset"),

  exportSettings: () =>
    api.get<Blob>("/settings/system/export", { responseType: "blob" }),

  importSettings: (file: File) => {
    const formData = new FormData()
    formData.append("file", file)
    return api.post<PanelSystemSettings>("/settings/system/import", formData, {
      headers: {
        "Content-Type": "multipart/form-data"
      }
    })
  }
}

export default settingsApi
