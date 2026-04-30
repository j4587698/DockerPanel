<template>
  <div class="config-view-container">
    <div class="config-content">
      <div class="config-grid auto-update-grid">
        <!-- 状态概览卡片 -->
        <div class="config-card">
          <div class="config-card-header">
            <el-icon class="config-card-icon"><Refresh /></el-icon>
            <h4 class="settings-group-title">{{ t('container.updateStatus') }}</h4>
            <el-tag :type="statusColor" size="small" class="status-tag">{{ statusText }}</el-tag>
          </div>
          
          <div class="status-info-list">
            <div class="status-info-item">
              <span class="info-label">{{ t('container.currentImage') }}</span>
              <span class="info-value font-mono">{{ containerImage }}</span>
            </div>
            <div class="status-info-item" v-if="config?.currentLocalDigest">
              <span class="info-label">{{ t('container.localDigest') }}</span>
              <span class="info-value font-mono digest">{{ formatDigest(config.currentLocalDigest) }}</span>
            </div>
            <div class="status-info-item" v-if="config?.lastRemoteDigest">
              <span class="info-label">{{ t('container.remoteDigest') }}</span>
              <span class="info-value font-mono digest">{{ formatDigest(config.lastRemoteDigest) }}</span>
            </div>
            <div class="status-info-item" v-if="config?.lastCheckTime">
              <span class="info-label">{{ t('container.lastCheck') }}</span>
              <span class="info-value">{{ formatDate(config.lastCheckTime) }}</span>
            </div>
          </div>
          
          <!-- 操作按钮 -->
          <div class="action-buttons">
            <el-button 
              type="primary" 
              :loading="checking"
              @click="handleCheckUpdate"
            >
              <el-icon><Search /></el-icon>
              {{ t('container.checkUpdate') }}
            </el-button>
            <el-button 
              v-if="config?.hasUpdateAvailable"
              type="warning"
              :loading="updating"
              @click="handleUpdate(false)"
            >
              <el-icon><Download /></el-icon>
              {{ t('container.pullAndRestart') }}
            </el-button>
            <el-button 
              v-if="config?.hasUpdateAvailable"
              type="success"
              :loading="updating"
              @click="handleUpdate(true)"
            >
              <el-icon><Download /></el-icon>
              {{ t('container.pullOnly') }}
            </el-button>
          </div>
          
          <!-- 回滚功能 -->
          <div class="rollback-section">
            <el-divider content-position="left">
              <el-icon><Back /></el-icon>
              {{ t('container.rollback') }}
            </el-divider>
            <div class="rollback-controls">
              <el-select 
                v-model="selectedTag" 
                :placeholder="t('container.selectTargetVersion')" 
                style="width: 200px"
                :loading="loadingTags"
                filterable
              >
                <el-option 
                  v-for="tag in availableTags" 
                  :key="tag" 
                  :label="tag" 
                  :value="tag"
                />
              </el-select>
              <el-button 
                type="warning" 
                :loading="rollingBack"
                :disabled="!selectedTag"
                @click="handleRollback"
              >
                <el-icon><Back /></el-icon>
                {{ t('container.confirmRollback') }}
              </el-button>
              <el-button @click="loadImageTags" :loading="loadingTags">
                {{ t('common.refresh') }}
              </el-button>
            </div>
            <div class="rollback-hint">
              {{ t('container.selectTargetVersion') }}
            </div>
          </div>
        </div>
        
        <!-- 配置卡片 -->
        <div class="config-card">
          <div class="config-card-header">
            <el-icon class="config-card-icon"><Setting /></el-icon>
            <h4 class="settings-group-title">{{ t('container.upgradeConfig') }}</h4>
          </div>
          
          <el-form :model="formConfig" label-position="top" size="small">
            <!-- 第一行：自动检测 + 检测间隔 -->
            <div class="form-row">
              <div class="form-row-item">
                <div class="switch-option">
                  <el-switch v-model="formConfig.enableUpdateCheck" />
                  <div class="switch-content">
                    <span class="switch-label">{{ t('container.autoCheck') }}</span>
                    <span class="switch-hint">{{ t('container.autoCheckHint') }}</span>
                  </div>
                </div>
              </div>
              <div class="form-row-item interval-item">
                <span class="interval-label">{{ t('container.checkInterval') }}</span>
                <el-select v-model="formConfig.checkIntervalHours" style="width: 120px">
                  <el-option :label="t('container.everyHour')" :value="1" />
                  <el-option :label="t('container.every3Hours')" :value="3" />
                  <el-option :label="t('container.every6Hours')" :value="6" />
                  <el-option :label="t('container.every12Hours')" :value="12" />
                  <el-option :label="t('container.everyDay')" :value="24" />
                </el-select>
              </div>
            </div>
            
            <el-alert type="warning" :closable="false" style="margin: 16px 0">
              {{ t('container.warning') }}
            </el-alert>
            
            <!-- 第二行：自动拉取 + 自动重启 -->
            <div class="form-row">
              <div class="form-row-item">
                <div class="switch-option">
                  <el-switch v-model="formConfig.enableAutoPull" />
                  <div class="switch-content">
                    <span class="switch-label">{{ t('container.autoPull') }}</span>
                    <span class="switch-hint">{{ t('container.autoPullHint') }}</span>
                  </div>
                </div>
              </div>
              <div class="form-row-item">
                <div class="switch-option" :class="{ disabled: !formConfig.enableAutoPull }">
                  <el-switch v-model="formConfig.enableAutoRestart" :disabled="!formConfig.enableAutoPull" />
                  <div class="switch-content">
                    <span class="switch-label">{{ t('container.autoRestart') }}</span>
                    <span class="switch-hint">{{ t('container.autoRestartHint') }}</span>
                  </div>
                </div>
              </div>
            </div>
            
            <div class="form-actions">
              <el-button type="primary" @click="handleSaveConfig()" :loading="saving">
                {{ t('container.saveConfig') }}
              </el-button>
            </div>
          </el-form>
        </div>
      </div>
      
      <!-- 升级历史 -->
      <div class="config-card history-card" v-if="config?.updateHistory && config.updateHistory.length > 0">
        <div class="config-card-header">
          <el-icon class="config-card-icon"><Clock /></el-icon>
          <h4 class="settings-group-title">{{ t('container.updateHistory') }}</h4>
        </div>
        
        <el-table :data="config.updateHistory.slice(-10).reverse()" size="small">
          <el-table-column :label="t('container.time')" width="180">
            <template #default="{ row }">
              {{ formatDate(row.time) }}
            </template>
          </el-table-column>
          <el-table-column :label="t('container.action')" width="100">
            <template #default="{ row }">
              {{ actionText(row.action) }}
            </template>
          </el-table-column>
          <el-table-column :label="t('common.status')" width="80">
            <template #default="{ row }">
              <el-tag :type="row.success ? 'success' : 'danger'" size="small">
                {{ row.success ? t('common.success') : t('common.error') }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column :label="t('container.duration')" width="100">
            <template #default="{ row }">
              {{ row.durationMs ? `${row.durationMs}ms` : '-' }}
            </template>
          </el-table-column>
          <el-table-column :label="t('container.message')" min-width="200">
            <template #default="{ row }">
              <span v-if="row.errorMessage" class="error-text">{{ row.errorMessage }}</span>
              <span v-else-if="row.newDigest" class="font-mono digest">{{ formatDigest(row.newDigest) }}</span>
              <span v-else>-</span>
            </template>
          </el-table-column>
        </el-table>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh, Search, Download, Setting, Clock, Back } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()
