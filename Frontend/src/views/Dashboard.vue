<template>
  <div class="dashboard-page" v-loading="loading">
    <!-- Page Header -->
    <div class="page-header">
      <div class="header-content">
        <h1 class="page-title">{{ t('dashboard.title') }}</h1>
        <p class="page-subtitle">{{ t('dashboard.subtitle') }}</p>
      </div>
      <div class="header-actions">
        <el-button :icon="Refresh" @click="loadData" class="refresh-btn">
          <span class="btn-text">{{ t('dashboard.syncData') }}</span>
        </el-button>
      </div>
    </div>

    <!-- Metrics Grid -->
    <div class="metrics-grid">
      <div 
        v-for="(metric, index) in metrics" 
        :key="metric.label" 
        class="metric-card saas-card"
        :style="{ '--index': index }"
      >
        <div class="metric-header">
          <div class="metric-icon" :class="metric.color">
            <el-icon><component :is="metric.icon" /></el-icon>
          </div>
        </div>
        <div class="metric-body">
          <span class="metric-value">
            {{ metric.value }}
            <span class="metric-unit" v-if="metric.unit">{{ metric.unit }}</span>
          </span>
          <span class="metric-label">
            {{ metric.label }}
            <span class="metric-subtext" v-if="metric.subText">{{ metric.subText }}</span>
          </span>
        </div>
        <div class="metric-sparkline" :class="metric.color"></div>
      </div>
    </div>

    <!-- Dashboard Content Grid -->
    <div class="dashboard-grid">
      <!-- Docker Info Card -->
      <div class="saas-card docker-info-card">
        <div class="saas-card-header">
          <div class="header-title">
            <el-icon class="header-icon"><Platform /></el-icon>
            <span>{{ t('dashboard.dockerStatus') }}</span>
          </div>
          <div class="docker-status" :class="dockerStatus.class">
            <span class="status-dot"></span>
            <span class="status-text">{{ dockerStatus.text }}</span>
          </div>
        </div>
        <div class="saas-card-body">
          <div class="docker-info-grid">
            <div class="info-item">
              <span class="info-label">{{ t('dashboard.dockerVersion') }}</span>
              <span class="info-value">{{ dockerStats?.docker?.version || '-' }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">{{ t('dashboard.apiVersion') }}</span>
              <span class="info-value">{{ dockerStats?.docker?.apiVersion || '-' }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">{{ t('dashboard.operatingSystem') }}</span>
              <span class="info-value">{{ dockerStats?.docker?.os || '-' }}/{{ dockerStats?.docker?.arch || '-' }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">{{ t('dashboard.totalContainersLabel') }}</span>
              <span class="info-value">
                <span class="running-count">{{ dockerStats?.containers?.running || 0 }}</span> / {{ dockerStats?.containers?.total || 0 }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Host Resources Card（可选：需要 /host/proc 只读挂载） -->
      <div class="saas-card host-card">
        <div class="saas-card-header">
          <div class="header-title">
            <el-icon class="header-icon"><Monitor /></el-icon>
            <span>{{ t('dashboard.hostMonitor') }}</span>
            <span class="host-scope">{{ t('dashboard.hostMonitorScope') }}</span>
          </div>
          <div class="host-status" :class="hostAvailable ? 'enabled' : 'disabled'">
            <span class="status-dot"></span>
            <span>{{ hostAvailable ? t('dashboard.hostMonitorEnabled') : t('dashboard.hostMonitorDisabled') }}</span>
          </div>
        </div>
        <div class="saas-card-body">
          <template v-if="hostAvailable">
            <div class="host-meta">
              <span v-if="hostInfo?.osName">
                {{ hostInfo.osName }}<template v-if="hostInfo.osVersion"> {{ hostInfo.osVersion }}</template>
              </span>
              <span v-if="hostInfo?.kernelVersion">Kernel {{ hostInfo.kernelVersion }}</span>
              <span v-if="hostInfo?.hostName">{{ hostInfo.hostName }}</span>
              <span>{{ t('dashboard.hostUptime') }} {{ hostUptime }}</span>
              <span>{{ t('dashboard.hostLoad') }} {{ hostLoad }}</span>
            </div>
            <div class="host-grid">
              <div class="host-metric">
                <div class="host-metric-head">
                  <span>{{ t('dashboard.hostCpu') }}</span>
                  <strong>{{ hostCpuPercent.toFixed(2) }}%</strong>
                </div>
                <el-progress :percentage="hostCpuPercent" :stroke-width="8" :show-text="false" color="#3b82f6" />
                <span class="host-metric-sub">{{ t('dashboard.hostCores', { count: hostInfo?.cpu?.coreCount || 0 }) }}</span>
              </div>
              <div class="host-metric">
                <div class="host-metric-head">
                  <span>{{ t('dashboard.hostMemory') }}</span>
                  <strong>{{ hostInfo?.memory?.usedFormatted }} / {{ hostInfo?.memory?.totalFormatted }}</strong>
                </div>
                <el-progress :percentage="hostMemoryPercent" :stroke-width="8" :show-text="false" color="#8b5cf6" />
                <span class="host-metric-sub">{{ hostMemoryPercent.toFixed(2) }}%</span>
              </div>
              <div class="host-metric">
                <div class="host-metric-head">
                  <span>{{ t('dashboard.hostDisk') }}</span>
                  <strong v-if="hostRootFs">{{ hostRootFs.usedFormatted }} / {{ hostRootFs.totalFormatted }}</strong>
                  <strong v-else>{{ t('dashboard.hostUnavailable') }}</strong>
                </div>
                <el-progress v-if="hostRootFs" :percentage="hostRootFs.usagePercent" :stroke-width="8" :show-text="false" color="#f97316" />
                <span class="host-metric-sub">
                  {{ hostRootFs
                    ? `${hostRootFs.mountPoint} · ${hostRootFs.usagePercent.toFixed(2)}%`
                    : (hostInfo?.fileSystemUnavailableReason || t('dashboard.hostUnavailable')) }}
                </span>
              </div>
              <div class="host-metric">
                <div class="host-metric-head">
                  <span>{{ t('dashboard.hostDiskIo') }}</span>
                  <strong v-if="hostInfo?.disk?.available">{{ hostInfo?.disk?.readSpeedFormatted }} / {{ hostInfo?.disk?.writeSpeedFormatted }}</strong>
                  <strong v-else>{{ t('dashboard.hostUnavailable') }}</strong>
                </div>
                <span class="host-metric-sub">
                  {{ hostInfo?.disk?.available
                    ? t('dashboard.hostDiskIoHint')
                    : (hostInfo?.disk?.unavailableReason || t('dashboard.hostUnavailable')) }}
                </span>
              </div>
              <div class="host-metric">
                <div class="host-metric-head">
                  <span>{{ t('dashboard.hostNetwork') }}</span>
                  <strong v-if="hostInfo?.network?.available">{{ hostInfo.network.rxSpeedFormatted }} / {{ hostInfo.network.txSpeedFormatted }}</strong>
                  <strong v-else>{{ t('dashboard.hostUnavailable') }}</strong>
                </div>
                <span class="host-metric-sub">
                  {{ hostInfo?.network?.available
                    ? t('dashboard.hostNetworkHint')
                    : (hostInfo?.network?.unavailableReason || t('dashboard.hostUnavailable')) }}
                </span>
              </div>
              <div class="host-metric">
                <div class="host-metric-head">
                  <span>{{ t('dashboard.hostSwap') }}</span>
                  <strong>{{ hostSwapText }}</strong>
                </div>
                <span class="host-metric-sub">{{ t('dashboard.hostSwapHint') }}</span>
              </div>
            </div>
          </template>
          <div v-else class="host-disabled">
            <el-icon class="host-disabled-icon"><Warning /></el-icon>
            <div class="host-disabled-body">
              <p class="host-disabled-title">{{ t('dashboard.hostMonitorDisabledTitle') }}</p>
              <p class="host-disabled-desc">{{ hostUnavailableReason || t('dashboard.hostMonitorDisabledDesc') }}</p>
              <pre class="host-mount-example">volumes:
  - /proc:/host/proc:ro
  - /sys:/host/sys:ro
  - /etc/os-release:/host/etc/os-release:ro
  # optional: disk capacity
  - /:/host/root:ro</pre>
              <p class="host-disabled-note">{{ t('dashboard.hostMonitorDisabledNote') }}</p>
            </div>
          </div>
        </div>
      </div>

      <!-- Resource Charts Card（宿主机优先；未启用 /host/proc 时回退到容器聚合口径） -->
      <div class="saas-card charts-card">
        <div class="saas-card-header">
          <div class="header-title">
            <el-icon class="header-icon"><TrendCharts /></el-icon>
            <span>{{ chartTitle }}</span>
            <span class="host-scope">{{ chartScope }}</span>
            <span class="chart-window">{{ t('dashboard.trendWindow') }}</span>
          </div>
          <div class="live-indicator">
            <span class="status-dot"></span>
            <span>{{ t('dashboard.liveData') }}</span>
          </div>
        </div>
        <div class="saas-card-body chart-body">
          <p class="chart-scope-hint">{{ chartHint }}</p>
          <div class="resource-chart-grid">
            <div class="resource-chart-panel gauge-panel">
              <div class="resource-chart-header">
                <div>
                  <span class="resource-label">{{ chartCpuLabel }}</span>
                  <div class="resource-value">{{ chartCpuValue }}<span>%</span></div>
                </div>
                <span class="resource-subtitle">{{ chartCpuSubtitle }}</span>
              </div>
              <div class="mini-chart-container gauge-chart-container" ref="cpuChartContainer"></div>
            </div>

            <div class="resource-chart-panel gauge-panel">
              <div class="resource-chart-header">
                <div>
                  <span class="resource-label">{{ chartMemoryLabel }}</span>
                  <div class="resource-value resource-value-text">{{ chartMemoryValue }}</div>
                </div>
                <span class="resource-subtitle">{{ chartMemorySubtitle }}</span>
              </div>
              <div class="mini-chart-container gauge-chart-container" ref="memoryChartContainer"></div>
            </div>

            <div class="resource-chart-panel">
              <div class="resource-chart-header network-resource-header">
                <div>
                  <span class="resource-label">{{ chartNetworkLabel }}</span>
                  <span class="resource-subtitle">{{ t('dashboard.receiveSpeed') }} / {{ t('dashboard.sendSpeed') }}</span>
                </div>
              </div>
              <div class="network-live-values">
                <span><i class="legend-dot rx"></i>{{ t('dashboard.receiveLabel') }} {{ chartRxText }}</span>
                <span><i class="legend-dot tx"></i>{{ t('dashboard.sendLabel') }} {{ chartTxText }}</span>
              </div>
              <div class="mini-chart-container" ref="networkChartContainer"></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Quick Actions Card -->
      <div class="saas-card actions-card">
        <div class="saas-card-header">
          <div class="header-title">
            <el-icon class="header-icon"><Lightning /></el-icon>
            <span>{{ t('dashboard.quickLaunch') }}</span>
          </div>
        </div>
        <div class="saas-card-body">
          <div class="action-grid">
            <button 
              v-for="action in quickActions" 
              :key="action.path" 
              class="action-tile"
              @click="router.push(action.path)"
            >
              <div class="action-icon" :class="action.color">
                <el-icon><component :is="action.icon" /></el-icon>
              </div>
              <span class="action-label">{{ action.label }}</span>
              <el-icon class="action-arrow"><ArrowRight /></el-icon>
            </button>
          </div>
        </div>
      </div>

      <!-- Recent Containers Card -->
      <div class="saas-card containers-card">
        <div class="saas-card-header">
          <div class="header-title">
            <el-icon class="header-icon"><Box /></el-icon>
            <span>{{ t('dashboard.recentContainers') }}</span>
          </div>
          <el-button text size="small" @click="router.push('/containers')">{{ t('dashboard.viewAll') }}</el-button>
        </div>
        <div class="saas-card-body">
          <div class="container-list">
            <div 
              class="container-item" 
              v-for="container in recentContainers" 
              :key="container.id"
              @click="router.push(`/containers/${container.id}`)"
            >
              <div class="container-status" :class="container.state">
                <span class="status-dot"></span>
              </div>
              <div class="container-info">
                <span class="container-name">{{ container.name }}</span>
                <span class="container-image">{{ container.image }}</span>
              </div>
              <div class="container-time">{{ container.timeAgo }}</div>
            </div>
            <div v-if="recentContainers.length === 0" class="empty-state">
              <el-icon><Box /></el-icon>
              <span>{{ t('dashboard.noContainers') }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick, shallowRef, h } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { 
  Box, Picture, Cpu, Coin, Refresh, Plus, Download, Setting,
  TrendCharts, Connection, Folder, ArrowRight, Platform, Monitor, Warning
} from '@element-plus/icons-vue'
import { useContainersStore } from '@/stores/containers'
import { useImagesStore } from '@/stores/images'
import { useSystemStore } from '@/stores/system'
import { signalrService } from '@/services/signalr'
import { echarts, type ECharts } from '@/plugins/echarts'
import { formatLocalizedTime } from '@/utils/date'

// Lightning icon component - using render function instead of template
const Lightning = {
  render() {
    return h('svg', { viewBox: '0 0 24 24', fill: 'currentColor' }, [
      h('path', { d: 'M13 2L3 14h9l-1 8 10-12h-9l1-8z' })
    ])
  }
}

const { t } = useI18n()
const router = useRouter()
const cStore = useContainersStore()
const iStore = useImagesStore()
const sStore = useSystemStore()

const loading = ref(false)
const cpuChartContainer = ref<HTMLElement | null>(null)
const memoryChartContainer = ref<HTMLElement | null>(null)
const networkChartContainer = ref<HTMLElement | null>(null)
const cpuChartInstance = shallowRef<ECharts | null>(null)
const memoryChartInstance = shallowRef<ECharts | null>(null)
const networkChartInstance = shallowRef<ECharts | null>(null)
let unsubscribeSignalR: (() => void) | null = null
let unsubscribeHostSignalR: (() => void) | null = null

// Use Docker stats from store
const dockerStats = computed(() => sStore.dockerStats)

// Host metrics (optional; available=false when /host/proc is not mounted)
const hostInfo = computed(() => sStore.hostMetrics)
const hostAvailable = computed(() => !!hostInfo.value?.available)
const hostCpuPercent = computed(() => hostInfo.value?.cpu?.usagePercent || 0)
const hostMemoryPercent = computed(() => hostInfo.value?.memory?.usagePercent || 0)
const hostRootFs = computed(() => {
  const fileSystems = hostInfo.value?.fileSystems || []
  return fileSystems.find(fs => fs.mountPoint === '/') || fileSystems[0] || null
})
const hostUnavailableReason = computed(() => hostInfo.value?.unavailableReason || '')
const hostLoad = computed(() => {
  const load = hostInfo.value?.load
  if (!load) return '-'
  return `${load.load1.toFixed(2)} / ${load.load5.toFixed(2)} / ${load.load15.toFixed(2)}`
})
const hostUptime = computed(() => formatDuration(hostInfo.value?.uptimeSeconds || 0))
const hostSwapText = computed(() => {
  const memory = hostInfo.value?.memory
  if (!memory || memory.swapTotalBytes <= 0) return t('dashboard.hostSwapNone')
  return `${formatBytes(memory.swapUsedBytes)} / ${formatBytes(memory.swapTotalBytes)}`
})

const formatBytes = (bytes: number) => {
  if (!bytes || bytes <= 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let value = bytes
  let index = 0
  while (value >= 1024 && index < units.length - 1) {
    value /= 1024
    index++
  }
  return `${value.toFixed(value >= 100 ? 0 : 1)} ${units[index]}`
}

const formatDuration = (seconds: number) => {
  if (!seconds || seconds <= 0) return '-'
  const days = Math.floor(seconds / 86400)
  const hours = Math.floor((seconds % 86400) / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  if (days > 0) return `${days}d ${hours}h`
  if (hours > 0) return `${hours}h ${minutes}m`
  return `${minutes}m`
}

const runningCount = computed(() => dockerStats.value?.containers?.running || 0)
const totalImages = computed(() => dockerStats.value?.images?.count || iStore.images.length)

// —— 宿主机口径 ——
const hostCpuText = computed(() => {
  if (!hostAvailable.value) return '—'
  return `${hostCpuPercent.value.toFixed(2)}%`
})

const hostMemoryUsed = computed(() => {
  if (!hostAvailable.value) return '—'
  return hostInfo.value?.memory?.usedFormatted || '0 B'
})

const hostMemorySubText = computed(() => {
  if (!hostAvailable.value) return t('dashboard.hostMonitorDisabled')
  const memory = hostInfo.value?.memory
  if (!memory) return t('dashboard.hostMonitorDisabled')
  return `${memory.totalFormatted} · ${hostMemoryPercent.value.toFixed(2)}%`
})

const hostRxSpeed = computed(() => hostInfo.value?.network?.rxSpeedFormatted || '0 B/s')
const hostTxSpeed = computed(() => hostInfo.value?.network?.txSpeedFormatted || '0 B/s')

// —— 容器聚合口径（宿主机监控未启用时的回退数据源，始终明确标注为“容器聚合”）——
const dockerAvailable = computed(() => dockerStats.value?.docker?.status === 'running')
const containerCpuPercent = computed(() => dockerStats.value?.resources?.cpuUsagePercent || 0)
const containerMemPercent = computed(() => dockerStats.value?.resources?.memoryPercent || 0)
const containerMemUsed = computed(() => dockerStats.value?.resources?.memoryUsedFormatted || '0 B')
const containerMemLimit = computed(() => dockerStats.value?.resources?.memoryLimitFormatted || '0 B')
const containerRxSpeed = computed(() => dockerStats.value?.network?.rxSpeedFormatted || '0 B/s')
const containerTxSpeed = computed(() => dockerStats.value?.network?.txSpeedFormatted || '0 B/s')
const containerCpuCores = computed(() => dockerStats.value?.docker?.ncpu || 0)

// —— 图表口径：有宿主机数据就用宿主机，否则回退到容器聚合 ——
const chartCpuLabel = computed(() => hostAvailable.value ? t('dashboard.hostCpu') : t('dashboard.cpuUsage'))
const chartMemoryLabel = computed(() => hostAvailable.value ? t('dashboard.hostMemory') : t('dashboard.memoryUsage'))
const chartTitle = computed(() => hostAvailable.value ? t('dashboard.hostTrendTitle') : t('dashboard.containerTrendTitle'))
const chartScope = computed(() => hostAvailable.value ? t('dashboard.hostMonitorScope') : t('dashboard.containerAggregate'))
const chartHint = computed(() => hostAvailable.value ? t('dashboard.hostTrendHint') : t('dashboard.containerFallbackHint'))
const chartCpuValue = computed(() => (hostAvailable.value ? hostCpuPercent.value : containerCpuPercent.value).toFixed(2))
const chartCpuSubtitle = computed(() => {
  if (hostAvailable.value) {
    return t('dashboard.hostCores', { count: hostInfo.value?.cpu?.coreCount || 0 })
  }
  return containerCpuCores.value
    ? t('dashboard.cpuCores', { count: containerCpuCores.value })
    : t('dashboard.containerAggregate')
})
const chartMemoryValue = computed(() => hostAvailable.value ? hostMemoryUsed.value : containerMemUsed.value)
const chartMemorySubtitle = computed(() => {
  if (hostAvailable.value) {
    return `${hostInfo.value?.memory?.totalFormatted || '0 B'} · ${hostMemoryPercent.value.toFixed(2)}%`
  }
  return `${containerMemLimit.value} · ${containerMemPercent.value.toFixed(2)}%`
})
const chartNetworkLabel = computed(() => hostAvailable.value ? t('dashboard.hostNetwork') : t('dashboard.containerNetwork'))
const chartRxText = computed(() => hostAvailable.value ? hostRxSpeed.value : containerRxSpeed.value)
const chartTxText = computed(() => hostAvailable.value ? hostTxSpeed.value : containerTxSpeed.value)

const dockerStatus = computed(() => {
  const status = dockerStats.value?.docker?.status
  if (status === 'running') {
    return { class: 'running', text: t('container.running') }
  }
  return { class: 'stopped', text: t('container.exited') }
})

const metrics = computed(() => [
  {
    label: t('dashboard.runningContainers'),
    value: runningCount.value,
    subText: t('dashboard.totalContainers', { count: dockerStats.value?.containers?.total || cStore.containers.length }),
    icon: Box,
    color: 'gradient-blue',
    glowColor: 'glow-blue'
  },
  {
    label: t('dashboard.localImages'),
    value: totalImages.value,
    subText: dockerStats.value?.images?.totalSizeFormatted || '0 B',
    icon: Picture,
    color: 'gradient-purple',
    glowColor: 'glow-purple'
  },
  {
    label: hostAvailable.value ? t('dashboard.hostCpuCard') : t('dashboard.cpuUsage'),
    value: hostAvailable.value ? hostCpuText.value : (dockerAvailable.value ? `${containerCpuPercent.value.toFixed(2)}%` : '—'),
    subText: hostAvailable.value
      ? t('dashboard.hostCores', { count: hostInfo.value?.cpu?.coreCount || 0 })
      : (containerCpuCores.value
        ? `${t('dashboard.cpuCores', { count: containerCpuCores.value })} · ${t('dashboard.containerAggregate')}`
        : t('dashboard.containerAggregate')),
    icon: Cpu,
    color: 'gradient-orange',
    glowColor: 'glow-orange'
  },
  {
    label: hostAvailable.value ? t('dashboard.hostMemoryCard') : t('dashboard.memoryUsage'),
    value: hostAvailable.value ? hostMemoryUsed.value : (dockerAvailable.value ? containerMemUsed.value : '—'),
    subText: hostAvailable.value
      ? hostMemorySubText.value
      : `${containerMemLimit.value} · ${containerMemPercent.value.toFixed(2)}% · ${t('dashboard.containerAggregate')}`,
    icon: Coin,
    color: 'gradient-pink',
    glowColor: 'glow-pink'
  }
])
const quickActions = computed(() => [
  { path: '/containers', label: t('dashboard.deployApp'), icon: Plus, color: 'gradient-blue' },
  { path: '/images', label: t('dashboard.pullImage'), icon: Download, color: 'gradient-purple' },
  { path: '/networks', label: t('common.networks'), icon: Connection, color: 'gradient-cyan' },
  { path: '/volumes', label: t('common.volumes'), icon: Folder, color: 'gradient-orange' },
  { path: '/settings', label: t('dashboard.sysSettings'), icon: Setting, color: 'gradient-pink' }
])

const recentContainers = computed(() => {
  return cStore.containers.slice(0, 5).map((c: any) => ({
    id: c.id,
    name: c.name || c.Names?.[0]?.replace('/', '') || 'unknown',
    image: c.image || c.Image || '-',
    state: c.state || 'stopped',
    timeAgo: formatRelativeTime(c.created || c.Created)
  }))
})

const formatRelativeTime = (timestamp: number | string | undefined) => {
  if (!timestamp) return t('dashboard.unknown')

  const now = Date.now()
  const time = typeof timestamp === 'number' 
    ? (timestamp > 1e12 ? timestamp : timestamp * 1000)
    : new Date(timestamp).getTime()

  const diff = now - time
  const seconds = Math.floor(diff / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)
  const days = Math.floor(hours / 24)

  if (days > 0) return t('dashboard.daysAgo', { count: days })
  if (hours > 0) return t('dashboard.hoursAgo', { count: hours })
  if (minutes > 0) return t('dashboard.minutesAgo', { count: minutes })
  return t('dashboard.justNow')
}

const historyPoints = 60

const generateTimeLabels = () => {
  const labels: string[] = []
  const now = new Date()
  for (let i = historyPoints - 1; i >= 0; i--) {
    const time = new Date(now.getTime() - i * 5000)
    labels.push(formatLocalizedTime(time, '--', { hour: '2-digit', minute: '2-digit', second: '2-digit' }))
  }
  return labels
}

const padHistory = (values: number[]) => {
  const normalized = values.map((value) => Number(value.toFixed(2))).slice(-historyPoints)
  return Array(Math.max(historyPoints - normalized.length, 0)).fill(null).concat(normalized)
}

const getCssVar = (name: string, fallback: string) => {
  if (typeof window === 'undefined') return fallback
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback
}

const createChartBaseOption = (unit: string, yAxisFormatter: string | ((value: number) => string), max?: number) => {
  const axisColor = getCssVar('--border-color', '#e2e8f0')
  const splitColor = getCssVar('--border-color-light', '#f1f5f9')
  const labelColor = getCssVar('--text-muted', '#94a3b8')

  return {
    grid: {
      top: 12,
      right: 16,
      bottom: 30,
      left: 46,
      outerBoundsMode: 'same',
      outerBoundsContain: 'axisLabel'
    },
    tooltip: {
      trigger: 'axis',
      backgroundColor: 'rgba(15, 23, 42, 0.92)',
      borderColor: 'rgba(148, 163, 184, 0.25)',
      borderWidth: 1,
      textStyle: { color: '#fff' },
      formatter: (params: any) => {
        if (!params || params.length === 0) return ''
        const validParams = params.filter((item: any) => item.value !== null && item.value !== undefined)
        const values = validParams.map((item: any) => `${item.marker} ${item.seriesName}: ${item.value} ${unit}`).join('<br/>')
        return `<div style="margin-bottom: 4px; font-weight: 600;">${params[0].axisValue}</div>${values || '-'}`
      }
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: generateTimeLabels(),
      axisLine: { show: true, lineStyle: { color: axisColor, width: 1 } },
      axisTick: { show: true, lineStyle: { color: axisColor } },
      axisLabel: { color: labelColor, fontSize: 10, interval: 14 },
      splitLine: { show: false }
    },
    yAxis: {
      type: 'value',
      max,
      axisLine: { show: true, lineStyle: { color: axisColor, width: 1 } },
      axisTick: { show: true, lineStyle: { color: axisColor } },
      axisLabel: { color: labelColor, fontSize: 10, formatter: yAxisFormatter },
      splitLine: { show: true, lineStyle: { color: splitColor, type: 'dashed' } }
    },
    animationDuration: 300
  }
}

const lineAreaStyle = (from: string, to: string) => ({
  color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
    { offset: 0, color: from },
    { offset: 1, color: to }
  ])
})

const getNetworkChartData = () => {
  const rxHistory = hostAvailable.value ? sStore.hostRxHistory : sStore.rxSpeedHistory
  const txHistory = hostAvailable.value ? sStore.hostTxHistory : sStore.txSpeedHistory
  const maxNetBytes = Math.max(...rxHistory, ...txHistory, 1)
  const divisor = maxNetBytes > 1024 * 1024 ? 1024 * 1024 : 1024
  const unit = divisor === 1024 * 1024 ? 'MB/s' : 'KB/s'

  return {
    unit,
    rxData: padHistory(rxHistory.map((value: number) => value / divisor)),
    txData: padHistory(txHistory.map((value: number) => value / divisor))
  }
}

// CPU / 内存：百分比折线（近 60 个采样点），让「趋势」名副其实
const createPercentTrendOption = (
  data: (number | null)[],
  color: string,
  areaFrom: string,
  areaTo: string,
  seriesName: string
) => ({
  ...createChartBaseOption('%', (value: number) => `${value}%`, 100),
  series: [
    {
      name: seriesName,
      type: 'line',
      data,
      smooth: true,
      showSymbol: false,
      lineStyle: { width: 2, color },
      areaStyle: lineAreaStyle(areaFrom, areaTo)
    }
  ]
})

const initCharts = () => {
  disposeCharts()

  if (cpuChartContainer.value) cpuChartInstance.value = echarts.init(cpuChartContainer.value)
  if (memoryChartContainer.value) memoryChartInstance.value = echarts.init(memoryChartContainer.value)
  if (networkChartContainer.value) networkChartInstance.value = echarts.init(networkChartContainer.value)

  updateCharts()
  window.addEventListener('resize', handleResize)
}

const disposeCharts = () => {
  cpuChartInstance.value?.dispose()
  memoryChartInstance.value?.dispose()
  networkChartInstance.value?.dispose()
  cpuChartInstance.value = null
  memoryChartInstance.value = null
  networkChartInstance.value = null
}

const handleResize = () => {
  cpuChartInstance.value?.resize()
  memoryChartInstance.value?.resize()
  networkChartInstance.value?.resize()
}

const updateCharts = () => {
  // CPU / 内存使用与网络图一致的时间窗口（近 60 个采样点），
  // 数据源跟随宿主机 / 容器聚合口径切换。
  const cpuHistory = hostAvailable.value ? sStore.hostCpuHistory : sStore.cpuHistory
  const memoryHistory = hostAvailable.value ? sStore.hostMemoryHistory : sStore.memoryHistory

  cpuChartInstance.value?.setOption(createPercentTrendOption(
    padHistory(cpuHistory),
    '#3b82f6',
    'rgba(59, 130, 246, 0.24)',
    'rgba(59, 130, 246, 0)',
    chartCpuLabel.value
  ), { notMerge: true })

  memoryChartInstance.value?.setOption(createPercentTrendOption(
    padHistory(memoryHistory),
    '#8b5cf6',
    'rgba(139, 92, 246, 0.24)',
    'rgba(139, 92, 246, 0)',
    chartMemoryLabel.value
  ), { notMerge: true })

  const networkChart = getNetworkChartData()
  networkChartInstance.value?.setOption({
    ...createChartBaseOption(networkChart.unit, (value: number) => `${value.toFixed(1)} ${networkChart.unit}`),
    series: [
      {
        name: t('dashboard.receiveLabel'),
        type: 'line',
        data: networkChart.rxData,
        smooth: true,
        showSymbol: false,
        lineStyle: { width: 2, color: '#10b981' },
        areaStyle: lineAreaStyle('rgba(16, 185, 129, 0.24)', 'rgba(16, 185, 129, 0)')
      },
      {
        name: t('dashboard.sendLabel'),
        type: 'line',
        data: networkChart.txData,
        smooth: true,
        showSymbol: false,
        lineStyle: { width: 2, color: '#f97316' },
        areaStyle: lineAreaStyle('rgba(249, 115, 22, 0.20)', 'rgba(249, 115, 22, 0)')
      }
    ]
  })
}

const currentNodeId = () => localStorage.getItem('docker-panel-current-node') || undefined
let systemStatsSubscribed = false

// 首页的容器统计订阅是按需开关的：
// - 宿主机指标可用：不订阅 systemstats，避免每 5 秒逐容器双采样
// - 宿主机指标不可用：回退到容器聚合口径，才订阅 systemstats
const ensureSystemStatsSubscription = async (subscribe: boolean) => {
  if (subscribe === systemStatsSubscribed) return
  try {
    if (subscribe) {
      await signalrService.subscribeToSystemStats(currentNodeId())
    } else {
      await signalrService.unsubscribeFromSystemStats(currentNodeId())
    }
    systemStatsSubscribed = subscribe
  } catch (err) {
    console.error('切换容器统计订阅失败:', err)
  }
}

watch(
  () => [
    hostAvailable.value,
    hostInfo.value?.timestamp,
    hostCpuPercent.value,
    hostMemoryPercent.value,
    dockerStats.value?.timestamp,
    dockerStats.value?.resources?.cpuUsagePercent,
    dockerStats.value?.resources?.memoryPercent,
    sStore.hostRxHistory[sStore.hostRxHistory.length - 1],
    sStore.hostTxHistory[sStore.hostTxHistory.length - 1],
    sStore.rxSpeedHistory[sStore.rxSpeedHistory.length - 1],
    sStore.txSpeedHistory[sStore.txSpeedHistory.length - 1]
  ],
  () => updateCharts()
)

// 宿主机监控状态变化时切换数据源与订阅：
// 有宿主机数据 → 停掉容器 stats 采样；没有 → 回退到容器聚合（明确标注）。
watch(hostAvailable, async (available) => {
  await ensureSystemStatsSubscription(!available)
  await sStore.fetchDockerStats(!available)
  await nextTick()
  updateCharts()
})

const loadData = async () => {
  loading.value = true
  try {
    // 先确认宿主机监控是否可用，再决定 docker-stats 是否需要逐容器 stats 采样
    await Promise.all([
      cStore.fetchContainers({ all: true }),
      iStore.fetchImages(),
      sStore.fetchHostMetrics()
    ])
    await sStore.fetchDockerStats(!hostAvailable.value)
  } finally { 
    loading.value = false 
  }
}

// 数据由服务端 SignalR 推送驱动：
// - host-stats：宿主机指标（可选能力）
// - systemstats：仅在宿主机监控不可用时订阅，用于回退展示容器聚合指标
// - 容器/镜像列表：DockerEventService 监听 docker events，变化时推送
const startRealtimeStats = async () => {
  // Ensure SignalR is connected before subscribing
  if (!signalrService.isConnected()) {
    await signalrService.connect()
  }

  // 先注册监听，再发起订阅：SubscribeToHostStats 会立即回推一次当前状态，
  // 监听注册晚于订阅会丢掉这一帧（虽然下一次轮询会补上）。
  unsubscribeHostSignalR = signalrService.subscribe('host-stats', (message) => {
    sStore.updateHostMetrics(message.data)
  })

  unsubscribeSignalR = signalrService.subscribe('docker-stats', (message) => {
    sStore.updateDockerStats(message.data)
  })

  // 宿主机指标是可选能力；未挂载 /host/proc 时后端会推送 available=false 的说明
  await signalrService.subscribeToHostStats()

  // 没有宿主机数据时，回退到容器聚合口径（需要订阅 systemstats 才会推送 DockerStatsUpdated）
  await ensureSystemStatsSubscription(!hostAvailable.value)

  // 容器/镜像列表更新由后端事件推送
  await cStore.startContainerListSync()
  await iStore.startRealtimeSync()
}

onMounted(async () => {
  await loadData()
  await nextTick()
  initCharts()
  await startRealtimeStats()
})

onUnmounted(() => {
  // 取消宿主机指标与（可能存在的）容器统计订阅
  signalrService.unsubscribeFromHostStats().catch(() => {})
  if (systemStatsSubscribed) {
    signalrService.unsubscribeFromSystemStats(currentNodeId()).catch(() => {})
    systemStatsSubscribed = false
  }

  if (unsubscribeSignalR) {
    unsubscribeSignalR()
    unsubscribeSignalR = null
  }
  if (unsubscribeHostSignalR) {
    unsubscribeHostSignalR()
    unsubscribeHostSignalR = null
  }
  cStore.stopContainerListSync().catch(() => {})
  iStore.stopRealtimeSync().catch(() => {})
  window.removeEventListener('resize', handleResize)
  disposeCharts()
})
</script>

<style scoped>

.dashboard-page {
  padding: var(--space-xl);
  max-width: 1440px;
  margin: 0 auto;
  animation: fadeInUp 0.4s ease-out;
}

/* === Page Header === */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--space-xl);
}

.page-subtitle {
  margin: 8px 0 0 0;
  color: var(--text-secondary);
  font-size: 15px;
}

.refresh-btn {
  background: var(--bg-glass);
  border: 1px solid var(--border-color);
}

/* === Metrics Grid === */
.metrics-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 20px;
  margin-bottom: var(--space-xl);
}

.metric-card {
  background: var(--bg-glass);
  backdrop-filter: var(--glass-blur);
  border-radius: var(--border-radius-lg);
  border: var(--glass-border);
  padding: 24px;
  position: relative;
  overflow: hidden;
  transition: all 0.35s cubic-bezier(0.4, 0, 0.2, 1);
  animation: fadeInUp 0.5s ease-out backwards;
  animation-delay: calc(var(--index) * 0.1s);
}

.metric-card:hover {
  transform: translateY(-4px);
  box-shadow: var(--shadow-raised), var(--shadow-glow);
}

.metric-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 20px;
}

