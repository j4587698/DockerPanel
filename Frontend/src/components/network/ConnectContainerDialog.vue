<template>
  <el-dialog
    v-model="dialogVisible"
    :title="t('network.connectTitle')"
    width="700px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <div v-if="network" class="connect-content">
      <!-- 网络信息 -->
      <div class="network-info-card">
        <div class="info-item">
          <span class="label">{{ t('network.networkName') }}</span>
          <span class="value">{{ network.name }}</span>
        </div>
        <div class="info-item">
          <span class="label">{{ t('network.driver') }}</span>
          <el-tag :type="getDriverType(network.driver)" size="small">
            {{ network.driver?.toUpperCase() }}
          </el-tag>
        </div>
        <div class="info-item">
          <span class="label">{{ t('network.subnet') }}</span>
          <span class="value">{{ network.subnet || '-' }}</span>
        </div>
        <div class="info-item">
          <span class="label">{{ t('network.gateway') }}</span>
          <span class="value">{{ network.gateway || '-' }}</span>
        </div>
      </div>

      <!-- 搜索和过滤 -->
      <div class="filter-row">
        <el-input
          v-model="containerFilter.search"
          :placeholder="t('network.searchContainer')"
          clearable
          style="flex: 1;"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
        <el-select
          v-model="containerFilter.status"
          :placeholder="t('common.status')"
          clearable
          style="width: 120px;"
        >
          <el-option :label="t('common.all')" value="" />
          <el-option :label="t('container.running')" value="running" />
          <el-option :label="t('container.stopped')" value="exited" />
        </el-select>
      </div>

      <!-- 容器选择列表 -->
      <div class="section-title">{{ t('network.selectContainer') }}</div>
      <div class="container-list">
        <div v-if="filteredContainers.length === 0" class="empty-tip">
          {{ t('network.noAvailableContainers') }}
        </div>
        <div
          v-for="container in filteredContainers"
          :key="container.id"
          class="container-item"
          :class="{ selected: selectedContainers.has(container.id) }"
        >
          <el-checkbox
            :model-value="selectedContainers.has(container.id)"
            @change="toggleContainerSelection(container)"
          />
          <div class="container-info">
            <span class="container-name">{{ container.name }}</span>
            <el-tag :type="getStatusType(container.status)" size="small">
              {{ container.status }}
            </el-tag>
          </div>
        </div>
      </div>

      <!-- IP 配置区域 -->
      <div v-if="selectedContainers.size > 0" class="ip-config-section">
        <div class="section-title">
          <span>{{ t('network.ipConfig') }}</span>
          <span class="hint">{{ t('network.ipConfigHint') }}</span>
        </div>
        <div class="ip-config-list">
          <div
            v-for="containerId in Array.from(selectedContainers)"
            :key="containerId"
            class="ip-config-item"
          >
            <div class="config-header">
              <span class="container-label">{{ getContainerName(containerId) }}</span>
            </div>
            <div class="config-fields">
              <div class="field-group">
                <label>{{ t('network.ipv4Address') }}</label>
                <el-input
                  v-model="getIPConfig(containerId).ipv4Address"
                  :placeholder="t('network.ipv4Placeholder')"
                  size="small"
                />
              </div>
              <div class="field-group">
                <label>{{ t('network.ipv6Address') }}</label>
                <el-input
                  v-model="getIPConfig(containerId).ipv6Address"
                  :placeholder="t('common.optional')"
                  size="small"
                />
              </div>
              <div class="field-group aliases-field">
                <label>{{ t('network.aliases') }}</label>
                <el-input
                  v-model="getIPConfig(containerId).aliases"
                  :placeholder="t('network.aliasesHint')"
                  size="small"
                />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <template #footer>
      <el-button @click="handleClose">{{ t('common.cancel') }}</el-button>
      <el-button
        type="primary"
        @click="handleSubmit"
        :loading="loading"
        :disabled="selectedContainers.size === 0"
      >
        {{ t('network.connect') }} ({{ selectedContainers.size }})
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import { useNetworksStore } from '@/stores/networks'
import { useContainersStore } from '@/stores/containers'
import { useI18n } from 'vue-i18n'
import type { NetworkInfo } from '@/types/network'
import type { ContainerInfo } from '@/api/containers'

const { t } = useI18n()

