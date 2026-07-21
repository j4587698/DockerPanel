<template>
  <div class="stats-view-container">
    <div class="stats-content">
      <!-- 实时指标卡片 -->
      <div class="stats-cards">
        <div class="stat-metric-card">
          <div class="metric-header">
            <el-icon class="metric-icon cpu"><Cpu /></el-icon>
            <span class="metric-title">{{ t('metrics.cpuUsage') }}</span>
          </div>
          <div class="metric-value">
            <span class="value-number">{{ currentStats.cpu.toFixed(1) }}</span>
            <span class="value-unit">%</span>
          </div>
          <div class="metric-progress">
            <div class="progress-bar" :style="{ width: Math.min(currentStats.cpu, 100) + '%' }" :class="getProgressClass(currentStats.cpu)"></div>
          </div>
        </div>

        <div class="stat-metric-card">
          <div class="metric-header">
            <el-icon class="metric-icon memory"><Monitor /></el-icon>
            <span class="metric-title">{{ t('metrics.memoryUsage') }}</span>
          </div>
          <div class="metric-value">
            <span class="value-number">{{ formatBytes(currentStats.memory) }}</span>
          </div>
          <div class="metric-progress">
            <div class="progress-bar" :style="{ width: Math.min(memoryPercent, 100) + '%' }" :class="getProgressClass(memoryPercent)"></div>
          </div>
          <div class="metric-detail" v-if="container?.memory">
            / {{ formatBytes(container.memory) }} {{ t('metrics.limit') }}
          </div>
        </div>

        <div class="stat-metric-card">
          <div class="metric-header">
            <el-icon class="metric-icon network"><Connection /></el-icon>
            <span class="metric-title">{{ t('metrics.networkIO') }}</span>
          </div>
          <div class="metric-value">
            <span class="value-number">{{ formatBytes(currentStats.networkRxRate) }}/s</span>
            <span class="value-unit">↓</span>
            <span class="value-number">{{ formatBytes(currentStats.networkTxRate) }}/s</span>
            <span class="value-unit">↑</span>
          </div>
          <div class="metric-detail">
            {{ t('metrics.total') }}: {{ formatBytes(currentStats.networkRxTotal) }} ↓ / {{ formatBytes(currentStats.networkTxTotal) }} ↑
          </div>
        </div>

        <div class="stat-metric-card">
          <div class="metric-header">
            <el-icon class="metric-icon disk"><Folder /></el-icon>
            <span class="metric-title">{{ t('metrics.diskIO') }}</span>
          </div>
          <div class="metric-value">
            <span class="value-number">{{ formatBytes(currentStats.blockReadRate) }}/s</span>
            <span class="value-unit">↓</span>
            <span class="value-number">{{ formatBytes(currentStats.blockWriteRate) }}/s</span>
            <span class="value-unit">↑</span>
          </div>
        </div>
      </div>

      <!-- 图表区域 -->
      <div class="charts-grid">
        <div class="chart-card">
          <div class="chart-header">
            <h4 class="chart-title">{{ t('metrics.cpuTrend') }}</h4>
            <span class="chart-subtitle">{{ t('metrics.last60s') }}</span>
          </div>
          <div ref="cpuChartRef" class="chart-container"></div>
        </div>

        <div class="chart-card">
          <div class="chart-header">
            <h4 class="chart-title">{{ t('metrics.memoryTrend') }}</h4>
            <span class="chart-subtitle">{{ t('metrics.last60s') }}</span>
          </div>
          <div ref="memoryChartRef" class="chart-container"></div>
        </div>

        <div class="chart-card full-width">
          <div class="chart-header">
            <h4 class="chart-title">{{ t('metrics.networkThroughput') }}</h4>
            <span class="chart-subtitle">{{ t('metrics.rxTx') }}</span>
          </div>
          <div ref="networkChartRef" class="chart-container"></div>
        </div>

        <div class="chart-card full-width">
          <div class="chart-header">
            <h4 class="chart-title">{{ t('metrics.diskThroughput') }}</h4>
            <span class="chart-subtitle">{{ t('metrics.readWrite') }}</span>
          </div>
          <div ref="diskChartRef" class="chart-container"></div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, computed, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import { Cpu, Monitor, Connection, Folder } from '@element-plus/icons-vue'
import { echarts, type ECharts } from '@/plugins/echarts'

interface ContainerStats {
  cpu: number
  memory: number
  networkRxRate: number
  networkTxRate: number
  networkRxTotal: number
  networkTxTotal: number
  blockReadRate: number
  blockWriteRate: number
}

interface Container {
  id: string
  name: string
  state: string
  memory?: number
}

