<template>
  <div class="container-detail-page">
    <div class="page-header-glass">
      <div class="header-left">
        <el-button @click="goBack" text circle class="back-btn" :icon="ArrowLeft" />
        <div class="header-title-block">
          <div class="title-row">
             <h1 class="page-title">{{ container?.name || t('common.loading') }}</h1>
             <el-tag v-if="container" :type="getStatusType(container.state)" effect="dark" size="small" round class="status-badge">
               <span class="status-dot"></span>
               {{ getStatusLabel(container.state) }}
             </el-tag>
          </div>
          <div class="subtitle-row" v-if="container">
             <span class="font-mono text-muted text-xs">ID: {{ container.id.substring(0, 12) }}</span>
          </div>
        </div>
      </div>

      <div class="header-center">
        <div class="glass-tabs">
          <div 
            v-for="tab in tabs" 
            :key="tab.key"
            class="glass-tab-item"
            :class="{ active: activeTab === tab.key }"
            @click="activeTab = tab.key"
          >
            {{ tab.label }}
          </div>
        </div>
      </div>

      <div class="header-right">
         <el-button type="success" v-if="container?.state === 'exited' || container?.state === 'created'" @click="startContainer" :loading="actionLoading.start" :icon="VideoPlay" :title="t('container.start')">{{ t('container.start') }}</el-button>

         <el-button type="warning" v-if="container?.state === 'running'" @click="stopContainer" :loading="actionLoading.stop" :icon="VideoPause" :title="t('container.stop')">{{ t('container.stop') }}</el-button>

         <el-button type="primary" v-if="container?.state === 'running'" @click="restartContainer" :loading="actionLoading.restart" :icon="RefreshRight" :title="t('container.restart')">{{ t('container.restart') }}</el-button>

         <el-dropdown trigger="click">
           <el-button :icon="MoreFilled">
              {{ t('common.actions') }}
           </el-button>
           <template #dropdown>
             <el-dropdown-menu>
               <el-dropdown-item @click="refreshContainerData"><el-icon><Refresh /></el-icon>{{ t('common.refresh') }}</el-dropdown-item>
               <el-dropdown-item @click="editContainer"><el-icon><Edit /></el-icon>{{ t('container.containerDetail.editContainer') }}</el-dropdown-item>
               <el-dropdown-item @click="recreateContainer"><el-icon><RefreshRight /></el-icon>{{ t('container.recreate') }}</el-dropdown-item>
               <el-dropdown-item @click="exportContainer"><el-icon><Download /></el-icon>{{ t('container.export') }}</el-dropdown-item>
               <el-dropdown-item divided @click="removeContainer" style="color: var(--color-danger)">
                 <el-icon><Delete /></el-icon>{{ t('common.delete') }}
               </el-dropdown-item>
             </el-dropdown-menu>
           </template>
         </el-dropdown>
      </div>
    </div>

    <div class="main-content" v-loading="loading && activeTab !== 'terminal'">
      <div v-if="container" class="content-wrapper">
        <transition name="fade-slide" mode="out-in">
          <!-- OVERVIEW TAB -->
          <ContainerOverview 
            v-if="activeTab === 'overview'" 
            key="overview"
            :container="container"
            :stats="stats"
            :env-vars="envVars"
            :volumes="volumes"
            @connect-network="showConnectNetworkDialog"
            @disconnect-network="disconnectFromNetwork"
          />

          <!-- LOGS TAB -->
          <ContainerLogs 
            v-else-if="activeTab === 'logs'" 
            key="logs"
            :logs="logs"
            :tail="logOptions.tail"
            :follow="logOptions.follow"
            @update:tail="handleTailChange"
            @update:follow="logOptions.follow = $event"
            @refresh="refreshLogs"
            @clear="clearLogs"
            @download="downloadLogs"
          />

          <!-- TERMINAL TAB -->
          <ContainerTerminal 
            v-else-if="activeTab === 'terminal'" 
            key="terminal"
            ref="terminalRef"
            :connected="terminalConnected"
            :connecting="terminalConnecting"
            :shell="selectedShell"
            :available-shells="availableShells"
            @update:shell="selectedShell = $event"
            @connect="connectTerminal"
            @disconnect="disconnectTerminal"
            @focus="focusTerminal"
          />

          <!-- STATS TAB -->
          <ContainerStats 
            v-else-if="activeTab === 'stats'" 
            key="stats"
            :container="container"
            :stats="stats"
          />

          <!-- CONFIG TAB -->
          <ContainerConfig 
            v-else-if="activeTab === 'config'" 
            key="config"
            :config="config"
            :saving="configSaving"
            @update:config="config = $event"
            @save="saveConfig"
            @reset="resetConfig"
          />

          <!-- AUTO UPDATE TAB -->
          <ContainerAutoUpdate
            v-else-if="activeTab === 'autoUpdate'"
            key="autoUpdate"
            :container-id="container?.id || ''"
            :container-name="container?.name || ''"
            :container-image="container?.image || ''"
          />

          <!-- FILES TAB -->
          <ContainerFiles
            v-else-if="activeTab === 'files'"
            key="files"
            :container-id="container?.id || ''"
            :container-state="container?.state"
          />

          <!-- JSON TAB -->
          <ContainerJson 
            v-else-if="activeTab === 'json'" 
            key="json"
            :container="container"
          />
        </transition>
      </div>
      <div v-else class="loading-state">
        <el-skeleton :rows="5" animated />
      </div>
    </div>

    <!-- DIALOGS -->
    <ContainerDialogs 
      v-model:edit-dialog-visible="editDialogVisible"
      v-model:recreate-dialog-visible="recreateDialogVisible"
      v-model:connect-network-dialog-visible="connectNetworkDialogVisible"
      :available-networks="availableNetworks"
      :connecting-network="connectingNetwork"
      :loading="recreateLoading"
      :container-name="container?.name"
      :container-image="container?.image"
      :container-state="container?.state"
      :auto-start="container?.state === 'running'"
      @save-edit="saveEdit"
      @confirm-recreate="confirmRecreate"
      @connect-network="connectToNetworkById"
    />
    
    <!-- Edit Container Dialog (full form) -->
    <CreateContainerDialog 
      v-model="editContainerDialogVisible"
      :edit-container="container"
      :edit-mode="true"
      @success="handleEditSuccess"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick, watch, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  ArrowLeft, Refresh, Setting, VideoPlay, VideoPause, RefreshRight, Delete,
  Download, MoreFilled, Edit
} from '@element-plus/icons-vue'
import { useContainersStore } from '@/stores/containers'
import { useNetworksStore } from '@/stores/networks'
import { networkApi } from '@/api/network'
import { containerApi } from '@/api/containers'
import { signalrService } from '@/services/signalr'
import type { Container } from '@/types/container'
import type { NetworkInfo } from '@/types/network'
import * as signalR from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { loadTerminalDeps, type XtermFitAddon, type XtermTerminal } from '@/utils/terminalDeps'

