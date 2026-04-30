<template>
  <el-container class="app-layout" :class="layoutClasses">
    <!-- 移动端遮罩 -->
    <transition name="fade">
      <div 
        v-if="isMobile && isMobileMenuOpen" 
        class="mobile-overlay" 
        @click="closeMobileMenu"
      />
    </transition>

    <!-- Sidebar -->
    <el-aside 
      :width="sidebarWidth" 
      class="app-sidebar"
      :class="{ 'mobile-open': isMobile && isMobileMenuOpen }"
    >
      <!-- Brand Section -->
      <div class="sidebar-brand">
        <AppLogo :name="settingsStore.systemName || APP_NAME" :show-text="!isCollapsed || isMobile" size="sm" />
      </div>

      <!-- Navigation Menu -->
      <el-scrollbar class="sidebar-scroll">
        <nav class="sidebar-nav">
          <router-link
            v-for="item in menuItems"
            :key="item.path"
            :to="item.path"
            class="nav-item"
            :class="{ 'is-active': isActiveRoute(item.path) }"
            @click="onNavClick"
          >
            <div class="nav-icon">
              <el-icon><component :is="item.icon" /></el-icon>
            </div>
            <transition name="fade">
              <span v-show="!isCollapsed || isMobile" class="nav-label">{{ item.title }}</span>
            </transition>
            <transition name="fade">
              <span v-show="(!isCollapsed || isMobile) && item.badge" class="nav-badge">{{ item.badge }}</span>
            </transition>
          </router-link>
        </nav>
      </el-scrollbar>

      <!-- Sidebar Footer -->
      <div class="sidebar-footer" v-if="!isMobile">
        <button class="collapse-toggle" @click="toggleSidebar">
          <el-icon :class="{ 'is-rotated': isCollapsed }">
            <ArrowLeft />
          </el-icon>
          <transition name="fade">
            <span v-show="!isCollapsed">{{ t('common.collapse') }}</span>
          </transition>
        </button>
      </div>
    </el-aside>

    <el-container class="main-container">
      <!-- Header -->
      <el-header class="app-header">
        <div class="header-left">
          <!-- 移动端汉堡菜单 -->
          <button v-if="isMobile" class="hamburger-btn" @click="toggleSidebar">
            <el-icon :size="20"><Fold v-if="isMobileMenuOpen" /><Expand v-else /></el-icon>
          </button>
          
          <!-- 节点选择器 -->
          <NodeSelector v-if="!isMobile" />
          
          <h2 class="header-title">{{ currentPageTitle }}</h2>
        </div>

        <div class="header-right">
          <!-- 后台任务 -->
          <button class="action-btn task-btn" @click="showTasksDrawer = true" :title="t('layout.backgroundTasks')">
            <el-icon><Clock /></el-icon>
            <span v-if="runningTaskCount > 0" class="task-badge">{{ runningTaskCount }}</span>
          </button>

          <!-- 主题切换 -->
          <div class="theme-switcher">
            <button 
              class="theme-btn"
              :class="{ active: theme === 'light' }"
              @click="setTheme('light')"
              :title="t('common.themeLight')"
            >
              <el-icon :size="16"><Sunny /></el-icon>
            </button>
            <button 
              class="theme-btn"
              :class="{ active: theme === 'dark' }"
              @click="setTheme('dark')"
              :title="t('common.themeDark')"
            >
              <el-icon :size="16"><Moon /></el-icon>
            </button>
            <button 
              class="theme-btn"
              :class="{ active: theme === 'auto' }"
              @click="setTheme('auto')"
              :title="t('common.themeAuto')"
            >
              <el-icon :size="16"><Monitor /></el-icon>
            </button>
          </div>

          <div class="header-divider hidden-xs"></div>

          <!-- Action Icons -->
          <div v-if="authStore.isAdmin" class="header-actions hidden-xs">
            <button class="action-btn" :title="t('common.settings')" @click="router.push('/settings')">
              <el-icon><Setting /></el-icon>
            </button>
          </div>

          <div class="header-divider hidden-xs"></div>

          <!-- User Profile -->
          <el-dropdown trigger="click" placement="bottom-end">
            <div class="user-profile">
              <div class="avatar">
                <el-icon><User /></el-icon>
              </div>
              <div class="user-info hidden-xs">
                <span class="user-name">{{ authStore.displayName }}</span>
              </div>
              <el-icon class="dropdown-icon hidden-xs"><ArrowDown /></el-icon>
            </div>
            <template #dropdown>
              <el-dropdown-menu class="user-dropdown-menu">
                <!-- 移动端显示设置入口 -->
                <el-dropdown-item v-if="isMobile && authStore.isAdmin" @click="router.push('/settings')">
                  <el-icon><Setting /></el-icon>
                  <span>{{ t('common.settings') }}</span>
                </el-dropdown-item>
                <el-dropdown-item v-if="authStore.isAdmin" @click="router.push('/users')">
                  <el-icon><User /></el-icon>
                  <span>{{ t('common.users') }}</span>
                </el-dropdown-item>
                <el-dropdown-item @click="openChangePasswordDialog">
                  <el-icon><Lock /></el-icon>
                  <span>{{ t('common.changePassword') }}</span>
                </el-dropdown-item>
                <el-dropdown-item @click="switchLanguage('zh-CN')">
                  <span>🇨🇳 {{ t('layout.langChinese') }}</span>
                </el-dropdown-item>
                <el-dropdown-item @click="switchLanguage('en-US')">
                  <span>🇺🇸 {{ t('layout.langEnglish') }}</span>
                </el-dropdown-item>
                <div class="dropdown-divider"></div>
                <el-dropdown-item class="logout-item" @click="handleLogout">
                  <el-icon><SwitchButton /></el-icon>
                  {{ t('common.logout') }}
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <!-- Main Content Area -->
      <el-main class="app-main">
        <router-view v-slot="{ Component }">
          <transition name="fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </el-main>

      <!-- 后台任务抽屉 -->
      <el-drawer
        v-model="showTasksDrawer"
        :title="t('layout.backgroundTasks')"
        direction="rtl"
        size="400px"
        :with-header="true"
      >
        <div class="tasks-container">
          <div class="tasks-header">
            <span>{{ t('common.tasksRunning', { count: runningTaskCount }) }}</span>
            <el-button size="small" text @click="clearCompletedTasks" :disabled="runningTaskCount === tasksStore.tasks.length">
              {{ t('common.tasksClearCompleted') }}
            </el-button>
          </div>
          
          <div v-if="tasksStore.tasks.length === 0" class="no-tasks">
            <el-icon :size="48"><Clock /></el-icon>
            <p>{{ t('common.tasksNoTasks') }}</p>
          </div>

          <div v-else class="tasks-list">
            <div v-for="task in tasksStore.tasks" :key="task.id" class="task-item">
              <div class="task-header">
                <span class="task-title">{{ task.title }}</span>
                <span class="task-status" :class="task.status">
                  {{ getStatusText(task.status) }}
                </span>
              </div>
              <el-progress 
                :percentage="task.progress" 
                :status="task.status === 'failed' ? 'exception' : (task.status === 'completed' ? 'success' : '')"
                :stroke-width="6"
              />
              <div v-if="task.detail || task.stream" class="task-info">
                <span v-if="task.detail" class="task-detail">{{ task.detail }}</span>
                <span v-if="task.stream" class="task-stream">{{ task.stream }}</span>
              </div>
              <div class="task-time">{{ formatTime(task.updatedAt) }}</div>
            </div>
          </div>
        </div>
      </el-drawer>

      <el-dialog
        v-model="showChangePasswordDialog"
        :title="t('common.changePassword')"
        width="460px"
        :show-close="!passwordChangeRequired"
        :close-on-click-modal="!passwordChangeRequired"
        :close-on-press-escape="!passwordChangeRequired"
        destroy-on-close
      >
        <el-alert
          v-if="passwordChangeRequired"
          class="change-password-alert"
          type="warning"
          show-icon
          :closable="false"
          :title="t('common.passwordChangeRequiredTitle')"
          :description="t('common.passwordChangeRequiredDesc')"
        />
        <el-form ref="changePasswordFormRef" :model="changePasswordForm" :rules="changePasswordRules" label-position="top">
          <el-form-item :label="t('common.currentPassword')" prop="currentPassword">
            <el-input v-model="changePasswordForm.currentPassword" type="password" show-password autocomplete="current-password" />
          </el-form-item>
          <el-form-item :label="t('common.newPassword')" prop="newPassword">
            <el-input v-model="changePasswordForm.newPassword" type="password" show-password autocomplete="new-password" />
          </el-form-item>
          <el-form-item :label="t('common.confirmPassword')" prop="confirmPassword">
            <el-input v-model="changePasswordForm.confirmPassword" type="password" show-password autocomplete="new-password" />
          </el-form-item>
        </el-form>
        <template #footer>
          <el-button v-if="passwordChangeRequired" @click="handleLogout">{{ t('common.logout') }}</el-button>
          <el-button v-else @click="showChangePasswordDialog = false">{{ t('common.cancel') }}</el-button>
          <el-button type="primary" :loading="changingPassword" @click="submitChangePassword">{{ t('common.save') }}</el-button>
        </template>
      </el-dialog>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed, reactive, ref, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { setLocale } from '../i18n'