const props = defineProps<{
  container: Container | null
  stats: { cpu: number; memory: number; networkIO: number }
}>()

const { t } = useI18n()

// 当前统计数据
const currentStats = ref<ContainerStats>({
  cpu: 0,
  memory: 0,
  networkRxRate: 0,
  networkTxRate: 0,
  networkRxTotal: 0,
  networkTxTotal: 0,
  blockReadRate: 0,
  blockWriteRate: 0
})

// 上一次的网络累计值
let prevNetworkRx = 0
let prevNetworkTx = 0
let prevNetworkTime = Date.now()

// 历史数据 (60秒)
const maxDataPoints = 60
const historyData = ref({
  cpu: [] as number[],
  memory: [] as number[],
  networkRxRate: [] as number[],
  networkTxRate: [] as number[],
  blockReadRate: [] as number[],
  blockWriteRate: [] as number[],
  timestamps: [] as string[]
})

// 图表引用
const cpuChartRef = ref<HTMLElement>()
const memoryChartRef = ref<HTMLElement>()
const networkChartRef = ref<HTMLElement>()
const diskChartRef = ref<HTMLElement>()

let cpuChart: ECharts | null = null
let memoryChart: ECharts | null = null
let networkChart: ECharts | null = null
let diskChart: ECharts | null = null

// 内存使用百分比
const memoryPercent = computed(() => {
  if (!props.container?.memory || props.container.memory === 0) return 0
  return (currentStats.value.memory / props.container.memory) * 100
})

// 格式化字节 - 修复边界情况
const formatBytes = (bytes: number): string => {
  if (bytes === null || bytes === undefined || isNaN(bytes)) return '0 B'
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const absBytes = Math.abs(bytes)
  // 当值小于1时，直接返回 B 单位
  if (absBytes < 1) return absBytes.toFixed(1) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}

// 进度条颜色
const getProgressClass = (value: number): string => {
  if (value >= 80) return 'danger'
  if (value >= 60) return 'warning'
  return 'normal'
}

// X轴标签格式化
const getXAxisLabels = (timestamps: string[]): string[] => {
  return timestamps.map((ts, i) => {
    if (i % 10 === 0 || i === timestamps.length - 1) {
      return ts
    }
    return ''
  })
}

// 图表通用配置
const getChartBaseOption = () => ({
  backgroundColor: 'transparent',
  textStyle: { color: '#94a3b8', fontSize: 11 },
  grid: { left: 65, right: 20, top: 20, bottom: 40 },
  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(15, 23, 42, 0.9)',
    borderColor: '#334155',
    textStyle: { color: '#e2e8f0' }
  }
})