// 子组件
import ContainerOverview from './containers/components/ContainerOverview.vue'
import ContainerLogs from './containers/components/ContainerLogs.vue'
import ContainerTerminal from './containers/components/ContainerTerminal.vue'
import ContainerConfig from './containers/components/ContainerConfig.vue'
import ContainerStats from './containers/components/ContainerStats.vue'
import ContainerJson from './containers/components/ContainerJson.vue'
import ContainerDialogs from './containers/components/ContainerDialogs.vue'
import ContainerAutoUpdate from '@/components/container/ContainerAutoUpdate.vue'
import ContainerFiles from './containers/components/ContainerFiles.vue'
import { formatLocalizedTime } from '@/utils/date'
import CreateContainerDialog from '@/components/container/CreateContainerDialog.vue'

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const containersStore = useContainersStore()
const networksStore = useNetworksStore()

// 标签页配置
const tabs = [
  { key: 'overview', label: computed(() => t('container.overview')) },
  { key: 'stats', label: computed(() => t('container.stats')) },
  { key: 'logs', label: computed(() => t('container.logs')) },
  { key: 'terminal', label: computed(() => t('container.terminal')) },
  { key: 'files', label: computed(() => t('container.files')) },
  { key: 'config', label: computed(() => t('container.config')) },
  { key: 'autoUpdate', label: computed(() => t('container.containerDetail.autoUpdate')) },
  { key: 'json', label: computed(() => t('container.json')) }
]