import { 
  autoUpdateApi, 
  type ContainerAutoUpdateConfig, 
  AutoUpdateStatus,
  UpdateAction,
  statusTextMap,
  statusColorMap
} from '@/api/autoUpdate'

interface Props {
  containerId: string
  containerName: string
  containerImage: string
}

const props = defineProps<Props>()

const config = ref<ContainerAutoUpdateConfig | null>(null)
const checking = ref(false)
const updating = ref(false)
const saving = ref(false)

// 回滚相关
const availableTags = ref<string[]>([])
const selectedTag = ref('')
const loadingTags = ref(false)
const rollingBack = ref(false)

// 配置表单
const formConfig = ref({
  enableUpdateCheck: true,
  enableAutoPull: false,
  enableAutoRestart: false,
  checkIntervalHours: 6
})

// 状态显示
const statusText = computed(() => {
  if (!config.value) return t('container.autoUpdateStatus.notConfigured')
  if (config.value.status === AutoUpdateStatus.Unknown && !config.value.lastCheckTime) {
    return t('container.autoUpdateStatus.pending')
  }
  return statusTextMap[config.value.status] || t('common.unknown')
})

const statusColor = computed(() => {
  if (!config.value) return 'info'
  if (config.value.status === AutoUpdateStatus.Unknown && !config.value.lastCheckTime) {
    return 'info'
  }
  return statusColorMap[config.value.status] || 'info'
})