import { useAppStore } from '../stores/app'
import { useAuthStore } from '../stores/auth'
import { useSettingsStore } from '../stores/settings'
import { useTasksStore } from '../stores/tasks'
import { authApi } from '@/api/auth'
import { formatLocalizedTime } from '@/utils/date'
import { tasksApi } from '@/api/tasks'
import { signalrService } from '../services/signalr'
import AppLogo from '@/components/common/AppLogo.vue'
import { APP_NAME } from '@/utils/branding'
import {
  Monitor, Box, Picture, Connection, Folder,
  Setting, User, SwitchButton,
  ArrowLeft, Grid, ArrowDown, Link, Document,
  Sunny, Moon, Fold, Expand, Clock, Lock
} from '@element-plus/icons-vue'
import NodeSelector from '@/components/NodeSelector.vue'

const route = useRoute()
const router = useRouter()
const { t, locale } = useI18n()
const appStore = useAppStore()
const authStore = useAuthStore()
const settingsStore = useSettingsStore()
const tasksStore = useTasksStore()

const showTasksDrawer = ref(false)

const isCollapsed = computed(() => appStore.isCollapsed)
const isMobile = computed(() => appStore.isMobile)
const isMobileMenuOpen = computed(() => appStore.isMobileMenuOpen)
const theme = computed(() => appStore.theme)
const layoutClasses = computed(() => appStore.layoutClasses)
const runningTaskCount = computed(() => tasksStore.runningCount)
const passwordChangeRequired = computed(() => Boolean(authStore.user?.mustChangePassword))