// 基础状态
const loading = ref(false)
const container = ref<Container | null>(null)
const activeTab = ref('overview')

// Action Loading States
const actionLoading = ref({ start: false, stop: false, restart: false, remove: false })

// Logs
const logs = ref('')
const logOptions = ref({ tail: 100, follow: false })

// Terminal
const terminalRef = ref<InstanceType<typeof ContainerTerminal>>()
const terminalConnected = ref(false)
const terminalConnecting = ref(false)
const selectedShell = ref('/bin/sh')
const availableShells = ref<string[]>(['/bin/sh', '/bin/bash', '/bin/ash'])
let terminal: XtermTerminal | null = null
let fitAddon: XtermFitAddon | null = null
let terminalConnection: HubConnection | null = null
let terminalResizeObserver: ResizeObserver | null = null

// Stats
const stats = ref({ cpu: 0, memory: 0, networkIO: 0 })

// Config
const config = ref({
  restartPolicy: 'no',
  memoryLimit: '',
  memoryUnit: 'm',
  memoryReservation: '',
  memoryReservationUnit: 'm',
  cpuQuota: 0,
  cpuShares: 1024
})
const configSaving = ref(false)

// 环境变量和挂载卷
const volumes = ref<Array<{ source: string; target: string; mode: string }>>([])
const envVars = ref<Array<{ key: string; value: string }>>([])

// Dialogs
const editDialogVisible = ref(false)
const editContainerDialogVisible = ref(false)
const recreateDialogVisible = ref(false)
const connectNetworkDialogVisible = ref(false)

// Network
const availableNetworks = ref<NetworkInfo[]>([])
const connectingNetwork = ref(false)

// SignalR
let statsUnsubscribe: (() => void) | null = null
let logsUnsubscribe: (() => void) | null = null
let containerUnsubscribe: (() => void) | null = null

// --- Helpers ---
const getStatusLabel = (state: string | any) => {
  const status = typeof state === 'string' ? state : state?.status || ''
  return t(`container.${status}`) || status
}

const getStatusType = (state: string | any) => {
  const status = typeof state === 'string' ? state : state?.status || ''
  const s = status.toLowerCase()
  if (s === 'running') return 'success'
  if (s === 'exited' || s === 'dead') return 'danger'
  if (s === 'paused') return 'warning'
  return 'info'
}

// --- Actions ---
const goBack = () => router.push('/containers')

const loadContainerDetail = async () => {
  const containerId = route.params.id as string
  if (!containerId) return
  loading.value = true
  try {
    const data = await containersStore.fetchContainerDetails(containerId)
    if (data) {
      container.value = data
      extractContainerConfig(data)
      if (activeTab.value === 'logs') loadLogs()
    }
  } catch (err) {
    console.error(err)
    ElMessage.error(t('common.error'))
  } finally {
    loading.value = false
  }
}

const refreshContainerData = async () => {
  await loadContainerDetail()
  ElMessage.success(t('common.success'))
}

// 从容器数据中提取配置
const extractContainerConfig = (c: Container) => {
  if (c.restartPolicy?.name) {
    config.value.restartPolicy = c.restartPolicy.name
  }
  
  if (c.memory && c.memory > 0) {
    const memMB = Math.round(c.memory / 1024 / 1024)
    if (memMB >= 1024) {
      config.value.memoryLimit = String(Math.round(memMB / 1024 * 100) / 100)
      config.value.memoryUnit = 'g'
    } else {
      config.value.memoryLimit = String(memMB)
      config.value.memoryUnit = 'm'
    }
  } else {
    config.value.memoryLimit = ''
  }
  
  config.value.cpuShares = c.cpuShares || 1024
  
  if (c.mounts && c.mounts.length > 0) {
    volumes.value = c.mounts.map(m => ({
      source: m.source || m.name || '',
      target: m.destination || '',
      mode: m.rw ? 'rw' : 'ro'
    }))
  } else {
    volumes.value = []
  }
  
  if ((c as any).environment && (c as any).environment.length > 0) {
    envVars.value = (c as any).environment.map((env: string) => {
      const idx = env.indexOf('=')
      if (idx > 0) {
        return { key: env.substring(0, idx), value: env.substring(idx + 1) }
      }
      return { key: env, value: '' }
    })
  } else {
    envVars.value = []
  }
}

