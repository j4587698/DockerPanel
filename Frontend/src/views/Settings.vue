<template>
  <div class="settings-page" v-loading="settingsStore.loading">
    <!-- Page Header -->
    <header class="page-header">
      <div class="header-content">
        <h1 class="page-title">{{ t('settings.title') }}</h1>
        <p class="page-subtitle">{{ t('settings.subtitle') }}</p>
      </div>
      <div class="header-actions">
        <button class="settings-action" @click="handleExportSettings">{{ t('settings.exportSettings') }}</button>
        <button class="settings-action" @click="triggerImportSettings">{{ t('settings.importSettings') }}</button>
        <button class="settings-action danger" @click="handleResetSettings">{{ t('settings.resetDefault') }}</button>
        <input
          ref="importInputRef"
          class="hidden-file-input"
          type="file"
          accept="application/json,.json"
          @change="handleImportSettings"
        />
      </div>
    </header>

    <!-- Settings Grid -->
    <div class="settings-grid">
      <!-- Appearance Settings -->
      <div class="settings-card">
        <div class="card-header">
          <div class="card-icon purple">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="5"></circle>
              <path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42"></path>
            </svg>
          </div>
          <h3 class="card-title">{{ t('settings.appearance') }}</h3>
        </div>
        <div class="card-body">
          <div class="form-group">
            <label class="form-label">{{ t('settings.theme') }}</label>
            <div class="theme-options">
              <button 
                v-for="opt in themeOptions" 
                :key="opt.value" 
                :class="['theme-btn', { active: form.theme === opt.value }]"
                @click="handleThemeChange(opt.value)"
              >
                <span class="theme-icon" :class="opt.icon"></span>
                <span class="theme-label">{{ opt.label }}</span>
              </button>
            </div>
          </div>

          <div class="form-group">
            <label class="form-label">{{ t('settings.language') }}</label>
            <select v-model="form.language" @change="handleLanguageChange" class="form-select">
              <option value="zh-CN">简体中文</option>
              <option value="en-US">English</option>
            </select>
          </div>
        </div>
      </div>

      <!-- General Settings -->
      <div class="settings-card">
        <div class="card-header">
          <div class="card-icon blue">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="3"></circle>
              <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
            </svg>
          </div>
          <h3 class="card-title">{{ t('settings.general') }}</h3>
        </div>
        <div class="card-body">
          <div class="form-group">
            <label class="form-label">{{ t('settings.systemName') }}</label>
            <input v-model="form.systemName" type="text" class="form-input" :placeholder="APP_NAME" @change="handleGeneralChange" />
          </div>

          <div class="form-group">
            <label class="form-label">{{ t('settings.systemDescription') }}</label>
            <textarea v-model="form.systemDescription" class="form-input form-textarea" rows="3" @change="handleGeneralChange" />
          </div>

          <div class="form-row">
            <div class="form-group">
              <label class="form-label">{{ t('settings.adminEmail') }}</label>
              <input v-model="form.adminEmail" type="email" class="form-input" placeholder="admin@example.com" @change="handleGeneralChange" />
            </div>

            <div class="form-group">
              <label class="form-label">{{ t('settings.timezone') }}</label>
              <select v-model="form.defaultTimezone" @change="handleGeneralChange" class="form-select">
                <option value="Asia/Shanghai">Asia/Shanghai</option>
                <option value="UTC">UTC</option>
                <option value="America/New_York">America/New_York</option>
                <option value="Europe/London">Europe/London</option>
              </select>
            </div>
          </div>

          <div class="form-group">
            <label class="form-label">{{ t('settings.refreshInterval') }}</label>
            <select v-model.number="form.refreshInterval" @change="handleRefreshIntervalChange" class="form-select">
              <option :value="3000">3 {{ t('settings.seconds') }}</option>
              <option :value="5000">5 {{ t('settings.seconds') }}</option>
              <option :value="10000">10 {{ t('settings.seconds') }}</option>
              <option :value="30000">30 {{ t('settings.seconds') }}</option>
              <option :value="60000">1 {{ t('settings.minute') }}</option>
            </select>
            <span class="form-hint">{{ t('settings.refreshIntervalHint') }}</span>
          </div>

          <div class="form-group">
            <label class="form-label">{{ t('settings.defaultPageSize') }}</label>
            <select v-model.number="form.defaultPageSize" @change="handlePageSizeChange" class="form-select">
              <option :value="10">10</option>
              <option :value="20">20</option>
              <option :value="50">50</option>
              <option :value="100">100</option>
            </select>
          </div>
        </div>
      </div>

      <!-- Monitoring Settings -->
      <div class="settings-card">
        <div class="card-header">
          <div class="card-icon yellow">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
              <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
            </svg>
          </div>
          <h3 class="card-title">{{ t('settings.monitoring') }}</h3>
        </div>
        <div class="card-body">
          <label class="toggle-item">
            <span class="toggle-label">{{ t('settings.metricsEnabled') }}</span>
            <input type="checkbox" v-model="form.metricsEnabled" @change="handleMetricsEnabledChange" class="toggle-input" />
            <span class="toggle-switch"></span>
          </label>

          <label class="toggle-item">
            <span class="toggle-label">{{ t('settings.healthChecksEnabled') }}</span>
            <input type="checkbox" v-model="form.healthChecksEnabled" @change="handleMonitoringChange" class="toggle-input" />
            <span class="toggle-switch"></span>
          </label>

          <label class="toggle-item">
            <span class="toggle-label">{{ t('settings.alertsEnabled') }}</span>
            <input type="checkbox" v-model="form.alertsEnabled" @change="handleMonitoringChange" class="toggle-input" />
            <span class="toggle-switch"></span>
          </label>

          <div class="form-group">
            <label class="form-label">{{ t('settings.collectInterval') }}</label>
            <select v-model.number="form.metricsCollectionIntervalSeconds" @change="handleMetricsIntervalChange" class="form-select" :disabled="!form.metricsEnabled">
              <option :value="5">5 {{ t('settings.seconds') }}</option>
              <option :value="10">10 {{ t('settings.seconds') }}</option>
              <option :value="30">30 {{ t('settings.seconds') }}</option>
              <option :value="60">1 {{ t('settings.minute') }}</option>
              <option :value="300">5 {{ t('settings.minute') }}</option>
            </select>
            <span class="form-hint">{{ t('settings.metricsIntervalHint') }}</span>
          </div>

          <div class="form-group">
            <label class="form-label">{{ t('settings.retentionDays') }}</label>
            <input v-model.number="form.metricsRetentionDays" type="number" class="form-input small" min="1" max="3650" @change="handleMonitoringChange" />
            <span class="form-hint">{{ t('settings.metricsRetentionHint') }}</span>
          </div>

          <div class="divider">{{ t('settings.alertThresholds') }}</div>

          <div class="threshold-item">
            <div class="threshold-header">
              <span class="threshold-label">{{ t('settings.cpuUsage') }}</span>
              <span class="threshold-value">{{ form.alertThresholds.cpu }}%</span>
            </div>
            <input type="range" v-model.number="form.alertThresholds.cpu" @change="handleThresholdChange" min="50" max="100" class="slider" :disabled="!form.alertsEnabled" />
          </div>

          <div class="threshold-item">
            <div class="threshold-header">
              <span class="threshold-label">{{ t('settings.memoryUsage') }}</span>
              <span class="threshold-value">{{ form.alertThresholds.memory }}%</span>
            </div>
            <input type="range" v-model.number="form.alertThresholds.memory" @change="handleThresholdChange" min="50" max="100" class="slider" :disabled="!form.alertsEnabled" />
          </div>

          <div class="threshold-item">
            <div class="threshold-header">
              <span class="threshold-label">{{ t('settings.diskUsage') }}</span>
              <span class="threshold-value">{{ form.alertThresholds.disk }}%</span>
            </div>
            <input type="range" v-model.number="form.alertThresholds.disk" @change="handleThresholdChange" min="50" max="100" class="slider" :disabled="!form.alertsEnabled" />
          </div>
        </div>
      </div>

      <!-- Security Settings -->
      <div class="settings-card">
        <div class="card-header">
          <div class="card-icon green">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
              <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
            </svg>
          </div>
          <h3 class="card-title">{{ t('settings.security') }}</h3>
        </div>
        <div class="card-body">
          <div class="form-group">
            <label class="form-label">{{ t('settings.sessionTimeout') }}</label>
            <input v-model.number="form.sessionTimeout" type="number" class="form-input" min="300" max="86400" @change="handleSecurityChange" />
            <span class="form-hint">{{ t('settings.sessionTimeoutHint') }}</span>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label class="form-label">{{ t('settings.maxLoginAttempts') }}</label>
              <input v-model.number="form.maxLoginAttempts" type="number" class="form-input" min="1" max="20" @change="handleSecurityChange" />
            </div>

            <div class="form-group">
              <label class="form-label">{{ t('settings.lockoutDuration') }}</label>
              <input v-model.number="form.lockoutDurationMinutes" type="number" class="form-input" min="1" max="1440" @change="handleSecurityChange" />
            </div>
          </div>

          <label class="toggle-item disabled">
            <span class="toggle-label">{{ t('settings.enableTwoFactorAuth') }} <span class="inline-badge">{{ t('settings.planned') }}</span></span>
            <input type="checkbox" v-model="form.enableTwoFactorAuth" class="toggle-input" disabled />
            <span class="toggle-switch"></span>
          </label>

          <div class="divider">{{ t('settings.passwordPolicy') }}</div>

          <div class="form-group">
            <label class="form-label">{{ t('settings.passwordMinLength') }}</label>
            <input v-model.number="form.passwordMinLength" type="number" class="form-input small" min="6" max="32" @change="handleSecurityChange" />
          </div>

          <label class="toggle-item">
            <span class="toggle-label">{{ t('settings.requireUppercase') }}</span>
            <input type="checkbox" v-model="form.passwordRequireUppercase" @change="handleSecurityChange" class="toggle-input" />
            <span class="toggle-switch"></span>
          </label>

          <label class="toggle-item">
            <span class="toggle-label">{{ t('settings.requireLowercase') }}</span>
            <input type="checkbox" v-model="form.passwordRequireLowercase" @change="handleSecurityChange" class="toggle-input" />
            <span class="toggle-switch"></span>
          </label>

          <label class="toggle-item">
            <span class="toggle-label">{{ t('settings.requireNumbers') }}</span>
            <input type="checkbox" v-model="form.passwordRequireNumbers" @change="handleSecurityChange" class="toggle-input" />
            <span class="toggle-switch"></span>
          </label>

          <label class="toggle-item">
            <span class="toggle-label">{{ t('settings.requireSpecialChars') }}</span>
            <input type="checkbox" v-model="form.passwordRequireSpecialChars" @change="handleSecurityChange" class="toggle-input" />
            <span class="toggle-switch"></span>
          </label>
        </div>
      </div>

      <!-- Logging Settings -->
      <div class="settings-card">
        <div class="card-header">
          <div class="card-icon red">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <path d="M14 2v6h6"></path>
              <path d="M16 13H8M16 17H8M10 9H8"></path>
            </svg>
          </div>
          <h3 class="card-title">{{ t('settings.logging') }}</h3>
        </div>
        <div class="card-body">
          <div class="form-row">
            <div class="form-group">
              <label class="form-label">{{ t('settings.logLevel') }}</label>
              <select v-model="form.logLevel" @change="handleLoggingChange" class="form-select">
                <option value="Trace">{{ t('settings.logLevelTrace') }}</option>
                <option value="Debug">{{ t('settings.logLevelDebug') }}</option>
                <option value="Information">{{ t('settings.logLevelInfo') }}</option>
                <option value="Warning">{{ t('settings.logLevelWarning') }}</option>
                <option value="Error">{{ t('settings.logLevelError') }}</option>
                <option value="Critical">{{ t('settings.logLevelCritical') }}</option>
              </select>
              <span class="form-hint">{{ t('settings.logLevelHint') }}</span>
            </div>

            <div class="form-group">
              <label class="form-label">{{ t('settings.logRetentionDays') }}</label>
              <input v-model.number="form.logRetentionDays" type="number" class="form-input" min="1" max="3650" @change="handleLoggingChange" />
              <span class="form-hint">{{ t('settings.logRetentionHint') }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAppStore } from '@/stores/app'
