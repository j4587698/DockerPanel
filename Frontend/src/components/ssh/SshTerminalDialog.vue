<template>
  <div ref="dialogWrapper" class="ssh-terminal-dialog-wrapper">
    <el-dialog
      v-model="visible"
      :title="`SSH 终端 - ${connectionInfo?.host || ''}`"
      width="90%"
      :close-on-click-modal="false"
      class="ssh-terminal-dialog"
      @open="initTerminal"
      @close="handleClose"
    >
      <!-- 工具栏 - 全屏时隐藏 -->
      <div v-show="!isBrowserFullscreen" class="terminal-toolbar" @click.stop.prevent @mousedown.stop.prevent>
        <div class="toolbar-left">
          <el-tag :type="isConnected ? 'success' : 'danger'" size="small">
            {{ isConnected ? '已连接' : '未连接' }}
          </el-tag>
          <span v-if="connectionInfo" class="connection-info">
            {{ connectionInfo.username }}@{{ connectionInfo.host }}:{{ connectionInfo.port }}
          </span>
        </div>
        <div class="toolbar-right">
          <el-button size="small" @click.stop="reconnect" :loading="connecting">
            重新连接
          </el-button>
          <el-button size="small" @click.stop="clearTerminal">
            清屏
          </el-button>
          <el-button size="small" type="primary" @click.stop.prevent="toggleBrowserFullscreen" @mousedown.stop.prevent>
            全屏
          </el-button>
        </div>
      </div>

      <!-- 终端容器 -->
      <div ref="terminalContainer" class="terminal-container" @click="focusTerminal"></div>

      <!-- 状态栏 - 全屏时隐藏 -->
      <div v-show="!isBrowserFullscreen" class="terminal-status-bar">
        <span>{{ terminalSize.cols }} x {{ terminalSize.rows }}</span>
        <span v-if="sessionDuration">会话时长: {{ sessionDuration }}</span>
      </div>

      <!-- 全屏时的浮动退出按钮 -->
      <div v-if="isBrowserFullscreen" class="fullscreen-exit-btn" @click="toggleBrowserFullscreen">
        <el-icon><Close /></el-icon>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onUnmounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import * as signalR from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { loadTerminalDeps, type XtermFitAddon, type XtermTerminal } from '@/utils/terminalDeps'

interface Props {
  modelValue: boolean
  connection?: {
    id?: string
    host: string
    port: number
    username: string
    password?: string
    privateKeyPath?: string
    privateKeyPassphrase?: string
  }
}

const props = defineProps<Props>()
const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
}>()

// 状态
const dialogWrapper = ref<HTMLElement>()
const terminalContainer = ref<HTMLElement>()
const isConnected = ref(false)
const connecting = ref(false)
const isBrowserFullscreen = ref(false)
const terminalSize = ref({ cols: 80, rows: 24 })
const sessionStartTime = ref<Date | null>(null)
const sessionDuration = ref('')

// Terminal 和 SignalR
let terminal: XtermTerminal | null = null
let fitAddon: XtermFitAddon | null = null
let hubConnection: HubConnection | null = null
let resizeObserver: ResizeObserver | null = null
let durationTimer: ReturnType<typeof setInterval> | null = null

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const connectionInfo = computed(() => props.connection)

// 初始化终端
const initTerminal = async () => {
  await nextTick()
  
  if (!terminalContainer.value) return

  // 创建终端实例
  const { Terminal, FitAddon } = await loadTerminalDeps()
  terminal = new Terminal({
    cursorBlink: true,
    fontSize: 14,
    fontFamily: '"JetBrains Mono", "Fira Code", Consolas, Monaco, monospace',
    theme: {
      background: '#1e1e1e',
      foreground: '#d4d4d4',
      cursor: '#aeafad',
      cursorAccent: '#1e1e1e',
      selectionBackground: '#264f78',
      black: '#000000',
      red: '#cd3131',
      green: '#0dbc79',
      yellow: '#e5e510',
      blue: '#2472c8',
      magenta: '#bc3fbc',
      cyan: '#11a8cd',
      white: '#e5e5e5',
      brightBlack: '#666666',
      brightRed: '#f14c4c',
      brightGreen: '#23d18b',
      brightYellow: '#f5f543',
      brightBlue: '#3b8eea',
      brightMagenta: '#d670d6',
      brightCyan: '#29b8db',
      brightWhite: '#ffffff'
    },
    allowProposedApi: true
  })

  fitAddon = new FitAddon()
  terminal.loadAddon(fitAddon)
  terminal.open(terminalContainer.value)
  
  // 初始适配大小
  setTimeout(() => {
    fitAddon?.fit()
    updateTerminalSize()
  }, 100)

  // 监听终端容器大小变化
  resizeObserver = new ResizeObserver(() => {
    fitAddon?.fit()
    updateTerminalSize()
  })
  resizeObserver.observe(terminalContainer.value)

  // 监听全局对话框最大化事件
  const dialogEl = dialogWrapper.value?.querySelector('.el-dialog')
  if (dialogEl) {
    dialogEl.addEventListener('dialog-maximize-change', handleDialogMaximize)
  }

  // 监听终端输入
  terminal.onData((data) => {
    if (hubConnection && isConnected.value) {
      hubConnection.invoke('SendInput', data).catch(err => {
        console.error('发送输入失败:', err)
      })
    }
  })

  // 连接 SignalR
  await connectSignalR()
}