const showChangePasswordDialog = ref(false)
const changingPassword = ref(false)
const changePasswordFormRef = ref<FormInstance>()
const changePasswordForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const validateConfirmPassword = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
  if (!value) {
    callback(new Error(t('common.confirmPasswordRequired')))
    return
  }

  if (value !== changePasswordForm.newPassword) {
    callback(new Error(t('common.passwordMismatch')))
    return
  }

  callback()
}

const changePasswordRules: FormRules = {
  currentPassword: [{ required: true, message: t('common.currentPasswordRequired'), trigger: 'blur' }],
  newPassword: [
    { required: true, message: t('common.newPasswordRequired'), trigger: 'blur' },
    { min: 8, max: 128, message: t('common.passwordLength'), trigger: 'blur' }
  ],
  confirmPassword: [{ validator: validateConfirmPassword, trigger: 'blur' }]
}

const toggleSidebar = () => appStore.toggleSidebar()
const closeMobileMenu = () => appStore.closeMobileMenu()
const setTheme = (t: 'light' | 'dark' | 'auto') => appStore.setTheme(t)

const sidebarWidth = computed(() => {
  if (isMobile.value) return '280px'
  return isCollapsed.value ? '64px' : '260px'
})

const menuItems = computed(() => [
  { path: '/dashboard', title: t('common.dashboard'), icon: Monitor, visible: true },
  { path: '/containers', title: t('common.containers'), icon: Box, badge: '', visible: true },
  { path: '/images', title: t('common.images'), icon: Picture, visible: true },
  { path: '/networks', title: t('common.networks'), icon: Connection, visible: true },
  { path: '/volumes', title: t('common.volumes'), icon: Folder, visible: true },
  { path: '/yarp', title: t('common.yarp'), icon: Link, visible: authStore.canOperate },
  { path: '/certificates', title: t('common.certificates'), icon: Document, visible: authStore.canOperate },
  { path: '/compose', title: t('common.compose'), icon: Grid, visible: authStore.canOperate },
  { path: '/audit', title: t('common.audit'), icon: Document, visible: authStore.isAdmin },
  { path: '/ssh', title: t('common.ssh'), icon: Monitor, visible: authStore.isAdmin },
  { path: '/settings', title: t('common.settings'), icon: Setting, visible: authStore.isAdmin },
  { path: '/users', title: t('common.users'), icon: User, visible: authStore.isAdmin },
].filter(item => item.visible))