import { setLocale, getLocale } from '@/i18n'
import { useSettingsStore } from '@/stores/settings'
import { settingsApi } from '@/api/settings'
import { APP_NAME } from '@/utils/branding'
import { ElMessage, ElMessageBox } from 'element-plus'

const { t } = useI18n()
const appStore = useAppStore()
const settingsStore = useSettingsStore()
const importInputRef = ref<HTMLInputElement | null>(null)

const form = ref({
  theme: 'auto' as 'light' | 'dark' | 'auto',
  language: 'zh-CN',
  systemName: APP_NAME,
  systemDescription: 'Docker容器管理平台',
  adminEmail: '',
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
  enableTwoFactorAuth: false,
  passwordMinLength: 8,
  passwordRequireUppercase: true,
  passwordRequireLowercase: true,
  passwordRequireNumbers: true,
  passwordRequireSpecialChars: true
})

const themeOptions = computed(() => [
  { value: 'light', label: t('common.themeLight'), icon: 'icon-sun' },
  { value: 'dark', label: t('common.themeDark'), icon: 'icon-moon' },
  { value: 'auto', label: t('common.themeAuto'), icon: 'icon-auto' }
])

const syncFormFromStore = () => {
  form.value.theme = appStore.theme
  form.value.language = getLocale()
  form.value.systemName = settingsStore.systemName
  form.value.systemDescription = settingsStore.systemDescription
  form.value.adminEmail = settingsStore.adminEmail
  form.value.defaultTimezone = settingsStore.defaultTimezone
  form.value.refreshInterval = settingsStore.refreshInterval
  form.value.defaultPageSize = settingsStore.defaultPageSize
  form.value.metricsEnabled = settingsStore.metricsEnabled
  form.value.healthChecksEnabled = settingsStore.healthChecksEnabled
  form.value.alertsEnabled = settingsStore.alertsEnabled
  form.value.metricsRetentionDays = settingsStore.metricsRetentionDays
  form.value.metricsCollectionIntervalSeconds = settingsStore.metricsCollectionIntervalSeconds
  form.value.alertThresholds = { ...settingsStore.alertThresholds }
  form.value.logLevel = settingsStore.logLevel
  form.value.logRetentionDays = settingsStore.logRetentionDays
  form.value.sessionTimeout = settingsStore.sessionTimeout
  form.value.maxLoginAttempts = settingsStore.maxLoginAttempts
  form.value.lockoutDurationMinutes = settingsStore.lockoutDurationMinutes
  form.value.enableTwoFactorAuth = settingsStore.enableTwoFactorAuth
  form.value.passwordMinLength = settingsStore.passwordMinLength
  form.value.passwordRequireUppercase = settingsStore.passwordRequireUppercase
  form.value.passwordRequireLowercase = settingsStore.passwordRequireLowercase
  form.value.passwordRequireNumbers = settingsStore.passwordRequireNumbers
  form.value.passwordRequireSpecialChars = settingsStore.passwordRequireSpecialChars
}

