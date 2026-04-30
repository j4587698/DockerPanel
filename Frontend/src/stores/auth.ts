import { defineStore } from 'pinia'
import { authApi, type AuthRole, type AuthStatusResponse, type AuthUser, type LoginRequest, type SetupAdminRequest } from '@/api/auth'

const TOKEN_KEY = 'token'
const USER_KEY = 'user'
const EXPIRES_KEY = 'tokenExpiresAt'
const TOKEN_EXPIRY_BUFFER_MS = 30_000

function isTokenExpired(expiresAt: string): boolean {
  if (!expiresAt) return false

  const expires = Date.parse(expiresAt)
  if (Number.isNaN(expires)) return false

  return expires <= Date.now() + TOKEN_EXPIRY_BUFFER_MS
}

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
    token: localStorage.getItem(TOKEN_KEY) || '',
    expiresAt: localStorage.getItem(EXPIRES_KEY) || '',
    user: readStoredUser() as AuthUser | null,
    status: null as AuthStatusResponse | null,
    initialized: false
  }),

  getters: {
    isAuthenticated: (state) => Boolean(state.token) && !isTokenExpired(state.expiresAt),
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
      if (!this.token || isTokenExpired(this.expiresAt)) {
        this.clearAuth()
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
      if (this.token) {
        try {
          await authApi.logout()
        } catch {
          // token 可能已过期，忽略服务端退出失败
        }
      }

      this.clearAuth()
    },

    applyLoginResponse(response: { accessToken: string; expiresAt: string; user: AuthUser }) {
      response.user.role = normalizeRole(response.user.role)
      this.token = response.accessToken
      this.expiresAt = response.expiresAt
      this.user = response.user
      this.initialized = true
      if (this.status) {
        this.status = { ...this.status, hasUsers: true, requiresSetup: false }
      }

      localStorage.setItem(TOKEN_KEY, response.accessToken)
      localStorage.setItem(EXPIRES_KEY, response.expiresAt)
      localStorage.setItem(USER_KEY, JSON.stringify(response.user))
    },

    clearAuth() {
      this.token = ''
      this.expiresAt = ''
      this.user = null
      this.initialized = false

      localStorage.removeItem(TOKEN_KEY)
      localStorage.removeItem(EXPIRES_KEY)
      localStorage.removeItem(USER_KEY)
    }
  }
})