.metric-icon {
  width: 52px;
  height: 52px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
}

.metric-body {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.metric-value {
  font-family: 'Space Grotesk', sans-serif;
  font-size: 36px;
  font-weight: 700;
  color: var(--text-main);
  line-height: 1;
  letter-spacing: -0.02em;
}

.metric-unit {
  font-size: 20px;
  font-weight: 500;
  color: var(--text-secondary);
}

.metric-label {
  font-size: 13px;
  color: var(--text-secondary);
  font-weight: 500;
  display: flex;
  flex-direction: column;
}

.metric-subtext {
  font-size: 11px;
  color: var(--text-muted);
  margin-top: 2px;
  font-weight: 400;
}

.metric-sparkline {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 3px;
  opacity: 0.6;
}

/* === Dashboard Grid === */
.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 24px;
}

.header-title {
  display: flex;
  align-items: center;
  gap: 10px;
  font-family: 'Space Grotesk', sans-serif;
  font-weight: 600;
  font-size: 15px;
  color: var(--text-main);
}

.header-icon {
  color: var(--color-primary);
  font-size: 20px;
}

/* === Docker Info Card === */
.docker-info-card {
  grid-column: 1 / -1;
}

.docker-status {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 14px;
  border-radius: var(--border-radius-full);
  font-size: 12px;
  font-weight: 600;
}

