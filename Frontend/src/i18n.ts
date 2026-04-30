import { createI18n } from 'vue-i18n'

// 基础翻译 - 始终加载
import zhCommon from './locales/modules/zh-CN/common'
import zhPagination from './locales/modules/zh-CN/pagination'
import zhLayout from './locales/modules/zh-CN/layout'
import zhValidation from './locales/modules/zh-CN/validation'
import zhNode from './locales/modules/zh-CN/node'

import enCommon from './locales/modules/en-US/common'
import enPagination from './locales/modules/en-US/pagination'
import enLayout from './locales/modules/en-US/layout'
import enValidation from './locales/modules/en-US/validation'
import enNode from './locales/modules/en-US/node'

// 支持的语言列表
export const SUPPORTED_LOCALES = [
  { code: 'zh-CN', name: '简体中文', nativeName: '简体中文' },
  { code: 'en-US', name: 'English', nativeName: 'English' }
] as const

export type SupportedLocale = typeof SUPPORTED_LOCALES[number]['code']

// localStorage key
const LOCALE_STORAGE_KEY = 'dockerpanel-locale'

// 已加载的模块缓存
const loadedModules = new Set<string>()

// 基础翻译
// 'zh' and 'en' are added as aliases for fallback compatibility
// vue-i18n fallback chain strips region codes (zh-CN → zh, en-US → en)
const zhMessages = {
  common: zhCommon,
  pagination: zhPagination,
  layout: zhLayout,
  validation: zhValidation,
  node: zhNode
}

const enMessages = {
  common: enCommon,
  pagination: enPagination,
  layout: enLayout,
  validation: enValidation,
  node: enNode
}

// Create separate message objects for each locale to avoid shared references
// This ensures that when we update messages for 'zh-CN', 'zh' (alias) is not affected
// and vice versa
const baseMessages = {
  'zh-CN': { ...zhMessages },
  'zh': { ...zhMessages },
  'en-US': { ...enMessages },
  'en': { ...enMessages }
}

/**
 * 检测浏览器首选语言
 */
function detectBrowserLocale(): SupportedLocale {
  // 获取浏览器语言列表
  const browserLanguages = navigator.languages || [navigator.language]
  
  for (const lang of browserLanguages) {
    // 精确匹配
    if (lang === 'zh-CN' || lang === 'zh-Hans' || lang === 'zh') {
      return 'zh-CN'
    }
    if (lang === 'en-US' || lang === 'en') {
      return 'en-US'
    }
    
    // 前缀匹配
    if (lang.startsWith('zh')) {
      return 'zh-CN'
    }
    if (lang.startsWith('en')) {
      return 'en-US'
    }
  }
  
  // 默认返回中文
  return 'zh-CN'
}

/**
 * 获取保存的语言设置
 */
function getSavedLocale(): SupportedLocale | null {
  const saved = localStorage.getItem(LOCALE_STORAGE_KEY)
  if (saved && SUPPORTED_LOCALES.some(l => l.code === saved)) {
    return saved as SupportedLocale
  }
  return null
}

/**
 * 检查是否是支持的语言
 */
function checkIsSupportedLocale(locale: string): locale is SupportedLocale {
  return SUPPORTED_LOCALES.some(l => l.code === locale)
}

/**
 * 确定初始语言
 * 优先级：后端注入 > 用户设置 > 浏览器语言 > 默认语言
 */
function determineInitialLocale(): SupportedLocale {
  // 0. 检查后端注入的初始语言（ASP.NET Core 集成场景）
  // 后端可以在 index.html 中注入: <script>window.__INITIAL_LOCALE__='en-US'</script>
  const injectedLocale = (window as any).__INITIAL_LOCALE__
  if (injectedLocale && checkIsSupportedLocale(injectedLocale)) {
    // 保存到 localStorage 以便后续使用
    localStorage.setItem(LOCALE_STORAGE_KEY, injectedLocale)
    return injectedLocale
  }

  // 1. 检查用户保存的设置
  const savedLocale = getSavedLocale()
  if (savedLocale) {
    return savedLocale
  }

  // 2. 检测浏览器语言
  const browserLocale = detectBrowserLocale()

  // 3. 返回检测到的语言或默认语言
  return browserLocale || 'zh-CN'
}

// 创建 i18n 实例
const i18n = createI18n({
  legacy: false,
  locale: determineInitialLocale(),
  fallbackLocale: 'en-US',
  messages: baseMessages
})

// 模块映射 - 路由到翻译模块
const routeModuleMap: Record<string, string[]> = {
  'dashboard': ['dashboard', 'metrics'],
  'containers': ['container', 'containerTemplate', 'createContainer', 'fileManager', 'metrics'],
  'container-detail': ['container', 'containerTemplate', 'createContainer', 'fileManager', 'metrics'],
  'images': ['image', 'registry', 'createContainer'],
  'networks': ['network'],
  'volumes': ['volume', 'fileManager'],
  'yarp': ['proxy'],
  'nodes': ['node'],
  'compose': ['compose'],
  'registries': ['registry'],
  'certificates': ['certificate'],
  'audit': ['audit'],
  'ssh': ['ssh'],
  'settings': ['settings'],
  'users': ['users']
}