const resetConfig = () => {
  if (container.value) extractContainerConfig(container.value)
}

// --- Network Management ---
const showConnectNetworkDialog = async () => {
  try {
    await networksStore.fetchNetworks()
    const connectedNetworks = getContainerNetworks(container.value!).map(n => n.name)
    availableNetworks.value = networksStore.networks.filter(
      (n: NetworkInfo) => !connectedNetworks.includes(n.name)
    )
  } catch (err) {
    ElMessage.error(t('container.containerDetail.getNetworksFailed'))
  }
  connectNetworkDialogVisible.value = true
}

const getContainerNetworks = (c: Container | null) => {
  if (!c?.networkSettings?.networks) return []
  return Object.entries(c.networkSettings.networks).map(([name, net]) => ({
    name,
    ipAddress: net.ipAddress || '',
    gateway: net.gateway || '',
    macAddress: net.macAddress || ''
  }))
}

const connectToNetworkById = async (networkId: string) => {
  if (!container.value || !networkId) return
  connectingNetwork.value = true
  try {
    await networkApi.connectContainerToNetwork(networkId, container.value.id)
    ElMessage.success(t('container.containerDetail.networkConnected'))
    connectNetworkDialogVisible.value = false
    await loadContainerDetail()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.message || t('container.containerDetail.connectNetworkFailed'))
  } finally {
    connectingNetwork.value = false
  }
}

const disconnectFromNetwork = async (networkName: string) => {
  if (!container.value) return
  if (networkName === 'dockerpanel-default') return

  try {
    await ElMessageBox.confirm(
      t('container.containerDetail.disconnectNetworkConfirm', { network: networkName }),
      t('container.containerDetail.disconnectNetworkTitle'),
      { type: 'warning' }
    )
    await networkApi.disconnectContainerFromNetwork(networkName, container.value.id)
    ElMessage.success(t('container.containerDetail.networkDisconnected'))
    await loadContainerDetail()
  } catch (err: any) {
    if (err !== 'cancel') {
      ElMessage.error(err.response?.data?.message || t('container.containerDetail.disconnectNetworkFailed'))
    }
  }
}

// --- Logs ---
let lastLogTimestamp = ''

const loadLogs = async () => {
  if (!container.value) return
  
  try {
    const data = await containersStore.getContainerLogs(container.value.id, {
      tail: logOptions.value.tail,
      follow: false
    })
    if (data && Array.isArray(data.logs)) {
      if (data.logs.length > 0) {
        lastLogTimestamp = data.logs[data.logs.length - 1].timestamp
      } else {
        lastLogTimestamp = ''
      }
      logs.value = data.logs.map((l: any) => {
        const time = l.timestamp ? formatLocalizedTime(l.timestamp, '') : ''
        return time ? `[${time}] ${l.message}` : l.message
      }).join('\n') || 'No logs found.'
    } else {
      logs.value = 'No logs found.'
      lastLogTimestamp = ''
    }
  } catch (e) { 
    logs.value = 'Error loading logs.' 
    lastLogTimestamp = ''
  }
  
  startLogStream()
}

const startLogStream = async () => {
  if (!container.value || !signalrService.isConnected()) return
  if (logsUnsubscribe) return
  
  try {
    // 传0只订阅增量新日志
    await signalrService.subscribeToLogs(container.value.id, 0)
    
    logsUnsubscribe = signalrService.subscribe('logs', (msg: any) => {
      const data = msg.data
      if (data && data.containerId === container.value?.id) {
        // 去重
        if (lastLogTimestamp && data.timestamp && new Date(data.timestamp) <= new Date(lastLogTimestamp)) {
           return
        }
        
        const time = data.timestamp ? formatLocalizedTime(data.timestamp, '') : ''
        const logLine = time ? `[${time}] ${data.message}` : data.message
        
        if (logs.value === 'No logs found.' || logs.value === 'Error loading logs.') {
            logs.value = logLine
        } else {
            logs.value += '\n' + logLine
        }
        
        if (data.timestamp) {
           lastLogTimestamp = data.timestamp
        }
      }
    })
  } catch (err) {
    console.error('订阅日志流失败', err)
  }
}