const isActiveRoute = (path: string) => {
  return route.path === path || route.path.startsWith(path + '/')
}

const currentPageTitle = computed(() => {
  const item = menuItems.value.find(i => isActiveRoute(i.path))
  return item ? item.title : t('common.dashboard')
})

const onNavClick = () => {
  if (isMobile.value) {
    closeMobileMenu()
  }
}

const switchLanguage = async (lang: string) => {
  // 使用 setLocale 进行持久化并更新 locale
  await setLocale(lang)
  // 通知后台语言变化
  signalrService.setLanguage(lang).catch(() => {})
}

const loadRemoteAppSettings = async () => {
  try {
    const publicSettings = await settingsStore.loadPublicSettings()
    if (publicSettings.theme !== appStore.theme) {
      appStore.setTheme(publicSettings.theme)
    }
    if (publicSettings.defaultLanguage && publicSettings.defaultLanguage !== locale.value) {
      await setLocale(publicSettings.defaultLanguage)
    }
  } catch (error) {
    console.warn('同步系统设置失败，使用本地缓存:', error)
  }
}

const handleLogout = async () => {
  await authStore.logout()
  signalrService.disconnect()
  await router.replace('/login')
}

const resetChangePasswordForm = () => {
  Object.assign(changePasswordForm, {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  })
  changePasswordFormRef.value?.clearValidate()
}

const openChangePasswordDialog = () => {
  resetChangePasswordForm()
  showChangePasswordDialog.value = true
}

const submitChangePassword = async () => {
  try {
    await changePasswordFormRef.value?.validate()
  } catch {
    return
  }

  changingPassword.value = true
  try {
    const user = await authApi.changePassword({
      currentPassword: changePasswordForm.currentPassword,
      newPassword: changePasswordForm.newPassword
    })
    authStore.user = user
    localStorage.setItem('user', JSON.stringify(user))
    showChangePasswordDialog.value = false
    ElMessage.success(t('common.passwordChanged'))
  } catch (error) {
    ElMessage.error((error as Error).message || t('common.saveFailed'))
  } finally {
    changingPassword.value = false
  }
}

watch(passwordChangeRequired, (required) => {
  if (required) {
    openChangePasswordDialog()
  }
}, { immediate: true })

// 后台任务相关
const getStatusText = (status: string) => {
  const statusMap: Record<string, string> = {
    pending: t('common.tasksPending'),
    running: t('common.tasksRunningStatus'),
    completed: t('common.tasksCompleted'),
    failed: t('common.tasksFailed')
  }
  return statusMap[status] || status
}

const formatTime = (date: Date) => {
  return formatLocalizedTime(date, '--')
}

const clearCompletedTasks = async () => {
  // 同时清理前端和后端
  tasksStore.clearCompleted()
  try {
    await tasksApi.clearCompleted()
  } catch (e) {
    console.error('清理后端任务失败:', e)
  }
}

// SignalR 监听
let unsubscribeBuildProgress: (() => void) | null = null
let unsubscribeDeployProgress: (() => void) | null = null
let unsubscribeOperationProgress: (() => void) | null = null
let unsubscribeVolumeProgress: (() => void) | null = null
let unsubscribePullProgress: (() => void) | null = null
let unsubscribePushProgress: (() => void) | null = null

