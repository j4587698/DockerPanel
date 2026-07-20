import axios from "axios"
import type { AxiosRequestConfig } from "axios"
import { ElMessage } from "element-plus"
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

    // 对响应错误做点什么
    let message = "请求失败"

    if (error.code === "ECONNABORTED") {
      message = "请求超时，请检查网络连接或稍后重试"
    } else if (error.response) {
      const status = error.response.status
      const data = error.response.data

      if (status === 400) {
        message = data?.message || "请求参数错误"
      } else if (status === 401) {
        const originalRequest = error.config
        const code = data?.code

        // refresh 接口自身失败：必然需要重新登录
        if (originalRequest.url === '/auth/refresh') {
          forceLogout()
          return Promise.reject(error)
        }

        // 明确需要重新登录的认证错误（refresh 失效/账户禁用），直接登出
        if (code === 'REFRESH_EXPIRED' || code === 'REFRESH_INVALID' || code === 'ACCOUNT_DISABLED') {
          forceLogout()
          return Promise.reject(error)
        }

        // 业务类凭证错误（如登录密码错误）：不登出，仅把消息交给调用方提示
        if (code === 'INVALID_CREDENTIALS') {
          message = getLocalizedErrorMessage({ code }) || data?.message || "用户名或密码错误"
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
      } else if (status === 403) {
        // CSRF 头缺失等防御性拦截：不登出，交由调用方提示或重试
        if (data?.code === 'CSRF_INVALID') {
          message = getLocalizedErrorMessage({ code: data.code }) || data?.message || "请求被安全策略拒绝（缺少必要请求头）"
        } else {
          message = data?.message || "拒绝访问"
        }
      } else if (status === 404) {
        message = data?.message || "请求资源不存在"
      } else if (status >= 500) {
        // 尝试使用后端返回的错误代码进行翻译
        if (data?.code || data?.error) {
          message = getLocalizedErrorMessage({
            code: data.code,
            message: data.error || data.message
          })
        } else {
          message = data?.error || data?.message || `服务器错误 (${status})`
        }
      } else {
        message = data?.message || `请求失败 (${status})`
      }
    } else if (error.request) {
      message = "网络连接失败，请检查网络设置"
    } else {
      message = error.message || "请求失败"
    }

    console.error("API请求错误:", error)

    // 覆盖 error.message，让调用方获取正确的错误信息
    error.message = message

    return Promise.reject(error)
  }
)

export default api
