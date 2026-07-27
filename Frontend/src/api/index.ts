import axios from "axios"
import type { AxiosRequestConfig } from "axios"
import { getAcceptLanguageHeader, getLocalizedErrorMessage } from "../i18n"

// 扩展配置类型
interface ApiRequestConfig extends AxiosRequestConfig {
  skipErrorHandler?: boolean
  skipAuth?: boolean
}

// 创建axios实例
const api = axios.create({
  baseURL: "/api",
  timeout: 30000, // 增加超时时间到30秒
  withCredentials: true, // 允许携带 HttpOnly Cookie
  headers: {
    "Content-Type": "application/json"
  }
})

const forceLogout = () => {
  localStorage.removeItem("tokenExpiresAt")
  localStorage.removeItem("user")
  if (window.location.pathname !== '/login') {
    window.location.href = '/login'
  }
}

// 单飞（single-flight）保证同时刻只有一个刷新在执行，所有并发调用共享其结果
let refreshInFlight: Promise<any> | null = null

// 跨标签页安全的 Token 刷新机制（统一入口，所有调用方复用）
export async function safeRefreshToken(): Promise<any> {
  if (refreshInFlight) {
    return refreshInFlight
  }

  const doRefresh = async () => {
    const expiresAtStr = localStorage.getItem("tokenExpiresAt")
    if (expiresAtStr && new Date(expiresAtStr).getTime() > Date.now() + 60000) {
      return Promise.resolve() // 未过期，直接使用
    }
    const res: any = await api.post('/auth/refresh', {}, { withCredentials: true })
    if (res && res.expiresAt) {
      localStorage.setItem("tokenExpiresAt", res.expiresAt)
      if (res.user) {
        localStorage.setItem("user", JSON.stringify(res.user))
      }
    }
    return res
  }

  // 1. 优先使用浏览器标准互斥锁（支持跨标签页同步，避免多标签页互相使 refresh token 失效）
  if (navigator.locks) {
    refreshInFlight = navigator.locks.request('dockerpanel_auth_refresh', doRefresh)
  } else {
    // 2. 降级方案：当前标签页单飞
    refreshInFlight = doRefresh()
  }

  try {
    return await refreshInFlight
  } finally {
    refreshInFlight = null
  }
}

// 请求拦截器
api.interceptors.request.use(
  (config: ApiRequestConfig) => {
    // 统一由 baseURL 提供 `/api` 前缀，兼容历史代码里写成 `/api/...` 的路径，避免请求变成 `/api/api/...`。
    if (typeof config.url === "string" && config.url.startsWith("/api/")) {
      config.url = config.url.slice(4)
    }

    // 保存 skipErrorHandler 到自定义属性中
    if (config.skipErrorHandler) {
      config.headers = config.headers || {}
      ;(config as any)._skipErrorHandler = true
    }
    
    // 添加 Accept-Language 头，让后端知道客户端语言
    config.headers = config.headers || {}
    config.headers["Accept-Language"] = getAcceptLanguageHeader()
    
    // 添加防 CSRF 的自定义 Header
    config.headers["X-DockerPanel-Api"] = "1"
    
    return config
  },
  (error) => {
    // 对请求错误做些什么
    return Promise.reject(error)
  }
)

// 从后端响应体中解析出可读的错误信息。
// 约定格式：{ code?: string, message?: string, error?: string }
// ASP.NET 模型验证失败时为 ProblemDetails：{ title, detail, errors: { field: string[] } }
const extractResponseMessage = (data: any): string => {
  if (!data || typeof data !== "object") {
    return typeof data === "string" ? data.trim() : ""
  }

  if (data.errors && typeof data.errors === "object") {
    const details = Object.values(data.errors as Record<string, unknown>)
      .flatMap((item) => (Array.isArray(item) ? item : [item]))
      .filter((item): item is string => typeof item === "string" && item.length > 0)
    if (details.length > 0) {
      return details.join("；")
    }
  }

  const raw = data.message || data.error || data.detail || data.title
  const rawText = typeof raw === "string" ? raw.trim() : ""
  if (data.code) {
    return getLocalizedErrorMessage({ code: String(data.code), message: rawText })
  }
  return rawText
}

const statusFallbackMessage = (status: number): string => {
  if (status === 400) return "请求参数错误"
  if (status === 401) return "登录状态已失效，请重新登录"
  if (status === 403) return "拒绝访问"
  if (status === 404) return "请求资源不存在"
  if (status === 423) return "账户已锁定，请稍后重试"
  if (status === 429) return "操作过于频繁，请稍后重试"
  if (status >= 500) return `服务器错误 (${status})`
  return `请求失败 (${status})`
}

const resolveErrorMessage = (status: number, data: any): string =>
  extractResponseMessage(data) || statusFallbackMessage(status)

// 响应拦截器
api.interceptors.response.use(
  (response) => {
    // 拦截器统一解包，返回响应体本身（已无旧代码依赖 AxiosResponse 的 .data）。
    return response.data
  },
  (error) => {
    // 检查是否跳过错误处理
    const skipErrorHandler = (error.config as any)?._skipErrorHandler
    if (skipErrorHandler) {
      return Promise.reject(error)
    }

    if (error.code === "ECONNABORTED") {
      error.message = "请求超时，请检查网络连接或稍后重试"
      return Promise.reject(error)
    }

    if (!error.response) {
      error.message = error.request ? "网络连接失败，请检查网络设置" : error.message || "请求失败"
      return Promise.reject(error)
    }

    const status = error.response.status
    const data = error.response.data

    // 先统一生成可读消息，保证任何提前返回的分支也带有正确的 error.message
    error.message = resolveErrorMessage(status, data)

    if (status === 401) {
      const originalRequest = error.config
      const code = data?.code

      // refresh 接口自身失败：必然需要重新登录
      if (originalRequest.url === "/auth/refresh") {
        forceLogout()
        return Promise.reject(error)
      }

      // 明确需要重新登录的认证错误（refresh 失效/账户禁用），直接登出
      if (code === "REFRESH_EXPIRED" || code === "REFRESH_INVALID" || code === "ACCOUNT_DISABLED") {
        forceLogout()
        return Promise.reject(error)
      }

      // 业务类凭证错误（如登录密码错误）：不登出，仅把消息交给调用方提示
      if (code === "INVALID_CREDENTIALS") {
        return Promise.reject(error)
      }

      // 其余 401（access token 过期/缺失）：尝试用 refresh token 续期。
      // safeRefreshToken 内部已保证单飞 + 跨标签页互斥，多个并发 401 共享同一刷新过程。
      if (!originalRequest._retry) {
        originalRequest._retry = true

        return safeRefreshToken()
          .then(() => api(originalRequest))
          .catch((refreshError) => {
            forceLogout()
            return Promise.reject(refreshError)
          })
      }

      forceLogout()
    }

    console.error("API请求错误:", error)

    return Promise.reject(error)
  }
)

export default api
