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

      <!-- System Update & Version -->
      <div class="settings-card update-card">
        <div class="card-header">
          <div class="card-icon emerald">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67"></path>
            </svg>
          </div>
          <h3 class="card-title">{{ t('settings.systemUpdate') }}</h3>
          <div class="header-tag" v-if="currentVersion">
            <span class="version-badge" :class="{ 'has-update': updateCheckResult?.hasUpdate }">
              v{{ currentVersion }}
            </span>
          </div>
        </div>
        <div class="card-body">
          <div class="update-status-box">
            <div class="version-info-row">
              <div class="version-item">
                <span class="label">{{ t('settings.currentVersion') }}</span>
                <span class="val font-mono">v{{ currentVersion }}</span>
              </div>
              <div class="version-item" v-if="updateCheckResult?.imageName">
                <span class="label">{{ t('settings.currentImage') }}</span>
                <span class="val font-mono">{{ updateCheckResult.imageName }}</span>
              </div>
              <div class="version-action">
                <el-button 
                  size="small" 
                  @click="fetchUpdateCheck(true)" 
                  :loading="checkingUpdate"
                >
                  {{ t('settings.checkUpdate') }}
                </el-button>
              </div>
            </div>

            <!-- Has Update Info -->
            <div v-if="updateCheckResult?.hasUpdate" class="new-version-panel">
              <div class="new-version-header">
                <div class="tag-badge">
                  <span class="pulse-dot"></span>
                  {{ t('settings.hasNewVersion') }}
                </div>
                <span v-if="updateCheckResult.checkTime" class="publish-date">
                  {{ formatDate(updateCheckResult.checkTime) }}
                </span>
              </div>
              
              <div class="digest-details-box" v-if="updateCheckResult.remoteDigest || updateCheckResult.currentDigest">
                <div class="digest-line" v-if="updateCheckResult.currentDigest">
                  <span class="d-label">{{ t('settings.currentDigest') }}:</span>
                  <code class="d-val">{{ updateCheckResult.currentDigest.substring(0, 19) }}...</code>
                </div>
                <div class="digest-line" v-if="updateCheckResult.remoteDigest">
                  <span class="d-label">{{ t('settings.remoteDigest') }}:</span>
                  <code class="d-val highlight">{{ updateCheckResult.remoteDigest.substring(0, 19) }}...</code>
                </div>
              </div>

              <div class="upgrade-actions">
                <el-button 
                  type="primary" 
                  @click="confirmSelfUpgrade"
                  :disabled="!updateCheckResult.canSelfUpgrade"
                >
                  {{ t('settings.oneClickUpgrade') }}
                </el-button>
              </div>
              <div v-if="!updateCheckResult.canSelfUpgrade" class="cannot-upgrade-hint">
                <el-icon><Warning /></el-icon> {{ updateCheckResult.reason || t('settings.cannotSelfUpgradeHint') }}
              </div>
            </div>

            <div v-else-if="!checkingUpdate && updateChecked" class="up-to-date-hint">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="check-icon">
                <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                <polyline points="22 4 12 14.01 9 11.01"></polyline>
              </svg>
              <span>{{ t('settings.isLatest') }}</span>
              <code v-if="updateCheckResult?.currentDigest" class="current-digest-text">
                ({{ updateCheckResult.currentDigest.substring(0, 19) }}...)
              </code>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- System Upgrade Modal -->
    <el-dialog
      v-model="upgradeDialogVisible"
      :title="t('settings.oneClickUpgrade')"
      width="460px"
      :close-on-click-modal="false"
      :close-on-press-escape="false"
      :show-close="upgradePhase === 'failed'"
      class="glass-dialog upgrade-modal"
    >
      <div class="upgrade-modal-content">
        <div class="upgrade-step-icon" :class="upgradePhase">
          <el-icon v-if="upgradePhase === 'pulling'" class="is-loading" :size="36"><Loading /></el-icon>
          <el-icon v-else-if="upgradePhase === 'restarting'" class="is-loading" :size="36"><Refresh /></el-icon>
          <el-icon v-else-if="upgradePhase === 'success'" :size="36" color="#22c55e"><CircleCheckFilled /></el-icon>
          <el-icon v-else-if="upgradePhase === 'failed'" :size="36" color="#ef4444"><CircleCloseFilled /></el-icon>
        </div>

        <h4 class="upgrade-step-title">{{ upgradeStepTitle }}</h4>
        <p class="upgrade-step-desc">{{ upgradeStepDetail }}</p>

        <el-progress
          v-if="upgradePhase === 'pulling'"
          :percentage="pullProgress"
          :indeterminate="pullIndeterminate"
          :stroke-width="8"
          style="margin: 20px 0"
        />

        <div v-if="upgradePhase === 'restarting'" class="restart-countdown">
          <span>{{ t('settings.upgradeWaitingHealthy') }}</span>
        </div>
      </div>
      <template #footer v-if="upgradePhase === 'failed'">
        <el-button @click="upgradeDialogVisible = false">{{ t('common.close') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAppStore } from '@/stores/app'
import { setLocale, getLocale } from '@/i18n'
import { useSettingsStore } from '@/stores/settings'
import { settingsApi } from '@/api/settings'
import { systemApi, type SelfUpdateCheckResult } from '@/api/system'
import { useImagePullProgress } from '@/composables/useImagePullProgress'
import { APP_NAME } from '@/utils/branding'
import { formatLocalizedDate } from '@/utils/date'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Loading, Refresh, CircleCheckFilled, CircleCloseFilled, Warning } from '@element-plus/icons-vue'

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