// Locale alias mapping for fallback compatibility
const localeAliases: Record<string, string> = {
  'zh-CN': 'zh',
  'en-US': 'en'
}

/**
 * 异步加载翻译模块
 */
async function loadLocaleModule(locale: string, module: string): Promise<void> {
  const key = `${locale}-${module}`
  if (loadedModules.has(key)) return

  try {
    const messages = await import(`./locales/modules/${locale}/${module}.ts`)
    const currentMessages = i18n.global.messages.value[locale] || {}
    i18n.global.messages.value[locale] = {
      ...currentMessages,
      [module]: messages.default
    }
    
    // Also update the alias locale for fallback compatibility
    const alias = localeAliases[locale]
    if (alias) {
      const aliasMessages = i18n.global.messages.value[alias] || {}
      i18n.global.messages.value[alias] = {
        ...aliasMessages,
        [module]: messages.default
      }
    }
    
    loadedModules.add(key)
  } catch (e) {
    console.warn(`Failed to load locale module: ${locale}/${module}`, e)
  }
}

/**
 * 批量加载多个模块
 */
async function loadLocaleModules(locale: string, modules: string[]): Promise<void> {
  await Promise.all(modules.map(m => loadLocaleModule(locale, m)))
}

/**
 * 根据路由名称加载翻译
 */
export async function loadLocaleForRoute(routeName: string): Promise<void> {
  const locale = i18n.global.locale.value
  const modules = routeModuleMap[routeName] || []
  
  const fallbackLocale = i18n.global.fallbackLocale.value as string
  await Promise.all([
    loadLocaleModules(locale, modules),
    loadLocaleModules(fallbackLocale, modules)
  ])
}

/**
 * 预加载常用模块
 */
export async function preloadCommonModules(): Promise<void> {
  const locale = i18n.global.locale.value
  const fallbackLocale = i18n.global.fallbackLocale.value as string
  
  const commonModules = ['dashboard', 'container', 'containerTemplate', 'metrics']
  await Promise.all([
    loadLocaleModules(locale, commonModules),
    loadLocaleModules(fallbackLocale, commonModules)
  ])
}

/**
 * 切换语言
 * @param locale 目标语言
 * @param persist 是否持久化保存（默认 true）
 */
export async function setLocale(locale: SupportedLocale, persist: boolean = true): Promise<void> {
  if (i18n.global.locale.value === locale) return
  
  i18n.global.locale.value = locale
  
  // 持久化保存
  if (persist) {
    localStorage.setItem(LOCALE_STORAGE_KEY, locale)
  }
  
  // 更新 HTML lang 属性
  document.documentElement.lang = locale
  
  // 重新加载已使用过的模块
  // loadedModules key 格式: "zh-CN-dashboard" 或 "en-US-dashboard"
  // 需要正确解析出模块名
  const modulesToReload = Array.from(loadedModules)
    .filter(k => !k.startsWith(locale))
    .map(k => {
      // 找到第一个分隔符后的模块名
      // zh-CN-dashboard -> dashboard
      // en-US-dashboard -> dashboard
      const parts = k.split('-')
      // 对于 zh-CN-xxx 格式，前两部分是 locale，剩余的是 module
      // 对于 en-US-xxx 格式，前两部分是 locale，剩余的是 module
      if (parts.length >= 3 && (parts[0] + '-' + parts[1]).match(/^(zh-CN|en-US)$/)) {
        return parts.slice(2).join('-')
      }
      // fallback: 取最后一个部分
      return parts[parts.length - 1]
    })
  
  await loadLocaleModules(locale, modulesToReload)
}

/**
 * 获取当前语言
 */
export function getLocale(): SupportedLocale {
  return i18n.global.locale.value as SupportedLocale
}

/**
 * 获取当前语言的 HTTP Accept-Language 格式
 */
export function getAcceptLanguageHeader(): string {
  const currentLocale = getLocale()
  const fallbackLocale = i18n.global.fallbackLocale.value as string
  
  if (currentLocale === fallbackLocale) {
    return currentLocale
  }
  
  // 返回 "zh-CN, zh-CN;q=0.9, en-US;q=0.8" 格式
  return `${currentLocale}, ${currentLocale};q=0.9, ${fallbackLocale};q=0.8`
}

/**
 * 检查是否是支持的语言
 */
export function isSupportedLocale(locale: string): locale is SupportedLocale {
  return SUPPORTED_LOCALES.some(l => l.code === locale)
}

/**
 * 根据后端错误代码获取前端翻译
 * 后端返回格式: { code: 'ERROR_CODE', message: '原始消息' }
 */
export function getLocalizedErrorMessage(error: { code?: string; message?: string }): string {
  const { code, message } = error
  const t = i18n.global.t
  
  // 如果有错误代码，尝试翻译
  if (code) {
    const key = `common.errors.${code}`
    const translated = t(key)
    // 如果翻译存在（不等于 key 本身），返回翻译
    if (translated !== key) {
      return translated
    }
  }
  
  // 返回原始消息
  return message || t('common.operationFailed')
}

// 初始化时设置 HTML lang 属性
document.documentElement.lang = i18n.global.locale.value

export default i18n
