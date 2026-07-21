<template>
  <div class="json-view-container">
    <div class="json-header">
      <div class="json-title">
        <el-icon><Document /></el-icon>
        <span>{{ t('container.rawJson') || '容器原始数据' }}</span>
      </div>
      <div class="json-actions">
        <el-input
          v-model="searchText"
          :placeholder="t('common.search') || '搜索...'"
          :prefix-icon="Search"
          clearable
          size="small"
          class="search-input"
        />
        <el-button-group size="small">
          <el-button :type="viewMode === 'tree' ? 'primary' : 'default'" @click="viewMode = 'tree'" :icon="List">{{ t('container.treeView') || '树形' }}</el-button>
          <el-button :type="viewMode === 'raw' ? 'primary' : 'default'" @click="viewMode = 'raw'" :icon="Document">{{ t('container.rawView') || '原始' }}</el-button>
        </el-button-group>
        <el-button size="small" @click="copyJson" :icon="CopyDocument">{{ t('common.copy') || '复制' }}</el-button>
        <el-button size="small" @click="downloadJson" :icon="Download">{{ t('common.download') || '下载' }}</el-button>
      </div>
    </div>
    
    <div class="json-content">
      <!-- 树形视图 -->
      <div v-if="viewMode === 'tree'" class="json-tree">
        <div class="json-node" :class="{ 'is-root': true }">
          <JsonTreeNode 
            :data="containerData" 
            :path="[]"
            :search="searchText"
            :expanded-keys="expandedKeys"
            @toggle="toggleExpand"
          />
        </div>
      </div>
      
      <!-- 原始视图 -->
      <div v-else class="json-raw">
        <pre class="json-code" :class="{ 'with-highlight': searchText }"><code>{{ formattedJson }}</code></pre>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { Document, List, CopyDocument, Download, Search } from '@element-plus/icons-vue'
import JsonTreeNode from './JsonTreeNode.vue'

const { t } = useI18n()

interface Props {
  container: any
}

const props = defineProps<Props>()

const searchText = ref('')
const viewMode = ref<'tree' | 'raw'>('tree')
const expandedKeys = ref<Set<string>>(new Set(['']))

// 处理容器数据，移除一些不必要的字段
const containerData = computed(() => {
  if (!props.container) return {}
  
  // 创建一个清理后的副本
  const cleaned: any = {}
  
  // 按键排序并过滤
  const sortedKeys = Object.keys(props.container).sort()
  
  for (const key of sortedKeys) {
    // 跳过一些内部字段
    if (key.startsWith('_')) continue
    
    const value = props.container[key]
    
    // 格式化特定字段
    if (key === 'created' || key === 'finishedAt') {
      cleaned[key] = value
    } else {
      cleaned[key] = value
    }
  }
  
  return cleaned
})

const formattedJson = computed(() => {
  try {
    return JSON.stringify(containerData.value, null, 2)
  } catch (e) {
    return 'Error formatting JSON'
  }
})

const toggleExpand = (path: string) => {
  if (expandedKeys.value.has(path)) {
    expandedKeys.value.delete(path)
  } else {
    expandedKeys.value.add(path)
  }
  // 强制更新
  expandedKeys.value = new Set(expandedKeys.value)
}

// 自动展开搜索结果
watch(searchText, (text) => {
  if (text && text.length > 0) {
    // 搜索时自动展开所有
    expandAllPaths(containerData.value, '')
  }
})

const expandAllPaths = (obj: any, prefix: string) => {
  if (typeof obj !== 'object' || obj === null) return
  
  expandedKeys.value.add(prefix)
  
  for (const key in obj) {
    const path = prefix ? `${prefix}.${key}` : key
    if (typeof obj[key] === 'object' && obj[key] !== null) {
      expandAllPaths(obj[key], path)
    }
  }
}

const copyJson = async () => {
  try {
    await navigator.clipboard.writeText(formattedJson.value)
    ElMessage.success(t('common.copySuccess') || '已复制到剪贴板')
  } catch (e) {
    ElMessage.error(t('common.copyFailed') || '复制失败')
  }
}

const downloadJson = () => {
  const blob = new Blob([formattedJson.value], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${props.container?.name || 'container'}_${new Date().toISOString().slice(0, 10)}.json`
  a.click()
  URL.revokeObjectURL(url)
  ElMessage.success(t('common.downloadSuccess') || '下载成功')
}
</script>

<style scoped>

.json-view-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-surface);
  border-radius: 12px;
  overflow: hidden;
}

.json-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  background: var(--bg-app);
  border-bottom: 1px solid var(--border-color);
  flex-shrink: 0;
}

.json-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-main);
}

.json-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.search-input {
  width: 200px;
}

.json-content {
  flex: 1;
  overflow: auto;
  padding: 16px;
}

.json-tree {
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  font-size: 13px;
}

.json-node.is-root {
  padding: 0;
}

.json-raw {
  height: 100%;
}

.json-code {
  height: 100%;
  margin: 0;
  padding: 16px;
  background: #1e1e2e;
  border-radius: 8px;
  overflow: auto;
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: #cdd6f4;
  white-space: pre-wrap;
  word-break: break-all;
}

.json-code code {
  color: inherit;
}

</style>
