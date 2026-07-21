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

      <!-- Resource Charts Card -->
      <div class="saas-card charts-card">
        <div class="saas-card-header">
          <div class="header-title">
            <el-icon class="header-icon"><TrendCharts /></el-icon>
            <span>{{ t('dashboard.resourceMonitor') }}</span>
          </div>
          <div class="live-indicator">
            <span class="status-dot"></span>
            <span>实时数据</span>
          </div>
        </div>
        <div class="saas-card-body chart-body">
          <div class="resource-chart-grid">
            <div class="resource-chart-panel gauge-panel">
              <div class="resource-chart-header">
                <div>
                  <span class="resource-label">{{ t('dashboard.cpuLabel') }}</span>
                  <div class="resource-value">{{ cpuUsage }}<span>%</span></div>
                </div>
                <span class="resource-subtitle">{{ dockerStats?.docker?.ncpu ? t('dashboard.cpuCores', { count: dockerStats.docker.ncpu }) : t('dashboard.calculating') }}</span>
              </div>
              <div class="mini-chart-container gauge-chart-container" ref="cpuChartContainer"></div>
            </div>

            <div class="resource-chart-panel gauge-panel">
              <div class="resource-chart-header">
                <div>
                  <span class="resource-label">{{ t('dashboard.memoryLabel') }}</span>
                  <div class="resource-value resource-value-text">{{ memUsage }}</div>
                </div>
                <span class="resource-subtitle">{{ t('dashboard.totalMemory', { value: memLimit }) }} · {{ memPercent }}%</span>
              </div>
              <div class="mini-chart-container gauge-chart-container" ref="memoryChartContainer"></div>
            </div>

            <div class="resource-chart-panel">
              <div class="resource-chart-header network-resource-header">
                <div>
                  <span class="resource-label">{{ t('dashboard.networkTraffic') }}</span>
                  <span class="resource-subtitle">{{ t('dashboard.receiveSpeed') }} / {{ t('dashboard.sendSpeed') }}</span>
                </div>
              </div>
              <div class="network-live-values">
                <span><i class="legend-dot rx"></i>{{ t('dashboard.receiveLabel') }} {{ rxSpeed }}</span>
                <span><i class="legend-dot tx"></i>{{ t('dashboard.sendLabel') }} {{ txSpeed }}</span>
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
  TrendCharts, Connection, Folder, ArrowRight, Platform
} from '@element-plus/icons-vue'
import { useContainersStore } from '@/stores/containers'
import { useImagesStore } from '@/stores/images'
import { useSystemStore } from '@/stores/system'
import { useSettingsStore } from '@/stores/settings'
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
const settingsStore = useSettingsStore()

const loading = ref(false)
const cpuChartContainer = ref<HTMLElement | null>(null)
const memoryChartContainer = ref<HTMLElement | null>(null)
const networkChartContainer = ref<HTMLElement | null>(null)
const cpuChartInstance = shallowRef<ECharts | null>(null)
const memoryChartInstance = shallowRef<ECharts | null>(null)
const networkChartInstance = shallowRef<ECharts | null>(null)
let unsubscribeSignalR: (() => void) | null = null
let refreshTimer: ReturnType<typeof setInterval> | null = null

// Use Docker stats from store
const dockerStats = computed(() => sStore.dockerStats)

const runningCount = computed(() => dockerStats.value?.containers?.running || 0)
const totalImages = computed(() => dockerStats.value?.images?.count || iStore.images.length)

const cpuUsage = computed(() => {
  return (dockerStats.value?.resources?.cpuUsagePercent || 0).toFixed(2)
})

const memUsage = computed(() => {
  // 显示内存占用量而非百分比
  return dockerStats.value?.resources?.memoryUsedFormatted || '0 B'
})

const memLimit = computed(() => {
  return dockerStats.value?.resources?.memoryLimitFormatted || '0 B'
})

const memPercent = computed(() => {
  return (dockerStats.value?.resources?.memoryPercent || 0).toFixed(2)
})

const rxSpeed = computed(() => {
  return dockerStats.value?.network?.rxSpeedFormatted || '0 B/s'
})

const txSpeed = computed(() => {
  return dockerStats.value?.network?.txSpeedFormatted || '0 B/s'
})

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
    label: t('dashboard.cpuUsage'),
    value: cpuUsage.value,
    unit: '%',
    subText: dockerStats.value?.docker?.ncpu ? t('dashboard.cpuCores', { count: dockerStats.value.docker.ncpu }) : (dockerStats.value?.resources?.cpuUsagePercent ? t('dashboard.basedOnContainers') : t('dashboard.calculating')),
    icon: Cpu,
    color: 'gradient-orange',
    glowColor: 'glow-orange'
  },
  {
    label: t('dashboard.memoryUsage'),
    value: memUsage.value,
    subText: t('dashboard.totalMemory', { value: memLimit.value }),
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
    const time = new Date(now.getTime() - i * 3000)
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
  const maxNetBytes = Math.max(...sStore.rxSpeedHistory, ...sStore.txSpeedHistory, 1)
  const divisor = maxNetBytes > 1024 * 1024 ? 1024 * 1024 : 1024
  const unit = divisor === 1024 * 1024 ? 'MB/s' : 'KB/s'

  return {
    unit,
    rxData: padHistory(sStore.rxSpeedHistory.map((value: number) => value / divisor)),
    txData: padHistory(sStore.txSpeedHistory.map((value: number) => value / divisor))
  }
}

