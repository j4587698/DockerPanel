<template>
  <el-drawer
    v-model="drawerVisible"
    :title="t('image.detailTitle')"
    direction="rtl"
    size="60%"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <div v-if="loading" class="loading-container">
      <el-skeleton :rows="8" animated />
    </div>

    <div v-else-if="imageData" class="image-detail">
      <!-- 基本信息卡片 -->
      <el-card class="detail-card" shadow="never">
        <template #header>
          <div class="card-header">
            <h3>{{ t('image.basicInfo') }}</h3>
            <el-tag type="info" size="small">
              {{ imageData.repoTags.length > 0 ? t('image.tagged') : t('image.dangling') }}
            </el-tag>
          </div>
        </template>

        <div class="info-grid">
          <div class="info-item">
            <label>{{ t('image.imageIdLabel') }}</label>
            <div class="value">
              <el-text type="primary" class="image-id-text" :title="imageData.id">
                {{ imageData.id.substring(0, 19) }}
              </el-text>
              <el-button
                size="small"
                text
                @click="copyToClipboard(imageData.id)"
              >
                <el-icon><DocumentCopy /></el-icon>
              </el-button>
            </div>
          </div>

          <div class="info-item">
            <label>{{ t('image.imageName') }}</label>
            <div class="value">{{ imageData.repository }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('image.tagsLabel') }}</label>
            <div class="value">
              <div class="tags-list">
                <el-tag
                  v-for="tag in imageData.repoTags"
                  :key="tag"
                  size="small"
                  class="tag-item"
                >
                  {{ tag }}
                </el-tag>
                <el-text v-if="!imageData.repoTags || imageData.repoTags.length === 0" type="info">
                  {{ t('image.noTags') }}
                </el-text>
              </div>
            </div>
          </div>

          <div class="info-item">
            <label>{{ t('image.sizeLabel') }}</label>
            <div class="value">{{ formatFileSize(imageData.size) }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('image.createdAt') }}</label>
            <div class="value">{{ formatDateTime(imageData.createdAt) }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('image.imageType') }}</label>
            <div class="value">
              <el-tag v-if="imageData.repository && (imageData.repository.startsWith('library/') || imageData.repository === 'scratch' || !imageData.repository.includes('/'))" type="success" size="small">{{ t('image.officialImage') }}</el-tag>
              <el-tag v-else-if="imageData.repoTags.length === 0" type="warning" size="small">{{ t('image.danglingImage') }}</el-tag>
              <el-tag v-else type="info" size="small">{{ t('image.customImage') }}</el-tag>
            </div>
          </div>
        </div>
      </el-card>

      <!-- 镜像配置信息 -->
      <el-card class="detail-card" shadow="never" v-if="imageInspect">
        <template #header>
          <h3>{{ t('image.configInfo') }}</h3>
        </template>

        <div class="config-section">
          <div class="config-item" v-if="imageInspect.os">
            <label>{{ t('image.osLabel') }}</label>
            <div class="value">{{ imageInspect.os }}</div>
          </div>

          <div class="config-item" v-if="imageInspect.architecture">
            <label>{{ t('image.archLabel') }}</label>
            <div class="value">{{ imageInspect.architecture }}</div>
          </div>

          <div class="config-item" v-if="imageInspect.config?.env && imageInspect.config.env.length > 0">
            <label>{{ t('image.envLabel') }}</label>
            <div class="value">
              <el-tag
                v-for="env in imageInspect.config.env"
                :key="env"
                size="small"
                class="env-tag"
              >
                {{ env }}
              </el-tag>
            </div>
          </div>

          <div class="config-item" v-if="imageInspect.config?.exposedPorts && imageInspect.config.exposedPorts.length > 0">
            <label>{{ t('image.exposedPortsLabel') }}</label>
            <div class="value">
              <el-tag
                v-for="port in imageInspect.config.exposedPorts"
                :key="port"
                size="small"
                type="warning"
                class="port-tag"
              >
                {{ port }}
              </el-tag>
            </div>
          </div>

          <div class="config-item" v-if="imageInspect.config?.cmd && imageInspect.config.cmd.length > 0">
            <label>{{ t('image.defaultCommand') }}</label>
            <div class="value">
              <el-text style="font-family: monospace;">
                {{ imageInspect.config.cmd.join(' ') }}
              </el-text>
            </div>
          </div>

          <div class="config-item" v-if="imageInspect.config?.labels && Object.keys(imageInspect.config.labels).length > 0">
            <label>{{ t('image.labelsLabel') }}</label>
            <div class="value">
              <div
                v-for="(value, key) in imageInspect.config.labels"
                :key="key"
                class="label-item"
              >
                <el-text size="small">{{ key }}: {{ value }}</el-text>
              </div>
            </div>
          </div>

          <div class="config-item" v-if="imageInspect.parent">
            <label>{{ t('image.parentImage') }}</label>
            <div class="value" style="font-family: monospace; font-size: 12px;">
              {{ imageInspect.parent }}
            </div>
          </div>

          <div class="config-item">
            <label>{{ t('image.virtualSize') }}</label>
            <div class="value">{{ formatFileSize(imageInspect.virtualSize) }}</div>
          </div>
        </div>
      </el-card>

      <!-- 镜像层信息 -->
      <el-card class="detail-card" shadow="never">
        <template #header>
          <div class="card-header">
            <h3>{{ t('image.layersInfo') }}</h3>
            <el-button size="small" @click="refreshLayers">
              <el-icon><Refresh /></el-icon>
              {{ t('common.refresh') }}
            </el-button>
          </div>
        </template>

        <div v-if="imageLayers.length > 0" class="layers-list">
          <div
            v-for="(layer, index) in imageLayers"
            :key="layer.id"
            class="layer-item"
          >
            <div class="layer-header">
              <div class="layer-info">
                <span class="layer-index">{{ t('image.layerNumber', { n: imageLayers.length - index }) }}</span>
                <el-text
                  type="primary"
                  style="font-family: monospace;"
                  size="small"
                  :class="{ 'missing-id': layer.id === '<missing>' }"
                >
                  {{ layer.id === '<missing>' ? t('image.intermediateLayer') : layer.id.substring(0, 12) }}
                </el-text>
              </div>
              <div class="layer-size">{{ formatFileSize(layer.size) }}</div>
            </div>
            <div class="layer-command" v-if="layer.createdBy">
              <div class="command-content">
                <el-text
                  size="small"
                  type="info"
                  class="command-text"
                >
                  {{ layer.expanded ? layer.createdBy : truncateCommand(layer.createdBy) }}
                </el-text>
                <el-button
                  v-if="shouldShowExpandButton(layer.createdBy)"
                  size="small"
                  text
                  @click="toggleLayerExpand(layer)"
                  class="expand-btn"
                >
                  <el-icon>
                    <component :is="layer.expanded ? ArrowUp : ArrowDown" />
                  </el-icon>
                </el-button>
              </div>
            </div>
          </div>
        </div>

        <div v-else class="empty-layers">
          <el-empty :description="t('image.noLayersInfo')" :image-size="100" />
        </div>
      </el-card>
    </div>

    <div v-else-if="!loading" class="empty-state">
      <el-empty :description="t('image.imageInfoFailed')" />
    </div>

    <template #footer>
      <div class="drawer-footer">
        <el-button @click="handleClose">{{ t('common.close') }}</el-button>
        <el-button type="primary" @click="runContainer">
          <el-icon><VideoPlay /></el-icon>
          {{ t('image.runContainer') }}
        </el-button>
      </div>
    </template>

    <!-- 创建容器对话框 -->
    <CreateContainerDialog
      v-model="showCreateContainerDialog"
      :preselected-image="imageData"
      @success="handleCreateSuccess"
    />
  </el-drawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { DocumentCopy, Refresh, Search, VideoPlay, ArrowUp, ArrowDown } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { useImagesStore } from '@/stores/images'