// --- Self Update States & Methods ---
const currentVersion = ref('0.9.7')
const checkingUpdate = ref(false)
const updateChecked = ref(false)
const updateCheckResult = ref<SelfUpdateCheckResult | null>(null)

const upgradeDialogVisible = ref(false)
const upgradePhase = ref<'idle' | 'pulling' | 'restarting' | 'success' | 'failed'>('idle')
const upgradeStepTitle = ref('')
const upgradeStepDetail = ref('')
const pullProgress = ref(0)
const pullIndeterminate = ref(true)

const updateTracking = useImagePullProgress(
  (pullId) => pullId === 'self-upgrade',
  false
)

watch(updateTracking.hasData, (hasData) => {
  if (hasData && updateTracking.progress.value > 0) {
    pullIndeterminate.value = false
  }
})

watch(updateTracking.progress, (p) => {
  pullProgress.value = p
})

watch([updateTracking.detail, updateTracking.step], ([d, s]) => {
  const text = d || s
  if (text) upgradeStepDetail.value = text
})

const formatDate = (dateStr?: string) => {
  if (!dateStr) return ''
  return formatLocalizedDate(dateStr, '-', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const fetchUpdateCheck = async (force = false) => {
  checkingUpdate.value = true
  try {
    const res = await systemApi.checkSelfUpdate(force)
    const data = (res as any)?.data || res
    if (data) {
      updateCheckResult.value = data
      if (data.currentVersion) {
        currentVersion.value = data.currentVersion
      }
      updateChecked.value = true
      if (force && !data.hasUpdate) {
        ElMessage.success(t('settings.isLatest'))
      }
    }
  } catch (err: any) {
    console.error('Failed to check self update:', err)
    if (force) {
      ElMessage.error(err?.message || t('settings.saveFailed'))
    }
  } finally {
    checkingUpdate.value = false
  }
}

const confirmSelfUpgrade = async () => {
  try {
    await ElMessageBox.confirm(
      t('settings.upgradeConfirmMsg'),
      t('settings.upgradeConfirmTitle'),
      {
        type: 'warning',
        confirmButtonText: t('settings.oneClickUpgrade'),
        cancelButtonText: t('common.cancel')
      }
    )
    await startSelfUpgrade()
  } catch {
    // User cancelled
  }
}

const startSelfUpgrade = async () => {
  upgradeDialogVisible.value = true
  upgradePhase.value = 'pulling'
  upgradeStepTitle.value = t('settings.upgradePulling')
  upgradeStepDetail.value = t('settings.upgradePreparing')
  pullProgress.value = 0
  pullIndeterminate.value = true
  updateTracking.clear()
  updateTracking.start()

  try {
    await systemApi.executeSelfUpgrade({
      targetImage: updateCheckResult.value?.imageName
    })
    updateTracking.stop()
    await pollHealthAndReload()
  } catch (err: any) {
    updateTracking.stop()
    upgradePhase.value = 'failed'
    upgradeStepTitle.value = t('settings.upgradeFailed')
    upgradeStepDetail.value = err?.message || err?.error || t('common.unknown')
    ElMessage.error(upgradeStepDetail.value)
  }
}

const pollHealthAndReload = async () => {
  upgradePhase.value = 'restarting'
  upgradeStepTitle.value = t('settings.upgradeRestarting')
  upgradeStepDetail.value = t('settings.upgradeWaitingHealthy')

  // 等待 3 秒让容器完成重启交接
  await new Promise(r => setTimeout(r, 3000))

  const maxAttempts = 30
  for (let i = 0; i < maxAttempts; i++) {
    await new Promise(r => setTimeout(r, 2000))
    try {
      const res = await systemApi.getSystemInfo()
      const data = (res as any)?.data || res
      if (data && (data.system || data.runtime)) {
        upgradePhase.value = 'success'
        upgradeStepTitle.value = t('settings.upgradeSuccess')
        upgradeStepDetail.value = `v${data.runtime?.version || updateCheckResult.value?.latestVersion || ''}`
        ElMessage.success(t('settings.upgradeSuccess'))
        setTimeout(() => {
          window.location.reload()
        }, 1500)
        return
      }
    } catch {
      // 正在重启中，继续轮询
    }
  }

  upgradePhase.value = 'failed'
  upgradeStepTitle.value = t('settings.upgradeFailed')
  upgradeStepDetail.value = '服务重启超时，请在宿主机检查容器运行状态或手动刷新页面。'
}

onMounted(() => {
  void loadSettings()
  void fetchUpdateCheck(false)
})
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

/* System Update Card & Modal Styles */
.card-icon.emerald { background: linear-gradient(135deg, #10b981, #059669); }

.update-card {
  border-color: rgba(16, 185, 129, 0.2);
}

.version-badge {
  font-size: 12px;
  font-family: monospace;
  padding: 3px 8px;
  border-radius: 6px;
  background: var(--bg-surface);
  border: 1px solid var(--border-color);
  color: var(--text-secondary);
}

.version-badge.has-update {
  background: rgba(16, 185, 129, 0.15);
  border-color: rgba(16, 185, 129, 0.4);
  color: #10b981;
  font-weight: bold;
}

.update-status-box {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.version-info-row {
  display: flex;
  align-items: center;
  gap: 20px;
  flex-wrap: wrap;
}

.version-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.version-item .label {
  font-size: 12px;
  color: var(--text-muted);
}

.version-item .val {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main);
}

.version-item .val.highlight {
  color: #10b981;
}

.version-action {
  margin-left: auto;
}

.new-version-panel {
  margin-top: 6px;
  padding: 16px;
  background: rgba(16, 185, 129, 0.06);
  border: 1px solid rgba(16, 185, 129, 0.2);
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.new-version-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.tag-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
  font-size: 13px;
  color: #10b981;
}

.pulse-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #10b981;
  box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7);
  animation: pulse-emerald 2s infinite;
}

@keyframes pulse-emerald {
  0% {
    transform: scale(0.95);
    box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7);
  }
  70% {
    transform: scale(1);
    box-shadow: 0 0 0 6px rgba(16, 185, 129, 0);
  }
  100% {
    transform: scale(0.95);
    box-shadow: 0 0 0 0 rgba(16, 185, 129, 0);
  }
}

.publish-date {
  font-size: 12px;
  color: var(--text-muted);
}

.digest-details-box {
  background: var(--bg-surface);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 10px 14px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.digest-line {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
}

.digest-line .d-label {
  color: var(--text-secondary);
  min-width: 90px;
}

.digest-line .d-val {
  font-family: monospace;
  color: var(--text-main);
  background: var(--bg-subtle);
  padding: 2px 6px;
  border-radius: 4px;
}

.digest-line .d-val.highlight {
  color: #10b981;
  font-weight: 600;
}

.current-digest-text {
  font-size: 11px;
  font-family: monospace;
  opacity: 0.8;
}

.upgrade-actions {
  display: flex;
  align-items: center;
  gap: 14px;
}

.cannot-upgrade-hint {
  font-size: 12px;
  color: #f59e0b;
  display: flex;
  align-items: center;
  gap: 6px;
}

.up-to-date-hint {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #10b981;
  padding: 10px 14px;
  background: rgba(16, 185, 129, 0.05);
  border-radius: 8px;
  border: 1px solid rgba(16, 185, 129, 0.15);
}

.check-icon {
  width: 16px;
  height: 16px;
}

.upgrade-modal-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding: 16px 8px;
}

.upgrade-step-icon {
  margin-bottom: 16px;
}

.upgrade-step-title {
  margin: 0 0 8px 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-main);
}

.upgrade-step-desc {
  margin: 0 0 16px 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.restart-countdown {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 12px;
  font-size: 13px;
  color: var(--text-muted);
}
</style>
