<template>
  <div class="config-view-container">
    <div class="config-content">
      <div class="config-grid">
        <!-- 重启策略卡片 -->
        <div class="config-card">
          <div class="config-card-header">
            <el-icon class="config-card-icon"><RefreshRight /></el-icon>
            <h4 class="settings-group-title">{{ t('container.restartStrategy') }}</h4>
          </div>
          <el-form label-position="top" size="small">
            <el-form-item :label="t('container.selectPolicy')">
              <el-select :model-value="config.restartPolicy" class="w-full" @update:model-value="onRestartPolicyChange">
                <el-option :label="t('container.noRestart')" value="no" />
                <el-option :label="t('container.alwaysRestart')" value="always" />
                <el-option :label="t('container.restartOnFailure')" value="on-failure" />
                <el-option :label="t('container.restartUnlessStopped')" value="unless-stopped" />
              </el-select>
            </el-form-item>
          </el-form>
        </div>

        <!-- 资源限制卡片 -->
        <div class="config-card">
          <div class="config-card-header">
            <el-icon class="config-card-icon"><Cpu /></el-icon>
            <h4 class="settings-group-title">{{ t('container.resourceLimits') }}</h4>
          </div>
          <el-form label-position="top" size="small">
            <el-form-item :label="t('container.memoryLimit')">
              <el-input :model-value="config.memoryLimit" placeholder="512, 1024..." clearable @update:model-value="onMemoryLimitChange">
                <template #append>
                  <el-select :model-value="config.memoryUnit" style="width: 80px" @update:model-value="onMemoryUnitChange">
                    <el-option label="MB" value="m" />
                    <el-option label="GB" value="g" />
                  </el-select>
                </template>
              </el-input>
            </el-form-item>
            <el-form-item :label="t('container.cpuQuota')">
              <el-input-number :model-value="config.cpuQuota" :min="0" :max="16" :step="0.5" :precision="2" class="w-full" @update:model-value="onCpuQuotaChange" />
            </el-form-item>
          </el-form>
        </div>

        <!-- 高级设置卡片 -->
        <div class="config-card">
          <div class="config-card-header">
            <el-icon class="config-card-icon"><Setting /></el-icon>
            <h4 class="settings-group-title">{{ t('container.advancedSettings') }}</h4>
          </div>
          <el-form label-position="top" size="small">
            <el-form-item :label="t('container.memoryReservation')">
              <el-input :model-value="config.memoryReservation" placeholder="256..." clearable @update:model-value="onMemoryReservationChange">
                <template #append>
                  <el-select :model-value="config.memoryReservationUnit" style="width: 80px" @update:model-value="onMemoryReservationUnitChange">
                    <el-option label="MB" value="m" />
                    <el-option label="GB" value="g" />
                  </el-select>
                </template>
              </el-input>
            </el-form-item>
            <el-form-item :label="t('container.cpuPriority')">
              <el-input-number :model-value="config.cpuShares" :min="0" :max="1024" :step="64" class="w-full" @update:model-value="onCpuSharesChange" />
              <span class="form-hint">{{ t('container.cpuPriorityHint') }}</span>
            </el-form-item>
          </el-form>
        </div>
      </div>
    </div>

    <!-- 底部操作栏 -->
    <div class="config-footer">
      <div class="config-footer-content">
        <span class="config-footer-hint">{{ t('container.configHint') }}</span>
        <div class="config-footer-actions">
          <el-button @click="$emit('reset')" :icon="RefreshLeft">{{ t('container.reset') }}</el-button>
          <el-button type="primary" @click="$emit('save')" :loading="saving" :icon="Check">{{ t('container.saveConfig') }}</el-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Check, RefreshRight, RefreshLeft, Setting, Cpu } from '@element-plus/icons-vue'

export interface ContainerConfigData {
  restartPolicy: string
  memoryLimit: string
  memoryUnit: string
  memoryReservation: string
  memoryReservationUnit: string
  cpuQuota: number
  cpuShares: number
}

const props = defineProps<{
  config: ContainerConfigData
  saving: boolean
}>()

const emit = defineEmits<{
  'update:config': [value: ContainerConfigData]
  'save': []
  'reset': []
}>()

const { t } = useI18n()

const updateConfig = (key: keyof ContainerConfigData, value: any) => {
  emit('update:config', { ...props.config, [key]: value })
}

const onRestartPolicyChange = (v: string) => updateConfig('restartPolicy', v)
const onMemoryLimitChange = (v: string) => updateConfig('memoryLimit', v)
const onMemoryUnitChange = (v: string) => updateConfig('memoryUnit', v)
const onMemoryReservationChange = (v: string) => updateConfig('memoryReservation', v)
const onMemoryReservationUnitChange = (v: string) => updateConfig('memoryReservationUnit', v)
const onCpuQuotaChange = (v: number) => updateConfig('cpuQuota', v)
const onCpuSharesChange = (v: number) => updateConfig('cpuShares', v)
</script>

<style>

/* 使用非scoped样式，继承父组件全局样式 */

</style>