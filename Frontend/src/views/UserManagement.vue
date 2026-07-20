<template>
  <div class="page-container user-management-page" v-loading="loading">
    <div class="page-header">
      <div>
        <h1 class="page-title">{{ t('users.title') }}</h1>
        <p class="page-subtitle">{{ t('users.subtitle') }}</p>
      </div>
      <div class="header-actions">
        <el-button type="primary" :icon="Plus" @click="openCreateDialog">{{ t('users.createUser') }}</el-button>
        <el-button :icon="Refresh" @click="fetchUsers">{{ t('common.refresh') }}</el-button>
      </div>
    </div>

    <div class="stats-cards user-stats">
      <div class="stat-card">
        <div class="stat-icon total"><el-icon><User /></el-icon></div>
        <div class="stat-content">
          <div class="stat-value">{{ userStats.total }}</div>
          <div class="stat-label">{{ t('users.totalUsers') }}</div>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon active"><el-icon><CircleCheck /></el-icon></div>
        <div class="stat-content">
          <div class="stat-value">{{ userStats.active }}</div>
          <div class="stat-label">{{ t('users.activeUsers') }}</div>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon admin"><el-icon><UserFilled /></el-icon></div>
        <div class="stat-content">
          <div class="stat-value">{{ userStats.admins }}</div>
          <div class="stat-label">{{ t('users.adminUsers') }}</div>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon locked"><el-icon><Lock /></el-icon></div>
        <div class="stat-content">
          <div class="stat-value">{{ userStats.locked }}</div>
          <div class="stat-label">{{ t('users.lockedUsers') }}</div>
        </div>
      </div>
    </div>

    <div class="saas-card">
      <div class="toolbar">
        <el-input v-model="searchText" :placeholder="t('users.searchPlaceholder')" :prefix-icon="Search" class="toolbar-search" clearable />
        <div class="toolbar-filters">
          <el-select v-model="filterRole" :placeholder="t('users.filterRole')" clearable style="width: 140px">
            <el-option :label="t('users.roleAdmin')" value="Admin" />
            <el-option :label="t('users.roleOperator')" value="Operator" />
            <el-option :label="t('users.roleViewer')" value="Viewer" />
          </el-select>
          <el-select v-model="filterStatus" :placeholder="t('users.filterStatus')" clearable style="width: 150px">
            <el-option :label="t('users.statusActive')" value="active" />
            <el-option :label="t('users.statusInactive')" value="inactive" />
            <el-option :label="t('users.statusLocked')" value="locked" />
            <el-option :label="t('users.statusMustChange')" value="mustChange" />
          </el-select>
        </div>
      </div>

      <el-table :data="filteredUsers" style="width: 100%">
        <el-table-column :label="t('users.account')" min-width="230">
          <template #default="{ row }">
            <div class="user-cell">
              <div class="user-avatar">{{ getInitial(row) }}</div>
              <div class="user-text">
                <div class="user-name-row">
                  <span class="user-name">{{ row.displayName || row.username }}</span>
                  <el-tag v-if="isCurrentUser(row)" type="info" size="small">{{ t('users.currentUser') }}</el-tag>
                </div>
                <span class="user-sub">@{{ row.username }}</span>
              </div>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('users.role')" width="120" align="center">
          <template #default="{ row }">
            <el-tag :type="getRoleTag(row.role)" size="small">
              {{ getRoleLabel(row.role) }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column :label="t('common.status')" width="140" align="center">
          <template #default="{ row }">
            <el-tag :type="getStatusTag(row)" size="small">
              {{ getStatusLabel(row) }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column :label="t('users.lastLogin')" min-width="170">
          <template #default="{ row }">
            <div class="time-cell">
              <span>{{ formatDate(row.lastLoginAt) }}</span>
              <small>{{ row.lastLoginIp || '-' }}</small>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('users.createdAt')" min-width="160">
          <template #default="{ row }">
            {{ formatDate(row.createdAt) }}
          </template>
        </el-table-column>

        <el-table-column :label="t('common.actions')" width="190" align="center" fixed="right">
          <template #default="{ row }">
            <div class="actions-cell">
              <el-button class="table-action-btn edit" circle size="small" :icon="Edit" :title="t('common.edit')" @click="openEditDialog(row)" />
              <el-button class="table-action-btn reset" circle size="small" :icon="Key" :title="t('users.resetPassword')" @click="openResetDialog(row)" />
              <span>
                <el-button class="table-action-btn delete" circle size="small" :icon="Delete" :title="isCurrentUser(row) ? t('users.cannotDeleteSelf') : t('common.delete')" :disabled="isCurrentUser(row)" @click="deleteUser(row)" />
              </span>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog
      v-model="showUserDialog"
      :title="dialogMode === 'create' ? t('users.createUser') : t('users.editUser')"
      width="560px"
      destroy-on-close
    >
      <el-form ref="userFormRef" :model="userForm" :rules="userRules" label-position="top">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item :label="t('users.username')" prop="username">
              <el-input v-model.trim="userForm.username" :disabled="dialogMode === 'edit'" autocomplete="username" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item :label="t('users.displayName')" prop="displayName">
              <el-input v-model.trim="userForm.displayName" :placeholder="t('users.displayNamePlaceholder')" autocomplete="name" />
            </el-form-item>
          </el-col>
        </el-row>

        <el-form-item v-if="dialogMode === 'create'" :label="t('users.password')" prop="password">
          <el-input v-model="userForm.password" type="password" show-password autocomplete="new-password" />
        </el-form-item>
        <PasswordPolicy v-if="dialogMode === 'create'" ref="userPasswordPolicyRef" :password="userForm.password" />

        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item :label="t('users.role')" prop="role">
              <el-select v-model="userForm.role" style="width: 100%" :disabled="isEditingCurrentUser">
                <el-option :label="t('users.roleAdmin')" value="Admin" />
                <el-option :label="t('users.roleOperator')" value="Operator" />
                <el-option :label="t('users.roleViewer')" value="Viewer" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item :label="t('users.status')">
              <el-switch
                v-model="userForm.isActive"
                :disabled="isEditingCurrentUser"
                :active-text="t('users.statusActive')"
                :inactive-text="t('users.statusInactive')"
              />
            </el-form-item>
          </el-col>
        </el-row>

        <el-alert v-if="isEditingCurrentUser" class="form-alert" type="info" :closable="false" show-icon :title="t('users.selfEditHint')" />

        <label class="toggle-line">
          <span>
            <strong>{{ t('users.mustChangePassword') }}</strong>
            <small>{{ t('users.mustChangePasswordHint') }}</small>
          </span>
          <el-switch v-model="userForm.mustChangePassword" />
        </label>
      </el-form>
      <template #footer>
        <el-button @click="showUserDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" :loading="submitting" @click="submitUser">{{ t('common.save') }}</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showResetDialog" :title="t('users.resetPassword')" width="480px" destroy-on-close>
      <el-alert class="form-alert" type="warning" :closable="false" show-icon :title="t('users.resetPasswordHint', { username: resetTarget?.username || '' })" />
      <el-form ref="resetFormRef" :model="resetForm" :rules="resetRules" label-position="top">
        <el-form-item :label="t('users.newPassword')" prop="newPassword">
          <el-input v-model="resetForm.newPassword" type="password" show-password autocomplete="new-password" />
        </el-form-item>
        <PasswordPolicy ref="resetPasswordPolicyRef" :password="resetForm.newPassword" />
        <label class="toggle-line">
          <span>
            <strong>{{ t('users.mustChangePassword') }}</strong>
            <small>{{ t('users.mustChangePasswordHint') }}</small>
          </span>
          <el-switch v-model="resetForm.mustChangePassword" />
        </label>
      </el-form>
      <template #footer>
        <el-button @click="showResetDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" :loading="submitting" @click="submitResetPassword">{{ t('common.confirm') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { authApi, type AuthRole, type UserAccount } from '@/api/auth'
import { useAuthStore } from '@/stores/auth'
import { CircleCheck, Delete, Edit, Key, Lock, Plus, Refresh, Search, User, UserFilled } from '@element-plus/icons-vue'
import PasswordPolicy from '@/components/common/PasswordPolicy.vue'
import { formatLocalizedDateTime } from '@/utils/date'

type UserRole = AuthRole
type UserStatusFilter = '' | 'active' | 'inactive' | 'locked' | 'mustChange'

const { t } = useI18n()
const authStore = useAuthStore()

const loading = ref(false)
const submitting = ref(false)
const users = ref<UserAccount[]>([])
const searchText = ref('')
const filterRole = ref<UserRole | ''>('')
const filterStatus = ref<UserStatusFilter>('')

const showUserDialog = ref(false)
const showResetDialog = ref(false)
const dialogMode = ref<'create' | 'edit'>('create')
const editingUser = ref<UserAccount | null>(null)
const resetTarget = ref<UserAccount | null>(null)
const userFormRef = ref<FormInstance>()
const resetFormRef = ref<FormInstance>()
const userPasswordPolicyRef = ref()
const resetPasswordPolicyRef = ref()

const userForm = reactive({
  username: '',
  displayName: '',
  password: '',
  role: 'Operator' as UserRole,
  isActive: true,
  mustChangePassword: true
})

const resetForm = reactive({
  newPassword: '',
  mustChangePassword: true
})

const validateUserPassword = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
  if (dialogMode.value !== 'create') return callback()
  if (!value) return callback(new Error(t('users.passwordRequired')))
  if (userPasswordPolicyRef.value && !userPasswordPolicyRef.value.isValid) {
    return callback(new Error(t('common.passwordComplexityRequired', '密码必须满足全部复杂度要求')))
  }
  callback()
}

const validateResetPassword = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
  if (!value) return callback(new Error(t('users.passwordRequired')))
  if (resetPasswordPolicyRef.value && !resetPasswordPolicyRef.value.isValid) {
    return callback(new Error(t('common.passwordComplexityRequired', '密码必须满足全部复杂度要求')))
  }
  callback()
}

const userRules = computed<FormRules>(() => ({
  username: [
    { required: true, message: t('users.usernameRequired'), trigger: 'blur' },
    { min: 3, max: 64, message: t('users.usernameLength'), trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9_.@-]+$/, message: t('users.usernameInvalid'), trigger: 'blur' }
  ],
  password: dialogMode.value === 'create'
    ? [
        { required: true, message: t('users.passwordRequired'), trigger: 'blur' },
        { validator: validateUserPassword, trigger: ['blur', 'change'] }
      ]
    : [],
  role: [{ required: true, message: t('users.roleRequired'), trigger: 'change' }]
}))

const resetRules: FormRules = {
  newPassword: [
    { required: true, message: t('users.passwordRequired'), trigger: 'blur' },
    { validator: validateResetPassword, trigger: ['blur', 'change'] }
  ]
}

const userStats = computed(() => ({
  total: users.value.length,
  active: users.value.filter(user => user.isActive).length,
  admins: users.value.filter(user => user.role === 'Admin').length,
  locked: users.value.filter(user => isLocked(user)).length
}))

const filteredUsers = computed(() => {
  let data = users.value
  const keyword = searchText.value.trim().toLowerCase()
  if (keyword) {
    data = data.filter(user =>
      user.username.toLowerCase().includes(keyword) ||
      (user.displayName || '').toLowerCase().includes(keyword) ||
      (user.lastLoginIp || '').toLowerCase().includes(keyword)
    )
  }

  if (filterRole.value) {
    data = data.filter(user => user.role === filterRole.value)
  }

  if (filterStatus.value) {
    data = data.filter(user => {
      if (filterStatus.value === 'active') return user.isActive && !isLocked(user)
      if (filterStatus.value === 'inactive') return !user.isActive
      if (filterStatus.value === 'locked') return isLocked(user)
      if (filterStatus.value === 'mustChange') return user.mustChangePassword
      return true
    })
  }

  return data
})

const isEditingCurrentUser = computed(() => Boolean(editingUser.value && isCurrentUser(editingUser.value)))

const fetchUsers = async () => {
  loading.value = true
  try {
    users.value = await authApi.listUsers()
  } catch (error) {
    ElMessage.error((error as Error).message || t('users.loadFailed'))
  } finally {
    loading.value = false
  }
}

const openCreateDialog = async () => {
  dialogMode.value = 'create'
  editingUser.value = null
  Object.assign(userForm, {
    username: '',
    displayName: '',
    password: '',
    role: 'Operator',
    isActive: true,
    mustChangePassword: true
  })
  showUserDialog.value = true
  await nextTick()
  userFormRef.value?.clearValidate()
}

const openEditDialog = async (user: UserAccount) => {
  dialogMode.value = 'edit'
  editingUser.value = user
  Object.assign(userForm, {
    username: user.username,
    displayName: user.displayName || '',
    password: '',
    role: user.role as UserRole,
    isActive: user.isActive,
    mustChangePassword: user.mustChangePassword
  })
  showUserDialog.value = true
  await nextTick()
  userFormRef.value?.clearValidate()
}

const submitUser = async () => {
  try {
    await userFormRef.value?.validate()
  } catch {
    return
  }

  submitting.value = true
  try {
    if (dialogMode.value === 'create') {
      await authApi.createUser({
        username: userForm.username,
        displayName: userForm.displayName || undefined,
        password: userForm.password,
        role: userForm.role,
        isActive: userForm.isActive,
        mustChangePassword: userForm.mustChangePassword
      })
      ElMessage.success(t('users.createSuccess'))
    } else if (editingUser.value) {
      const updated = await authApi.updateUser(editingUser.value.id, {
        displayName: userForm.displayName || undefined,
        role: userForm.role,
        isActive: userForm.isActive,
        mustChangePassword: userForm.mustChangePassword
      })
      if (isCurrentUser(updated)) {
        authStore.user = { ...authStore.user!, ...updated }
        localStorage.setItem('user', JSON.stringify(authStore.user))
      }
      ElMessage.success(t('users.updateSuccess'))
    }

    showUserDialog.value = false
    await fetchUsers()
  } catch (error) {
    ElMessage.error((error as Error).message || t('users.saveFailed'))
  } finally {
    submitting.value = false
  }
}

const openResetDialog = async (user: UserAccount) => {
  resetTarget.value = user
  Object.assign(resetForm, {
    newPassword: '',
    mustChangePassword: true
  })
  showResetDialog.value = true
  await nextTick()
  resetFormRef.value?.clearValidate()
}

const submitResetPassword = async () => {
  if (!resetTarget.value) return

  try {
    await resetFormRef.value?.validate()
  } catch {
    return
  }

  submitting.value = true
  try {
    await authApi.resetUserPassword(resetTarget.value.id, {
      newPassword: resetForm.newPassword,
      mustChangePassword: resetForm.mustChangePassword
    })
    ElMessage.success(t('users.resetSuccess'))
    showResetDialog.value = false
    await fetchUsers()
  } catch (error) {
    ElMessage.error((error as Error).message || t('users.resetFailed'))
  } finally {
    submitting.value = false
  }
}

const deleteUser = async (user: UserAccount) => {
  if (isCurrentUser(user)) {
    ElMessage.warning(t('users.cannotDeleteSelf'))
    return
  }

  try {
    await ElMessageBox.confirm(
      t('users.deleteConfirm', { username: user.username }),
      t('common.deleteConfirm'),
      { type: 'warning', confirmButtonText: t('common.delete'), cancelButtonText: t('common.cancel') }
    )
    await authApi.deleteUser(user.id)
    ElMessage.success(t('users.deleteSuccess'))
    await fetchUsers()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error((error as Error).message || t('users.deleteFailed'))
    }
  }
}