const stopLogStream = async () => {
  if (logsUnsubscribe) {
    logsUnsubscribe()
    logsUnsubscribe = null
  }
  if (container.value) {
    try {
      await signalrService.unsubscribeFromLogs(container.value.id)
    } catch (err) {}
  }
}

const handleTailChange = (val: number) => {
  logOptions.value.tail = val
  refreshLogs()
}

const refreshLogs = () => {
  stopLogStream()
  loadLogs()
}
const clearLogs = () => logs.value = ''
const downloadLogs = () => {
  const blob = new Blob([logs.value], { type: 'text/plain' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${container.value?.name}_logs.txt`
  a.click()
  URL.revokeObjectURL(url)
}

// --- Container Actions ---
const startContainer = async () => {
  if (!container.value) return
  actionLoading.value.start = true
  try {
    await containersStore.startContainer(container.value.id)
    ElMessage.success(t('container.containerDetail.startSuccess'))
    await loadContainerDetail()
  } catch(e: any) {
    ElMessage.error(`${t('container.containerDetail.startFailed')}: ${e.message || t('container.containerDetail.unknownError')}`)
  }
  finally { actionLoading.value.start = false }
}

const stopContainer = async () => {
  if (!container.value) return
  actionLoading.value.stop = true
  try {
    await containersStore.stopContainer(container.value.id)
    ElMessage.success(t('container.containerDetail.stopSuccess'))
    await loadContainerDetail()
  } catch(e: any) {
    ElMessage.error(`${t('container.containerDetail.stopFailed')}: ${e.message || t('container.containerDetail.unknownError')}`)
  }
  finally { actionLoading.value.stop = false }
}

const restartContainer = async () => {
  if (!container.value) return
  actionLoading.value.restart = true
  try {
    await containersStore.restartContainer(container.value.id)
    ElMessage.success(t('container.containerDetail.restartSuccess'))
    await loadContainerDetail()
  } catch(e: any) {
    ElMessage.error(`${t('container.containerDetail.restartFailed')}: ${e.message || t('container.containerDetail.unknownError')}`)
  }
  finally { actionLoading.value.restart = false }
}

const removeContainer = async () => {
  if (!container.value) return
  try {
    await ElMessageBox.confirm(t('common.deleteConfirm'), t('common.warning'), { type: 'warning' })
    try {
      await containersStore.removeContainer(container.value.id)
      router.push('/containers')
    } catch (error: any) {
      if (error.needForce) {
        // 容器正在运行，询问是否强制删除
        try {
          await ElMessageBox.confirm(
            t('container.containerDetail.forceDeleteConfirm'),
            t('container.containerDetail.forceDeleteTitle'),
            { type: 'warning', confirmButtonText: t('container.containerDetail.forceDeleteButton'), cancelButtonText: t('common.cancel') }
          )
          await containersStore.removeContainer(container.value.id, true)
          router.push('/containers')
        } catch {
          // 用户取消
        }
      } else {
        ElMessage.error(`${t('container.containerDetail.deleteFailed')}: ${error.message || error.error || t('container.containerDetail.unknownError')}`)
      }
    }
  } catch {
    // 用户取消
  }
}

// 编辑容器
const editContainer = () => {
  editContainerDialogVisible.value = true
}

const handleEditSuccess = async () => {
  ElMessage.success(t('container.containerDetail.containerUpdated'))
  // 刷新容器列表并跳转到新容器
  await containersStore.fetchContainers()
  // 重新加载当前页面数据
  await loadContainerDetail()
}

const saveEdit = async (payload: { name: string; pullLatest: boolean }) => {
  if (!container.value) return
  const { name: newName, pullLatest } = payload

  try {
    if (pullLatest) {
      // 如果名称发生变化，先重命名
      const currentName = container.value.name?.replace(/^\//, '')
      if (newName !== currentName) {
        await containerApi.renameContainer(container.value.id, newName)
      }
      
      // 拉取最新镜像并重建容器（使用原配置，应用新镜像）
      const result = await containerApi.recreateContainer(container.value.id, {
        pullLatest: true,
        autoStart: true
      })
      editDialogVisible.value = false
      ElMessage.success(t('container.containerDetail.recreateSuccess'))
      if (result.newId) {
        router.push(`/containers/${result.newId}`)
      } else {
        await loadContainerDetail()
      }
    } else {
      await containerApi.renameContainer(container.value.id, newName)
      editDialogVisible.value = false
      ElMessage.success(t('container.containerDetail.renameSuccess'))
      await loadContainerDetail()
    }
  } catch (e: any) {
    ElMessage.error(e.message || t('container.containerDetail.renameFailed'))
  }
}

// 重建容器
const recreateLoading = ref(false)
const recreateContainer = () => {
  recreateDialogVisible.value = true
}

const confirmRecreate = async (options: { pullLatest: boolean; autoStart: boolean; keepVolumes: boolean }) => {
  if (!container.value) return

  try {
    recreateLoading.value = true
    const result = await containerApi.recreateContainer(container.value.id, {
      pullLatest: options.pullLatest,
      autoStart: options.autoStart
    })

    recreateDialogVisible.value = false
    ElMessage.success(t('container.containerDetail.recreateSuccess'))

    // 跳转到新容器
    if (result.newId) {
      router.push(`/containers/${result.newId}`)
    } else {
      await loadContainerDetail()
    }
  } catch (e: any) {
    ElMessage.error(e.message || t('container.containerDetail.recreateFailed'))
  } finally {
    recreateLoading.value = false
  }
}

// 导出容器
const exportLoading = ref(false)
const exportContainer = async () => {
  if (!container.value) return

  try {
    await ElMessageBox.confirm(
      t('container.containerDetail.exportConfirm'),
      t('container.containerDetail.exportTitle'),
      {
        type: 'info',
        confirmButtonText: t('container.containerDetail.exportConfirmButton'),
        cancelButtonText: t('common.cancel')
      }
    )

    exportLoading.value = true
    ElMessage.info(t('container.containerDetail.exporting'))

    const response = await containerApi.exportContainer(container.value.id)

    // 创建下载链接
    const blob = new Blob([response as any], { type: 'application/x-tar' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${container.value.name || container.value.id.substring(0, 12)}_${new Date().toISOString().slice(0, 10)}.tar`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)

    ElMessage.success(t('container.containerDetail.exportSuccess'))
  } catch (e: any) {
    if (e !== 'cancel' && e !== 'close') {
      ElMessage.error(e.message || t('container.containerDetail.exportFailed'))
    }
  } finally {
    exportLoading.value = false
  }
}