const loadSettings = async () => {
  try {
    const remoteSettings = await settingsStore.loadRemoteSettings()
    await applyRemoteUiState(remoteSettings)
  } catch (error) {
    console.warn('Failed to load remote settings, using local settings:', error)
  }

  syncFormFromStore()
}

const applyFormToStore = () => {
  settingsStore.setGeneralSettings({
    systemName: form.value.systemName,
    systemDescription: form.value.systemDescription,
    adminEmail: form.value.adminEmail,
    defaultLanguage: form.value.language,
    defaultTimezone: form.value.defaultTimezone
  })
  settingsStore.setRefreshInterval(form.value.refreshInterval)
  settingsStore.setDefaultPageSize(form.value.defaultPageSize)
  settingsStore.setMonitoringSettings({
    metricsEnabled: form.value.metricsEnabled,
    healthChecksEnabled: form.value.healthChecksEnabled,
    alertsEnabled: form.value.alertsEnabled,
    metricsRetentionDays: form.value.metricsRetentionDays,
    metricsCollectionIntervalSeconds: form.value.metricsCollectionIntervalSeconds,
    alertThresholds: form.value.alertThresholds
  })
  settingsStore.setLoggingSettings({
    logLevel: form.value.logLevel,
    logRetentionDays: form.value.logRetentionDays
  })
  settingsStore.setSecuritySettings({
    sessionTimeout: form.value.sessionTimeout,
    maxLoginAttempts: form.value.maxLoginAttempts,
    lockoutDurationMinutes: form.value.lockoutDurationMinutes,
    passwordMinLength: form.value.passwordMinLength,
    passwordRequireUppercase: form.value.passwordRequireUppercase,
    passwordRequireLowercase: form.value.passwordRequireLowercase,
    passwordRequireNumbers: form.value.passwordRequireNumbers,
    passwordRequireSpecialChars: form.value.passwordRequireSpecialChars,
    enableTwoFactorAuth: form.value.enableTwoFactorAuth
  })
}

