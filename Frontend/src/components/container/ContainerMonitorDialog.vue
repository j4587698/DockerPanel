<template>
  <el-dialog
    v-model="visible"
    :title="`${t('container.monitorDialog.title')} - ${containerName || ''}`"
    width="1200px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <!-- 监控控制 -->
    <div class="monitor-controls">
      <div class="control-left">
        <el-select v-model="refreshInterval" :placeholder="t('container.monitorDialog.refreshInterval')" style="width: 120px">
          <el-option :label="t('container.monitorDialog.oneSecond')" :value="1000" />
          <el-option :label="t('container.monitorDialog.twoSeconds')" :value="2000" />
          <el-option :label="t('container.monitorDialog.fiveSeconds')" :value="5000" />
          <el-option :label="t('container.monitorDialog.tenSeconds')" :value="10000" />
        </el-select>
        <el-button @click="toggleMonitoring" :type="isMonitoring ? 'danger' : 'primary'">
          <el-icon><VideoPlay v-if="!isMonitoring" /><VideoPause v-else /></el-icon>
          {{ isMonitoring ? t('container.monitorDialog.stopMonitor') : t('container.monitorDialog.startMonitor') }}
        </el-button>
        <el-button @click="refreshStats" :loading="loading" :icon="Refresh">{{ t('container.monitorDialog.refresh') }}</el-button>
      </div>
      <div class="control-right">
        <el-text type="info" v-if="lastUpdateTime">
          {{ t('container.monitorDialog.lastUpdate') }}: {{ formatTime(lastUpdateTime) }}
        </el-text>
      </div>
    </div>

    <!-- 监控概览 -->
    <div class="monitor-overview">
      <el-row :gutter="16">
        <el-col :span="6">
          <el-card class="metric-card cpu-card">
            <div class="metric-header">
              <el-icon class="metric-icon cpu-icon"><Cpu /></el-icon>
              <span class="metric-title">{{ t('container.monitorDialog.cpuUsage') }}</span>
            </div>
            <div class="metric-value">
              <div class="value-number">{{ currentStats.cpuStats?.percent?.toFixed(2) || 0 }}%</div>
              <div class="value-bar">
                <el-progress
                  :percentage="currentStats.cpuStats?.percent || 0"
                  :color="getCpuColor(currentStats.cpuStats?.percent || 0)"
                  :show-text="false"
                />
              </div>
            </div>
            <div class="metric-chart">
              <div ref="cpuChart" class="chart-container"></div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card class="metric-card memory-card">
            <div class="metric-header">
              <el-icon class="metric-icon memory-icon"><Monitor /></el-icon>
              <span class="metric-title">{{ t('container.monitorDialog.memoryUsage') }}</span>
            </div>
            <div class="metric-value">
              <div class="value-number">{{ currentStats.memoryStats?.percent?.toFixed(2) || 0 }}%</div>
              <div class="value-info">
                {{ formatBytes(currentStats.memoryStats?.usage || 0) }} / {{ formatBytes(currentStats.memoryStats?.limit || 0) }}
              </div>
              <div class="value-bar">
                <el-progress
                  :percentage="currentStats.memoryStats?.percent || 0"
                  :color="getMemoryColor(currentStats.memoryStats?.percent || 0)"
                  :show-text="false"
                />
              </div>
            </div>
            <div class="metric-chart">
              <div ref="memoryChart" class="chart-container"></div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card class="metric-card network-card">
            <div class="metric-header">
              <el-icon class="metric-icon network-icon"><Connection /></el-icon>
              <span class="metric-title">{{ t('container.monitorDialog.networkIO') }}</span>
            </div>
            <div class="metric-value">
              <div class="network-item">
                <span class="network-label">{{ t('container.monitorDialog.upload') }}:</span>
                <span class="network-value">{{ formatBytes(currentStats.networkStats?.txBytes || 0) }}/s</span>
              </div>
              <div class="network-item">
                <span class="network-label">{{ t('container.monitorDialog.download') }}:</span>
                <span class="network-value">{{ formatBytes(currentStats.networkStats?.rxBytes || 0) }}/s</span>
              </div>
            </div>
            <div class="metric-chart">
              <div ref="networkChart" class="chart-container"></div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card class="metric-card disk-card">
            <div class="metric-header">
              <el-icon class="metric-icon disk-icon"><Folder /></el-icon>
              <span class="metric-title">{{ t('container.monitorDialog.diskIO') }}</span>
            </div>
            <div class="metric-value">
              <div class="disk-item">
                <span class="disk-label">{{ t('container.monitorDialog.read') }}:</span>
                <span class="disk-value">{{ formatBytes(currentStats.blockIoStats?.readBytes || 0) }}/s</span>
              </div>
              <div class="disk-item">
                <span class="disk-label">{{ t('container.monitorDialog.write') }}:</span>
                <span class="disk-value">{{ formatBytes(currentStats.blockIoStats?.writeBytes || 0) }}/s</span>
              </div>
            </div>
            <div class="metric-chart">
              <div ref="diskChart" class="chart-container"></div>
            </div>
          </el-card>
        </el-col>
      </el-row>
    </div>

    <!-- 详细统计信息 -->
    <div class="detailed-stats">
      <el-divider content-position="left">
        <span style="color: #909399;">{{ t('container.monitorDialog.detailedStats') }}</span>
      </el-divider>

      <el-tabs v-model="activeTab" type="card">
        <el-tab-pane label="CPU" name="cpu">
          <el-descriptions :column="2" border>
            <el-descriptions-item :label="t('container.monitorDialog.totalUsageTime')">
              {{ formatDuration(currentStats.cpuStats?.totalUsage || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.systemUsageTime')">
              {{ formatDuration(currentStats.cpuStats?.systemUsage || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.userUsageTime')">
              {{ formatDuration((currentStats.cpuStats as any)?.userUsage || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.coreCount')">
              {{ currentStats.cpuStats?.percpuUsage?.length || 0 }}
            </el-descriptions-item>
          </el-descriptions>

          <div v-if="currentStats.cpuStats?.percpuUsage" class="per-cpu-stats">
            <h4>{{ t('container.monitorDialog.perCoreUsage') }}</h4>
            <div class="cpu-list">
              <div
                v-for="(usage, index) in currentStats.cpuStats.percpuUsage"
                :key="index"
                class="cpu-item"
              >
                <span class="cpu-label">{{ t('container.monitorDialog.core') }} {{ index }}:</span>
                <el-progress
                  :percentage="usage"
                  :color="getCpuColor(usage)"
                  :show-text="true"
                  style="width: 200px"
                />
              </div>
            </div>
          </div>
        </el-tab-pane>

        <el-tab-pane :label="t('container.monitorDialog.memoryUsage')" name="memory">
          <el-descriptions :column="2" border>
            <el-descriptions-item :label="t('container.monitorDialog.used')">
              {{ formatBytes(currentStats.memoryStats?.usage || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.limit')">
              {{ formatBytes(currentStats.memoryStats?.limit || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.cache')">
              {{ formatBytes(currentStats.memoryStats?.cache || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.cacheUsage')">
              {{ formatBytes(currentStats.memoryStats?.cacheUsage || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.rss')">
              {{ formatBytes(currentStats.memoryStats?.rss || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.rssUsage')">
              {{ formatBytes(currentStats.memoryStats?.rssUsage || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.swapSpace')">
              {{ formatBytes(currentStats.memoryStats?.swap || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.swapUsage')">
              {{ formatBytes(currentStats.memoryStats?.swapUsage || 0) }}
            </el-descriptions-item>
          </el-descriptions>
        </el-tab-pane>

        <el-tab-pane :label="t('container.monitorDialog.networkIO')" name="network">
          <el-descriptions :column="2" border>
            <el-descriptions-item :label="t('container.monitorDialog.receivedBytes')">
              {{ formatBytes(currentStats.networkStats?.rxBytes || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.sentBytes')">
              {{ formatBytes(currentStats.networkStats?.txBytes || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.receivedPackets')">
              {{ currentStats.networkStats?.rxPackets || 0 }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.sentPackets')">
              {{ currentStats.networkStats?.txPackets || 0 }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.receiveErrors')">
              {{ currentStats.networkStats?.rxErrors || 0 }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.sendErrors')">
              {{ currentStats.networkStats?.txErrors || 0 }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.receiveDropped')">
              {{ currentStats.networkStats?.rxDropped || 0 }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.sendDropped')">
              {{ currentStats.networkStats?.txDropped || 0 }}
            </el-descriptions-item>
          </el-descriptions>
        </el-tab-pane>

        <el-tab-pane :label="t('container.monitorDialog.diskIO')" name="disk">
          <el-descriptions :column="2" border>
            <el-descriptions-item :label="t('container.monitorDialog.readBytes')">
              {{ formatBytes(currentStats.blockIoStats?.readBytes || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.writeBytes')">
              {{ formatBytes(currentStats.blockIoStats?.writeBytes || 0) }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.readOps')">
              {{ currentStats.blockIoStats?.readOps || 0 }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('container.monitorDialog.writeOps')">
              {{ currentStats.blockIoStats?.writeOps || 0 }}
            </el-descriptions-item>
          </el-descriptions>
        </el-tab-pane>
      </el-tabs>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('container.monitorDialog.close') }}</el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { VideoPlay, VideoPause, Refresh, Cpu, Monitor, Connection, Folder } from '@element-plus/icons-vue'
import { containerApi, type ContainerStats } from '@/api/containers'
import { useI18n } from 'vue-i18n'
// import { LineChart } from 'echarts/charts'
// import { GridComponent, TooltipComponent, LegendComponent, MarkLineComponent, DataZoomComponent } from 'echarts/components'
// import { CanvasRenderer } from 'echarts/renderers'
import type { EChartsOption } from 'echarts/types/dist/shared'
import { formatLocalizedTime } from '@/utils/date'

const { t } = useI18n()

interface Props {
  modelValue: boolean
  containerId?: string
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// 响应式数据
const loading = ref(false)
const isMonitoring = ref(false)
const refreshInterval = ref(5000)
const lastUpdateTime = ref<Date | null>(null)
const currentStats = ref<ContainerStats>({
  containerId: '',
  name: '',
  cpuStats: { usage: 0, percent: 0 },
  memoryStats: { usage: 0, limit: 0, percent: 0 },
  networkStats: { rxBytes: 0, txBytes: 0, rxPackets: 0, txPackets: 0, rxErrors: 0, txErrors: 0, rxDropped: 0, txDropped: 0 },
  blockIoStats: { readBytes: 0, writeBytes: 0, readOps: 0, writeOps: 0 },
  timestamp: new Date().toISOString()
})

// 历史数据
const historyData = reactive({
  cpu: [] as Array<{ time: string, value: number }>,
  memory: [] as Array<{ time: string, value: number }>,
  networkRx: [] as Array<{ time: string, value: number }>,
  networkTx: [] as Array<{ time: string, value: number }>,
  diskRead: [] as Array<{ time: string, value: number }>,
  diskWrite: [] as Array<{ time: string, value: number }>
})

// 图表实例
const cpuChart = ref<any>(null)
const memoryChart = ref<any>(null)
const networkChart = ref<any>(null)
const diskChart = ref<any>(null)
let monitoringTimer: number | null = null

// 注册 ECharts 组件
// LineChart.use([GridComponent, TooltipComponent, LegendComponent, MarkLineComponent, DataZoomComponent, CanvasRenderer])

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const containerId = computed(() => props.containerId)
const containerName = computed(() => containerId.value?.slice(0, 12) || '')

const activeTab = ref('cpu')

// 监听对话框打开
watch(visible, (newValue) => {
  if (newValue && containerId.value) {
    initCharts()
    refreshStats()
  } else {
    stopMonitoring()
  }
})

// 监听刷新间隔变化
watch(refreshInterval, () => {
  if (isMonitoring.value) {
    stopMonitoring()
    startMonitoring()
  }
})

// 生命周期
onMounted(() => {
  // 注册 ECharts
})

onUnmounted(() => {
  stopMonitoring()
  disposeCharts()
})

// 方法
const initCharts = () => {
  nextTick(() => {
    if (cpuChart.value) cpuChart.value.dispose()
    if (memoryChart.value) memoryChart.value.dispose()
    if (networkChart.value) networkChart.value.dispose()
    if (diskChart.value) diskChart.value.dispose()

    // CPU 图表
    if (cpuChart.value) {
      const cpuOption: EChartsOption = {
        grid: { top: 10, right: 10, bottom: 20, left: 10 },
        xAxis: { type: 'time', show: false },
        yAxis: { type: 'value', min: 0, max: 100 },
        series: [{
          type: 'line',
          data: historyData.cpu,
          smooth: true,
          lineStyle: { width: 2 },
          itemStyle: { color: '#409eff' },
          areaStyle: { opacity: 0.3 }
        }]
      }
      cpuChart.value.setOption(cpuOption)
    }

    // 内存图表
    if (memoryChart.value) {
      const memoryOption: EChartsOption = {
        grid: { top: 10, right: 10, bottom: 20, left: 10 },
        xAxis: { type: 'time', show: false },
        yAxis: { type: 'value', min: 0, max: 100 },
        series: [{
          type: 'line',
          data: historyData.memory,
          smooth: true,
          lineStyle: { width: 2 },
          itemStyle: { color: '#67c23a' },
          areaStyle: { opacity: 0.3 }
        }]
      }
      memoryChart.value.setOption(memoryOption)
    }

    // 网络图表
    if (networkChart.value) {
      const networkOption: EChartsOption = {
        grid: { top: 10, right: 10, bottom: 20, left: 10 },
        xAxis: { type: 'time', show: false },
        yAxis: { type: 'value' },
        series: [
          {
            name: '上行',
            type: 'line',
            data: historyData.networkTx,
            smooth: true,
            lineStyle: { width: 2 },
            itemStyle: { color: '#e6a23c' }
          },
          {
            name: '下行',
            type: 'line',
            data: historyData.networkRx,
            smooth: true,
            lineStyle: { width: 2 },
            itemStyle: { color: '#f56c6c' }
          }
        ]
      }
      networkChart.value.setOption(networkOption)
    }

    // 磁盘图表
    if (diskChart.value) {
      const diskOption: EChartsOption = {
        grid: { top: 10, right: 10, bottom: 20, left: 10 },
        xAxis: { type: 'time', show: false },
        yAxis: { type: 'value' },
        series: [
          {
            name: '读取',
            type: 'line',
            data: historyData.diskRead,
            smooth: true,
            lineStyle: { width: 2 },
            itemStyle: { color: '#909399' }
          },
          {
            name: '写入',
            type: 'line',
            data: historyData.diskWrite,
            smooth: true,
            lineStyle: { width: 2 },
            itemStyle: { color: '#606266' }
          }
        ]
      }
      diskChart.value.setOption(diskOption)
    }
  })
}

const disposeCharts = () => {
  if (cpuChart.value) {
    cpuChart.value.dispose()
    cpuChart.value = null
  }
  if (memoryChart.value) {
    memoryChart.value.dispose()
    memoryChart.value = null
  }
  if (networkChart.value) {
    networkChart.value.dispose()
    networkChart.value = null
  }
  if (diskChart.value) {
    diskChart.value.dispose()
    diskChart.value = null
  }
}

const refreshStats = async () => {
  if (!containerId.value) return

  loading.value = true
  try {
    const response = await containerApi.getContainerStats(containerId.value)
    const newStats = response

    // 更新当前统计
    Object.assign(currentStats, newStats)
    lastUpdateTime.value = new Date()

    // 添加历史数据
    const now = new Date().toISOString()
    historyData.cpu.push({ time: now, value: newStats.cpuStats.percent })
    historyData.memory.push({ time: now, value: newStats.memoryStats.percent })
    historyData.networkRx.push({ time: now, value: newStats.networkStats.rxBytes })
    historyData.networkTx.push({ time: now, value: newStats.networkStats.txBytes })
    historyData.diskRead.push({ time: now, value: newStats.blockIoStats.readBytes })
    historyData.diskWrite.push({ time: now, value: newStats.blockIoStats.writeBytes })

    // 保持最近30个数据点
    const maxDataPoints = 30
    if (historyData.cpu.length > maxDataPoints) {
      historyData.cpu.splice(0, historyData.cpu.length - maxDataPoints)
      historyData.memory.splice(0, historyData.memory.length - maxDataPoints)
      historyData.networkRx.splice(0, historyData.networkRx.length - maxDataPoints)
      historyData.networkTx.splice(0, historyData.networkTx.length - maxDataPoints)
      historyData.diskRead.splice(0, historyData.diskRead.length - maxDataPoints)
      historyData.diskWrite.splice(0, historyData.diskWrite.length - maxDataPoints)
    }

    // 更新图表
    updateCharts()
  } catch (error: any) {
    console.error('获取容器统计信息失败:', error)
    ElMessage.error(t('container.monitorDialog.loadStatsFailed'))
  } finally {
    loading.value = false
  }
}

const updateCharts = () => {
  nextTick(() => {
    if (cpuChart.value) {
      cpuChart.value.setOption({
        series: [{ data: historyData.cpu }]
      })
    }
    if (memoryChart.value) {
      memoryChart.value.setOption({
        series: [{ data: historyData.memory }]
      })
    }
    if (networkChart.value) {
      networkChart.value.setOption({
        series: [
          { data: historyData.networkTx },
          { data: historyData.networkRx }
        ]
      })
    }
    if (diskChart.value) {
      diskChart.value.setOption({
        series: [
          { data: historyData.diskRead },
          { data: historyData.diskWrite }
        ]
      })
    }
  })
}

const toggleMonitoring = () => {
  if (isMonitoring.value) {
    stopMonitoring()
  } else {
    startMonitoring()
  }
}

const startMonitoring = () => {
  isMonitoring.value = true
  monitoringTimer = window.setInterval(() => {
    refreshStats()
  }, refreshInterval.value)
}

const stopMonitoring = () => {
  isMonitoring.value = false
  if (monitoringTimer) {
    clearInterval(monitoringTimer)
    monitoringTimer = null
  }
}

const getCpuColor = (percent: number) => {
  if (percent < 50) return '#67c23a'
  if (percent < 80) return '#e6a23c'
  return '#f56c6c'
}

const getMemoryColor = (percent: number) => {
  if (percent < 50) return '#67c23a'
  if (percent < 80) return '#e6a23c'
  return '#f56c6c'
}

const formatBytes = (bytes: number): string => {
  if (bytes === null || bytes === undefined || isNaN(bytes) || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const absBytes = Math.abs(bytes)
  if (absBytes < 1) return absBytes.toFixed(2) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const formatDuration = (nanoseconds: number): string => {
  const seconds = Math.floor(nanoseconds / 1e9)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)

  if (hours > 0) {
    return `${hours}小时${minutes % 60}分钟${seconds % 60}秒`
  } else if (minutes > 0) {
    return `${minutes}分钟${seconds % 60}秒`
  } else {
    return `${seconds}秒`
  }
}

const formatTime = (date: Date): string => {
  return formatLocalizedTime(date, '--')
}

const handleClose = () => {
  visible.value = false
  stopMonitoring()
}
</script>

<style scoped>
.monitor-controls {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
  padding: 12px;
  background-color: #f5f7fa;
  border-radius: 6px;
}

.control-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.control-right {
  font-size: 12px;
}

.monitor-overview {
  margin-bottom: 20px;
}

.metric-card {
  height: 280px;
}

.metric-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.metric-icon {
  font-size: 18px;
}

.cpu-icon { color: #409eff; }
.memory-icon { color: #67c23a; }
.network-icon { color: #e6a23c; }
.disk-icon { color: #909399; }

.metric-title {
  font-weight: 600;
  color: #303133;
}

.metric-value {
  margin-bottom: 8px;
}

.value-number {
  font-size: 24px;
  font-weight: bold;
  color: #303133;
  margin-bottom: 4px;
}

.value-info {
  font-size: 12px;
  color: #909399;
  margin-bottom: 8px;
}

.value-bar {
  margin-bottom: 8px;
}

.network-item,
.disk-item {
  display: flex;
  justify-content: space-between;
  margin-bottom: 4px;
  font-size: 12px;
}

.network-label,
.disk-label {
  color: #606266;
}

.network-value,
.disk-value {
  color: #303133;
  font-weight: 500;
}

.metric-chart {
  height: 120px;
}

.chart-container {
  width: 100%;
  height: 100%;
}

.detailed-stats {
  margin-top: 20px;
}

.per-cpu-stats {
  margin-top: 16px;
}

.per-cpu-stats h4 {
  margin-bottom: 12px;
  color: #303133;
}

.cpu-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.cpu-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.cpu-label {
  width: 60px;
  font-size: 12px;
  color: #606266;
}

.dialog-footer {
  text-align: right;
}

@media (max-width: 768px) {
  .monitor-controls {
    flex-direction: column;
    gap: 12px;
    align-items: stretch;
  }

  .control-left,
  .control-right {
    justify-content: center;
  }

  .monitor-overview .el-col {
    margin-bottom: 16px;
  }

  .metric-card {
    height: 320px;
  }
}
</style>