// --- Config Save ---
const saveConfig = async () => {
  if (!container.value) return
  configSaving.value = true
  try {
    const updateData: any = {
      restartPolicy: { name: config.value.restartPolicy }
    }
    
    if (config.value.memoryLimit) {
      const memValue = parseFloat(config.value.memoryLimit)
      if (!isNaN(memValue)) {
        updateData.memory = config.value.memoryUnit === 'g' 
          ? Math.round(memValue * 1024 * 1024 * 1024)
          : Math.round(memValue * 1024 * 1024)
      }
    }
    
    if (config.value.cpuQuota && config.value.cpuQuota > 0) {
      updateData.cpuPeriod = 100000
      updateData.cpuQuota = Math.round(config.value.cpuQuota * 100000)
    }
    
    if (config.value.cpuShares) {
      updateData.cpuShares = config.value.cpuShares
    }
    
    await containerApi.updateContainer(container.value.id, updateData)
    ElMessage.success(t('common.operationSuccess'))
    await loadContainerDetail()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || t('common.error'))
  } finally {
    configSaving.value = false
  }
}

// --- Terminal ---
const focusTerminal = () => terminal?.focus()

const connectTerminal = async () => {
  if (!container.value?.id || terminalConnected.value || terminalConnecting.value) return
  
  terminalConnecting.value = true
  
  try {
    await nextTick()
    
    const xtermContainer = terminalRef.value?.xtermContainer
    if (!xtermContainer) {
      await new Promise(resolve => setTimeout(resolve, 100))
    }
    
    if (!terminal && xtermContainer) {
      const { Terminal, FitAddon } = await loadTerminalDeps()
      terminal = new Terminal({
        fontSize: 14,
        fontFamily: 'JetBrains Mono, Consolas, monospace',
        theme: {
          background: '#1e1e1e',
          foreground: '#d4d4d4',
          cursor: '#ffffff'
        },
        cursorBlink: true,
        cursorStyle: 'bar',
        scrollback: 10000
      })
      
      fitAddon = new FitAddon()
      terminal.loadAddon(fitAddon)
      terminal.open(xtermContainer)
      
      terminal.onData((data) => {
        if (terminalConnection && terminalConnected.value) {
          terminalConnection.invoke('SendInput', data).catch(() => {})
        }
      })
      
      if (xtermContainer) {
        terminalResizeObserver = new ResizeObserver(() => {
          if (fitAddon && terminal) {
            fitAddon.fit()
            // 通知后端调整终端尺寸
            if (terminalConnection && terminalConnected.value) {
              terminalConnection.invoke('Resize', terminal.cols, terminal.rows).catch(() => {})
            }
          }
        })
        terminalResizeObserver.observe(xtermContainer)
      }
    }
    
    terminalConnection = new signalR.HubConnectionBuilder()
      .withUrl('/containerTerminalHub', {
        accessTokenFactory: () => localStorage.getItem('token') || ''
      })
      .withAutomaticReconnect()
      .build()
    
    terminalConnection.on('Output', (data: string) => terminal?.write(data))
    
    terminalConnection.on('Connected', (data: any) => {
      terminalConnected.value = true
      terminalConnecting.value = false
      if (data.shell) selectedShell.value = data.shell
      terminal?.write(`\x1b[32m${t('container.containerDetail.terminalConnected', { name: container.value?.name })}\x1b[0m\r\n`)
      nextTick(() => {
        fitAddon?.fit()
        terminal?.focus()
      })
    })

    terminalConnection.on('Disconnected', () => {
      terminalConnected.value = false
      terminalConnecting.value = false
      terminal?.write(`\r\n\x1b[33m${t('container.containerDetail.terminalDisconnected')}\x1b[0m\r\n`)
    })

    // 监听错误 - 支持新的错误码格式和旧的字符串格式
    terminalConnection.on('Error', (error: any) => {
      terminalConnecting.value = false
      // 检查是否为错误码格式
      const errorMsg = typeof error === 'object' && error.message ? error.message : error
      terminal?.write(`\r\n\x1b[31m${t('container.containerDetail.terminalError', { error: errorMsg })}\x1b[0m\r\n`)
    })
    
    await terminalConnection.start()
    await terminalConnection.invoke('Connect', {
      containerId: container.value.id,
      shell: selectedShell.value,
      cols: terminal?.cols || 80,
      rows: terminal?.rows || 24
    })
    
  } catch (error) {
    terminalConnecting.value = false
    ElMessage.error(t('container.containerDetail.terminalConnectFailed', { error }))
  }
}

