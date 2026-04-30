/**
 * Settings Store
 * 用户设置管理：优先读写后端系统设置，失败时保留 localStorage 兜底。
 */

import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import { settingsApi, type PanelSystemSettings, type PanelUiSettings, type PublicSystemSettings } from '@/api/settings'

export interface AlertThresholds {
  cpu: number
  memory: number
  disk: number
}

const STORAGE_KEY = 'dockerpanel-settings'

// 默认设置
const defaultSettings = {
  systemName: 'DockerPanel',
  systemDescription: 'Docker容器管理平台',
  adminEmail: '',
  defaultLanguage: 'zh-CN',
  defaultTimezone: 'Asia/Shanghai',
  refreshInterval: 3000,
  defaultPageSize: 20,
  metricsEnabled: true,
  healthChecksEnabled: true,
  alertsEnabled: true,
  metricsRetentionDays: 30,
  metricsCollectionIntervalSeconds: 5,
  alertThresholds: { cpu: 80, memory: 80, disk: 90 },
  logLevel: 'Information',
  logRetentionDays: 7,
  sessionTimeout: 3600,
  maxLoginAttempts: 5,
  lockoutDurationMinutes: 15,
  passwordMinLength: 8,
  passwordRequireUppercase: true,
  passwordRequireLowercase: true,
  passwordRequireNumbers: true,
  passwordRequireSpecialChars: true,
  enableTwoFactorAuth: false
}

// 从 localStorage 加载设置
function loadFromStorage() {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    if (saved) {
      return { ...defaultSettings, ...JSON.parse(saved) }
    }
  } catch (e) {
    console.warn('Failed to load settings from localStorage:', e)
  }
  return { ...defaultSettings }
}