const saveSettings = async (successKey = 'settings.saved') => {
  applyFormToStore()
  try {
    const savedSettings = await settingsStore.saveRemoteSettings({
      theme: form.value.theme,
      language: form.value.language
    })
    await applyRemoteUiState(savedSettings)
    syncFormFromStore()
    ElMessage.success(t(successKey))
  } catch (error: any) {
    syncFormFromStore()
    ElMessage.error(error?.message || t('settings.saveFailed'))
  }
}

const handleThemeChange = async (theme: 'light' | 'dark' | 'auto') => {
  form.value.theme = theme
  appStore.setTheme(theme)
  await saveSettings('settings.themeChanged')
}

const handleLanguageChange = async () => {
  await setLocale(form.value.language)
  await saveSettings('settings.languageChanged')
}

const handleGeneralChange = () => saveSettings()

const handleRefreshIntervalChange = () => saveSettings()

const handlePageSizeChange = () => saveSettings()

const handleMetricsEnabledChange = () => saveSettings()

const handleMetricsIntervalChange = () => saveSettings()

const handleMonitoringChange = () => saveSettings()

const handleThresholdChange = () => saveSettings()

const handleSecurityChange = () => saveSettings()

const handleLoggingChange = () => saveSettings()

const applyRemoteUiState = async (remoteSettings: any) => {
  if (remoteSettings.ui.theme !== appStore.theme) {
    appStore.setTheme(remoteSettings.ui.theme)
  }

  if (remoteSettings.general.defaultLanguage && remoteSettings.general.defaultLanguage !== getLocale()) {
    await setLocale(remoteSettings.general.defaultLanguage)
  }
}

