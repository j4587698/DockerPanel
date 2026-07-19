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

let isRefreshing = false
let failedQueue: Array<{ resolve: (value?: unknown) => void; reject: (reason?: any) => void }> = []

const processQueue = (error: any = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error)
    } else {
      prom.resolve()
    }
  })
  failedQueue = []
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
    // 项目内多数 API 调用直接使用拦截器解包后的数据；少量旧代码仍按 AxiosResponse 读取 `.data`。
    // 给普通对象/数组补一个不可枚举的自引用 `.data`，兼容旧调用且不影响序列化和正常字段遍历。
    const data = response.data
    if (data && typeof data === "object" && !("data" in data)) {
      try {
        Object.defineProperty(data, "data", {
          value: data,
          configurable: true,
          enumerable: false
        })
      } catch {
        // Blob、只读对象等无法扩展时保持原样返回。
      }
    }
    return data
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
        
        if (originalRequest.url === '/auth/refresh') {
          message = "登录已过期，请重新登录"
          localStorage.removeItem("tokenExpiresAt")
          localStorage.removeItem("user")
          if (window.location.pathname !== '/login') {
            window.location.href = '/login'
          }
          return Promise.reject(error)
        }

        if (!originalRequest._retry) {
          if (isRefreshing) {
            return new Promise((resolve, reject) => {
              failedQueue.push({ resolve, reject })
            }).then(() => {
              return api(originalRequest)
            }).catch(err => {
              return Promise.reject(err)
            })
          }
          
          originalRequest._retry = true
          isRefreshing = true
          
          return new Promise((resolve, reject) => {
            axios.post('/api/auth/refresh', {}, { withCredentials: true })
              .then(() => {
                processQueue(null)
                resolve(api(originalRequest))
              })
              .catch((refreshError) => {
                processQueue(refreshError)
                localStorage.removeItem("tokenExpiresAt")
                localStorage.removeItem("user")
                if (window.location.pathname !== '/login') {
                  window.location.href = '/login'
                }
                reject(refreshError)
              })
              .finally(() => {
                isRefreshing = false
              })
          })
        }
        
        message = "未授权，请重新登录"
        localStorage.removeItem("tokenExpiresAt")
        localStorage.removeItem("user")
        if (window.location.pathname !== '/login') {
          window.location.href = '/login'
        }
      } else if (status === 403) {
        message = "拒绝访问"
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