.docker-status.running {
  background: var(--color-success-bg);
  color: var(--color-success);
}

.docker-status.stopped {
  background: var(--color-danger-bg);
  color: var(--color-danger);
}

.docker-status .status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: currentColor;
  animation: pulse 2s ease-in-out infinite;
}

.docker-info-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 12px;
  background: var(--bg-glass-dark);
  border-radius: 10px;
}

.info-label {
  font-size: 12px;
  color: var(--text-muted);
}

.info-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main);
}

.running-count {
  color: var(--color-success);
}

/* === Charts Card === */
.charts-card {
  grid-column: 1 / -1;
}

.live-indicator {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  border-radius: var(--border-radius-full);
  background: var(--color-success-bg);
  color: var(--color-success);
  font-size: 12px;
  font-weight: 600;
}

.live-indicator .status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
  animation: pulse 2s ease-in-out infinite;
}

.chart-body {
  padding: 18px 20px 20px;
}

.resource-chart-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}

.resource-chart-panel {
  min-width: 0;
  padding: 16px;
  border: 1px solid var(--border-color-light);
  border-radius: var(--border-radius-lg);
  background: var(--bg-glass-dark);
}

.gauge-panel {
  background:
    radial-gradient(circle at 50% 78%, rgba(59, 130, 246, 0.08), transparent 38%),
    var(--bg-glass-dark);
}

