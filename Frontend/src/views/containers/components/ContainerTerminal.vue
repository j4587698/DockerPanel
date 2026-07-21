<template>
  <div class="full-height-view">
    <div class="view-toolbar terminal-toolbar-bg">
      <div class="terminal-toolbar-left">
        <span class="terminal-status-badge" :class="connected ? 'connected' : 'disconnected'">
          <span class="status-indicator"></span>
          {{ connected ? t('container.connected') : t('container.disconnected') }}
        </span>
      </div>
      <div class="terminal-toolbar-center">
        <span class="shell-label">{{ t('container.terminalDialog.shell') }}:</span>
        <el-select :model-value="shell" size="small" style="width: 130px" :disabled="connected" @update:model-value="$emit('update:shell', $event)">
          <el-option v-for="s in availableShells" :key="s" :label="s" :value="s" />
        </el-select>
      </div>
      <div class="terminal-toolbar-right">
        <el-button 
          v-if="!connected"
          type="primary" 
          size="small" 
          @click="$emit('connect')"
          :loading="connecting"
         :icon="Connection">{{ t('container.terminalDialog.connect') }}</el-button>
        <el-button 
          v-else 
          type="danger" 
          size="small" 
          plain 
          @click="$emit('disconnect')"
         :icon="Close">{{ t('container.terminalDialog.disconnect') }}</el-button>
      </div>
    </div>
    <div class="console-window terminal-window" ref="terminalContainer">
      <div ref="xtermContainer" class="xterm-container" @click="$emit('focus')"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Connection, Close } from '@element-plus/icons-vue'

defineProps<{
  connected: boolean
  connecting: boolean
  shell: string
  availableShells: string[]
}>()

defineEmits<{
  'update:shell': [value: string]
  'connect': []
  'disconnect': []
  'focus': []
}>()

const { t } = useI18n()
const terminalContainer = ref<HTMLElement>()
const xtermContainer = ref<HTMLElement>()

defineExpose({
  terminalContainer,
  xtermContainer
})
</script>

<style>

/* 使用非scoped样式，继承父组件全局样式 */

</style>
