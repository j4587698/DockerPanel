import { defineStore } from 'pinia'
import { authApi, type AuthRole, type AuthStatusResponse, type AuthUser, type LoginRequest, type SetupAdminRequest } from '@/api/auth'

const TOKEN_KEY = 'token'
const USER_KEY = 'user'
const EXPIRES_KEY = 'tokenExpiresAt'

function normalizeRole(role?: string | null): AuthRole {
  if (role === 'Admin') return 'Admin'
  if (role === 'Viewer') return 'Viewer'
  return 'Operator'
}

function readStoredUser(): AuthUser | null {
  const raw = localStorage.getItem(USER_KEY)
  if (!raw) return null

  try {
    return JSON.parse(raw) as AuthUser
  } catch {
    localStorage.removeItem(USER_KEY)
    return null
  }
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    expiresAt: localStorage.getItem(EXPIRES_KEY) || '',
    user: readStoredUser() as AuthUser | null,
    status: null as AuthStatusResponse | null,
    initialized: false
  }),

  getters: {
    isAuthenticated: (state) => state.user !== null,
    displayName: (state) => state.user?.displayName || state.user?.username || 'Admin',
    effectiveRole: (state): AuthRole => normalizeRole(state.user?.role),
    isAdmin: (state) => normalizeRole(state.user?.role) === 'Admin',
    isOperator: (state) => normalizeRole(state.user?.role) === 'Operator',
    canOperate: (state) => normalizeRole(state.user?.role) === 'Admin' || normalizeRole(state.user?.role) === 'Operator'
  },

  actions: {
    async loadStatus() {
      this.status = await authApi.getStatus()
      return this.status
    },

    async login(request: LoginRequest) {
      const response = await authApi.login(request)
      this.applyLoginResponse(response)
      return response
    },

    async setupAdmin(request: SetupAdminRequest) {
      const response = await authApi.setupAdmin(request)
      this.applyLoginResponse(response)
      return response
    },

    async loadCurrentUser() {
      if (!this.user) {
        return false
      }

      try {
        this.user = await authApi.me()
        this.user.role = normalizeRole(this.user.role)
        localStorage.setItem(USER_KEY, JSON.stringify(this.user))
        this.initialized = true
        return true
      } catch {
        this.clearAuth()
        return false
      }
    },

    async logout() {
      try {
        await authApi.logout()
      } catch {
        // 忽略服务端退出失败
      }

      this.clearAuth()
    },

    applyLoginResponse(response: { accessToken: string; expiresAt: string; user: AuthUser }) {
      response.user.role = normalizeRole(response.user.role)
      this.expiresAt = response.expiresAt
      this.user = response.user
      this.initialized = true
      if (this.status) {
        this.status = { ...this.status, hasUsers: true, requiresSetup: false }
      }

      if (this.status) {
        this.status = { ...this.status, hasUsers: true, requiresSetup: false }
      }

      localStorage.setItem(EXPIRES_KEY, response.expiresAt)
      localStorage.setItem(USER_KEY, JSON.stringify(response.user))
    },

    clearAuth() {
      this.expiresAt = ''
      this.user = null
      this.initialized = false

      localStorage.removeItem(TOKEN_KEY) // 兼容旧版，清理掉它
      localStorage.removeItem(EXPIRES_KEY)
      localStorage.removeItem(USER_KEY)
    }
  }
})