import { imageApi, type ImageInfo } from '@/api/images'
import CreateContainerDialog from '@/components/container/CreateContainerDialog.vue'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()

interface Props {
  modelValue: boolean
  imageId?: string
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'run-container', image: ImageInfo): void
  (e: 'show-pull-image'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const imagesStore = useImagesStore()

// 响应式数据
const loading = ref(false)
const imageData = ref<ImageInfo | null>(null)
const imageLayers = ref<any[]>([])
const imageInspect = ref<any>(null)
const showCreateContainerDialog = ref(false)

const drawerVisible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

// 监听imageId变化
watch(() => props.imageId, (newId) => {
  if (newId && drawerVisible.value) {
    loadImageDetail()
  }
})

// 监听抽屉打开状态
watch(drawerVisible, (isOpen) => {
  if (isOpen && props.imageId) {
    loadImageDetail()
  }
})

// 加载镜像详情
const loadImageDetail = async () => {
  if (!props.imageId) return

  try {
    loading.value = true

    // 从store中获取镜像信息
    const image = imagesStore.images.find(img => img.id === props.imageId)
    if (image) {
      imageData.value = image
    } else {
      // 如果store中没有，重新获取
      await imagesStore.fetchImages()
      imageData.value = imagesStore.images.find(img => img.id === props.imageId) || null
    }

    if (imageData.value) {
      // 加载镜像层信息
      await loadImageLayers()
      // 加载镜像详细信息
      await loadImageInspect()
    }
  } catch (error: any) {
    console.error(t('image.loadDetailFailed') + ':', error)
    ElMessage.error(error.message || t('image.loadDetailFailed'))
  } finally {
    loading.value = false
  }
}

// 加载镜像层信息
const loadImageLayers = async () => {
  try {
    // 调用API获取镜像历史信息
    const layers = await imagesStore.getImageHistory(props.imageId!)
    imageLayers.value = layers || []
  } catch (error) {
    console.error(t('image.loadLayersFailed') + ':', error)
    imageLayers.value = []
  }
}

// 加载镜像详细信息
const loadImageInspect = async () => {
  try {
    // 走统一 API 客户端，自动携带认证头并复用错误处理
    const inspectData = await imageApi.inspectImage(props.imageId!)
    imageInspect.value = inspectData
  } catch (error) {
    console.error(t('image.loadInspectFailed') + ':', error)
    imageInspect.value = null
  }
}


// 刷新镜像层信息
const refreshLayers = async () => {
  await loadImageLayers()
  ElMessage.success(t('image.layersRefreshed'))
}


// 运行容器
const runContainer = () => {
  if (imageData.value) {
    showCreateContainerDialog.value = true
  }
}

const handleCreateSuccess = () => {
  // 容器创建成功后的回调
  ElMessage.success(t('image.containerCreated'))
}

const handleShowPullImage = () => {
  // 显示拉取镜像对话框
  emit('show-pull-image')
}

// 判断是否显示展开按钮
const shouldShowExpandButton = (command: string) => {
  return command && command.length > 50
}

// 截断命令文本
const truncateCommand = (command: string) => {
  if (!command) return ''
  return command.length > 50 ? command.substring(0, 50) + '...' : command
}

// 切换层级展开状态
const toggleLayerExpand = (layer: any) => {
  layer.expanded = !layer.expanded
}

// 格式化文件大小
const formatFileSize = (bytes: number) => {
  if (bytes === null || bytes === undefined || isNaN(bytes) || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const absBytes = Math.abs(bytes)
  if (absBytes < 1) return absBytes.toFixed(2) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

// 格式化日期时间
const formatDateTime = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

// 复制到剪贴板
const copyToClipboard = async (text: string) => {
  try {
    await navigator.clipboard.writeText(text)
    ElMessage.success(t('common.copySuccess'))
  } catch (error) {
    ElMessage.error(t('common.copyFailed'))
  }
}

// 处理关闭
const handleClose = () => {
  drawerVisible.value = false
  // 清理数据
  imageData.value = null
  imageLayers.value = []
  imageInspect.value = null
}
</script>

<style scoped>
.loading-container {
  padding: 20px;
}

.image-detail {
  padding: 0 20px;
}

.detail-card {
  margin-bottom: 20px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-main);
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 16px;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.info-item label {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
}

.info-item .value {
  font-size: 14px;
  color: var(--text-main);
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.image-id-text {
  font-family: monospace;
  cursor: pointer;
}

.tags-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.tag-item {
  margin: 0;
}

.layers-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.layer-item {
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  padding: 12px;
  background-color: var(--bg-subtle);
}

.layer-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.layer-info {
  display: flex;
  align-items: center;
  gap: 12px;
}

.layer-index {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
}

.layer-size {
  font-size: 12px;
  color: var(--text-secondary);
  font-weight: 500;
}

.layer-command {
  font-family: monospace;
  font-size: 12px;
  color: var(--text-secondary);
  background-color: var(--bg-surface);
  padding: 8px;
  border-radius: 4px;
  word-break: break-all;
  border: 1px solid var(--border-color);
}

.missing-id {
  color: var(--text-muted) !important;
  font-style: italic;
}

.command-content {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
  width: 100%;
}

.command-text {
  flex: 1;
  word-break: break-all;
  text-align: left;
}

.expand-btn {
  flex-shrink: 0;
  padding: 2px 4px;
  min-width: auto;
}

.config-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.config-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.config-item label {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
}

.config-item .value {
  font-size: 14px;
  color: var(--text-main);
}

.env-tag,
.port-tag {
  margin: 2px;
}

.scan-results {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.scan-summary {
  padding: 16px;
  background-color: var(--bg-subtle);
  border-radius: var(--border-radius);
}

.summary-item {
  text-align: center;
  padding: 12px;
  border-radius: var(--border-radius);
}

.summary-item.critical {
  background-color: var(--color-danger-bg);
  color: var(--color-danger);
}

.summary-item.high {
  background-color: var(--color-danger-bg);
  color: var(--color-danger);
}

.summary-item.medium {
  background-color: var(--color-warning-bg);
  color: var(--color-warning);
}

.summary-item.low {
  background-color: var(--color-info-bg);
  color: var(--color-info);
}

.summary-item .count {
  font-size: 24px;
  font-weight: 600;
  line-height: 1;
}

.summary-item .label {
  font-size: 12px;
  margin-top: 4px;
}

.vulnerabilities-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.vulnerability-item {
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  padding: 12px;
}

.vuln-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.vuln-details {
  font-size: 12px;
  color: var(--text-secondary);
  line-height: 1.5;
}

.vuln-package {
  margin-top: 4px;
}

.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 200px;
}

.label-item {
  padding: 4px 0;
}

.empty-layers {
  padding: 40px 20px;
}

@media (max-width: 768px) {
  .info-grid {
    grid-template-columns: 1fr;
  }

  .image-detail {
    padding: 0 12px;
  }
}
</style>
