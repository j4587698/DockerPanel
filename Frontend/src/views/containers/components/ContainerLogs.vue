<template>
  <div class="full-height-view">
    <div class="view-toolbar">
      <div class="toolbar-left">
        <el-select v-model="localTail" size="small" style="width: 140px" @change="onTailChange">
          <el-option :label="t('container.lastLines', { n: 100 })" :value="100" />
          <el-option :label="t('container.lastLines', { n: 500 })" :value="500" />
          <el-option :label="t('container.lastLines', { n: 1000 })" :value="1000" />
          <el-option :label="t('common.all')" :value="-1" />
        </el-select>
        <el-checkbox v-model="localFollow" size="small" class="auto-scroll-chk">{{ t('container.autoScroll') }}</el-checkbox>
      </div>
      <div class="toolbar-right">
        <el-tooltip :content="t('common.refresh')">
          <el-button @click="$emit('refresh')" size="small" plain><el-icon><Refresh /></el-icon></el-button>
        </el-tooltip>
        <el-tooltip :content="t('container.clearLogs')">
          <el-button @click="$emit('clear')" size="small" plain><el-icon><Delete /></el-icon></el-button>
        </el-tooltip>
        <el-tooltip :content="t('container.downloadLogs')">
          <el-button @click="$emit('download')" size="small" plain><el-icon><Download /></el-icon></el-button>
        </el-tooltip>
      </div>
    </div>
    <div class="console-window">
      <el-input
        ref="logsTextareaRef"
        :model-value="logs"
        type="textarea"
        readonly
        :placeholder="t('common.loading')"
        class="console-textarea"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import { Refresh, Delete, Download } from '@element-plus/icons-vue'

const props = defineProps<{
  logs: string
  tail: number
  follow: boolean
}>()

const emit = defineEmits<{
  'update:tail': [value: number]
  'update:follow': [value: boolean]
  'refresh': []
  'clear': []
  'download': []
}>()

const { t } = useI18n()
const logsTextareaRef = ref<{ textarea: HTMLTextAreaElement } | null>(null)

const localTail = ref(props.tail)
const localFollow = ref(props.follow)

watch(() => props.tail, v => localTail.value = v)
watch(() => props.follow, v => localFollow.value = v)

const onTailChange = (val: number) => {
  emit('update:tail', val)
}

watch(localFollow, (val) => {
  emit('update:follow', val)
  if (val) {
    nextTick(() => {
      const textarea = logsTextareaRef.value?.textarea
      if (textarea) {
        textarea.scrollTop = textarea.scrollHeight
      }
    })
  }
})
</script>

<style>
/* 使用非scoped样式，继承父组件全局样式 */
</style>