// 连接 SignalR Hub
const connectSignalR = async () => {
  if (!connectionInfo.value) {
    terminal?.writeln('\r\n\x1b[31m错误: 缺少连接信息\x1b[0m\r\n')
    return
  }

  connecting.value = true
  terminal?.writeln('\x1b[33m正在连接...\x1b[0m\r\n')

  try {
    // 创建 SignalR 连接
    hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/sshTerminalHub', {
        accessTokenFactory: () => localStorage.getItem('token') || ''
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    // 监听输出
    hubConnection.on('Output', (output: string) => {
      terminal?.write(output)
    })

    // 监听连接成功
    hubConnection.on('Connected', (info: any) => {
      isConnected.value = true
      sessionStartTime.value = new Date()
      startDurationTimer()
      terminal?.writeln(`\x1b[32m已连接到 ${info.Host}:${info.Port}\x1b[0m\r\n`)
      // 聚焦终端以接收键盘输入
      terminal?.focus()
    })

    // 监听断开
    hubConnection.on('Disconnected', () => {
      isConnected.value = false
      stopDurationTimer()
      terminal?.writeln('\r\n\x1b[33m连接已断开\x1b[0m\r\n')
    })

    // 监听错误 - 支持新的错误码格式和旧的字符串格式
    hubConnection.on('Error', (error: any) => {
      // 检查是否为错误码格式
      if (typeof error === 'object' && error.code) {
        terminal?.writeln(`\r\n\x1b[31m错误: ${error.message}\x1b[0m\r\n`)
      } else {
        // 旧格式：直接是字符串
        terminal?.writeln(`\r\n\x1b[31m错误: ${error}\x1b[0m\r\n`)
      }
    })

    // 连接断开处理
    hubConnection.onclose(() => {
      isConnected.value = false
      stopDurationTimer()
    })

    // 启动连接
    await hubConnection.start()

    // 发送连接请求
    await hubConnection.invoke('Connect', {
      host: connectionInfo.value.host,
      port: connectionInfo.value.port,
      username: connectionInfo.value.username,
      password: connectionInfo.value.password,
      privateKeyPath: connectionInfo.value.privateKeyPath,
      passphrase: connectionInfo.value.privateKeyPassphrase,
      cols: terminalSize.value.cols,
      rows: terminalSize.value.rows,
      timeout: 30
    })

  } catch (error: any) {
    console.error('SignalR 连接失败:', error)
    terminal?.writeln(`\r\n\x1b[31m连接失败: ${error.message}\x1b[0m\r\n`)
    ElMessage.error('SSH 连接失败')
  } finally {
    connecting.value = false
  }
}

// 更新终端大小
const updateTerminalSize = () => {
  if (terminal) {
    terminalSize.value = {
      cols: terminal.cols,
      rows: terminal.rows
    }
    
    // 通知后端调整大小
    if (hubConnection && isConnected.value) {
      hubConnection.invoke('Resize', terminal.cols, terminal.rows).catch(() => {})
    }
  }
}

// 重新连接
const reconnect = async () => {
  await disconnectSignalR()
  terminal?.clear()
  await connectSignalR()
}

// 断开 SignalR
const disconnectSignalR = async () => {
  if (hubConnection) {
    try {
      await hubConnection.invoke('Disconnect')
      await hubConnection.stop()
    } catch (e) {
      // 忽略断开时的错误
    }
    hubConnection = null
  }
  isConnected.value = false
  stopDurationTimer()
}

// 清屏
const clearTerminal = () => {
  terminal?.clear()
}

// 聚焦终端
const focusTerminal = () => {
  terminal?.focus()
}

// 处理全局最大化事件（由 dialogMaximize 插件触发）
const handleDialogMaximize = () => {
  // 延迟重新适配终端大小
  setTimeout(() => {
    fitAddon?.fit()
    updateTerminalSize()
    terminal?.focus()
  }, 100)
}

// 浏览器全屏切换
const toggleBrowserFullscreen = async () => {
  // 使用 wrapper 中的 dialog 元素
  const dialogEl = dialogWrapper.value?.querySelector('.el-dialog') as HTMLElement
  if (!dialogEl) {
    console.error('找不到对话框元素')
    return
  }

  try {
    if (!document.fullscreenElement) {
      await dialogEl.requestFullscreen()
      isBrowserFullscreen.value = true
    } else {
      await document.exitFullscreen()
      isBrowserFullscreen.value = false
    }
    // 延迟重新适配终端大小
    setTimeout(() => {
      fitAddon?.fit()
      updateTerminalSize()
      terminal?.focus()
    }, 200)
  } catch (err) {
    console.error('全屏切换失败:', err)
  }
}

// 监听全屏状态变化
const handleFullscreenChange = () => {
  isBrowserFullscreen.value = !!document.fullscreenElement
  setTimeout(() => {
    fitAddon?.fit()
    updateTerminalSize()
  }, 100)
}

