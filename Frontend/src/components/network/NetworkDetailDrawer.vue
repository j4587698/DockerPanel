<template>
  <el-drawer
    v-model="drawerVisible"
    :title="t('network.detailTitle')"
    direction="rtl"
    size="60%"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <div v-if="loading" class="loading-container">
      <el-skeleton :rows="8" animated />
    </div>

    <div v-else-if="networkData" class="network-detail">
      <!-- 基本信息卡片 -->
      <el-card class="detail-card" shadow="never">
        <template #header>
          <div class="card-header">
            <h3>{{ t('network.basicInfo') }}</h3>
            <div class="header-actions">
              <el-button size="small" @click="refreshNetwork" :icon="Refresh">{{ t('common.refresh') }}</el-button>
              <el-button size="small" @click="copyNetworkId" :icon="DocumentCopy">{{ t('network.copyId') }}</el-button>
            </div>
          </div>
        </template>

        <div class="info-grid">
          <div class="info-item">
            <label>{{ t('network.networkId') }}</label>
            <div class="value">
              <el-text type="primary" style="font-family: monospace;">
                {{ networkData.id }}
              </el-text>
            </div>
          </div>

          <div class="info-item">
            <label>{{ t('network.networkName') }}</label>
            <div class="value">{{ networkData.name }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('network.networkDriver') }}</label>
            <div class="value">
              <el-tag :type="getDriverType(networkData.driver)" size="small">
                {{ networkData.driver?.toUpperCase() }}
              </el-tag>
            </div>
          </div>

          <div class="info-item">
            <label>{{ t('network.scope') }}</label>
            <div class="value">
              <el-tag :type="getScopeType(networkData.scope)" size="small">
                {{ networkData.scope }}
              </el-tag>
            </div>
          </div>

          <div class="info-item">
            <label>{{ t('common.created') }}</label>
            <div class="value">{{ formatDateTime(networkData.createdAt) }}</div>
          </div>

          <div class="info-item">
            <label>{{ t('network.features') }}</label>
            <div class="value">
              <div class="feature-tags">
                <el-tag v-if="networkData.internal" type="info" size="small">{{ t('network.internalNetwork') }}</el-tag>
                <el-tag v-if="networkData.ingress" type="warning" size="small">Ingress</el-tag>
                <el-tag v-if="networkData.attachable" type="success" size="small">{{ t('network.attachable') }}</el-tag>
                <el-tag v-if="networkData.enableIPv6" type="primary" size="small">IPv6</el-tag>
              </div>
            </div>
          </div>
        </div>
      </el-card>

      <!-- IPAM配置 -->
      <el-card class="detail-card" shadow="never">
        <template #header>
          <h3>{{ t('network.ipamConfig') }}</h3>
        </template>

        <div class="ipam-config">
          <div class="ipam-driver">
            <label>{{ t('network.ipamDriver') }}:</label>
            <el-tag size="small">{{ networkData.ipam?.driver || 'default' }}</el-tag>
          </div>

          <div class="subnet-configs" v-if="networkData.ipam?.config?.length > 0">
            <h4>{{ t('network.subnetConfig') }}</h4>
            <div
              v-for="(subnet, index) in networkData.ipam.config"
              :key="index"
              class="subnet-item"
            >
              <el-card>
                <div class="subnet-info">
                  <div class="subnet-field">
                    <label>{{ t('network.subnet') }}:</label>
                    <el-text type="primary" style="font-family: monospace;">
                      {{ subnet.subnet }}
                    </el-text>
                  </div>
                  <div class="subnet-field" v-if="subnet.gateway">
                    <label>{{ t('network.gateway') }}:</label>
                    <el-text style="font-family: monospace;">
                      {{ subnet.gateway }}
                    </el-text>
                  </div>
                  <div class="subnet-field" v-if="subnet.ipRange">
                    <label>{{ t('network.ipRange') }}:</label>
                    <el-text style="font-family: monospace;">
                      {{ subnet.ipRange }}
                    </el-text>
                  </div>
                  <div class="subnet-field" v-if="subnet.auxiliaryAddresses?.length > 0">
                    <label>{{ t('network.auxiliaryAddresses') }}:</label>
                    <div class="aux-addresses">
                      <el-tag
                        v-for="(aux, auxIndex) in subnet.auxiliaryAddresses"
                        :key="auxIndex"
                        size="small"
                        class="aux-tag"
                      >
                        {{ aux }}
                      </el-tag>
                    </div>
                  </div>
                </div>
              </el-card>
            </div>
          </div>
        </div>
      </el-card>

      <!-- 连接的容器 -->
      <el-card class="detail-card" shadow="never">
        <template #header>
          <div class="card-header">
            <h3>{{ t('network.connectedContainers') }} ({{ networkData.containers?.length || 0 }})</h3>
            <el-button size="small" @click="connectContainer" :icon="Plus">{{ t('network.connectContainer') }}</el-button>
          </div>
        </template>

        <div class="containers-list">
          <div v-if="networkData.containers?.length > 0">
            <div
              v-for="container in networkData.containers"
              :key="container.id"
              class="container-item"
            >
              <div class="container-info">
                <div class="container-name">
                  <el-text type="primary" style="cursor: pointer;" @click="viewContainer(container)">
                    {{ container.name || container.Name }}
                  </el-text>
                </div>
                <div class="container-details">
                  <span>ID: {{ (container.id || container.Id || '').substring(0, 12) }}</span>
                </div>
                <div class="container-ips" v-if="container.ipAddress || container.IpAddress">
                  <span>{{ t('network.ipAddress') }}:</span>
                  <el-tag
                    v-for="ip in getIpAddresses(container)"
                    :key="ip"
                    size="small"
                    type="info"
                    style="margin-left: 4px;"
                  >
                    {{ ip }}
                  </el-tag>
                </div>
                <div class="container-mac" v-if="container.macAddress || container.MacAddress">
                  <span>MAC: {{ container.macAddress || container.MacAddress }}</span>
                </div>
              </div>
              <div class="container-actions">
                <el-button
                  size="small"
                  type="danger"
                  text
                  @click="disconnectContainer(container)"
                >
                  {{ t('network.disconnect') }}
                </el-button>
              </div>
            </div>
          </div>
          <el-empty v-else :description="t('network.noContainers')" />
        </div>
      </el-card>

      <!-- 网络选项和标签 -->
      <el-card class="detail-card" shadow="never">
        <template #header>
          <h3>{{ t('network.networkConfig') }}</h3>
        </template>

        <div class="config-section">
          <div class="config-group" v-if="networkData.labels && Object.keys(networkData.labels).length > 0">
            <h4>{{ t('network.labels') }}</h4>
            <div class="labels-list">
              <div
                v-for="(value, key) in networkData.labels"
                :key="key"
                class="label-item"
              >
                <el-text type="primary" style="font-weight: 500;">{{ key }}:</el-text>
                <el-text style="margin-left: 8px;">{{ value }}</el-text>
              </div>
            </div>
          </div>

          <div class="config-group" v-if="networkData.options && Object.keys(networkData.options).length > 0">
            <h4>{{ t('network.options') }}</h4>
            <div class="options-list">
              <div
                v-for="(value, key) in networkData.options"
                :key="key"
                class="option-item"
              >
                <el-text type="primary" style="font-weight: 500;">{{ key }}:</el-text>
                <el-text style="margin-left: 8px;">{{ value }}</el-text>
              </div>
            </div>
          </div>

          <div v-if="(!networkData.labels || Object.keys(networkData.labels).length === 0) &&
                       (!networkData.options || Object.keys(networkData.options).length === 0)"
               class="empty-config">
            <el-empty :description="t('network.noExtraConfig')" />
          </div>
        </div>
      </el-card>
    </div>

    <div v-else-if="!loading" class="empty-state">
      <el-empty :description="t('network.loadFailed')" />
    </div>

    <template #footer>
      <div class="drawer-footer">
        <el-button @click="handleClose">{{ t('common.close') }}</el-button>
        <el-button type="primary" @click="connectContainer" :icon="Plus">{{ t('network.connectContainer') }}</el-button>
        <el-button type="danger" @click="removeNetwork" :disabled="networkData?.containers?.length > 0">
          <el-icon><Delete /></el-icon>
          {{ t('network.removeNetwork') }}
        </el-button>
      </div>
    </template>
  </el-drawer>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh, DocumentCopy, Plus, Delete } from '@element-plus/icons-vue'