.gauge-panel:nth-child(2) {
  background:
    radial-gradient(circle at 50% 78%, rgba(139, 92, 246, 0.08), transparent 38%),
    var(--bg-glass-dark);
}

.resource-chart-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  margin-bottom: 10px;
}

.resource-label {
  display: block;
  margin-bottom: 4px;
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 600;
}

.resource-value {
  font-family: 'Space Grotesk', sans-serif;
  font-size: 24px;
  font-weight: 700;
  line-height: 1;
  color: var(--text-main);
}

.resource-value span {
  margin-left: 2px;
  color: var(--text-secondary);
  font-size: 14px;
  font-weight: 600;
}

.resource-value-text {
  font-size: 22px;
}

.resource-subtitle {
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 500;
  white-space: nowrap;
}

.network-live-values {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 12px;
  color: var(--text-main);
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 10px;
}

.network-live-values span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.legend-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}

.legend-dot.rx {
  background: #10b981;
}

.legend-dot.tx {
  background: #f97316;
}

.mini-chart-container {
  width: 100%;
  height: 220px;
}

.gauge-chart-container {
  height: 190px;
  margin-top: 4px;
}

/* === Host Resources Card === */
.host-card {
  grid-column: 1 / -1;
}

.host-scope {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: var(--border-radius-full);
  background: var(--bg-glass-dark);
  border: 1px solid var(--border-color-light);
  color: var(--text-muted);
  font-size: 11px;
  font-weight: 500;
}