const isCurrentUser = (user: UserAccount) => user.id === authStore.user?.id

const isLocked = (user: UserAccount) => Boolean(user.lockedUntil && new Date(user.lockedUntil).getTime() > Date.now())

const normalizeRole = (role: string): UserRole => {
  if (role === 'Admin') return 'Admin'
  if (role === 'Viewer') return 'Viewer'
  return 'Operator'
}

const getRoleLabel = (role: string) => {
  const normalizedRole = normalizeRole(role)
  if (normalizedRole === 'Admin') return t('users.roleAdmin')
  if (normalizedRole === 'Viewer') return t('users.roleViewer')
  return t('users.roleOperator')
}

const getRoleTag = (role: string) => {
  const normalizedRole = normalizeRole(role)
  if (normalizedRole === 'Admin') return 'warning'
  if (normalizedRole === 'Viewer') return 'info'
  return 'primary'
}

const getStatusLabel = (user: UserAccount) => {
  if (!user.isActive) return t('users.statusInactive')
  if (isLocked(user)) return t('users.statusLocked')
  if (user.mustChangePassword) return t('users.statusMustChange')
  return t('users.statusActive')
}

const getStatusTag = (user: UserAccount) => {
  if (!user.isActive) return 'danger'
  if (isLocked(user)) return 'warning'
  if (user.mustChangePassword) return 'info'
  return 'success'
}