import { useNetworksStore } from '@/stores/networks'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import type { NetworkInfo } from '@/api/network'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()

interface Props {
  modelValue: boolean
  networkId?: string
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'connect-container', network: NetworkInfo): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const networksStore = useNetworksStore()
const router = useRouter()

// 响应式数据
const loading = ref(false)
const networkData = ref<NetworkInfo | null>(null)

const drawerVisible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

// 监听networkId变化
watch(() => props.networkId, (newId) => {
  if (newId && drawerVisible.value) {
    loadNetworkDetail()
  }
})

// 监听抽屉打开状态
watch(drawerVisible, (isOpen) => {
  if (isOpen && props.networkId) {
    loadNetworkDetail()
  }
})

// 加载网络详情
const loadNetworkDetail = async () => {
  if (!props.networkId) return

  try {
    loading.value = true

    // 调用 API 获取完整的网络详情
    const { networkApi } = await import('@/api/network')
    const response = await networkApi.getNetwork(props.networkId)
    networkData.value = response as any
  } catch (error: any) {
    console.error('加载网络详情失败:', error)
    ElMessage.error(error.message || t('network.loadDetailFailed'))
  } finally {
    loading.value = false
  }
}

// 刷新网络信息
const refreshNetwork = async () => {
  await loadNetworkDetail()
  ElMessage.success(t('network.refreshSuccess'))
}