/* 趋势图的时间窗口标注：说明折线覆盖的是最近 5 分钟，而不是长期趋势 */
.chart-window {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: var(--border-radius-full);
  background: rgba(59, 130, 246, 0.1);
  border: 1px solid rgba(59, 130, 246, 0.25);
  color: #3b82f6;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}

.host-status {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  border-radius: var(--border-radius-full);
  font-size: 12px;
  font-weight: 600;
}

.host-status.enabled {
  background: var(--color-success-bg);
  color: var(--color-success);
}

.host-status.disabled {
  background: var(--bg-glass-dark);
  color: var(--text-muted);
  border: 1px solid var(--border-color-light);
}

.host-status .status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
}

.host-status.enabled .status-dot {
  animation: pulse 2s ease-in-out infinite;
}

.host-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 18px;
  margin-bottom: 16px;
  color: var(--text-muted);
  font-size: 12px;
}

.host-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 14px;
}

.host-metric {
  min-width: 0;
  padding: 14px;
  border: 1px solid var(--border-color-light);
  border-radius: var(--border-radius-lg);
  background: var(--bg-glass-dark);
}

.host-metric-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 10px;
  margin-bottom: 10px;
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 600;
}

.host-metric-head strong {
  color: var(--text-main);
  font-size: 13px;
  font-weight: 700;
  text-align: right;
  word-break: break-word;
}