const handleExportSettings = async () => {
  try {
    const blob = await settingsApi.exportSettings() as Blob
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `dockerpanel-settings-${new Date().toISOString().replace(/[:.]/g, '-')}.json`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
    ElMessage.success(t('settings.exportSuccess'))
  } catch (error: any) {
    ElMessage.error(error?.message || t('settings.exportFailed'))
  }
}

const triggerImportSettings = () => {
  importInputRef.value?.click()
}

const handleImportSettings = async (event: Event) => {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  try {
    const importedSettings = await settingsStore.importRemoteSettings(file)
    await applyRemoteUiState(importedSettings)
    syncFormFromStore()
    ElMessage.success(t('settings.importSuccess'))
  } catch (error: any) {
    ElMessage.error(error?.message || t('settings.importFailed'))
  }
}

const handleResetSettings = async () => {
  try {
    await ElMessageBox.confirm(t('settings.resetConfirmMessage'), t('settings.resetConfirmTitle'), {
      type: 'warning',
      confirmButtonText: t('settings.resetDefault'),
      cancelButtonText: t('common.cancel')
    })
    const resetSettings = await settingsStore.resetRemoteSettings()
    await applyRemoteUiState(resetSettings)
    syncFormFromStore()
    ElMessage.success(t('settings.resetSuccess'))
  } catch (error: any) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(error?.message || t('settings.saveFailed'))
    }
  }
}

