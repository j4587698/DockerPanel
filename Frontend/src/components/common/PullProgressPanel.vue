<template>
  <div class="pull-progress-panel">
    <div class="pull-progress-head">
      <span v-if="image" class="pull-progress-image font-mono">{{ image }}</span>
      <span class="pull-progress-step">{{ stepText }}</span>
    </div>
    <el-progress
      :percentage="displayProgress"
      :indeterminate="indeterminate"
      :stroke-width="8"
      :status="status"
      style="margin: 8px 0"
    />
    <div v-if="detail" class="pull-progress-detail">
      <span class="detail-dot" :class="{ done: isDone }"></span>
      {{ detail }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ComputedRef } from 'vue'

const props = defineProps<{
  /** 0-100 的聚合进度 */
  progress?: number
  /** 当前阶段文字（准备中/拉取中/重启中/完成/失败 等） */
  step?: string
  /** 详细描述 */
  detail?: string
  /** 镜像名 */
  image?: string
  /** 用 indeterminate 进度条（例如没有拉镜像但正在重建时） */
  indeterminate?: boolean
}>()

const displayProgress: ComputedRef<number> = computed(() => {
  if (props.indeterminate) return 0
  return Math.max(0, Math.min(100, props.progress ?? 0))
})

const isDone = computed(() => {
  const s = props.step || ''
  return s === '完成' || s === '失败' || s === 'Success' || s === 'Failed'
})

const status = computed<'' | 'success' | 'exception'>(() => {
  if (props.indeterminate) return ''
  if ((props.step || '') === '完成') return 'success'
  if ((props.step || '') === '失败') return 'exception'
  return ''
})

const stepText = computed(() => {
  if (props.image && !props.step) return ''
  return props.step || ''
})
</script>

<style scoped>
.pull-progress-panel {
  padding: 4px 0;
}

.pull-progress-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.pull-progress-image {
  font-size: 12px;
  color: var(--text-secondary);
  word-break: break-all;
}

.pull-progress-step {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-main);
  white-space: nowrap;
}

.pull-progress-detail {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-muted);
  line-height: 1.5;
  word-break: break-all;
}

.detail-dot {
  flex: none;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-primary, #409eff);
  animation: dot-pulse 1.2s ease-in-out infinite;
}

.detail-dot.done {
  animation: none;
  background: var(--color-success, #67c23a);
}

@keyframes dot-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.35; }
}
</style>