onMounted(async () => {
  await loadRemoteAppSettings()

  // 确保 SignalR 连接已建立
  if (!signalrService.isConnected()) {
    try {
      await signalrService.connect()
    } catch (e) {
      console.error('SignalR 连接失败:', e)
    }
  }

  // 同步当前语言到后端
  signalrService.setLanguage(locale.value).catch(() => {})

  // 从后端同步进行中的任务
  await tasksStore.syncFromBackend()

  // 监听镜像构建进度
  unsubscribeBuildProgress = signalrService.subscribe('image-build-progress', (message) => {
    const data = message.data
    const existingTask = tasksStore.tasks.find(t => t.id === data.buildId)

    // 使用 status 字段判断状态
    const taskStatus = data.status || 'running'

    // 根据 stepKey 翻译 step
    const stepKey = data.stepKey || data.step
    const translatedStep = stepKey ? t(`image.${stepKey}`, stepKey) : data.step

    if (existingTask) {
      tasksStore.updateTask(data.buildId, {
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || translatedStep,
        stream: data.stream,
        error: data.isError ? data.detail : undefined
      })
    } else {
      tasksStore.addTask({
        id: data.buildId,
        type: 'image-build',
        title: t('common.buildingImage') + `: ${data.tag || data.buildId}`,
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || translatedStep,
        stream: data.stream
      })
    }
  })

  // 监听镜像拉取进度
  unsubscribePullProgress = signalrService.subscribe('image-pull-progress', (message) => {
    const data = message.data
    // 后端返回的 pullId 可能已经包含 "pull-" 前缀，也可能没有
    const taskId = data.pullId.startsWith('pull-') ? data.pullId : `pull-${data.pullId}`
    const existingTask = tasksStore.tasks.find(t => t.id === taskId)

    const taskStatus = data.status || (data.step === '完成' ? 'completed' : (data.step === '失败' ? 'failed' : 'running'))

    if (existingTask) {
      tasksStore.updateTask(taskId, {
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || data.step
      })
    } else {
      tasksStore.addTask({
        id: taskId,
        type: 'image-pull',
        title: t('common.pullingImage') + `: ${data.imageName}`,
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || data.step
      })
    }
  })

  // 监听镜像推送进度
  unsubscribePushProgress = signalrService.subscribe('image-push-progress', (message) => {
    const data = message.data
    // 后端返回的 pushId 可能已经包含 "push-" 前缀，也可能没有
    const taskId = data.pushId.startsWith('push-') ? data.pushId : `push-${data.pushId}`
    const existingTask = tasksStore.tasks.find(t => t.id === taskId)

    const taskStatus = data.status || (data.step === '完成' ? 'completed' : (data.step === '失败' ? 'failed' : 'running'))

    if (existingTask) {
      tasksStore.updateTask(taskId, {
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || data.step
      })
    } else {
      tasksStore.addTask({
        id: taskId,
        type: 'image-push',
        title: t('common.pushingImage') + `: ${data.imageName}`,
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || data.step
      })
    }
  })

  // 监听 Compose 部署进度
  unsubscribeDeployProgress = signalrService.subscribe('compose-deploy-progress', (message) => {
    const data = message.data
    const taskId = `deploy-${data.projectId}`
    const existingTask = tasksStore.tasks.find(t => t.id === taskId)

    const taskStatus = data.status || (data.progress >= 100 ? 'completed' : 'running')

    if (existingTask) {
      tasksStore.updateTask(taskId, {
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || data.step
      })
    } else {
      tasksStore.addTask({
        id: taskId,
        type: 'compose-deploy',
        title: t('common.deployingProject') + `: ${data.projectId}`,
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || data.step
      })
    }
  })

  // 监听 Compose 操作进度（启动/停止等）
  unsubscribeOperationProgress = signalrService.subscribe('compose-operation-progress', (message) => {
    const data = message.data
    const taskId = `operation-${data.projectName}`
    const existingTask = tasksStore.tasks.find(t => t.id === taskId)

    // 使用 status 字段判断状态
    const taskStatus = data.status || 'running'

    // 根据 stepKey 翻译 step
    const stepKey = data.stepKey || data.step
    const translatedStep = stepKey ? t(`compose.${stepKey}`, stepKey) : data.step

    if (existingTask) {
      tasksStore.updateTask(taskId, {
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || translatedStep
      })
    } else {
      tasksStore.addTask({
        id: taskId,
        type: 'compose-operation',
        title: t('common.composeOperation') + `: ${data.projectName}`,
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || translatedStep
      })
    }
  })

  // 监听卷打包进度
  unsubscribeVolumeProgress = signalrService.subscribe('volume-archive-progress', (message) => {
    const data = message.data
    const taskId = `volume-${data.volumeId}`
    const existingTask = tasksStore.tasks.find(t => t.id === taskId)

    const taskStatus = data.status || (data.progress >= 100 ? 'completed' : 'running')

    // 根据 stepKey 翻译 step
    const stepKey = data.stepKey || data.step
    const translatedStep = stepKey ? t(`volume.${stepKey}`, stepKey) : data.step

    if (existingTask) {
      tasksStore.updateTask(taskId, {
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || translatedStep
      })
    } else {
      tasksStore.addTask({
        id: taskId,
        type: 'volume-archive',
        title: t('common.archivingVolume') + `: ${data.volumeId}`,
        status: taskStatus,
        progress: data.progress,
        detail: data.detail || translatedStep
      })
    }
  })
})

