import { defineStore } from 'pinia'
import { ref, computed, watch } from 'vue'

export const useAppStore = defineStore('app', () => {
  // 状态
  const isCollapsed = ref(false)
  const isMobileMenuOpen = ref(false)
  const isMobile = ref(false)
  const theme = ref<'light' | 'dark' | 'auto'>('auto')

  // 从localStorage加载主题设置
  const loadTheme = () => {
    const savedTheme = localStorage.getItem('app-theme')
    if (savedTheme && ['light', 'dark', 'auto'].includes(savedTheme)) {
      theme.value = savedTheme as 'light' | 'dark' | 'auto'
    }
  }

  // 获取实际应用的主题
  const actualTheme = computed(() => {
    if (theme.value === 'auto') {
      if (typeof window !== 'undefined') {
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
      }
      return 'light'
    }
    return theme.value
  })

  // 计算属性
  const layoutClasses = computed(() => ({
    'is-collapsed': isCollapsed.value,
    'is-mobile': isMobile.value,
    'theme-dark': actualTheme.value === 'dark'
  }))

  // 应用主题到DOM
  const applyTheme = () => {
    if (typeof document === 'undefined') return
    const root = document.documentElement
    if (actualTheme.value === 'dark') {
      root.classList.add('dark')
      root.setAttribute('data-theme', 'dark')
    } else {
      root.classList.remove('dark')
      root.setAttribute('data-theme', 'light')
    }
  }

  // 监听主题变化
  watch(theme, (newTheme) => {
    localStorage.setItem('app-theme', newTheme)
    applyTheme()
  })

  // 检测移动端
  const checkMobile = () => {
    if (typeof window === 'undefined') return
    const wasMobile = isMobile.value
    isMobile.value = window.innerWidth < 768
    // 从桌面切换到移动端时自动收起侧边栏
    if (isMobile.value && !wasMobile) {
      isMobileMenuOpen.value = false
    }
    // 从移动端切换到桌面时
    if (!isMobile.value && wasMobile) {
      isMobileMenuOpen.value = false
    }
  }

  // 监听系统主题变化
  const handleSystemThemeChange = () => {
    if (theme.value === 'auto') {
      applyTheme()
    }
  }

  // 方法
  const toggleSidebar = () => {
    if (isMobile.value) {
      isMobileMenuOpen.value = !isMobileMenuOpen.value
    } else {
      isCollapsed.value = !isCollapsed.value
    }
  }

  const closeMobileMenu = () => {
    isMobileMenuOpen.value = false
  }

  const setTheme = (newTheme: 'light' | 'dark' | 'auto') => {
    theme.value = newTheme
  }

  const toggleTheme = () => {
    if (theme.value === 'light') {
      setTheme('dark')
    } else if (theme.value === 'dark') {
      setTheme('auto')
    } else {
      setTheme('light')
    }
  }

  // 立即初始化（不依赖 onMounted）
  const init = () => {
    if (typeof window === 'undefined') return
    loadTheme()
    applyTheme()
    checkMobile()
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', handleSystemThemeChange)
    window.addEventListener('resize', checkMobile)
  }

  // 立即执行初始化
  init()

  return {
    isCollapsed,
    isMobileMenuOpen,
    isMobile,
    theme,
    actualTheme,
    layoutClasses,
    toggleSidebar,
    closeMobileMenu,
    setTheme,
    toggleTheme,
    checkMobile
  }
})
