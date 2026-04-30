<template>
  <div class="ssh-settings">
    <el-form :model="settings" label-width="160px" v-loading="loading">
      <div class="settings-grid">
        <!-- 连接设置 -->
        <el-card class="settings-section">
          <template #header>
            <div class="section-header">
              <el-icon><Connection /></el-icon>
              <span>{{ t('ssh.settings.connectionSettings') }}</span>
            </div>
          </template>

          <el-form-item :label="t('ssh.settings.connectionTimeout')">
            <el-input-number v-model="settings.connectionTimeout" :min="5" :max="120" />
            <span class="input-suffix">{{ t('common.seconds') }}</span>
          </el-form-item>

          <el-form-item :label="t('ssh.settings.commandExecTimeout')">
            <el-input-number v-model="settings.commandTimeout" :min="5" :max="600" />
            <span class="input-suffix">{{ t('common.seconds') }}</span>
          </el-form-item>

          <el-form-item :label="t('ssh.settings.keepAliveInterval')">
            <el-input-number v-model="settings.keepAliveInterval" :min="0" :max="300" />
            <span class="input-suffix">{{ t('ssh.settings.keepAliveIntervalHint') }}</span>
          </el-form-item>
        </el-card>

        <!-- 安全设置 -->
        <el-card class="settings-section">
          <template #header>
            <div class="section-header">
              <el-icon><Lock /></el-icon>
              <span>{{ t('ssh.settings.securitySettings') }}</span>
            </div>
          </template>

          <el-form-item :label="t('ssh.settings.defaultKeyPath')">
            <el-input v-model="settings.defaultKeyPath" :placeholder="t('ssh.settings.defaultKeyPathPlaceholder')" />
          </el-form-item>

          <el-form-item :label="t('ssh.settings.preferKeyAuth')">
            <el-switch v-model="settings.preferKeyAuth" />
            <span class="switch-hint">{{ t('ssh.settings.preferKeyAuthHint') }}</span>
          </el-form-item>

          <el-form-item :label="t('ssh.settings.strictHostKeyChecking')">
            <el-switch v-model="settings.strictHostKeyChecking" />
            <span class="switch-hint">{{ t('ssh.settings.strictHostKeyCheckingHint') }}</span>
          </el-form-item>
        </el-card>

        <!-- 终端设置 -->
        <el-card class="settings-section">
          <template #header>
            <div class="section-header">
              <el-icon><Monitor /></el-icon>
              <span>{{ t('ssh.settings.terminalSettings') }}</span>
            </div>
          </template>

          <el-form-item :label="t('ssh.settings.terminalType')">
            <el-select v-model="settings.terminalType" style="width: 200px">
              <el-option label="xterm-256color" value="xterm-256color" />
              <el-option label="xterm" value="xterm" />
              <el-option label="vt100" value="vt100" />
              <el-option label="linux" value="linux" />
            </el-select>
          </el-form-item>

          <el-form-item :label="t('ssh.settings.defaultTerminalSize')">
            <el-input-number v-model="settings.terminalCols" :min="40" :max="500" />
            <span class="input-suffix">{{ t('ssh.settings.columns') }} ×</span>
            <el-input-number v-model="settings.terminalRows" :min="10" :max="100" style="margin-left: 8px" />
            <span class="input-suffix">{{ t('ssh.settings.rows') }}</span>
          </el-form-item>
        </el-card>

        <!-- 日志设置 -->
        <el-card class="settings-section">
          <template #header>
            <div class="section-header">
              <el-icon><DocumentCopy /></el-icon>
              <span>{{ t('ssh.settings.logSettings') }}</span>
            </div>
          </template>

          <el-form-item :label="t('ssh.settings.enableOperationLog')">
            <el-switch v-model="settings.enableOperationLog" />
          </el-form-item>

          <el-form-item :label="t('ssh.settings.logRetentionDays')" v-if="settings.enableOperationLog">
            <el-input-number v-model="settings.logRetentionDays" :min="1" :max="365" />
            <span class="input-suffix">{{ t('common.days') }}</span>
          </el-form-item>

          <el-form-item :label="t('ssh.settings.logCommandContent')" v-if="settings.enableOperationLog">
            <el-switch v-model="settings.logCommandContent" />
            <span class="switch-hint">{{ t('ssh.settings.logCommandContentHint') }}</span>
          </el-form-item>
        </el-card>
      </div>

      <!-- 操作按钮 -->
      <div class="form-actions">
        <el-button @click="resetSettings">{{ t('ssh.settings.resetToDefault') }}</el-button>
        <el-button type="primary" @click="saveSettings" :loading="saving">
          {{ t('ssh.settings.saveSettings') }}
        </el-button>
      </div>
    </el-form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Connection, Lock, Monitor, DocumentCopy } from '@element-plus/icons-vue'