onUnmounted(() => {
  if (unsubscribeBuildProgress) unsubscribeBuildProgress()
  if (unsubscribeDeployProgress) unsubscribeDeployProgress()
  if (unsubscribeOperationProgress) unsubscribeOperationProgress()
  if (unsubscribeVolumeProgress) unsubscribeVolumeProgress()
  if (unsubscribePullProgress) unsubscribePullProgress()
  if (unsubscribePushProgress) unsubscribePushProgress()
})
</script>

<style scoped>
.app-layout {
  height: 100vh;
  background: var(--bg-app);
}

/* 后台任务按钮 */
.task-btn {
  position: relative;
}

.task-badge {
  position: absolute;
  top: -4px;
  right: -4px;
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  background: #3b82f6;
  color: white;
  font-size: 10px;
  font-weight: 600;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
}

html.dark .task-badge {
  background: #60a5fa;
}

.change-password-alert {
  margin-bottom: 16px;
}

/* 后台任务抽屉 */
.tasks-container {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.tasks-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--border-color);
  margin-bottom: 16px;
  color: var(--text-secondary);
  font-size: 14px;
}

.no-tasks {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: var(--text-muted);
}

.no-tasks p {
  margin-top: 16px;
  font-size: 14px;
}

.tasks-list {
  flex: 1;
  overflow-y: auto;
}

.task-item {
  padding: 16px;
  background: var(--bg-subtle);
  border-radius: var(--border-radius);
  margin-bottom: 12px;
}

.task-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.task-title {
  font-weight: 500;
  color: var(--text-main);
  font-size: 14px;
}

.task-status {
  font-size: 12px;
  padding: 2px 8px;
  border-radius: 4px;
}

.task-status.running {
  background: rgba(59, 130, 246, 0.1);
  color: #3b82f6;
}

.task-status.completed {
  background: rgba(34, 197, 94, 0.1);
  color: #22c55e;
}