const disconnectTerminal = async () => {
  if (terminalResizeObserver) {
    terminalResizeObserver.disconnect()
    terminalResizeObserver = null
  }
  if (terminalConnection) {
    try {
      await terminalConnection.stop()
    } catch (e) {}
    terminalConnection = null
    terminalConnected.value = false
  }
  if (terminal) {
    terminal.dispose()
    terminal = null
  }
  fitAddon = null
}

// --- SignalR ---
const startSignalRSubscriptions = async () => {
  if (!signalrService.isConnected()) {
    try { await signalrService.connect() } catch (e) { return }
  }

  statsUnsubscribe = signalrService.subscribe('container-stats', (msg: any) => {
    const statsList = msg.data as any[]
    if (Array.isArray(statsList)) {
      const stat = statsList.find((s: any) => s.containerId === container.value?.id)
      if (stat) {
        let networkTotal = 0
        if (Array.isArray(stat.networks)) {
          networkTotal = stat.networks.reduce((sum: number, n: any) => sum + (n.rxBytes || 0) + (n.txBytes || 0), 0)
        }
        stats.value = {
          cpu: parseFloat((stat.cpuStats?.percentCpu || 0).toFixed(2)),
          memory: stat.memoryStats?.usage || 0,
          networkIO: networkTotal
        }
      }
    }
  })

  containerUnsubscribe = signalrService.subscribe('container', async (msg: any) => {
    if (msg.data?.id === container.value?.id) {
      await loadContainerDetail()
    }
  })

  await signalrService.subscribeToContainerStats()
}