const getInitial = (user: UserAccount) => (user.displayName || user.username || '?').slice(0, 1).toUpperCase()

const formatDate = (value?: string | null) => {
  return formatLocalizedDateTime(value, '-')
}

onMounted(() => {
  fetchUsers()
})
</script>

<style scoped>
.user-management-page {
  max-width: 1480px;
}

.header-actions {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.user-stats .stat-card {
  min-width: 0;
}

.stat-icon {
  width: 46px;
  height: 46px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22px;
  flex-shrink: 0;
}

.stat-icon.total {
  color: #3b82f6;
  background: rgba(59, 130, 246, 0.12);
}

.stat-icon.active {
  color: #10b981;
  background: rgba(16, 185, 129, 0.12);
}

.stat-icon.admin {
  color: #f59e0b;
  background: rgba(245, 158, 11, 0.14);
}

.stat-icon.locked {
  color: #ef4444;
  background: rgba(239, 68, 68, 0.12);
}

.stat-content {
  min-width: 0;
}

.toolbar {
  padding: 18px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  border-bottom: 1px solid var(--border-color);
}

.toolbar-search {
  max-width: 360px;
}

.toolbar-filters {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.user-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}

.user-avatar {
  width: 38px;
  height: 38px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  color: #fff;
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  box-shadow: 0 8px 18px rgba(59, 130, 246, 0.22);
}

.user-text {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.user-name-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.user-name {
  font-weight: 600;
  color: var(--text-main);
}

.user-sub {
  font-size: 12px;
  color: var(--text-muted);
}

.time-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.time-cell small {
  color: var(--text-muted);
}

.actions-cell {
  display: flex;
  justify-content: center;
  gap: 8px;
  width: 100%;
}

.actions-cell :deep(.table-action-btn) {
  width: 30px;
  height: 30px;
  min-width: 30px;
  padding: 0;
  border-radius: 50%;
  background: var(--bg-surface);
  border-color: var(--border-color);
  color: var(--text-secondary);
}

.actions-cell :deep(.table-action-btn:hover) {
  background: var(--bg-subtle);
  border-color: var(--border-color);
}

.actions-cell :deep(.table-action-btn.edit:hover) {
  color: var(--color-primary);
}

.actions-cell :deep(.table-action-btn.reset:hover) {
  color: var(--color-warning);
}

.actions-cell :deep(.table-action-btn.delete:hover) {
  color: var(--color-danger);
}

.actions-cell :deep(.table-action-btn.is-disabled),
.actions-cell :deep(.table-action-btn.is-disabled:hover) {
  background: var(--bg-subtle);
  border-color: var(--border-color-light);
  color: var(--text-muted);
  opacity: 0.55;
}

.actions-cell :deep(.el-button + .el-button) {
  margin-left: 0;
}

.form-alert {
  margin-bottom: 16px;
}

.toggle-line {
  padding: 14px 16px;
  border: 1px solid var(--border-color-light);
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
  cursor: pointer;
  background: var(--bg-glass-dark);
}

.toggle-line span {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.toggle-line strong {
  color: var(--text-main);
  font-size: 14px;
}

.toggle-line small {
  color: var(--text-muted);
  line-height: 1.5;
}

@media (max-width: 900px) {
  .page-header,
  .toolbar {
    flex-direction: column;
    align-items: stretch;
  }

  .toolbar-search {
    max-width: none;
  }
}
</style>