.host-metric-sub {
  display: block;
  margin-top: 8px;
  color: var(--text-muted);
  font-size: 11px;
  line-height: 1.5;
}

.host-disabled {
  display: flex;
  gap: 16px;
  padding: 18px;
  border: 1px dashed var(--border-color);
  border-radius: var(--border-radius-lg);
  background: var(--bg-glass-dark);
}

.host-disabled-icon {
  color: var(--color-warning, #f59e0b);
  font-size: 26px;
  flex-shrink: 0;
  margin-top: 2px;
}

.host-disabled-body {
  min-width: 0;
  flex: 1;
}

.host-disabled-title {
  margin: 0 0 6px;
  color: var(--text-main);
  font-size: 14px;
  font-weight: 600;
}

.host-disabled-desc,
.host-disabled-note {
  margin: 0 0 10px;
  color: var(--text-secondary);
  font-size: 12px;
  line-height: 1.6;
}

.host-mount-example {
  margin: 0 0 10px;
  padding: 12px 14px;
  border-radius: 10px;
  background: var(--bg-surface);
  border: 1px solid var(--border-color-light);
  color: var(--text-main);
  font-size: 12px;
  line-height: 1.6;
  overflow-x: auto;
}

.chart-scope-hint {
  margin: 0 0 14px;
  color: var(--text-muted);
  font-size: 12px;
}

/* === Quick Actions === */
.action-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.action-tile {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 16px;
  border-radius: 12px;
  background: var(--bg-glass-dark);
  border: 1px solid var(--border-color-light);
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  text-align: left;
  font-family: 'DM Sans', sans-serif;
}

.action-tile:hover {
  background: var(--bg-surface);
  border-color: var(--color-primary);
  transform: translateX(4px);
}

.action-icon {
  width: 42px;
  height: 42px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  flex-shrink: 0;
}

.action-label {
  flex: 1;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main);
}