// 加载配置
const loadConfig = async () => {
  try {
    const res = await autoUpdateApi.getConfig(props.containerId)
    config.value = res.data || res as any
    
    // 同步到表单
    if (config.value) {
      formConfig.value = {
        enableUpdateCheck: config.value.enableUpdateCheck,
        enableAutoPull: config.value.enableAutoPull,
        enableAutoRestart: config.value.enableAutoRestart,
        checkIntervalHours: config.value.checkIntervalHours
      }
    }
  } catch (error) {
    console.error('加载自动升级配置失败:', error)
  }
}

// 检查更新
const handleCheckUpdate = async () => {
  checking.value = true
  try {
    // 先确保配置存在
    if (!config.value || config.value.status === AutoUpdateStatus.Disabled) {
      await handleSaveConfig(true) // 静默保存默认配置
    }
    
    const res = await autoUpdateApi.checkUpdate(props.containerId)
    const result = res.data || res as any
    
    if (result.errorMessage) {
      ElMessage.error(result.errorMessage)
    } else if (result.hasUpdate) {
      ElMessage.warning(t('container.newVersionAvailable'))
    } else {
      ElMessage.success(t('container.autoUpdateStatus.upToDate'))
    }
    
    await loadConfig()
  } catch (error: any) {
    ElMessage.error(error.message || t('container.autoUpdateStatus.checkFailed'))
  } finally {
    checking.value = false
  }
}

// 更新容器
const handleUpdate = async (pullOnly: boolean) => {
  updating.value = true
  try {
    const res = await autoUpdateApi.updateContainer(props.containerId, pullOnly)
    const result = res.data || res as any
    
    if (result.success) {
      ElMessage.success(pullOnly ? t('container.imagePullSuccess') : t('container.containerUpdated'))
    } else {
      ElMessage.error(result.errorMessage || t('common.error'))
    }
    
    await loadConfig()
  } catch (error: any) {
    ElMessage.error(error.message || t('container.autoUpdateStatus.updateFailed'))
  } finally {
    updating.value = false
  }
}

// 保存配置
const handleSaveConfig = async (silent = false) => {
  saving.value = true
  try {
    const res = await autoUpdateApi.setConfig(props.containerId, {
      ...formConfig.value,
      containerName: props.containerName
    })
    // request 拦截器已经处理了 data.data，所以 res 就是配置对象
    config.value = res as any
    if (!silent) {
      ElMessage.success(t('container.configSaved'))
    }
  } catch (error: any) {
    console.error('保存配置失败:', error)
    if (!silent) {
      ElMessage.error(error.message || t('container.autoUpdateStatus.saveConfigFailed'))
    }
  } finally {
    saving.value = false
  }
}

// 加载镜像标签列表
const loadImageTags = async () => {
  if (!props.containerImage) return
  
  loadingTags.value = true
  try {
    const res = await autoUpdateApi.getImageTags(props.containerImage)
    availableTags.value = res.data || res as any || []
    
    // 从当前镜像中提取当前标签并移除
    const currentTag = props.containerImage.split(':')[1] || 'latest'
    availableTags.value = availableTags.value.filter(t => t !== currentTag)
  } catch (error: any) {
    console.error('加载镜像标签失败:', error)
    ElMessage.warning(t('container.autoUpdateStatus.loadTagsFailed'))
  } finally {
    loadingTags.value = false
  }
}

