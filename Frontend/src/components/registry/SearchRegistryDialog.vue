<template>
  <el-dialog
    v-model="visible"
    :title="t('registry.searchImages')"
    width="600px"
    :close-on-click-modal="false"
  >
    <div class="search-content">
      <el-input
        v-model="searchTerm"
        :placeholder="t('registry.searchPlaceholder')"
        @keyup.enter="handleSearch"
        clearable
      >
        <template #append>
          <el-button @click="handleSearch" :loading="loading">
            {{ t('common.search') }}
          </el-button>
        </template>
      </el-input>

      <div v-if="loading" class="loading-container">
        <el-icon class="is-loading"><Loading /></el-icon>
        <span>{{ t('registry.searching') }}</span>
      </div>

      <div v-else-if="results.length > 0" class="results-container">
        <div
          v-for="image in results"
          :key="image.name"
          class="result-item"
          @click="handleSelect(image)"
        >
          <div class="image-name">{{ image.name }}</div>
          <div class="image-meta">
            <span v-if="image.description">{{ image.description }}</span>
            <span v-if="image.stars">{{ image.stars }} stars</span>
          </div>
        </div>
      </div>

      <el-empty v-else-if="searched" :description="t('registry.noResults')" />
    </div>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Loading } from '@element-plus/icons-vue'
import { imageApi } from '@/api/image'
import type { ImageRegistry } from '@/types/registry'

const { t } = useI18n()

const props = defineProps<{
  modelValue: boolean
  registry: ImageRegistry | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  'pull': [image: { name: string; tag: string }]
}>()

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

const searchTerm = ref('')
const loading = ref(false)
const searched = ref(false)
const results = ref<Array<{ name: string; description?: string; stars?: number }>>([])

async function handleSearch() {
  if (!searchTerm.value.trim()) return
  
  loading.value = true
  searched.value = true
  
  try {
    // 统一通过后端搜索，避免浏览器跨域/代理环境问题
    const data = await imageApi.searchImages({ term: searchTerm.value, limit: 20 }) as any
    const rawResults = Array.isArray(data?.data) ? data.data : (Array.isArray(data) ? data : [])
    results.value = rawResults.map((r: any) => ({
      name: r.name || r.Name || r.repo_name,
      description: r.description || r.Description || r.short_description,
      stars: r.stars || r.Stars || r.starCount || r.StarCount || 0
    }))
  } catch (error) {
    results.value = []
  } finally {
    loading.value = false
  }
}

function handleSelect(image: { name: string }) {
  emit('pull', { name: image.name, tag: 'latest' })
  visible.value = false
}
</script>

<style scoped>
.search-content {
  min-height: 200px;
}

.loading-container {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 40px;
  color: var(--text-muted);
}

.results-container {
  margin-top: 16px;
  max-height: 400px;
  overflow-y: auto;
}

.result-item {
  padding: 12px;
  border: 1px solid var(--border-color-light);
  border-radius: 6px;
  margin-bottom: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.result-item:hover {
  background: var(--bg-glass);
  border-color: var(--primary-color);
}

.image-name {
  font-weight: 600;
  color: var(--text-primary);
}

.image-meta {
  display: flex;
  gap: 12px;
  margin-top: 4px;
  font-size: 12px;
  color: var(--text-muted);
}
</style>