// 复制网络ID
const copyNetworkId = async () => {
  if (!networkData.value?.id) return

  try {
    await navigator.clipboard.writeText(networkData.value.id)
    ElMessage.success(t('network.copyIdSuccess'))
  } catch (error) {
    ElMessage.error(t('common.copyFailed'))
  }
}

// 连接容器
const connectContainer = () => {
  if (networkData.value) {
    emit('connect-container', networkData.value)
  }
}

// 查看容器
const viewContainer = (container: any) => {
  const containerId = container.id || container.Id
  router.push(`/containers/${containerId}`)
}

// 断开容器连接
const disconnectContainer = async (container: any) => {
  const containerId = container.id || container.Id
  const containerName = container.name || container.Name
  try {
    await ElMessageBox.confirm(
      t('network.disconnectConfirm', { name: containerName }),
      t('network.disconnectConfirmTitle'),
      {
        confirmButtonText: t('network.disconnect'),
        cancelButtonText: t('common.cancel'),
        type: 'warning'
      }
    )

    await networksStore.disconnectContainer(networkData.value!.id, containerId)
    ElMessage.success(t('network.disconnectSuccess'))
    await loadNetworkDetail() // 刷新网络信息
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('断开容器连接失败:', error)
      ElMessage.error(error.message || t('network.disconnectFailed'))
    }
  }
}

// 删除网络
const removeNetwork = async () => {
  if (!networkData.value) return

  try {
    await ElMessageBox.confirm(
      t('network.removeConfirm', { name: networkData.value.name }),
      t('common.deleteConfirm'),
      {
        confirmButtonText: t('common.delete'),
        cancelButtonText: t('common.cancel'),
        type: 'warning'
      }
    )

    await networksStore.deleteNetwork(networkData.value.id)
    ElMessage.success(t('network.removeSuccess'))
    handleClose()
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('删除网络失败:', error)
      ElMessage.error(error.message || t('network.removeFailed'))
    }
  }
}

// 获取容器 IP 地址列表
const getIpAddresses = (container: any) => {
  const ip = container.ipAddress || container.IpAddress
  if (!ip) return []
  return Array.isArray(ip) ? ip : [ip]
}

// 获取驱动类型
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

// 获取作用域类型
const getScopeType = (scope: string) => {
  const scopeMap: Record<string, string> = {
    'local': 'primary',
    'swarm': 'warning',
    'global': 'success'
  }
  return scopeMap[scope] || 'info'
}

// 格式化日期时间
const formatDateTime = (dateString: string) => {
  if (!dateString) return '--'
  return formatLocalizedDateTime(dateString, '--')
}

// 处理关闭
const handleClose = () => {
  drawerVisible.value = false
  // 清理数据
  networkData.value = null
}
</script>

<style scoped>
.loading-container {
  padding: 20px;
}

.network-detail {
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
}

.header-actions {
  display: flex;
  gap: 8px;
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

.feature-tags {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.ipam-config {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.ipam-driver {
  display: flex;
  align-items: center;
  gap: 8px;
}

.ipam-driver label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-secondary);
}

.subnet-configs h4 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main);
}

.subnet-item {
  margin-bottom: 12px;
}

.subnet-item:last-child {
  margin-bottom: 0;
}

.subnet-info {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.subnet-field {
  display: flex;
  align-items: center;
  gap: 8px;
}

.subnet-field label {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
  min-width: 60px;
}

.aux-addresses {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.aux-tag {
  margin: 0;
}

.containers-list {
  max-height: 400px;
  overflow-y: auto;
}

.container-item {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 12px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background-color: var(--bg-subtle);
  margin-bottom: 8px;
}

.container-item:last-child {
  margin-bottom: 0;
}

.container-info {
  flex: 1;
  min-width: 0;
}

.container-name {
  display: flex;
  align-items: center;
  margin-bottom: 4px;
}

.container-details {
  font-size: 12px;
  color: var(--text-muted);
  display: flex;
  gap: 12px;
  margin-bottom: 4px;
}

.container-ips {
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  align-items: center;
  gap: 4px;
}

.container-actions {
  margin-left: 12px;
}

.config-section {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.config-group h4 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main);
}

.labels-list,
.options-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.label-item,
.option-item {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  background-color: var(--bg-subtle);
  border-radius: 4px;
}

.empty-config {
  text-align: center;
  padding: 20px;
}

.empty-state {
  display: flex;
  justify-content: center;
  align-items: center;
  height: 200px;
  background-color: var(--bg-subtle);
}

:deep(.el-card__body) {
  padding: 16px;
}

@media (max-width: 768px) {
  .info-grid {
    grid-template-columns: 1fr;
  }

  .network-detail {
    padding: 0 12px;
  }

  .container-item {
    flex-direction: column;
    align-items: stretch;
  }

  .container-actions {
    margin-left: 0;
    margin-top: 8px;
  }
}
</style>