.task-status.failed {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.task-status.pending {
  background: rgba(156, 163, 175, 0.1);
  color: #9ca3af;
}

.task-detail {
  font-size: 12px;
  color: var(--text-secondary);
}

.task-stream {
  font-size: 11px;
  font-family: monospace;
  color: var(--text-muted);
}

.task-info {
  margin-top: 6px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.task-time {
  margin-top: 6px;
  font-size: 11px;
  color: var(--text-muted);
}

/* === Mobile Overlay === */
.mobile-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: 199;
  backdrop-filter: blur(2px);
}

/* === Sidebar === */
.app-sidebar {
  background: var(--bg-sidebar);
  border-right: 1px solid var(--border-color);
  display: flex;
  flex-direction: column;
  transition: width 0.3s ease;
  z-index: 200;
  overflow: hidden;
}

/* 移动端侧边栏：固定定位 + 默认隐藏 */
.is-mobile .app-sidebar {
  position: fixed;
  top: 0;
  left: 0;
  bottom: 0;
  width: 280px !important;
  transform: translateX(-100%);
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: none;
}

.is-mobile .app-sidebar.mobile-open {
  transform: translateX(0);
  box-shadow: 4px 0 24px rgba(0, 0, 0, 0.15);
}

.sidebar-brand {
  height: 64px;
  display: flex;
  align-items: center;
  padding: 0 16px;
  border-bottom: 1px solid var(--border-color);
  flex-shrink: 0;
}

/* Navigation */
.sidebar-scroll {
  flex: 1;
  overflow: hidden;
}

.sidebar-nav {
  padding: 16px 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: var(--border-radius-sm);
  color: var(--text-secondary);
  text-decoration: none;
  transition: all 0.2s ease;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
}

.nav-item:hover {
  background: var(--bg-subtle);
  color: var(--text-main);
}

.nav-item.is-active {
  background: var(--color-primary-light);
  color: var(--text-main);
  font-weight: 600;
}

.nav-item.is-active .nav-icon {
  color: var(--color-primary);
}

.nav-icon {
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  flex-shrink: 0;
}

.nav-label {
  white-space: nowrap;
}

.nav-badge {
  margin-left: auto;
  background: var(--color-primary);
  color: var(--text-inverse);
  font-size: 11px;
  padding: 2px 6px;
  border-radius: 10px;
}

/* Sidebar Footer */
.sidebar-footer {
  padding: 16px;
  border-top: 1px solid var(--border-color);
  flex-shrink: 0;
}

.collapse-toggle {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 8px 12px;
  border-radius: var(--border-radius-sm);
  border: 1px solid transparent;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 14px;
}

.collapse-toggle:hover {
  background: var(--bg-subtle);
  color: var(--text-main);
}

.collapse-toggle .el-icon {
  transition: transform 0.3s ease;
}

.collapse-toggle .el-icon.is-rotated {
  transform: rotate(180deg);
}

/* === Header === */
.app-header {
  height: 64px;
  background: var(--bg-surface);
  border-bottom: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  position: sticky;
  top: 0;
  z-index: 50;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.header-title {
  font-size: 18px;
  font-weight: 600;
  margin: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;
}

/* 汉堡菜单按钮 */
.hamburger-btn {
  width: 36px;
  height: 36px;
  border-radius: var(--border-radius-sm);
  border: 1px solid var(--border-color);
  background: var(--bg-surface);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: var(--text-secondary);
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.hamburger-btn:hover {
  background: var(--bg-subtle);
  color: var(--text-main);
}

/* 主题切换器 */
.theme-switcher {
  display: flex;
  align-items: center;
  background: var(--bg-subtle);
  border-radius: var(--border-radius);
  padding: 3px;
  gap: 2px;
  border: 1px solid var(--border-color);
}

.theme-btn {
  width: 30px;
  height: 30px;
  border-radius: var(--border-radius-sm);
  border: none;
  background: transparent;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: var(--text-muted);
  transition: all 0.2s ease;
}

.theme-btn:hover {
  color: var(--text-main);
  background: var(--bg-surface);
}

.theme-btn.active {
  background: var(--bg-surface);
  color: var(--color-secondary);
  box-shadow: var(--shadow-sm);
}

.action-btn {
  width: 36px;
  height: 36px;
  border-radius: var(--border-radius-sm);
  border: 1px solid var(--border-color);
  background: var(--bg-surface);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: var(--text-secondary);
  transition: all 0.2s ease;
}

.action-btn:hover {
  background: var(--bg-subtle);
  color: var(--text-main);
}

.header-divider {
  width: 1px;
  height: 24px;
  background: var(--border-color);
}

.user-profile {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  border-radius: var(--border-radius-sm);
  border: 1px solid transparent;
  cursor: pointer;
  transition: all 0.2s ease;
}

.user-profile:hover {
  background: var(--bg-subtle);
}

.avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: var(--bg-subtle);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-secondary);
}

.user-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-main);
}

.dropdown-icon {
  color: var(--text-muted);
  font-size: 12px;
}

/* === Main Content === */
.app-main {
  padding: 0;
  overflow: auto;
  background: var(--bg-app);
  flex: 1;
}

/* Transitions */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.dropdown-divider {
  height: 1px;
  background: var(--border-color);
  margin: 4px 0;
}

.logout-item {
  color: var(--color-danger) !important;
}

/* === Responsive === */

/* 隐藏工具类 */
@media (max-width: 768px) {
  .hidden-xs {
    display: none !important;
  }

  .app-header {
    padding: 0 12px;
    height: 56px;
  }

  .header-title {
    font-size: 16px;
  }

  .theme-switcher {
    padding: 2px;
  }

  .theme-btn {
    width: 28px;
    height: 28px;
  }

  .user-profile {
    padding: 4px 8px;
  }
}

@media (max-width: 480px) {
  .header-right {
    gap: 8px;
  }
}
</style>
