import api from './index'

export type AuthRole = 'Admin' | 'Operator' | 'Viewer'

export interface AuthUser {
  id: string
  username: string
  displayName: string
  role: AuthRole
  mustChangePassword: boolean
  lastLoginAt?: string | null
}

export interface UserAccount extends AuthUser {
  isActive: boolean
  failedLoginAttempts: number
  lockedUntil?: string | null
  lastLoginIp?: string | null
  createdAt: string
  updatedAt: string
}

export interface AuthStatusResponse {
  enabled: boolean
  hasUsers: boolean
  requiresSetup: boolean
  defaultAdminUsername: string
  canBootstrapFromEnvironment: boolean
}

export interface LoginRequest {
  username: string
  password: string
}

export interface SetupAdminRequest extends LoginRequest {
  displayName?: string
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string
  user: AuthUser
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}

export interface CreateUserRequest {
  username: string
  password: string
  displayName?: string
  role: AuthRole
  isActive: boolean
  mustChangePassword: boolean
}

export interface UpdateUserRequest {
  displayName?: string
  role?: AuthRole
  isActive?: boolean
  mustChangePassword?: boolean
}

export interface ResetUserPasswordRequest {
  newPassword: string
  mustChangePassword: boolean
}

export const authApi = {
  getStatus(): Promise<AuthStatusResponse> {
    return api.get('/auth/status', { skipAuth: true, skipErrorHandler: true } as any)
  },

  setupAdmin(data: SetupAdminRequest): Promise<LoginResponse> {
    return api.post('/auth/setup', data, { skipAuth: true } as any)
  },

  login(data: LoginRequest): Promise<LoginResponse> {
    return api.post('/auth/login', data, { skipAuth: true } as any)
  },

  me(): Promise<AuthUser> {
    return api.get('/auth/me', { skipErrorHandler: true } as any)
  },

  logout(): Promise<{ success: boolean }> {
    return api.post('/auth/logout', {}, { skipErrorHandler: true } as any)
  },

  changePassword(data: ChangePasswordRequest): Promise<AuthUser> {
    return api.post('/auth/change-password', data)
  },

  listUsers(): Promise<UserAccount[]> {
    return api.get('/auth/users')
  },

  createUser(data: CreateUserRequest): Promise<UserAccount> {
    return api.post('/auth/users', data)
  },

  updateUser(id: string, data: UpdateUserRequest): Promise<UserAccount> {
    return api.put(`/auth/users/${id}`, data)
  },

  resetUserPassword(id: string, data: ResetUserPasswordRequest): Promise<UserAccount> {
    return api.post(`/auth/users/${id}/reset-password`, data)
  },

  deleteUser(id: string): Promise<void> {
    return api.delete(`/auth/users/${id}`)
  }
}