// 添加全屏事件监听
if (typeof document !== 'undefined') {
  document.addEventListener('fullscreenchange', handleFullscreenChange)
}

// 关闭对话框
const handleClose = async () => {
  await disconnectSignalR()
  
  resizeObserver?.disconnect()
  terminal?.dispose()
  terminal = null
  fitAddon = null
  
  visible.value = false
}

// 会话时长计时器
const startDurationTimer = () => {
  durationTimer = setInterval(() => {
    if (sessionStartTime.value) {
      const duration = Date.now() - sessionStartTime.value.getTime()
      const hours = Math.floor(duration / 3600000)
      const minutes = Math.floor((duration % 3600000) / 60000)
      const seconds = Math.floor((duration % 60000) / 1000)
      sessionDuration.value = `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
    }
  }, 1000)
}

const stopDurationTimer = () => {
  if (durationTimer) {
    clearInterval(durationTimer)
    durationTimer = null
  }
}

// 生命周期
onUnmounted(() => {
  disconnectSignalR()
  resizeObserver?.disconnect()
  terminal?.dispose()
  stopDurationTimer()
})
</script>

<!-- 全局样式 - Element Plus dialog 使用 teleport 到 body，需要非 scoped 样式 -->
<style>
/* SSH Terminal Dialog - 必须是全局样式因为 el-dialog teleport 到 body */
.ssh-terminal-dialog.el-dialog {
  display: flex !important;
  flex-direction: column !important;
  max-height: 90vh;
  overflow: hidden !important;
}

/* 非全屏和非最大化模式时设置固定高度 */
.ssh-terminal-dialog.el-dialog:not(.is-fullscreen):not(.is-maximized) {
  height: 70vh !important;
}

.ssh-terminal-dialog .el-dialog__header {
  flex-shrink: 0;
  padding: 12px 16px;
  border-bottom: 1px solid #e4e7ed;
}

.ssh-terminal-dialog .el-dialog__body {
  flex: 1 1 auto !important;
  padding: 0 !important;
  display: flex !important;
  flex-direction: column !important;
  min-height: 0;
  overflow: hidden !important;
}

/* 全屏模式下的对话框 */
.ssh-terminal-dialog.el-dialog.is-fullscreen {
  max-height: 100vh;
  height: 100vh !important;
  overflow: hidden !important;
}

.ssh-terminal-dialog.el-dialog.is-fullscreen .el-dialog__body {
  flex: 1 1 auto !important;
  height: auto !important;
  min-height: 0;
  overflow: hidden !important;
}

/* 浏览器全屏模式 */
.ssh-terminal-dialog.el-dialog:fullscreen {
  width: 100% !important;
  height: 100% !important;
  max-height: 100vh !important;
  margin: 0 !important;
  border-radius: 0 !important;
  overflow: hidden !important;
  background: #1e1e1e !important;
}

/* 全屏时隐藏对话框 header */
.ssh-terminal-dialog.el-dialog:fullscreen .el-dialog__header {
  display: none !important;
}

.ssh-terminal-dialog.el-dialog:fullscreen .el-dialog__body {
  flex: 1 1 auto !important;
  height: 100% !important;
  min-height: 0;
  overflow: hidden !important;
  padding: 0 !important;
}

/* 全屏时终端容器占满整个空间 */
.ssh-terminal-dialog.el-dialog:fullscreen .terminal-container {
  height: 100% !important;
  border-radius: 0 !important;
}
</style>

<style scoped>
/* 工具栏 */
.terminal-toolbar {
  flex-shrink: 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 16px;
  background: #f5f7fa;
  border-bottom: 1px solid #e4e7ed;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.connection-info {
  font-family: 'JetBrains Mono', monospace;
  font-size: 13px;
  color: #606266;
}

.toolbar-right {
  display: flex;
  gap: 8px;
}

/* 终端容器 - 关键布局 */
.terminal-container {
  flex: 1;
  min-height: 0;
  background: #1e1e1e;
  padding: 4px;
  overflow: hidden;
}

.terminal-container :deep(.xterm) {
  height: 100% !important;
  width: 100% !important;
}

.terminal-container :deep(.xterm-screen) {
  height: 100% !important;
  width: 100% !important;
}

.terminal-container :deep(.xterm-viewport) {
  overflow-y: auto !important;
}

/* 状态栏 */
.terminal-status-bar {
  flex-shrink: 0;
  display: flex;
  justify-content: space-between;
  padding: 4px 16px;
  background: #1e1e1e;
  color: #909399;
  font-size: 12px;
  font-family: 'JetBrains Mono', monospace;
  border-top: 1px solid #333;
}

/* 全屏退出浮动按钮 */
.fullscreen-exit-btn {
  position: fixed;
  top: 16px;
  right: 16px;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  z-index: 10000;
  transition: all 0.25s ease;
  color: #fff;
  font-size: 18px;
  opacity: 0.3;
}

.fullscreen-exit-btn:hover {
  background: rgba(239, 68, 68, 0.8);
  opacity: 1;
  transform: scale(1.1);
}
</style>