.action-arrow {
  color: var(--text-muted);
  font-size: 14px;
  transition: transform 0.2s ease;
}

.action-tile:hover .action-arrow {
  transform: translateX(4px);
  color: var(--color-primary);
}

/* === Container List === */
.container-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.container-item {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 14px;
  background: var(--bg-glass-dark);
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.container-item:hover {
  background: var(--bg-surface);
}

.container-status {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex-shrink: 0;
}

.container-status .status-dot {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  display: block;
}

.container-status.running .status-dot {
  background: var(--color-success);
  box-shadow: 0 0 8px var(--color-success);
}

.container-status.exited .status-dot,
.container-status.stopped .status-dot {
  background: var(--text-muted);
}

.container-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.container-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.container-image {
  font-size: 12px;
  color: var(--text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.container-time {
  font-size: 12px;
  color: var(--text-muted);
  white-space: nowrap;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 32px;
  color: var(--text-muted);
}

.empty-state .el-icon {
  font-size: 32px;
  opacity: 0.5;
}

/* === Gradient Colors === */
.gradient-blue {
  background: linear-gradient(135deg, #3b82f6, #1d4ed8);
  color: white;
}

.gradient-purple {
  background: linear-gradient(135deg, #8b5cf6, #6d28d9);
  color: white;
}

.gradient-orange {
  background: linear-gradient(135deg, #f97316, #ea580c);
  color: white;
}

.gradient-pink {
  background: linear-gradient(135deg, #ec4899, #db2777);
  color: white;
}

.gradient-cyan {
  background: linear-gradient(135deg, #06b6d4, #0891b2);
  color: white;
}

.gradient-green {
  background: linear-gradient(135deg, #10b981, #059669);
  color: white;
}

/* === Animations === */
@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

/* === Responsive === */
@media (max-width: 1200px) {
  .metrics-grid {
    grid-template-columns: repeat(2, 1fr);
  }
  .resource-chart-grid {
    grid-template-columns: 1fr;
  }
  .dashboard-grid {
    grid-template-columns: 1fr;
  }
  .docker-info-grid {
    grid-template-columns: repeat(2, 1fr);
  }
  .host-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .dashboard-page {
    padding: var(--space-md);
  }
  .metrics-grid {
    grid-template-columns: 1fr;
    gap: 12px;
  }
  .page-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 16px;
  }
  .page-title {
    font-size: 26px;
  }
  .metric-card {
    padding: 20px;
  }
  .metric-value {
    font-size: 28px;
  }
  .action-grid {
    grid-template-columns: 1fr;
  }
  .docker-info-grid {
    grid-template-columns: 1fr;
  }
  .resource-chart-header {
    flex-direction: column;
    align-items: flex-start;
  }
  .mini-chart-container {
    height: 190px;
  }
  .host-grid {
    grid-template-columns: 1fr;
  }
  .host-disabled {
    flex-direction: column;
  }
}

</style>