interface Props {
  modelValue: boolean
  network?: NetworkInfo | null
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'success'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const networksStore = useNetworksStore()
const containersStore = useContainersStore()

interface IPConfig {
  ipv4Address: string
  ipv6Address: string
  aliases: string
}

const loading = ref(false)
const availableContainers = ref<ContainerInfo[]>([])
const selectedContainers = ref(new Set<string>())
const ipConfigurations = ref(new Map<string, IPConfig>())

const containerFilter = reactive({
  search: '',
  status: ''
})

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const filteredContainers = computed(() => {
  let containers = availableContainers.value

  // 排除已经连接到该网络的容器
  if (props.network) {
    const connectedContainerIds = new Set(
      props.network.containers?.map(c => c.id) || []
    )
    containers = containers.filter(c => !connectedContainerIds.has(c.id))
  }

  if (containerFilter.search) {
    const search = containerFilter.search.toLowerCase()
    containers = containers.filter(container =>
      container.name.toLowerCase().includes(search) ||
      container.id.toLowerCase().includes(search)
    )
  }

  if (containerFilter.status) {
    const filterStatus = containerFilter.status.toLowerCase()
    containers = containers.filter(container => {
      const status = (container.status || '').toLowerCase()
      // 运行中: 包含 "running" 或 "up"
      if (filterStatus === 'running') {
        return status.includes('running') || status.includes('up')
      }
      // 已停止: 包含 "exited" 或 "stopped" 或 "created"
      if (filterStatus === 'exited') {
        return status.includes('exited') || status.includes('stopped') || status.includes('created')
      }
      return status.includes(filterStatus)
    })
  }

  return containers
})

watch(dialogVisible, (isOpen) => {
  if (isOpen && props.network) {
    loadAvailableContainers()
    resetForm()
  }
})

const loadAvailableContainers = async () => {
  try {
    // 获取所有容器（包括停止的）
    await containersStore.fetchContainers({ all: true })
    availableContainers.value = containersStore.containers
  } catch (error: any) {
    console.error('加载容器列表失败:', error)
    ElMessage.error(error.message || t('network.loadContainersFailed'))
  }
}

const resetForm = () => {
  selectedContainers.value.clear()
  ipConfigurations.value.clear()
  containerFilter.search = ''
  containerFilter.status = ''
}

const toggleContainerSelection = (container: ContainerInfo) => {
  if (selectedContainers.value.has(container.id)) {
    selectedContainers.value.delete(container.id)
    ipConfigurations.value.delete(container.id)
  } else {
    selectedContainers.value.add(container.id)
    ipConfigurations.value.set(container.id, {
      ipv4Address: '',
      ipv6Address: '',
      aliases: ''
    })
  }
}

const getIPConfig = (containerId: string): IPConfig => {
  if (!ipConfigurations.value.has(containerId)) {
    ipConfigurations.value.set(containerId, {
      ipv4Address: '',
      ipv6Address: '',
      aliases: ''
    })
  }
  return ipConfigurations.value.get(containerId)!
}

const getContainerName = (containerId: string): string => {
  const container = availableContainers.value.find(c => c.id === containerId)
  return container?.name || containerId.substring(0, 12)
}

const handleSubmit = async () => {
  if (!props.network || selectedContainers.value.size === 0) return

  try {
    loading.value = true

    const containerIds = Array.from(selectedContainers.value)
    const connections = containerIds.map(containerId => {
      const config = ipConfigurations.value.get(containerId) || { ipv4Address: '', ipv6Address: '', aliases: '' }
      return {
        containerId,
        ipv4Address: config.ipv4Address || undefined,
        ipv6Address: config.ipv6Address || undefined,
        aliases: config.aliases ? config.aliases.split(',').map((a: string) => a.trim()) : undefined
      }
    })

    await networksStore.connectContainers(props.network.id, connections)

    ElMessage.success(t('network.connectSuccess', { count: connections.length }))
    emit('success')
    handleClose()
  } catch (error: any) {
    console.error('连接容器失败:', error)
    ElMessage.error(error.message || t('network.connectFailed'))
  } finally {
    loading.value = false
  }
}

const getDriverType = (driver: string) => {
  const driverMap: Record<string, string> = {
    'bridge': 'primary',
    'overlay': 'success',
    'host': 'warning',
    'macvlan': 'info',
    'none': 'info'
  }
  return driverMap[driver] || 'info'
}

const getStatusType = (status: string) => {
  const statusMap: Record<string, string> = {
    'running': 'success',
    'exited': 'info',
    'paused': 'warning',
    'restarting': 'warning',
    'error': 'danger'
  }
  return statusMap[status] || 'info'
}

const handleClose = () => {
  dialogVisible.value = false
}
</script>

<style scoped>
.connect-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.network-info-card {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  padding: 14px 16px;
  background: var(--bg-subtle);
  border-radius: 8px;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.info-item .label {
  font-size: 12px;
  color: var(--text-muted);
}

.info-item .value {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-main);
}

.filter-row {
  display: flex;
  gap: 12px;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  color: var(--text-main);
}

.section-title .hint {
  font-size: 12px;
  font-weight: normal;
  color: var(--text-muted);
}

.container-list {
  max-height: 200px;
  overflow-y: auto;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-surface);
}

.container-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  border-bottom: 1px solid var(--border-color-light);
  transition: background-color 0.15s;
}

.container-item:last-child {
  border-bottom: none;
}

.container-item:hover {
  background-color: var(--bg-subtle);
}

.container-item.selected {
  background-color: rgba(6, 182, 212, 0.1);
}

.container-info {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
}

.container-name {
  font-size: 14px;
  color: var(--text-main);
}

.empty-tip {
  padding: 40px 20px;
  text-align: center;
  color: var(--text-muted);
  font-size: 14px;
}

.ip-config-section {
  border-top: 1px solid var(--border-color);
  padding-top: 16px;
}

.ip-config-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  max-height: 200px;
  overflow-y: auto;
}

.ip-config-item {
  padding: 12px;
  background: var(--bg-subtle);
  border-radius: 8px;
}

.config-header {
  margin-bottom: 10px;
}

.container-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-main);
}

.config-fields {
  display: flex;
  gap: 12px;
}

.field-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field-group label {
  font-size: 11px;
  color: var(--text-muted);
}

.field-group:first-child {
  width: 150px;
}

.field-group:nth-child(2) {
  width: 150px;
}

.aliases-field {
  flex: 1;
}

/* 暗黑模式修复 */
:deep(.el-input__wrapper) {
  background: var(--bg-surface);
}

:deep(.el-input__inner) {
  color: var(--text-main);
}

:deep(.el-input__inner::placeholder) {
  color: var(--text-muted);
}

:deep(.el-select__wrapper) {
  background: var(--bg-surface);
}

:deep(.el-checkbox__label) {
  color: var(--text-main);
}

html.dark :deep(.el-input__wrapper) {
  background: #1e293b;
}

html.dark :deep(.el-select__wrapper) {
  background: #1e293b;
}

html.dark .ip-config-item {
  background: rgba(30, 41, 59, 0.5);
}
</style>