// 格式化函数 - 用于 ECharts (避免闭包问题)
const formatBytesForChart = (value: number): string => {
  if (value === null || value === undefined || isNaN(value)) return '0 B'
  if (value === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const absValue = Math.abs(value)
  // 当值小于1时，直接返回 B 单位
  if (absValue < 1) return absValue.toFixed(1) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absValue) / Math.log(k))), sizes.length - 1)
  return parseFloat((value / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}

// 初始化图表
const initCharts = () => {
  const baseOption = getChartBaseOption()

  // CPU 图表
  if (cpuChartRef.value) {
    cpuChart = echarts.init(cpuChartRef.value)
    cpuChart.setOption({
      ...baseOption,
      tooltip: {
        ...baseOption.tooltip,
        formatter: (params: any) => {
          if (!params || params.length === 0) return ''
          const time = params[0].axisValue
          const value = params[0].value
          return `<div style="margin-bottom: 4px; font-weight: bold;">${time}</div>
                  <div>${params[0].marker} CPU: ${value.toFixed(2)} %</div>`
        }
      },
      xAxis: {
        type: 'category',
        data: [],
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { show: true, color: '#64748b', fontSize: 10, interval: 9 },
        axisTick: { show: false }
      },
      yAxis: {
        type: 'value',
        min: 0,
        max: 100,
        axisLine: { show: false },
        splitLine: { lineStyle: { color: '#1e293b', type: 'dashed' } },
        axisLabel: { formatter: '{value}%', color: '#64748b', fontSize: 10 }
      },
      series: [{
        name: 'CPU',
        type: 'line',
        data: [],
        smooth: true,
        symbol: 'none',
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(59, 130, 246, 0.3)' },
            { offset: 1, color: 'rgba(59, 130, 246, 0.05)' }
          ])
        },
        lineStyle: { color: '#3b82f6', width: 2 }
      }]
    })
  }

  // 内存图表
  if (memoryChartRef.value) {
    memoryChart = echarts.init(memoryChartRef.value)
    memoryChart.setOption({
      ...baseOption,
      tooltip: {
        ...baseOption.tooltip,
        formatter: (params: any) => {
          if (!params || params.length === 0) return ''
          const time = params[0].axisValue
          const value = params[0].value
          return `<div style="margin-bottom: 4px; font-weight: bold;">${time}</div>
                  <div>${params[0].marker} ${t('metrics.memoryUsage')}: ${formatBytesForChart(value)}</div>`
        }
      },
      xAxis: {
        type: 'category',
        data: [],
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { show: true, color: '#64748b', fontSize: 10, interval: 9 },
        axisTick: { show: false }
      },
      yAxis: {
        type: 'value',
        min: 0,
        axisLine: { show: false },
        splitLine: { lineStyle: { color: '#1e293b', type: 'dashed' } },
        axisLabel: {
          formatter: function(value: number) { return formatBytesForChart(value) },
          color: '#64748b',
          fontSize: 10
        }
      },
      series: [{
        name: t('metrics.memoryUsage'),
        type: 'line',
        data: [],
        smooth: true,
        symbol: 'none',
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(16, 185, 129, 0.3)' },
            { offset: 1, color: 'rgba(16, 185, 129, 0.05)' }
          ])
        },
        lineStyle: { color: '#10b981', width: 2 }
      }]
    })
  }

  // 网络图表
  if (networkChartRef.value) {
    networkChart = echarts.init(networkChartRef.value)
    networkChart.setOption({
      ...baseOption,
      tooltip: {
        ...baseOption.tooltip,
        formatter: (params: any) => {
          if (!params || params.length === 0) return ''
          const time = params[0].axisValue
          let content = `<div style="margin-bottom: 4px; font-weight: bold;">${time}</div>`
          params.forEach((item: any) => {
            content += `<div>${item.marker} ${item.seriesName}: ${formatBytesForChart(item.value)}/s</div>`
          })
          return content
        }
      },
      legend: {
        data: [t('metrics.receive'), t('metrics.send')],
        top: 0,
        right: 0,
        textStyle: { color: '#94a3b8', fontSize: 11 }
      },
      xAxis: {
        type: 'category',
        data: [],
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { show: true, color: '#64748b', fontSize: 10, interval: 9 },
        axisTick: { show: false }
      },
      yAxis: {
        type: 'value',
        min: 0,
        axisLine: { show: false },
        splitLine: { lineStyle: { color: '#1e293b', type: 'dashed' } },
        axisLabel: {
          formatter: function(value: number) { return formatBytesForChart(value) + '/s' },
          color: '#64748b',
          fontSize: 10
        }
      },
      series: [
        {
          name: t('metrics.receive'),
          type: 'line',
          data: [],
          smooth: true,
          symbol: 'none',
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(139, 92, 246, 0.2)' },
              { offset: 1, color: 'rgba(139, 92, 246, 0.02)' }
            ])
          },
          lineStyle: { color: '#8b5cf6', width: 2 }
        },
        {
          name: t('metrics.send'),
          type: 'line',
          data: [],
          smooth: true,
          symbol: 'none',
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(245, 158, 11, 0.2)' },
              { offset: 1, color: 'rgba(245, 158, 11, 0.02)' }
            ])
          },
          lineStyle: { color: '#f59e0b', width: 2 }
        }
      ]
    })
  }

  // 磁盘图表
  if (diskChartRef.value) {
    diskChart = echarts.init(diskChartRef.value)
    diskChart.setOption({
      ...baseOption,
      tooltip: {
        ...baseOption.tooltip,
        formatter: (params: any) => {
          if (!params || params.length === 0) return ''
          const time = params[0].axisValue
          let content = `<div style="margin-bottom: 4px; font-weight: bold;">${time}</div>`
          params.forEach((item: any) => {
            content += `<div>${item.marker} ${item.seriesName}: ${formatBytesForChart(item.value)}/s</div>`
          })
          return content
        }
      },
      legend: {
        data: [t('metrics.read'), t('metrics.write')],
        top: 0,
        right: 0,
        textStyle: { color: '#94a3b8', fontSize: 11 }
      },
      xAxis: {
        type: 'category',
        data: [],
        axisLine: { lineStyle: { color: '#334155' } },
        axisLabel: { show: true, color: '#64748b', fontSize: 10, interval: 9 },
        axisTick: { show: false }
      },
      yAxis: {
        type: 'value',
        min: 0,
        axisLine: { show: false },
        splitLine: { lineStyle: { color: '#1e293b', type: 'dashed' } },
        axisLabel: {
          formatter: function(value: number) { return formatBytesForChart(value) + '/s' },
          color: '#64748b',
          fontSize: 10
        }
      },
      series: [
        {
          name: t('metrics.read'),
          type: 'line',
          data: [],
          smooth: true,
          symbol: 'none',
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(6, 182, 212, 0.2)' },
              { offset: 1, color: 'rgba(6, 182, 212, 0.02)' }
            ])
          },
          lineStyle: { color: '#06b6d4', width: 2 }
        },
        {
          name: t('metrics.write'),
          type: 'line',
          data: [],
          smooth: true,
          symbol: 'none',
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(236, 72, 153, 0.2)' },
              { offset: 1, color: 'rgba(236, 72, 153, 0.02)' }
            ])
          },
          lineStyle: { color: '#ec4899', width: 2 }
        }
      ]
    })
  }
}

