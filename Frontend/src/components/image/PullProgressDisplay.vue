<template>
  <div class="pull-progress-container">
    <div class="pull-header">
      <el-icon class="is-loading"><Loading /></el-icon>
      <span>{{ title }}</span>
    </div>
    <div class="progress-list">
      <div v-for="item in sortedProgress" :key="item.id" class="progress-item">
        <div class="item-meta">
          <span class="layer-id">{{ item.id === 'system' ? '' : item.id }}</span>
          <span class="status-text" :class="{ 'status-complete': item.isComplete }">
            {{ item.status }}
            <span v-if="item.showSize" class="size-info">{{ item.sizeText }}</span>
          </span>
        </div>
        <el-progress 
          v-if="item.showProgress" 
          :percentage="item.percentage"
          :status="item.isComplete ? 'success' : ''"
          :stroke-width="10"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { Loading } from '@element-plus/icons-vue'

const props = defineProps<{
  title: string
  progressMap: Record<string, any>
}>()

// 记录层的出现顺序
const layerOrder = ref<string[]>([])

// 格式化文件大小
const formatSize = (bytes: number): string => {
  if (bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return (bytes / Math.pow(1024, i)).toFixed(i > 0 ? 1 : 0) + ' ' + units[i]
}

// 处理并排序进度数据 - 按层出现顺序排序
const sortedProgress = computed(() => {
  const entries = Object.entries(props.progressMap)
  
  // 更新层顺序（新出现的层添加到末尾）
  entries.forEach(([id]) => {
    if (id !== 'system' && !layerOrder.value.includes(id)) {
      layerOrder.value.push(id)
    }
  })
  
  const items = entries.map(([id, item]) => {
    const status = item.status || ''
    const current = item.current || 0
    const total = item.total || 0
    
    // 判断是否完成
    const isComplete = status === 'Pull complete' || status === 'Already exists' || status.includes('complete')
    
    // 是否显示进度条：有总进度且大于0，或者已完成
    const hasValidProgress = total > 0 && current > 0
    const showProgress = hasValidProgress || isComplete
    
    // 计算百分比
    let percentage = 0
    if (isComplete) {
      percentage = 100
    } else if (total > 0) {
      percentage = Math.round((current / total) * 100)
    }
    
    // 是否显示大小（下载中时显示）
    const showSize = (status === 'Downloading' || status === 'Extracting') && total > 0
    const sizeText = showSize ? `${formatSize(current)} / ${formatSize(total)}` : ''
    
    // 获取层的排序索引
    const orderIndex = id === 'system' ? -1 : layerOrder.value.indexOf(id)
    
    return {
      id,
      status,
      current,
      total,
      isComplete,
      showProgress,
      percentage,
      showSize,
      sizeText,
      orderIndex: orderIndex >= 0 ? orderIndex : 999999
    }
  })
  
  // 按层出现顺序排序（system 排最前面）
  return items.sort((a, b) => {
    if (a.id === 'system') return -1
    if (b.id === 'system') return 1
    return a.orderIndex - b.orderIndex
  })
})
</script>

<style scoped>
.pull-progress-container { padding: 10px 0; }
.pull-header { display: flex; align-items: center; gap: 10px; font-weight: bold; margin-bottom: 20px; color: #409eff; }
.progress-list { max-height: 400px; overflow-y: auto; background: var(--bg-glass-dark); padding: 15px; border-radius: 8px; border: 1px solid var(--border-color); }
.progress-item { margin-bottom: 15px; }
.item-meta { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 5px; }
.layer-id { font-family: monospace; color: var(--text-muted); }
.status-text { color: var(--text-main); font-weight: 500; display: flex; align-items: center; gap: 6px; }
.status-complete { color: #67c23a; }
.size-info { 
  font-family: 'JetBrains Mono', monospace;
  font-size: 11px; 
  color: var(--text-muted); 
  background: var(--bg-subtle, rgba(255,255,255,0.1));
  padding: 1px 4px;
  border-radius: 3px;
}
</style>