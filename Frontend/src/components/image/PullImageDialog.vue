<template>
  <el-dialog
    v-model="dialogVisible"
    title="拉取镜像"
    width="600px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <!-- 加速器选择 -->
    <div class="mirror-section" v-if="mirrors.length > 0">
      <div class="mirror-header">
        <span class="mirror-label">镜像加速器</span>
        <el-tooltip content="选择加速器可大幅提升国内拉取 Docker Hub 镜像的速度" placement="top">
          <el-icon class="info-icon"><InfoFilled /></el-icon>
        </el-tooltip>
      </div>
      <el-select 
        v-model="selectedMirrorId" 
        placeholder="选择加速器（可选）" 
        clearable
        style="width: 100%"
      >
        <el-option 
          v-for="mirror in mirrors" 
          :key="mirror.id" 
          :label="mirror.name" 
          :value="mirror.id"
        >
          <div class="mirror-option">
            <span class="mirror-name">{{ mirror.name }}</span>
            <span class="mirror-domain">{{ mirror.domain }}</span>
            <el-tag v-if="mirror.isDefault" size="small" type="success">默认</el-tag>
          </div>
        </el-option>
      </el-select>
    </div>

    <el-alert
      title="提示"
      type="info"
      description="您可以搜索 Docker Hub 上的公共镜像，或直接输入私有镜像地址 (如 my-registry.com/my-app:v1)。"
      show-icon
      :closable="false"
      style="margin-bottom: 16px;"
    />
    
    <el-form ref="formRef" :model="formData" @submit.prevent="handleSubmit">
      <el-form-item label="镜像名称" required>
        <el-autocomplete
          v-model="formData.imageName"
          :fetch-suggestions="fetchSuggestions"
          placeholder="输入镜像名称，如 nginx 或 nginx:alpine"
          style="width: 100%"
          :trigger-on-focus="false"
          @select="handleSelect"
          clearable
        >
          <template #default="{ item }">
            <div class="search-item">
              <div class="item-name">{{ item.name }}</div>
              <div class="item-meta">
                <span v-if="item.isOfficial" class="official-tag">Official</span>
                <span class="stars">★ {{ item.stars }}</span>
                <span class="desc">{{ item.description }}</span>
              </div>
            </div>
          </template>
        </el-autocomplete>
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="handleClose">取消</el-button>
      <el-button type="primary" @click="handleSubmit" :disabled="!formData.imageName" :loading="isSearching">
        {{ isSearching ? '搜索中...' : '开始拉取' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { InfoFilled } from '@element-plus/icons-vue'
import { useImagesStore } from '@/stores/images'
import { useTasksStore } from '@/stores/tasks'
import { registryApi } from '@/api/registry'
import type { ImageRegistry } from '@/types/registry'
import axios from 'axios'

const { t } = useI18n()
const props = defineProps<{ modelValue: boolean }>()
const emit = defineEmits(['update:modelValue', 'success'])

const imagesStore = useImagesStore()
const tasksStore = useTasksStore()
const isSearching = ref(false)

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const formData = reactive({ imageName: '' })

// 加速器相关
const mirrors = ref<ImageRegistry[]>([])
const selectedMirrorId = ref<string | null>(null)

// 解析镜像名称和 tag
const parseImageName = (input: string) => {
  const parts = input.split(':')
  const name = parts[0] || ''
  const tag = parts[1] || 'latest'
  return { name, tag }
}

const loadMirrors = async () => {
  try {
    const res = await registryApi.getMirrors()
    mirrors.value = (res as any).data || res
    // 如果有默认加速器，自动选中
    const defaultMirror = mirrors.value.find(m => m.isDefault)
    if (defaultMirror) {
      selectedMirrorId.value = defaultMirror.id
    }
  } catch (error) {
    console.error('加载加速器列表失败:', error)
  }
}

// 远程搜索逻辑
const fetchSuggestions = async (queryString: string, cb: (results: any[]) => void) => {
  if (!queryString) return cb([])
  
  // 私有仓库地址（包含.或:或localhost）跳过搜索，Docker Hub 搜索不支持私有仓库
  const firstPart = queryString.split('/')[0]
  if (firstPart.includes('.') || firstPart.includes(':') || firstPart === 'localhost') {
    return cb([])
  }
  
  isSearching.value = true
  try {
    const res = await axios.get('/api/images/search', { params: { term: queryString } })
    const items = res.data.map((i: any) => ({
      value: i.name,
      ...i
    }))
    cb(items)
  } catch (e) {
    console.error(e)
    cb([])
  } finally {
    isSearching.value = false
  }
}

const handleSelect = (item: any) => {
  formData.imageName = item.value
}

const handleSubmit = async () => {
  if (!formData.imageName) return
  
  const { name, tag } = parseImageName(formData.imageName)
  if (!name) return

  const fullImage = `${name}:${tag}`
  
  // 先添加任务到全局列表
  const localTaskId = `pull-${Date.now()}`
  tasksStore.addTask({
    id: localTaskId,
    type: 'image-pull',
    title: `${t('image.pull')}: ${fullImage}`,
    status: 'running',
    progress: 0,
    detail: t('image.pullSubmitting')
  })
  
  try {
    const response = await axios.post('/api/images/pull', {
      imageName: name,
      tag: tag,
      registry: selectedMirrorId.value
    })
    
    const data = response.data
    if (data.pullId && data.pullId !== localTaskId) {
      tasksStore.removeTask(localTaskId)
      tasksStore.addTask({
        id: data.pullId,
        type: 'image-pull',
        title: `拉取镜像: ${fullImage}`,
        status: 'running',
        progress: 5,
        detail: '正在连接仓库...'
      })
    } else {
      tasksStore.updateTask(localTaskId, {
        progress: 5,
        detail: '正在连接仓库...'
      })
    }
    
    ElMessage.success(t('common.tasksPullSubmitted'))
    dialogVisible.value = false
    formData.imageName = ''
  } catch (error: any) {
    const errorMsg = error.response?.data?.error || error.message || '启动拉取失败'
    tasksStore.updateTask(localTaskId, {
      status: 'failed',
      detail: errorMsg
    })
    ElMessage.error(errorMsg)
  }
}

const handleClose = () => {
  dialogVisible.value = false
  formData.imageName = ''
}

watch(dialogVisible, (val) => {
  if (val) {
    loadMirrors()
  }
})

onMounted(() => loadMirrors())
</script>

<style scoped>
.mirror-section {
  margin-bottom: 16px;
}

.mirror-header {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 8px;
}

.mirror-label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-main, #333);
}

.info-icon {
  color: var(--text-muted, #999);
  cursor: help;
}

.mirror-option {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mirror-name {
  font-weight: 500;
}

.mirror-domain {
  font-family: 'JetBrains Mono', monospace;
  font-size: 12px;
  color: var(--text-muted, #999);
}

.search-item { padding: 4px 0; border-bottom: 1px solid #f0f0f0; }
.search-item:last-child { border-bottom: none; }
.item-name { font-weight: bold; color: #333; font-size: 14px; }
.item-meta { display: flex; align-items: center; gap: 8px; font-size: 12px; color: #666; margin-top: 4px; }
.official-tag { background: #e6f7ff; color: #1890ff; padding: 1px 4px; border-radius: 2px; border: 1px solid #91d5ff; font-size: 10px; }
.stars { color: #faad14; font-weight: bold; }
.desc { color: #999; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 300px; }

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

/* 深色模式 */
html.dark .mirror-label {
  color: #f1f5f9;
}

html.dark .mirror-domain {
  color: #64748b;
}
</style>