// 回滚容器
const handleRollback = async () => {
  if (!selectedTag.value) return
  
  try {
    await ElMessageBox.confirm(
      t('container.autoUpdateStatus.rollbackConfirmMessage', { tag: selectedTag.value }),
      t('container.confirmRollback'),
      {
        type: 'warning',
        confirmButtonText: t('container.confirmRollback'),
        cancelButtonText: t('common.cancel')
      }
    )
    
    rollingBack.value = true
    const res = await autoUpdateApi.rollbackContainer(props.containerId, selectedTag.value)
    const result = res.data || res as any
    
    if (result.success) {
      ElMessage.success(t('container.autoUpdateStatus.rollbackSuccess', { tag: selectedTag.value }))
      selectedTag.value = ''
      await loadConfig()
    } else {
      ElMessage.error(result.errorMessage || t('container.rollbackFailed'))
    }
  } catch (e: any) {
    if (e !== 'cancel' && e !== 'close') {
      ElMessage.error(e.message || t('container.rollbackFailed'))
    }
  } finally {
    rollingBack.value = false
  }
}

// 格式化摘要
const formatDigest = (digest: string | null) => {
  if (!digest) return '-'
  // 提取 sha256 后的 12 位
  const match = digest.match(/sha256:([a-f0-9]+)/i)
  if (match) {
    return match[1].substring(0, 12)
  }
  return digest.substring(0, 12)
}

// 格式化日期
const formatDate = (date: string) => {
  return formatLocalizedDateTime(date, '--')
}

// 操作类型文本
const actionText = (action: UpdateAction) => {
  const map: Record<UpdateAction, string> = {
    [UpdateAction.Check]: t('container.autoUpdateStatus.actionCheck'),
    [UpdateAction.Pull]: t('container.autoUpdateStatus.actionPull'),
    [UpdateAction.Restart]: t('container.autoUpdateStatus.actionRestart'),
    [UpdateAction.FullUpdate]: t('container.autoUpdateStatus.actionFullUpdate')
  }
  return map[action] || t('common.unknown')
}

onMounted(() => {
  loadConfig()
  loadImageTags()
})

// 监听容器变化
watch(() => props.containerId, () => {
  loadConfig()
  loadImageTags()
})
</script>

<style>
.auto-update-grid {
  grid-template-columns: 1fr;
}

.status-tag {
  margin-left: auto;
}

.status-info-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 20px;
}

.status-info-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.status-info-item .info-label {
  font-size: 13px;
  color: var(--text-muted);
  min-width: 80px;
}

.status-info-item .info-value {
  font-size: 14px;
  color: var(--text-main);
}

.status-info-item .info-value.digest {
  background: var(--bg-subtle);
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 13px;
}

.action-buttons {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  padding-top: 16px;
  border-top: 1px solid var(--border-color);
}

/* 回滚区域 */
.rollback-section {
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--border-color);
}

.rollback-controls {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-top: 12px;
}

.rollback-hint {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 8px;
}

/* 表单行布局 */
.form-row {
  display: flex;
  gap: 24px;
  margin-bottom: 16px;
}

.form-row-item {
  flex: 1;
  min-width: 0;
}

/* Switch 选项样式 */
.switch-option {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.switch-option.disabled {
  opacity: 0.5;
}

.switch-content {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.switch-label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-main);
}

.switch-hint {
  font-size: 12px;
  color: var(--text-muted);
}

/* 检测间隔行 */
.interval-item {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 0 0 auto;
}

.interval-label {
  font-size: 14px;
  color: var(--text-secondary);
  white-space: nowrap;
}

.form-actions {
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--border-color);
}

.history-card {
  margin-top: 20px;
}

.error-text {
  color: var(--color-danger);
}

.w-full {
  width: 100%;
}

html.dark .status-info-item .info-value.digest {
  background: var(--bg-elevated);
}

@media (max-width: 600px) {
  .form-row {
    flex-direction: column;
    gap: 16px;
  }
  
  .interval-item {
    width: 100%;
  }
}
</style>