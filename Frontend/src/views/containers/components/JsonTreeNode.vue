<template>
  <div class="json-tree-node" :class="{ 'is-collapsed': !isExpanded }">
    <!-- 基本类型 -->
    <template v-if="!isObject && !isArray">
      <span class="json-key" v-if="keyName">
        <span class="key-text" :class="{ 'search-match': isKeyMatch }">{{ keyName }}</span>
        <span class="json-colon">:</span>
      </span>
      <span class="json-value" :class="valueClass">{{ displayValue }}</span>
    </template>
    
    <!-- 对象 -->
    <template v-else-if="isObject">
      <div class="json-item-header" @click="toggle">
        <span class="json-toggle" :class="{ 'is-expanded': isExpanded }">
          <el-icon v-if="isExpanded"><ArrowDown /></el-icon>
          <el-icon v-else><ArrowRight /></el-icon>
        </span>
        <span class="json-key" v-if="keyName">
          <span class="key-text" :class="{ 'search-match': isKeyMatch }">{{ keyName }}</span>
          <span class="json-colon">:</span>
        </span>
        <span class="json-bracket">{</span>
        <span class="json-count" v-if="!isExpanded">{{ objectKeys.length }} {{ objectKeys.length === 1 ? 'item' : 'items' }}</span>
        <span class="json-bracket" v-if="!isExpanded">}</span>
      </div>
      <div class="json-children" v-show="isExpanded">
        <JsonTreeNode
          v-for="k in objectKeys"
          :key="k"
          :data="data[k]"
          :key-name="k"
          :path="[...path, k]"
          :search="search"
          :expanded-keys="expandedKeys"
          @toggle="$emit('toggle', $event)"
        />
      </div>
      <div class="json-close-bracket" v-show="isExpanded">}</div>
    </template>
    
    <!-- 数组 -->
    <template v-else-if="isArray">
      <div class="json-item-header" @click="toggle">
        <span class="json-toggle" :class="{ 'is-expanded': isExpanded }">
          <el-icon v-if="isExpanded"><ArrowDown /></el-icon>
          <el-icon v-else><ArrowRight /></el-icon>
        </span>
        <span class="json-key" v-if="keyName">
          <span class="key-text" :class="{ 'search-match': isKeyMatch }">{{ keyName }}</span>
          <span class="json-colon">:</span>
        </span>
        <span class="json-bracket">[</span>
        <span class="json-count" v-if="!isExpanded">{{ arrayLength }} items</span>
        <span class="json-bracket" v-if="!isExpanded">]</span>
      </div>
      <div class="json-children" v-show="isExpanded">
        <JsonTreeNode
          v-for="(item, index) in data"
          :key="index"
          :data="item"
          :key-name="String(index)"
          :path="[...path, String(index)]"
          :search="search"
          :expanded-keys="expandedKeys"
          @toggle="$emit('toggle', $event)"
        />
      </div>
      <div class="json-close-bracket" v-show="isExpanded">]</div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ArrowDown, ArrowRight } from '@element-plus/icons-vue'

interface Props {
  data: any
  keyName?: string
  path: string[]
  search?: string
  expandedKeys: Set<string>
}

const props = defineProps<Props>()
const emit = defineEmits(['toggle'])

const pathStr = computed(() => props.path.join('.'))

const isObject = computed(() => {
  return typeof props.data === 'object' && props.data !== null && !Array.isArray(props.data)
})

const isArray = computed(() => {
  return Array.isArray(props.data)
})

const objectKeys = computed(() => {
  if (!isObject.value) return []
  return Object.keys(props.data).sort((a, b) => {
    // 常用字段排前面
    const priorityFields = ['id', 'name', 'state', 'status', 'image', 'created', 'ports', 'networks']
    const aPriority = priorityFields.includes(a.toLowerCase()) ? 0 : 1
    const bPriority = priorityFields.includes(b.toLowerCase()) ? 0 : 1
    if (aPriority !== bPriority) return aPriority - bPriority
    return a.localeCompare(b)
  })
})

const arrayLength = computed(() => {
  return Array.isArray(props.data) ? props.data.length : 0
})

const isExpanded = computed(() => {
  // 顶层默认展开
  if (props.path.length === 0) return true
  return props.expandedKeys.has(pathStr.value)
})

const isKeyMatch = computed(() => {
  if (!props.search) return false
  return props.keyName?.toLowerCase().includes(props.search.toLowerCase())
})

const isValueMatch = computed(() => {
  if (!props.search || isObject.value || isArray.value) return false
  const searchLower = props.search.toLowerCase()
  return String(props.data).toLowerCase().includes(searchLower)
})

const valueClass = computed(() => {
  if (props.data === null) return 'json-null'
  if (props.data === true || props.data === false) return 'json-boolean'
  if (typeof props.data === 'number') return 'json-number'
  if (typeof props.data === 'string') return 'json-string'
  return ''
})

const displayValue = computed(() => {
  if (props.data === null) return 'null'
  if (typeof props.data === 'string') return `"${props.data}"`
  return String(props.data)
})

const toggle = () => {
  emit('toggle', pathStr.value)
}

// 自动展开匹配的节点
watch(() => props.search, (search) => {
  if (search && (isKeyMatch.value || isValueMatch.value)) {
    emit('toggle', pathStr.value)
  }
}, { immediate: true })
</script>

<style scoped>
.json-tree-node {
  padding-left: 0;
  line-height: 1.8;
}

.json-tree-node .json-tree-node {
  padding-left: 20px;
}

.json-item-header {
  display: inline-flex;
  align-items: center;
  cursor: pointer;
  border-radius: 4px;
  padding: 2px 4px;
  margin: -2px -4px;
}

.json-item-header:hover {
  background: rgba(255, 255, 255, 0.05);
}

.json-toggle {
  width: 16px;
  height: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-muted);
  transition: transform 0.2s;
}

.json-toggle .el-icon {
  font-size: 12px;
}

.json-key {
  color: #89b4fa;
}

.key-text {
  padding: 1px 4px;
  border-radius: 3px;
}

.key-text.search-match {
  background: rgba(249, 226, 79, 0.3);
  color: #f9e64f;
}

.json-colon {
  margin: 0 4px;
  color: var(--text-muted);
}

.json-bracket {
  color: var(--text-muted);
  margin: 0 4px;
}

.json-count {
  color: var(--text-muted);
  font-style: italic;
  font-size: 12px;
  margin-left: 8px;
}

.json-close-bracket {
  padding-left: 0;
  color: var(--text-muted);
}

.json-value {
  padding: 1px 6px;
  border-radius: 3px;
}

.json-string {
  color: #a6e3a1;
}

.json-number {
  color: #fab387;
}

.json-boolean {
  color: #cba6f7;
}

.json-null {
  color: var(--text-muted);
  font-style: italic;
}

.json-value:has(.search-match) {
  background: rgba(249, 226, 79, 0.2);
}

.json-children {
  border-left: 1px dashed var(--border-color);
  margin-left: 8px;
  padding-left: 12px;
}
</style>