export const useSettingsStore = defineStore('settings', () => {
  // 从 localStorage 加载初始值
  const saved = loadFromStorage()

  // 状态
  const systemName = ref<string>(saved.systemName)
  const systemDescription = ref<string>(saved.systemDescription)
  const adminEmail = ref<string>(saved.adminEmail)
  const defaultLanguage = ref<string>(saved.defaultLanguage)
  const defaultTimezone = ref<string>(saved.defaultTimezone)
  const refreshInterval = ref<number>(saved.refreshInterval)
  const defaultPageSize = ref<number>(saved.defaultPageSize)
  const metricsEnabled = ref<boolean>(saved.metricsEnabled)
  const healthChecksEnabled = ref<boolean>(saved.healthChecksEnabled)
  const alertsEnabled = ref<boolean>(saved.alertsEnabled)
  const metricsRetentionDays = ref<number>(saved.metricsRetentionDays)
  const metricsCollectionIntervalSeconds = ref<number>(saved.metricsCollectionIntervalSeconds)
  const alertThresholds = ref<AlertThresholds>(saved.alertThresholds)
  const logLevel = ref<string>(saved.logLevel)
  const logRetentionDays = ref<number>(saved.logRetentionDays)
  const sessionTimeout = ref<number>(saved.sessionTimeout)
  const maxLoginAttempts = ref<number>(saved.maxLoginAttempts)
  const lockoutDurationMinutes = ref<number>(saved.lockoutDurationMinutes)
  const passwordMinLength = ref<number>(saved.passwordMinLength)
  const passwordRequireUppercase = ref<boolean>(saved.passwordRequireUppercase)
  const passwordRequireLowercase = ref<boolean>(saved.passwordRequireLowercase)
  const passwordRequireNumbers = ref<boolean>(saved.passwordRequireNumbers)
  const passwordRequireSpecialChars = ref<boolean>(saved.passwordRequireSpecialChars)
  const enableTwoFactorAuth = ref<boolean>(saved.enableTwoFactorAuth)
  const loading = ref(false)
  const currentSettings = ref<PanelSystemSettings | null>(null)

  // 持久化到 localStorage
  const saveToStorage = () => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({
        systemName: systemName.value,
        systemDescription: systemDescription.value,
        adminEmail: adminEmail.value,
        defaultLanguage: defaultLanguage.value,
        defaultTimezone: defaultTimezone.value,
        refreshInterval: refreshInterval.value,
        defaultPageSize: defaultPageSize.value,
        metricsEnabled: metricsEnabled.value,
        healthChecksEnabled: healthChecksEnabled.value,
        alertsEnabled: alertsEnabled.value,
        metricsRetentionDays: metricsRetentionDays.value,
        metricsCollectionIntervalSeconds: metricsCollectionIntervalSeconds.value,
        alertThresholds: alertThresholds.value,
        logLevel: logLevel.value,
        logRetentionDays: logRetentionDays.value,
        sessionTimeout: sessionTimeout.value,
        maxLoginAttempts: maxLoginAttempts.value,
        lockoutDurationMinutes: lockoutDurationMinutes.value,
        passwordMinLength: passwordMinLength.value,
        passwordRequireUppercase: passwordRequireUppercase.value,
        passwordRequireLowercase: passwordRequireLowercase.value,
        passwordRequireNumbers: passwordRequireNumbers.value,
        passwordRequireSpecialChars: passwordRequireSpecialChars.value,
        enableTwoFactorAuth: enableTwoFactorAuth.value
      }))
    } catch (e) {
      console.warn('Failed to save settings to localStorage:', e)
    }
  }

  // 监听变化自动保存本地副本
  watch([
    systemName,
    systemDescription,
    adminEmail,
    defaultLanguage,
    defaultTimezone,
    refreshInterval,
    defaultPageSize,
    metricsEnabled,
    healthChecksEnabled,
    alertsEnabled,
    metricsRetentionDays,
    metricsCollectionIntervalSeconds,
    alertThresholds,
    logLevel,
    logRetentionDays,
    sessionTimeout,
    maxLoginAttempts,
    lockoutDurationMinutes,
    passwordMinLength,
    passwordRequireUppercase,
    passwordRequireLowercase,
    passwordRequireNumbers,
    passwordRequireSpecialChars,
    enableTwoFactorAuth
  ], saveToStorage, { deep: true })

  // 设置方法
  const setSystemName = (name: string) => {
    systemName.value = name
    document.title = name ? `${name} - DockerPanel` : 'DockerPanel'
  }

  const setGeneralSettings = (settings: {
    systemName: string
    systemDescription: string
    adminEmail: string
    defaultLanguage: string
    defaultTimezone: string
  }) => {
    systemName.value = settings.systemName
    systemDescription.value = settings.systemDescription
    adminEmail.value = settings.adminEmail
    defaultLanguage.value = settings.defaultLanguage
    defaultTimezone.value = settings.defaultTimezone
    document.title = systemName.value ? `${systemName.value} - DockerPanel` : 'DockerPanel'
  }

  const setRefreshInterval = (interval: number) => {
    refreshInterval.value = interval
  }

  const setDefaultPageSize = (size: number) => {
    defaultPageSize.value = size
  }

  const setMetricsEnabled = (enabled: boolean) => {
    metricsEnabled.value = enabled
  }

  const setMonitoringSettings = (settings: {
    metricsEnabled: boolean
    healthChecksEnabled: boolean
    alertsEnabled: boolean
    metricsRetentionDays: number
    metricsCollectionIntervalSeconds: number
    alertThresholds: AlertThresholds
  }) => {
    metricsEnabled.value = settings.metricsEnabled
    healthChecksEnabled.value = settings.healthChecksEnabled
    alertsEnabled.value = settings.alertsEnabled
    metricsRetentionDays.value = settings.metricsRetentionDays
    metricsCollectionIntervalSeconds.value = settings.metricsCollectionIntervalSeconds
    alertThresholds.value = { ...settings.alertThresholds }
  }

  const setMetricsCollectionIntervalSeconds = (seconds: number) => {
    metricsCollectionIntervalSeconds.value = seconds
  }

  const setAlertThresholds = (thresholds: AlertThresholds) => {
    alertThresholds.value = { ...thresholds }
  }

  const setLoggingSettings = (settings: { logLevel: string; logRetentionDays: number }) => {
    logLevel.value = settings.logLevel
    logRetentionDays.value = settings.logRetentionDays
  }

  const setSecuritySettings = (settings: {
    sessionTimeout: number
    maxLoginAttempts: number
    lockoutDurationMinutes: number
    passwordMinLength: number
    passwordRequireUppercase: boolean
    passwordRequireLowercase: boolean
    passwordRequireNumbers: boolean
    passwordRequireSpecialChars: boolean
    enableTwoFactorAuth: boolean
  }) => {
    sessionTimeout.value = settings.sessionTimeout
    maxLoginAttempts.value = settings.maxLoginAttempts
    lockoutDurationMinutes.value = settings.lockoutDurationMinutes
    passwordMinLength.value = settings.passwordMinLength
    passwordRequireUppercase.value = settings.passwordRequireUppercase
    passwordRequireLowercase.value = settings.passwordRequireLowercase
    passwordRequireNumbers.value = settings.passwordRequireNumbers
    passwordRequireSpecialChars.value = settings.passwordRequireSpecialChars
    enableTwoFactorAuth.value = settings.enableTwoFactorAuth
  }

  const createDefaultPanelSettings = (): PanelSystemSettings => ({
    id: 'default',
    general: {
      systemName: defaultSettings.systemName,
      systemDescription: defaultSettings.systemDescription,
      adminEmail: defaultSettings.adminEmail,
      defaultLanguage: defaultSettings.defaultLanguage,
      defaultTimezone: defaultSettings.defaultTimezone
    },
    security: {
      sessionTimeout: defaultSettings.sessionTimeout,
      sessionTimeoutMinutes: Math.ceil(defaultSettings.sessionTimeout / 60),
      maxLoginAttempts: defaultSettings.maxLoginAttempts,
      lockoutDurationMinutes: defaultSettings.lockoutDurationMinutes,
      passwordMinLength: defaultSettings.passwordMinLength,
      passwordRequireUppercase: defaultSettings.passwordRequireUppercase,
      passwordRequireLowercase: defaultSettings.passwordRequireLowercase,
      passwordRequireNumbers: defaultSettings.passwordRequireNumbers,
      passwordRequireSpecialChars: defaultSettings.passwordRequireSpecialChars,
      enableTwoFactorAuth: defaultSettings.enableTwoFactorAuth
    },
    monitoring: {
      metricsEnabled: defaultSettings.metricsEnabled,
      healthChecksEnabled: defaultSettings.healthChecksEnabled,
      alertsEnabled: defaultSettings.alertsEnabled,
      metricsRetentionDays: defaultSettings.metricsRetentionDays,
      metricsCollectionIntervalSeconds: defaultSettings.metricsCollectionIntervalSeconds,
      alertThresholds: { ...defaultSettings.alertThresholds }
    },
    ui: {
      theme: 'auto',
      refreshInterval: defaultSettings.refreshInterval,
      defaultPageSize: defaultSettings.defaultPageSize
    },
    logging: {
      logLevel: defaultSettings.logLevel,
      logRetentionDays: defaultSettings.logRetentionDays
    },
    updatedAt: new Date().toISOString()
  })

  const applyRemoteSettings = (settings: PanelSystemSettings) => {
    currentSettings.value = settings
    systemName.value = settings.general.systemName
    systemDescription.value = settings.general.systemDescription
    adminEmail.value = settings.general.adminEmail
    defaultLanguage.value = settings.general.defaultLanguage
    defaultTimezone.value = settings.general.defaultTimezone
    refreshInterval.value = settings.ui.refreshInterval
    defaultPageSize.value = settings.ui.defaultPageSize
    metricsEnabled.value = settings.monitoring.metricsEnabled
    healthChecksEnabled.value = settings.monitoring.healthChecksEnabled
    alertsEnabled.value = settings.monitoring.alertsEnabled
    metricsRetentionDays.value = settings.monitoring.metricsRetentionDays
    metricsCollectionIntervalSeconds.value = settings.monitoring.metricsCollectionIntervalSeconds
    alertThresholds.value = { ...settings.monitoring.alertThresholds }
    logLevel.value = settings.logging.logLevel
    logRetentionDays.value = settings.logging.logRetentionDays
    sessionTimeout.value = settings.security.sessionTimeout
    maxLoginAttempts.value = settings.security.maxLoginAttempts
    lockoutDurationMinutes.value = settings.security.lockoutDurationMinutes
    passwordMinLength.value = settings.security.passwordMinLength
    passwordRequireUppercase.value = settings.security.passwordRequireUppercase
    passwordRequireLowercase.value = settings.security.passwordRequireLowercase
    passwordRequireNumbers.value = settings.security.passwordRequireNumbers
    passwordRequireSpecialChars.value = settings.security.passwordRequireSpecialChars
    enableTwoFactorAuth.value = settings.security.enableTwoFactorAuth
    document.title = systemName.value ? `${systemName.value} - DockerPanel` : 'DockerPanel'
  }

  const applyPublicSettings = (settings: PublicSystemSettings) => {
    systemName.value = settings.systemName
    systemDescription.value = settings.systemDescription
    defaultLanguage.value = settings.defaultLanguage
    defaultTimezone.value = settings.defaultTimezone
    refreshInterval.value = settings.refreshInterval
    defaultPageSize.value = settings.defaultPageSize
    document.title = systemName.value ? `${systemName.value} - DockerPanel` : 'DockerPanel'
  }

  const buildRemoteSettings = (options?: { theme?: PanelUiSettings['theme']; language?: string }): PanelSystemSettings => {
    const base = currentSettings.value ?? createDefaultPanelSettings()
    const sessionTimeoutMinutes = Math.ceil(sessionTimeout.value / 60)

    return {
      ...base,
      general: {
        ...base.general,
        systemName: systemName.value,
        systemDescription: systemDescription.value,
        adminEmail: adminEmail.value,
        defaultLanguage: options?.language ?? defaultLanguage.value,
        defaultTimezone: defaultTimezone.value
      },
      security: {
        ...base.security,
        sessionTimeout: sessionTimeout.value,
        sessionTimeoutMinutes,
        maxLoginAttempts: maxLoginAttempts.value,
        lockoutDurationMinutes: lockoutDurationMinutes.value,
        passwordMinLength: passwordMinLength.value,
        passwordRequireUppercase: passwordRequireUppercase.value,
        passwordRequireLowercase: passwordRequireLowercase.value,
        passwordRequireNumbers: passwordRequireNumbers.value,
        passwordRequireSpecialChars: passwordRequireSpecialChars.value,
        enableTwoFactorAuth: enableTwoFactorAuth.value
      },
      monitoring: {
        ...base.monitoring,
        metricsEnabled: metricsEnabled.value,
        healthChecksEnabled: healthChecksEnabled.value,
        alertsEnabled: alertsEnabled.value,
        metricsRetentionDays: metricsRetentionDays.value,
        metricsCollectionIntervalSeconds: metricsCollectionIntervalSeconds.value,
        alertThresholds: { ...alertThresholds.value }
      },
      ui: {
        ...base.ui,
        theme: options?.theme ?? base.ui.theme,
        refreshInterval: refreshInterval.value,
        defaultPageSize: defaultPageSize.value
      },
      logging: {
        ...base.logging,
        logLevel: logLevel.value,
        logRetentionDays: logRetentionDays.value
      },
      updatedAt: new Date().toISOString()
    }
  }

  const loadPublicSettings = async () => {
    const settings = await settingsApi.getPublicSettings()
    applyPublicSettings(settings)
    return settings
  }

  const loadRemoteSettings = async () => {
    loading.value = true
    try {
      const settings = await settingsApi.getSystemSettings()
      applyRemoteSettings(settings)
      return settings
    } finally {
      loading.value = false
    }
  }

  const saveRemoteSettings = async (options?: { theme?: PanelUiSettings['theme']; language?: string }) => {
    loading.value = true
    try {
      const settings = await settingsApi.updateSystemSettings(buildRemoteSettings(options))
      applyRemoteSettings(settings)
      return settings
    } finally {
      loading.value = false
    }
  }

  const resetRemoteSettings = async () => {
    loading.value = true
    try {
      const settings = await settingsApi.resetSystemSettings()
      applyRemoteSettings(settings)
      return settings
    } finally {
      loading.value = false
    }
  }

  const importRemoteSettings = async (file: File) => {
    loading.value = true
    try {
      const settings = await settingsApi.importSettings(file)
      applyRemoteSettings(settings)
      return settings
    } finally {
      loading.value = false
    }
  }

  // 重置为默认值
  const resetToDefaults = () => {
    systemName.value = defaultSettings.systemName
    systemDescription.value = defaultSettings.systemDescription
    adminEmail.value = defaultSettings.adminEmail
    defaultLanguage.value = defaultSettings.defaultLanguage
    defaultTimezone.value = defaultSettings.defaultTimezone
    refreshInterval.value = defaultSettings.refreshInterval
    defaultPageSize.value = defaultSettings.defaultPageSize
    metricsEnabled.value = defaultSettings.metricsEnabled
    healthChecksEnabled.value = defaultSettings.healthChecksEnabled
    alertsEnabled.value = defaultSettings.alertsEnabled
    metricsRetentionDays.value = defaultSettings.metricsRetentionDays
    metricsCollectionIntervalSeconds.value = defaultSettings.metricsCollectionIntervalSeconds
    alertThresholds.value = { ...defaultSettings.alertThresholds }
    logLevel.value = defaultSettings.logLevel
    logRetentionDays.value = defaultSettings.logRetentionDays
    sessionTimeout.value = defaultSettings.sessionTimeout
    maxLoginAttempts.value = defaultSettings.maxLoginAttempts
    lockoutDurationMinutes.value = defaultSettings.lockoutDurationMinutes
    passwordMinLength.value = defaultSettings.passwordMinLength
    passwordRequireUppercase.value = defaultSettings.passwordRequireUppercase
    passwordRequireLowercase.value = defaultSettings.passwordRequireLowercase
    passwordRequireNumbers.value = defaultSettings.passwordRequireNumbers
    passwordRequireSpecialChars.value = defaultSettings.passwordRequireSpecialChars
    enableTwoFactorAuth.value = defaultSettings.enableTwoFactorAuth
  }

  // 初始化时设置页面标题
  if (systemName.value) {
    document.title = systemName.value
  }

  return {
    // 状态
    systemName,
    systemDescription,
    adminEmail,
    defaultLanguage,
    defaultTimezone,
    refreshInterval,
    defaultPageSize,
    metricsEnabled,
    healthChecksEnabled,
    alertsEnabled,
    metricsRetentionDays,
    metricsCollectionIntervalSeconds,
    alertThresholds,
    logLevel,
    logRetentionDays,
    sessionTimeout,
    maxLoginAttempts,
    lockoutDurationMinutes,
    passwordMinLength,
    passwordRequireUppercase,
    passwordRequireLowercase,
    passwordRequireNumbers,
    passwordRequireSpecialChars,
    enableTwoFactorAuth,
    loading,

    // 方法
    setSystemName,
    setGeneralSettings,
    setRefreshInterval,
    setDefaultPageSize,
    setMetricsEnabled,
    setMonitoringSettings,
    setMetricsCollectionIntervalSeconds,
    setAlertThresholds,
    setLoggingSettings,
    setSecuritySettings,
    loadPublicSettings,
    loadRemoteSettings,
    saveRemoteSettings,
    resetRemoteSettings,
    importRemoteSettings,
    resetToDefaults
  }
})