const stopSignalRSubscriptions = async () => {
  statsUnsubscribe?.()
  logsUnsubscribe?.()
  containerUnsubscribe?.()
  await signalrService.unsubscribeFromContainerStats().catch(() => {})
}

// --- Lifecycle ---
onMounted(async () => {
  if (route.query.tab && typeof route.query.tab === 'string') {
    activeTab.value = route.query.tab
  }
  await loadContainerDetail()
  await startSignalRSubscriptions()
})

onUnmounted(() => {
  stopSignalRSubscriptions()
  stopLogStream()
  terminalResizeObserver?.disconnect()
  terminal?.dispose()
  disconnectTerminal()
})

watch(activeTab, async (val, oldVal) => {
  router.replace({ query: { ...route.query, tab: val } })
  
  if (oldVal === 'terminal' && val !== 'terminal') {
    await disconnectTerminal()
  }
  
  if (oldVal === 'logs' && val !== 'logs') {
    stopLogStream()
  }
  
  if (val === 'logs') loadLogs()
})
</script>

<style scoped>

.container-detail-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-app);
  color: var(--text-main);
  overflow: hidden;
  font-family: 'DM Sans', sans-serif;
}

.page-header-glass {
  flex-shrink: 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 24px;
  background: var(--bg-surface);
  backdrop-filter: var(--glass-blur);
  border-bottom: 1px solid var(--border-color);
  z-index: 10;
  box-shadow: var(--shadow-sm);
}

.header-left { display: flex; align-items: center; gap: 16px; }
.header-title-block { display: flex; flex-direction: column; gap: 4px; }
.title-row { display: flex; align-items: center; gap: 12px; }
.status-badge { display: flex; align-items: center; gap: 6px; }
.status-dot { width: 6px; height: 6px; border-radius: 50%; background: currentColor; animation: pulse 2s infinite; }
.subtitle-row { font-size: 12px; }
@keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.6; } }

.header-center { flex: 1; display: flex; justify-content: center; }
.header-right { display: flex; align-items: center; gap: 12px; }

.glass-tabs {
  display: flex;
  background: var(--bg-app);
  border-radius: 8px;
  padding: 4px;
  gap: 4px;
}

.glass-tab-item {
  padding: 8px 20px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  color: var(--text-muted);
}

.glass-tab-item:hover { color: var(--text-main); }
.glass-tab-item.active { background: var(--color-secondary); color: white; }

.main-content {
  flex: 1;
  min-height: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.content-wrapper {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.content-wrapper > * {
  flex: 1;
  min-height: 0;
}

.loading-state { padding: 40px; }

.back-btn { color: var(--text-muted); }
.back-btn:hover { color: var(--text-main); }

.font-mono { font-family: 'JetBrains Mono', Consolas, monospace; }
.text-muted { color: var(--text-muted); }
.text-xs { font-size: 12px; }
.text-sm { font-size: 13px; }
.mr-1 { margin-right: 4px; }

.fade-slide-enter-active, .fade-slide-leave-active { transition: all 0.3s ease; }
.fade-slide-enter-from { opacity: 0; transform: translateY(10px); }
.fade-slide-leave-to { opacity: 0; transform: translateY(-10px); }

</style>