const createGaugeOption = (value: number, color: string, trackColor: string, title: string, detailText: string) => ({
  tooltip: { show: false },
  series: [
    {
      name: title,
      type: 'gauge',
      startAngle: 180,
      endAngle: 0,
      min: 0,
      max: 100,
      radius: '122%',
      center: ['50%', '72%'],
      splitNumber: 4,
      pointer: { show: false },
      progress: {
        show: true,
        roundCap: true,
        width: 16,
        itemStyle: { color }
      },
      axisLine: {
        roundCap: true,
        lineStyle: {
          width: 16,
          color: [[1, trackColor]]
        }
      },
      axisTick: { show: false },
      splitLine: {
        distance: -20,
        length: 7,
        lineStyle: { width: 1, color: getCssVar('--border-color', '#e2e8f0') }
      },
      axisLabel: {
        distance: -4,
        color: getCssVar('--text-muted', '#94a3b8'),
        fontSize: 10,
        formatter: (axisValue: number) => axisValue === 0 || axisValue === 50 || axisValue === 100 ? `${axisValue}` : ''
      },
      anchor: { show: false },
      title: {
        show: true,
        offsetCenter: [0, '10%'],
        color: getCssVar('--text-muted', '#94a3b8'),
        fontSize: 12,
        fontWeight: 500
      },
      detail: {
        valueAnimation: true,
        offsetCenter: [0, '-18%'],
        formatter: detailText,
        color: getCssVar('--text-main', '#0f172a'),
        fontSize: 24,
        fontWeight: 700
      },
      data: [{ value: Number(Math.min(Math.max(value, 0), 100).toFixed(2)), name: title }]
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
  cpuChartInstance.value?.setOption(createGaugeOption(
    Number(cpuUsage.value),
    '#3b82f6',
    'rgba(59, 130, 246, 0.12)',
    t('dashboard.cpuUsage'),
    '{value}%'
  ))

  memoryChartInstance.value?.setOption(createGaugeOption(
    Number(memPercent.value),
    '#8b5cf6',
    'rgba(139, 92, 246, 0.12)',
    t('dashboard.memoryUsage'),
    '{value}%'
  ))

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

watch(
  () => [
    dockerStats.value?.timestamp,
    dockerStats.value?.resources?.cpuUsagePercent,
    dockerStats.value?.resources?.memoryPercent,
    sStore.rxSpeedHistory[sStore.rxSpeedHistory.length - 1],
    sStore.txSpeedHistory[sStore.txSpeedHistory.length - 1]
  ],
  () => updateCharts()
)

const loadData = async () => {
  loading.value = true
  try {
    await Promise.all([
      cStore.fetchContainers({ all: true }), 
      iStore.fetchImages(),
      sStore.fetchDockerStats()
    ])
  } finally { 
    loading.value = false 
  }
}

const loadDataSilently = async () => {
  try {
    await Promise.all([
      cStore.fetchContainers({ all: true }), 
      iStore.fetchImages(),
      sStore.fetchDockerStats()
    ])
  } catch (error) {
    console.warn('Dashboard auto refresh failed:', error)
  }
}

const stopAutoRefreshTimer = () => {
  if (refreshTimer) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
}

const startAutoRefreshTimer = () => {
  stopAutoRefreshTimer()
  const interval = Math.max(settingsStore.refreshInterval || 3000, 3000)
  refreshTimer = setInterval(() => {
    void loadDataSilently()
  }, interval)
}

watch(() => settingsStore.refreshInterval, () => {
  startAutoRefreshTimer()
})

const startAutoRefresh = async () => {
  // Ensure SignalR is connected before subscribing
  if (!signalrService.isConnected()) {
    await signalrService.connect()
  }

  // Call backend Hub method to register subscription (required for HasConnections check)
  await signalrService.subscribeToSystemStats()

  // Subscribe to SignalR events for real-time stats
  unsubscribeSignalR = signalrService.subscribe('docker-stats', (message) => {
    sStore.updateDockerStats(message.data)
  })
}

onMounted(async () => {
  await loadData()
  await nextTick()
  initCharts()
  await startAutoRefresh()
  startAutoRefreshTimer()
})

onUnmounted(() => {
  stopAutoRefreshTimer()
  // 取消系统统计订阅
  signalrService.unsubscribeFromSystemStats().catch(() => {})
  
  if (unsubscribeSignalR) {
    unsubscribeSignalR()
    unsubscribeSignalR = null
  }
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
  color: var(--text-tertiary, #909399);
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
}

</style>