onMounted(() => { void loadSettings() })
</script>

<style scoped>
.settings-page {
  padding: 24px 32px;
  max-width: 1400px;
  margin: 0 auto;
  width: 100%;
  box-sizing: border-box;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 32px;
}

.page-subtitle { margin: 6px 0 0 0; color: var(--text-secondary); font-size: 14px; }
.header-actions { display: flex; gap: 10px; flex-wrap: wrap; }

.settings-action {
  height: 36px;
  padding: 0 14px;
  border-radius: 10px;
  border: 1px solid var(--border-color);
  background: var(--bg-surface);
  color: var(--text-main);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.settings-action:hover {
  border-color: var(--color-secondary);
  color: var(--color-secondary);
  background: rgba(59, 130, 246, 0.08);
}

.settings-action.danger {
  color: #ef4444;
  border-color: rgba(239, 68, 68, 0.35);
}

.settings-action.danger:hover {
  background: rgba(239, 68, 68, 0.08);
  border-color: #ef4444;
}

.hidden-file-input { display: none; }

.btn-icon { width: 16px; height: 16px; }
.btn-icon.spin { animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

.settings-grid {
  display: grid !important;
  grid-template-columns: 1fr 1fr !important;
  gap: 24px;
  width: 100%;
}

@media (max-width: 768px) {
  .settings-grid {
    grid-template-columns: 1fr !important;
  }
}

.settings-card {
  background: var(--bg-surface);
  border-radius: 16px;
  border: 1px solid var(--border-color);
  overflow: hidden;
  min-width: 0;
}

.card-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 20px 24px;
  border-bottom: 1px solid var(--border-color);
  background: var(--bg-subtle);
}

.card-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.card-icon svg { width: 20px; height: 20px; color: #fff; }
.card-icon.blue { background: linear-gradient(135deg, #3b82f6, #2563eb); }
.card-icon.yellow { background: linear-gradient(135deg, #f59e0b, #d97706); }
.card-icon.green { background: linear-gradient(135deg, #22c55e, #16a34a); }
.card-icon.purple { background: linear-gradient(135deg, #8b5cf6, #7c3aed); }
.card-icon.red { background: linear-gradient(135deg, #ef4444, #dc2626); }

.card-title { font-size: 16px; font-weight: 600; color: var(--text-main); margin: 0; }

.badge {
  padding: 4px 10px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.badge-planned {
  background: rgba(139, 92, 246, 0.15);
  color: #a78bfa;
  margin-left: auto;
}

.inline-badge {
  display: inline-flex;
  align-items: center;
  padding: 2px 6px;
  margin-left: 6px;
  border-radius: 999px;
  background: rgba(139, 92, 246, 0.14);
  color: #8b5cf6;
  font-size: 11px;
  font-weight: 600;
}

.card-body { padding: 24px; }

.form-group { margin-bottom: 20px; }
.form-label { display: block; font-size: 13px; font-weight: 500; color: var(--text-secondary); margin-bottom: 8px; }

.form-input, .form-select {
  width: 100%;
  padding: 10px 14px;
  border-radius: 8px;
  border: 1px solid var(--border-color);
  font-size: 14px;
  transition: border-color 0.2s;
  background: var(--bg-surface);
  color: var(--text-main);
}

.form-input:focus, .form-select:focus {
  outline: none;
  border-color: var(--color-secondary);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.form-input.small { width: 120px; }
.form-textarea { min-height: 84px; resize: vertical; line-height: 1.5; }

.form-hint {
  display: block;
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 6px;
}

.theme-options {
  display: flex;
  gap: 12px;
}

.theme-btn {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 16px 12px;
  border-radius: 12px;
  border: 2px solid var(--border-color);
  background: var(--bg-surface);
  cursor: pointer;
  transition: all 0.2s ease;
}

.theme-btn:hover {
  border-color: var(--color-primary);
  background: var(--bg-subtle);
}

.theme-btn.active {
  border-color: var(--color-secondary);
  background: rgba(59, 130, 246, 0.1);
}

.theme-icon {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.theme-icon.icon-sun {
  background: linear-gradient(135deg, #fbbf24, #f59e0b);
}

.theme-icon.icon-moon {
  background: linear-gradient(135deg, #6366f1, #4f46e5);
}

.theme-icon.icon-auto {
  background: linear-gradient(135deg, #8b5cf6, #7c3aed);
}

.theme-icon::before {
  content: '';
  width: 14px;
  height: 14px;
  background: white;
  border-radius: 50%;
}

.theme-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
}

.theme-btn.active .theme-label {
  color: var(--color-secondary);
}

.disabled-section {
  opacity: 0.6;
  pointer-events: none;
}

.disabled-section .toggle-item {
  cursor: not-allowed;
}

.form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }

.toggle-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 0;
  cursor: pointer;
}

.toggle-label { font-size: 14px; color: var(--text-main); }
.toggle-input { display: none; }

.toggle-switch {
  width: 44px;
  height: 24px;
  background: var(--border-color);
  border-radius: 12px;
  position: relative;
  transition: background 0.2s;
  flex-shrink: 0;
}

.toggle-switch::after {
  content: '';
  position: absolute;
  width: 18px;
  height: 18px;
  background: var(--bg-surface);
  border-radius: 50%;
  top: 3px;
  left: 3px;
  transition: transform 0.2s;
  box-shadow: 0 1px 3px rgba(0,0,0,0.2);
}

.toggle-input:checked + .toggle-switch { background: var(--color-secondary); }
.toggle-input:checked + .toggle-switch::after { transform: translateX(20px); }

.divider {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin: 24px 0 16px 0;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border-color);
}

.threshold-item { margin-bottom: 20px; }
.threshold-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
.threshold-label { font-size: 13px; color: var(--text-secondary); }
.threshold-value { font-size: 13px; font-weight: 600; color: var(--color-secondary); }

.slider {
  width: 100%;
  height: 6px;
  border-radius: 3px;
  background: var(--bg-subtle);
  appearance: none;
  cursor: pointer;
}

.slider::-webkit-slider-thumb {
  appearance: none;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: var(--color-secondary);
  cursor: pointer;
  box-shadow: 0 2px 6px rgba(59, 130, 246, 0.3);
}

.slider:disabled { opacity: 0.45; cursor: not-allowed; }

/* 响应式 */
@media (max-width: 768px) {
  .settings-page { padding: 16px; }
  .page-header { flex-direction: column; gap: 16px; }
  .settings-grid { grid-template-columns: 1fr !important; }
  .form-row { grid-template-columns: 1fr; }
}

@media (max-width: 480px) {
  .settings-page { padding: 12px; }
  .card-body { padding: 16px; }
  .card-header { padding: 16px; }
  .header-actions { width: 100%; }
  .btn { flex: 1; justify-content: center; }
}
</style>