import { useSshStore } from '@/stores/ssh'
import { useI18n } from 'vue-i18n'

interface SshSettings {
  // 连接设置
  connectionTimeout: number
  commandTimeout: number
  keepAliveInterval: number

  // 安全设置
  defaultKeyPath: string
  preferKeyAuth: boolean
  strictHostKeyChecking: boolean

  // 终端设置
  terminalType: string
  terminalCols: number
  terminalRows: number

  // 日志设置
  enableOperationLog: boolean
  logRetentionDays: number
  logCommandContent: boolean
}

const sshStore = useSshStore()
const loading = ref(false)
const saving = ref(false)

// i18n
const { t } = useI18n()

const defaultSettings: SshSettings = {
  connectionTimeout: 30,
  commandTimeout: 60,
  keepAliveInterval: 30,

  defaultKeyPath: '~/.ssh/id_rsa',
  preferKeyAuth: true,
  strictHostKeyChecking: false,

  terminalType: 'xterm-256color',
  terminalCols: 120,
  terminalRows: 30,

  enableOperationLog: true,
  logRetentionDays: 30,
  logCommandContent: true
}

const settings = reactive<SshSettings>({ ...defaultSettings })

onMounted(() => {
  loadSettings()
})

const loadSettings = async () => {
  loading.value = true
  try {
    const response = await sshStore.fetchSettings()
    if (response) {
      Object.assign(settings, response)
    }
  } catch {
    // 使用默认设置
  } finally {
    loading.value = false
  }
}

const saveSettings = async () => {
  saving.value = true
  try {
    await sshStore.updateSettings(settings)
    ElMessage.success(t('ssh.settings.saveSuccess'))
  } catch {
    ElMessage.error(t('ssh.settings.saveFailed'))
  } finally {
    saving.value = false
  }
}

const resetSettings = async () => {
  try {
    await ElMessageBox.confirm(
      t('ssh.settings.resetConfirm'),
      t('ssh.settings.resetToDefault'),
      { type: 'warning' }
    )

    Object.assign(settings, defaultSettings)
    ElMessage.success(t('ssh.settings.resetSuccess'))
  } catch {
    // 用户取消
  }
}
</script>

<style scoped>
.ssh-settings {
  width: 100%;
}

.settings-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
  width: 100%;
}

@media (max-width: 900px) {
  .settings-grid {
    grid-template-columns: 1fr;
  }
}

.settings-section {
  margin-bottom: 0;
}

.settings-section :deep(.el-card__header) {
  padding: 16px 20px;
  background-color: #f8f9fa;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 15px;
  font-weight: 600;
  color: #303133;
}

.section-header .el-icon {
  color: #409eff;
}

.input-suffix {
  margin-left: 8px;
  color: #909399;
  font-size: 13px;
}

.switch-hint {
  margin-left: 12px;
  color: #909399;
  font-size: 12px;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 20px 0;
}
</style>

<style>
/* === Dark Mode === */
html.dark .settings-section .el-card__header {
  background-color: #1a1a1a;
}

html.dark .section-header {
  color: #e5eaf3;
}
</style>