<template>
  <div class="overview-dashboard">
    <!-- Vital Stats Row -->
    <div class="stats-row" v-if="container?.state === 'running'">
      <div class="stat-card">
        <div class="stat-icon-wrapper cpu">
          <el-icon><Cpu /></el-icon>
        </div>
        <div class="stat-info">
          <span class="stat-value font-mono">{{ stats.cpu }}%</span>
          <span class="stat-label">{{ t('container.cpuUsage') }}</span>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon-wrapper memory">
          <el-icon><Monitor /></el-icon>
        </div>
        <div class="stat-info">
          <span class="stat-value font-mono">{{ formatBytes(stats.memory) }}</span>
          <span class="stat-label">{{ t('container.memoryUsage') }}</span>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon-wrapper network">
          <el-icon><Connection /></el-icon>
        </div>
        <div class="stat-info">
          <span class="stat-value font-mono">{{ formatBytes(stats.networkIO) }}</span>
          <span class="stat-label">{{ t('container.networkIO') }}</span>
        </div>
      </div>
    </div>

    <div class="info-grid-container">
      <!-- General Info -->
      <div class="glass-panel-section">
        <h3 class="section-heading">{{ t('container.details') }}</h3>
        <div class="details-grid">
          <div class="detail-item">
            <span class="label">{{ t('container.imageName') }}</span>
            <span class="value font-mono">{{ container?.image }}</span>
          </div>
          <div class="detail-item">
            <span class="label">{{ t('container.containerOverview.imageId') }}</span>
            <span class="value font-mono">{{ formatImageId(container?.imageId) }}</span>
          </div>
          <div class="detail-item full-width" v-if="container?.entrypoint && container.entrypoint.length > 0">
            <span class="label">Entrypoint</span>
            <span class="value font-mono">{{ formatCommand(container.entrypoint) }}</span>
          </div>
          <div class="detail-item full-width" v-if="container?.command && container.command.length > 0">
            <span class="label">CMD</span>
            <span class="value font-mono">{{ formatCommand(container.command) }}</span>
          </div>
          <div class="detail-item">
            <span class="label">{{ t('container.created') }}</span>
            <span class="value">{{ formatDate(container?.created) }}</span>
          </div>
          <div class="detail-item">
            <span class="label">{{ t('container.containerOverview.uptime') }}</span>
            <span class="value">{{ getUptime(container) }}</span>
          </div>
          <div class="detail-item">
            <span class="label">{{ t('container.restartPolicy') }}</span>
            <span class="value">{{ formatRestartPolicy(container?.restartPolicy) }}</span>
          </div>
          <div class="detail-item">
            <span class="label">{{ t('container.containerOverview.hostname') }}</span>
            <span class="value font-mono">{{ container?.hostname || container?.hostName || '-' }}</span>
          </div>
          <div class="detail-item">
            <span class="label">{{ t('container.platform') }}</span>
            <span class="value font-mono">{{ container?.platform || 'linux/amd64' }}</span>
          </div>
          <div class="detail-item full-width" v-if="container?.labels && Object.keys(container.labels).length > 0">
            <span class="label">{{ t('container.containerOverview.labels') }}</span>
            <div class="labels-list">
              <span 
                v-for="(value, key) in container.labels" 
                :key="key"
                class="label-tag"
              >
                <span class="label-key">{{ key }}</span>
                <span class="label-separator" v-if="value">=</span>
                <span class="label-value" v-if="value">{{ value }}</span>
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Network Info -->
      <div class="glass-panel-section">
        <div class="section-header-flex">
          <h3 class="section-heading">{{ t('container.containerOverview.networkInfo') }}</h3>
          <el-button type="primary" size="small" @click="$emit('connect-network')" :icon="Connection">
            {{ t('container.connectNetwork') }}
          </el-button>
        </div>
        <div class="details-grid">
          <div class="detail-item full-width">
            <span class="label">{{ t('container.containerOverview.networkConnections') }}</span>
            <div class="networks-list" v-if="containerNetworks.length > 0">
              <div
                v-for="(network, idx) in containerNetworks"
                :key="idx"
                class="network-card"
                :class="{ 'default-network': isDefaultNetwork(network.name) }"
              >
                <div class="network-card-header">
                  <div class="network-name">
                    {{ network.name }}
                    <span v-if="isDefaultNetwork(network.name)" class="default-badge">{{ t('common.default') }}</span>
                  </div>
                  <el-button
                    type="danger"
                    size="small"
                    text
                    @click="$emit('disconnect-network', network.name)"
                    :disabled="!canDisconnectNetwork(network.name)"
                    :title="!canDisconnectNetwork(network.name) ? t('container.containerOverview.defaultNetworkNoDisconnect') : t('container.containerOverview.disconnect')"
                  >
                    {{ t('container.disconnect') }}
                  </el-button>
                </div>
                <div class="network-details">
                  <span class="network-detail-item" v-if="network.ipAddress">
                    <span class="label">{{ t('container.containerOverview.ip') }}:</span>
                    <span class="value font-mono">{{ network.ipAddress }}</span>
                  </span>
                  <span class="network-detail-item" v-if="network.gateway">
                    <span class="label">{{ t('container.containerOverview.gateway') }}:</span>
                    <span class="value font-mono">{{ network.gateway }}</span>
                  </span>
                  <span class="network-detail-item" v-if="network.macAddress">
                    <span class="label">{{ t('container.containerOverview.mac') }}:</span>
                    <span class="value font-mono">{{ network.macAddress }}</span>
                  </span>
                </div>
              </div>
            </div>
            <div v-else class="empty-networks">
              <span class="text-muted">{{ t('container.noNetworks') }}</span>
            </div>
          </div>
          <!-- 端口映射 -->
          <div class="detail-item full-width">
            <span class="label">{{ t('container.containerOverview.ports') }}</span>
            <div class="ports-detail">
              <div v-if="mappedPorts.length > 0" class="ports-section">
                <span class="ports-section-label">{{ t('container.ports.mappedPorts') }}</span>
                <div class="ports-tags">
                  <span
                    v-for="(port, idx) in mappedPorts"
                    :key="'m-'+idx"
                    class="port-tag mapped"
                  >
                    {{ port.publicPort }} → {{ port.privatePort }}
                    <span class="port-type">{{ port.type?.toUpperCase() || 'TCP' }}</span>
                  </span>
                </div>
              </div>
              <div v-if="exposedPorts.length > 0" class="ports-section">
                <span class="ports-section-label">{{ t('container.ports.internalPorts') }}</span>
                <div class="ports-tags">
                  <span
                    v-for="(port, idx) in exposedPorts"
                    :key="'e-'+idx"
                    class="port-tag exposed"
                  >
                    {{ port.privatePort }}
                    <span class="port-type">{{ port.type?.toUpperCase() || 'TCP' }}</span>
                  </span>
                </div>
              </div>
              <span v-if="!container?.ports || container.ports.length === 0" class="text-muted text-sm">{{ t('container.containerOverview.noPortsConfig') }}</span>
            </div>
          </div>
          <!-- 反向代理 -->
          <div class="detail-item full-width" v-if="container?.domainMappings && container.domainMappings.length > 0">
            <span class="label">{{ t('container.ports.reverseProxy') }}</span>
            <div class="proxy-mappings">
              <div 
                v-for="(mapping, idx) in container.domainMappings" 
                :key="idx"
                class="proxy-mapping-item"
              >
                <div class="proxy-domain">
                  <el-icon><Link /></el-icon>
                  <span class="domain-text">{{ mapping.domain }}</span>
                  <el-tag v-if="mapping.enableSsl" type="success" size="small">SSL</el-tag>
                </div>
                <div class="proxy-target">
                  <span class="proxy-port">:{{ mapping.containerPort }}</span>
                  <span class="proxy-path" v-if="mapping.pathPrefix && mapping.pathPrefix !== '/'">{{ mapping.pathPrefix }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Environment Variables -->
      <div class="glass-panel-section">
        <h3 class="section-heading">{{ t('container.containerOverview.environmentVars') }}</h3>
        <div v-if="envVars.length > 0" class="env-list-readonly">
          <div v-for="(env, idx) in envVars" :key="idx" class="env-item-readonly">
            <span class="env-key">{{ env.key }}</span>
            <span class="env-eq">=</span>
            <span class="env-value">{{ env.value || t('container.containerOverview.emptyValue') }}</span>
          </div>
        </div>
        <div v-else class="empty-section-hint">
          <el-icon><Document /></el-icon>
          <span>{{ t('container.containerOverview.noEnvironmentVars') }}</span>
        </div>
      </div>

      <!-- Mounts -->
      <div class="glass-panel-section">
        <h3 class="section-heading">{{ t('container.mounts') }}</h3>
        <div v-if="volumes.length > 0" class="mounts-list-readonly">
          <div v-for="(vol, idx) in volumes" :key="idx" class="mount-item-readonly">
            <div class="mount-source">
              <el-icon><FolderOpened /></el-icon>
              <span class="font-mono">{{ vol.source }}</span>
            </div>
            <el-icon class="mount-arrow"><ArrowRight /></el-icon>
            <div class="mount-target">
              <span class="font-mono">{{ vol.target }}</span>
              <el-tag :type="vol.mode === 'rw' ? 'success' : 'warning'" size="small">{{ vol.mode }}</el-tag>
            </div>
          </div>
        </div>
        <div v-else class="empty-section-hint">
          <el-icon><FolderOpened /></el-icon>
          <span>{{ t('container.containerOverview.noVolumes') }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Cpu, Monitor, Connection, Document, FolderOpened, ArrowRight, Link } from '@element-plus/icons-vue'
import { formatLocalizedDateTime } from '@/utils/date'
import type { Container, ContainerPort, ContainerMount } from '@/types/container'

const props = defineProps<{
  container: Container | null
  stats: { cpu: number; memory: number; networkIO: number }
  envVars: Array<{ key: string; value: string }>
  volumes: Array<{ source: string; target: string; mode: string }>
}>()

defineEmits<{
  'connect-network': []
  'disconnect-network': [networkName: string]
}>()

const { t } = useI18n()

// 计算属性
const containerNetworks = computed(() => {
  if (!props.container?.networkSettings?.networks) return []
  return Object.entries(props.container.networkSettings.networks).map(([name, net]) => ({
    name,
    ipAddress: net.ipAddress || '',
    gateway: net.gateway || '',
    macAddress: net.macAddress || ''
  }))
})

const mappedPorts = computed(() => {
  if (!props.container?.ports) return []
  const seen = new Set<string>()
  return props.container.ports
    .filter(p => p.publicPort && p.publicPort > 0)
    .filter(port => {
      const key = `${port.publicPort}:${port.privatePort}:${port.type || 'tcp'}`
      if (seen.has(key)) return false
      seen.add(key)
      return true
    })
})

const exposedPorts = computed(() => {
  if (!props.container?.ports) return []
  const seen = new Set<string>()
  return props.container.ports
    .filter(p => !p.publicPort || p.publicPort === 0)
    .filter(port => {
      const key = `${port.privatePort}:${port.type || 'tcp'}`
      if (seen.has(key)) return false
      seen.add(key)
      return true
    })
})

// 工具函数
const isDefaultNetwork = (name: string) => name === 'dockerpanel-network'

const canDisconnectNetwork = (name: string) => {
  if (isDefaultNetwork(name)) return false
  return containerNetworks.value.length > 1
}

const formatBytes = (bytes: number) => {
  if (bytes === null || bytes === undefined || isNaN(bytes) || bytes === 0) return '0 B'
  const k = 1024, sizes = ['B', 'KB', 'MB', 'GB']
  const absBytes = Math.abs(bytes)
  if (absBytes < 1) return absBytes.toFixed(2) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const formatDate = (d: string) => formatLocalizedDateTime(d, '-')

const formatImageId = (imageId: string | undefined) => {
  if (!imageId) return '-'
  const id = imageId.replace(/^sha256:/, '')
  return id.length > 12 ? id.substring(0, 12) : id
}

const formatCommand = (cmd: string[] | undefined) => {
  if (!cmd || cmd.length === 0) return '-'
  return cmd.join(' ')
}

const formatRestartPolicy = (policy: any) => {
  if (!policy || !policy.name || policy.name === 'no') return t('container.containerOverview.noRestart')
  if (policy.name === 'always') return t('container.containerOverview.alwaysRestart')
  if (policy.name === 'unless-stopped') return t('container.containerOverview.unlessStopped')
  if (policy.name === 'on-failure') return t('container.containerOverview.restartOnFailure', { count: policy.maximumRetryCount || 0 })
  return policy.name
}

const getUptime = (c: Container | null) => {
  if (!c || c.state !== 'running') return t('container.exited')
  const startTime = (c as any).startedAt || (c as any).started
  if (!startTime) return '-'
  
  const start = new Date(startTime)
  const now = new Date()
  const diff = now.getTime() - start.getTime()
  
  if (diff < 0) return '-'
  
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))
  const hours = Math.floor((diff % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60))
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60))
  
  if (days > 0) return t('container.uptimeDays', { days, hours })
  if (hours > 0) return t('container.uptimeHours', { hours, minutes })
  return t('container.uptimeMinutes', { minutes })
}
</script>

<style>

/* 使用非scoped样式，继承父组件全局样式 */

/* 反向代理样式 */
.proxy-mappings {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.proxy-mapping-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  background: rgba(24, 144, 255, 0.08);
  border-radius: 6px;
  border: 1px solid rgba(24, 144, 255, 0.2);
}

.proxy-domain {
  display: flex;
  align-items: center;
  gap: 6px;
}

.proxy-domain .el-icon {
  color: var(--primary-color, #1890ff);
}

.domain-text {
  font-family: 'JetBrains Mono', 'SF Mono', monospace;
  font-weight: 600;
  color: var(--primary-color, #1890ff);
}

.proxy-target {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-left: auto;
  color: var(--text-secondary, #666);
  font-size: 13px;
}

.proxy-port {
  font-family: 'JetBrains Mono', 'SF Mono', monospace;
  color: var(--text-primary, #333);
}

.proxy-path {
  color: var(--text-muted, #999);
  font-family: 'JetBrains Mono', 'SF Mono', monospace;
  font-size: 12px;
}

</style>