// 更新图表数据
const updateCharts = () => {
  const timestamps = getXAxisLabels(historyData.value.timestamps)

  cpuChart?.setOption({
    xAxis: { data: timestamps },
    series: [{ data: [...historyData.value.cpu] }]
  })

  memoryChart?.setOption({
    xAxis: { data: timestamps },
    series: [{ data: [...historyData.value.memory] }]
  })

  networkChart?.setOption({
    xAxis: { data: timestamps },
    series: [
      { data: [...historyData.value.networkRxRate] },
      { data: [...historyData.value.networkTxRate] }
    ]
  })

  diskChart?.setOption({
    xAxis: { data: timestamps },
    series: [
      { data: [...historyData.value.blockReadRate] },
      { data: [...historyData.value.blockWriteRate] }
    ]
  })
}

// 添加数据点
const addDataPoint = () => {
  const now = new Date()
  const timestamp = `${now.getMinutes().toString().padStart(2, '0')}:${now.getSeconds().toString().padStart(2, '0')}`

  const cpuValue = props.stats.cpu ?? 0
  const memoryValue = props.stats.memory ?? 0
  const networkTotal = props.stats.networkIO ?? 0

  // 计算网络速率
  const currentTime = Date.now()
  const timeDiff = (currentTime - prevNetworkTime) / 1000
  
  const currentRx = networkTotal / 2
  const currentTx = networkTotal / 2
  
  let rxRate = 0
  let txRate = 0
  
  if (timeDiff > 0 && prevNetworkTime > 0) {
    rxRate = Math.max(0, (currentRx - prevNetworkRx) / timeDiff)
    txRate = Math.max(0, (currentTx - prevNetworkTx) / timeDiff)
  }
  
  prevNetworkRx = currentRx
  prevNetworkTx = currentTx
  prevNetworkTime = currentTime

  // 更新当前统计
  currentStats.value.cpu = cpuValue
  currentStats.value.memory = memoryValue
  currentStats.value.networkRxTotal = currentRx
  currentStats.value.networkTxTotal = currentTx
  currentStats.value.networkRxRate = rxRate
  currentStats.value.networkTxRate = txRate
  currentStats.value.blockReadRate = 0
  currentStats.value.blockWriteRate = 0

  // 添加到历史数据
  historyData.value.timestamps.push(timestamp)
  historyData.value.cpu.push(cpuValue)
  historyData.value.memory.push(memoryValue)
  historyData.value.networkRxRate.push(rxRate)
  historyData.value.networkTxRate.push(txRate)
  historyData.value.blockReadRate.push(0)
  historyData.value.blockWriteRate.push(0)

  // 保持最大数据点数量
  if (historyData.value.timestamps.length > maxDataPoints) {
    historyData.value.timestamps.shift()
    historyData.value.cpu.shift()
    historyData.value.memory.shift()
    historyData.value.networkRxRate.shift()
    historyData.value.networkTxRate.shift()
    historyData.value.blockReadRate.shift()
    historyData.value.blockWriteRate.shift()
  }

  updateCharts()
}

// 处理窗口大小变化
const handleResize = () => {
  cpuChart?.resize()
  memoryChart?.resize()
  networkChart?.resize()
  diskChart?.resize()
}

let updateInterval: number | null = null

onMounted(async () => {
  await nextTick()
  initCharts()
  addDataPoint()
  updateInterval = window.setInterval(addDataPoint, 1000)
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  if (updateInterval) clearInterval(updateInterval)
  window.removeEventListener('resize', handleResize)
  cpuChart?.dispose()
  memoryChart?.dispose()
  networkChart?.dispose()
  diskChart?.dispose()
})

// 监听 stats 变化
watch(() => props.stats, (newStats) => {
  if (newStats) {
    currentStats.value.cpu = newStats.cpu ?? 0
    currentStats.value.memory = newStats.memory ?? 0
  }
}, { immediate: true, deep: true })
</script>

<style>

/* 使用非scoped样式 */

</style>