<template>
  <div class="login-page">
    <div class="login-pattern" aria-hidden="true"></div>

    <main class="login-shell">
      <section class="login-card saas-card">
        <div class="brand-row">
          <AppLogo :name="appName" size="lg" />
        </div>

        <div class="form-header">
          <span class="mode-badge">{{ isSetupMode ? '首次安装' : '安全登录' }}</span>
          <h1>{{ isSetupMode ? `初始化 ${appName}` : '欢迎回来' }}</h1>
          <p>{{ isSetupMode ? '创建第一个管理员账户，完成后自动进入控制台。' : '登录后继续管理容器、镜像、网络和证书。' }}</p>
        </div>

        <el-alert
          v-if="isSetupMode"
          class="setup-alert"
          type="warning"
          :closable="false"
          show-icon
          title="创建管理员账户"
          description="这是首次使用前的必要步骤。请设置一个安全密码，并妥善保存账户信息。"
        />

        <el-form ref="formRef" :model="form" :rules="rules" size="large" @submit.prevent="handleSubmit">
          <el-form-item prop="username">
            <el-input v-model.trim="form.username" :placeholder="isSetupMode ? '管理员用户名' : '请输入用户名'" autocomplete="username">
              <template #prefix>
                <el-icon><User /></el-icon>
              </template>
            </el-input>
          </el-form-item>

          <el-form-item v-if="isSetupMode" prop="displayName">
            <el-input v-model.trim="form.displayName" placeholder="显示名称（可选）" autocomplete="name">
              <template #prefix>
                <el-icon><Avatar /></el-icon>
              </template>
            </el-input>
          </el-form-item>

          <el-form-item prop="password">
            <el-input
              v-model="form.password"
              type="password"
              :placeholder="isSetupMode ? '管理员密码' : '请输入密码'"
              :autocomplete="isSetupMode ? 'new-password' : 'current-password'"
              show-password
              @keyup.enter="handleSubmit"
            >
              <template #prefix>
                <el-icon><Lock /></el-icon>
              </template>
            </el-input>
          </el-form-item>

          <PasswordPolicy v-if="isSetupMode" ref="passwordPolicyRef" :password="form.password" />

          <el-form-item v-if="isSetupMode" prop="confirmPassword">
            <el-input
              v-model="form.confirmPassword"
              type="password"
              placeholder="再次输入管理员密码"
              autocomplete="new-password"
              show-password
              @keyup.enter="handleSubmit"
            >
              <template #prefix>
                <el-icon><Lock /></el-icon>
              </template>
            </el-input>
          </el-form-item>

          <el-button class="login-button" type="primary" size="large" :loading="loading" @click="handleSubmit">
            {{ isSetupMode ? '创建管理员并进入面板' : '登录面板' }}
          </el-button>
        </el-form>

        <div class="login-footer">
          建议仅在可信网络内访问面板，并为管理员账户使用高强度密码。
        </div>
      </section>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { Avatar, Check, Close, Lock, User } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'
import { useSettingsStore } from '@/stores/settings'
import AppLogo from '@/components/common/AppLogo.vue'
import PasswordPolicy from '@/components/common/PasswordPolicy.vue'
import { APP_NAME } from '@/utils/branding'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const settingsStore = useSettingsStore()

const formRef = ref<FormInstance>()
const passwordPolicyRef = ref()
const loading = ref(false)
const status = computed(() => authStore.status)
const isSetupMode = computed(() => route.name === 'Setup' || Boolean(status.value?.requiresSetup))
const appName = computed(() => settingsStore.systemName || APP_NAME)

const form = reactive({
  username: '',
  displayName: '',
  password: '',
  confirmPassword: ''
})

const validateConfirmPassword = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
  if (!isSetupMode.value) return callback()
  if (!value) return callback(new Error('请再次输入密码'))
  if (value !== form.password) return callback(new Error('两次输入的密码不一致'))
  callback()
}

const validateSetupPassword = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
  if (!isSetupMode.value) return callback()
  if (!value) return callback(new Error('请输入密码'))
  if (passwordPolicyRef.value && !passwordPolicyRef.value.isValid) {
    return callback(new Error('密码必须满足全部复杂度要求'))
  }
  callback()
}

const rules: FormRules = {
  username: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 3, max: 64, message: '用户名长度为 3-64 位', trigger: 'blur' }
  ],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 8, message: '密码至少 8 位', trigger: 'blur' },
    { validator: validateSetupPassword, trigger: ['blur', 'change'] }
  ],
  confirmPassword: [
    { validator: validateConfirmPassword, trigger: 'blur' }
  ]
}

onMounted(async () => {
  try {
    settingsStore.loadPublicSettings().catch(() => {})
    const currentStatus = await authStore.loadStatus()
    if (route.name === 'Setup' && !currentStatus.requiresSetup) {
      await router.replace('/login')
    }
  } catch (error: any) {
    ElMessage.error(error?.message || '无法获取认证状态')
  }
})

const handleSubmit = async () => {
  if (!formRef.value) return

  await formRef.value.validate()
  loading.value = true

  try {
    if (isSetupMode.value) {
      await authStore.setupAdmin({
        username: form.username,
        password: form.password,
        displayName: form.displayName
      })
      ElMessage.success('管理员账户已创建')
    } else {
      await authStore.login({ username: form.username, password: form.password })
      ElMessage.success('登录成功')
    }

    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard'
    await router.replace(redirect)
  } catch (error: any) {
    ElMessage.error(error?.message || (isSetupMode.value ? '初始化失败' : '登录失败'))
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>

.login-page {
  position: relative;
  min-height: 100vh;
  width: 100%;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  overflow: auto;
  isolation: isolate;
  padding: clamp(24px, 5vw, 56px);
  background:
    radial-gradient(circle at 12% 18%, rgba(59, 130, 246, 0.12), transparent 28%),
    radial-gradient(circle at 86% 14%, rgba(14, 165, 233, 0.1), transparent 24%),
    var(--bg-app);
}

.login-pattern {
  position: absolute;
  inset: 0;
  z-index: -1;
  background-image:
    linear-gradient(rgba(148, 163, 184, 0.12) 1px, transparent 1px),
    linear-gradient(90deg, rgba(148, 163, 184, 0.12) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: radial-gradient(circle at center, #000 0%, transparent 72%);
}

.login-shell {
  width: min(100%, 460px);
  display: block;
  margin: auto 0;
  animation: fadeInUp 0.35s ease-out;
}

.brand-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  margin-bottom: 24px;
  color: var(--text-main);
  font-size: 16px;
  font-weight: 700;
}

.login-hero {
  padding: 36px;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-xl);
  background: var(--bg-glass);
  backdrop-filter: var(--glass-blur);
  box-shadow: var(--shadow-sm);
}

.login-brand {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 56px;
  color: var(--text-main);
  font-size: 16px;
  font-weight: 700;
}

.form-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  background: var(--color-primary);
  color: var(--text-inverse);
}

.hero-copy {
  max-width: 560px;
}

.eyebrow {
  display: inline-flex;
  align-items: center;
  margin-bottom: 14px;
  color: var(--color-secondary);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.hero-copy .page-title {
  max-width: 520px;
  font-size: clamp(34px, 4.6vw, 52px);
  line-height: 1.08;
}

.hero-description {
  max-width: 500px;
  margin: 18px 0 0;
  color: var(--text-secondary);
  font-size: 15px;
  line-height: 1.8;
}

.preview-card {
  margin: 36px 0 0;
  padding: 0;
}

.preview-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px var(--space-lg);
  border-bottom: 1px solid var(--border-color);
  color: var(--text-main);
  font-size: 15px;
  font-weight: 600;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  padding: 5px 10px;
  border-radius: var(--border-radius-full);
  background: var(--color-success-bg);
  color: var(--color-success);
  font-size: 12px;
  font-weight: 700;
}

.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
}

.preview-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
  padding: var(--space-lg);
}

.preview-item {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 16px;
  border: 1px solid var(--border-color-light);
  border-radius: 12px;
  background: var(--bg-glass-dark);
}

.preview-icon {
  width: 42px;
  height: 42px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  border-radius: 10px;
  font-size: 20px;
}

.preview-item strong,
.preview-item span {
  display: block;
}

.preview-item strong {
  color: var(--text-main);
  font-size: 14px;
  font-weight: 700;
}

.preview-item span {
  margin-top: 2px;
  color: var(--text-muted);
  font-size: 12px;
}

.preview-bar {
  height: 4px;
  overflow: hidden;
  background: var(--bg-subtle);
}

.preview-bar span {
  display: block;
  width: 68%;
  height: 100%;
  border-radius: var(--border-radius-full);
  background: var(--gradient-primary);
}

.gradient-blue {
  background: linear-gradient(135deg, #3b82f6, #1d4ed8);
  color: #fff;
}

.gradient-cyan {
  background: linear-gradient(135deg, #06b6d4, #0891b2);
  color: #fff;
}

.login-card {
  width: 100%;
  margin: 0;
  padding: 32px;
  border-radius: var(--border-radius-xl);
  background: var(--bg-glass);
  backdrop-filter: var(--glass-blur);
  box-shadow: var(--shadow-floating);
}

.form-header {
  margin-bottom: 26px;
  text-align: center;
}

.form-header h1 {
  margin: 0 0 8px;
  color: var(--text-main);
  font-size: 28px;
  font-weight: 700;
}

.form-header p {
  margin: 0;
  color: var(--text-secondary);
  font-size: 14px;
  line-height: 1.7;
}

.mode-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 14px;
  padding: 5px 10px;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-full);
  background: var(--bg-subtle);
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 700;
}

.setup-alert {
  margin-bottom: 22px;
}

.password-policy {
  margin: -6px 0 18px;
  padding: 14px;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-lg);
  background: var(--bg-subtle);
  color: var(--text-secondary);
}

.password-policy__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
  font-size: 13px;
  font-weight: 600;
}

.password-strength.weak {
  color: #f87171;
}

.password-strength.medium {
  color: #fbbf24;
}

.password-strength.strong {
  color: #34d399;
}

.policy-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 12px;
}

.policy-item {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--text-muted);
  font-size: 12px;
}

.policy-item.passed {
  color: #34d399;
}

.login-button {
  width: 100%;
  height: 44px;
  margin-top: 6px;
  border-radius: var(--border-radius);
  font-weight: 700;
}

.login-footer {
  margin-top: 22px;
  padding: 12px 14px;
  border-radius: var(--border-radius);
  background: var(--bg-subtle);
  color: var(--text-muted);
  font-size: 12px;
  line-height: 1.6;
  text-align: left;
}

:deep(.el-form-item) {
  margin-bottom: 18px;
}

:deep(.el-input__wrapper) {
  min-height: 44px;
  border-radius: var(--border-radius);
  background: var(--bg-surface);
  box-shadow: 0 0 0 1px var(--border-color) inset;
  transition: var(--transition-base);
}

:deep(.el-input__wrapper:hover),
:deep(.el-input__wrapper.is-focus) {
  box-shadow: 0 0 0 1px var(--color-secondary) inset;
}

:deep(.el-input__inner) {
  color: var(--text-main);
}

:deep(.el-input__prefix) {
  color: var(--text-muted);
}

:deep(.el-alert) {
  border-radius: var(--border-radius);
}

html.dark .login-page {
  background:
    radial-gradient(circle at 12% 18%, rgba(96, 165, 250, 0.16), transparent 28%),
    radial-gradient(circle at 86% 14%, rgba(6, 182, 212, 0.12), transparent 24%),
    var(--bg-app);
}

html.dark .form-icon {
  background: var(--color-secondary);
}

@media (max-width: 960px) {
  .login-shell {
    grid-template-columns: 1fr;
    max-width: 520px;
  }

  .login-hero {
    display: none;
  }
}

@media (max-width: 520px) {
  .login-page {
    align-items: flex-start;
    padding: 18px;
  }

  .login-card {
    padding: 24px 20px;
  }

  .form-header {
    flex-direction: column;
  }

  .mode-badge {
    align-self: flex-start;
  }

  .policy-grid {
    grid-template-columns: 1fr;
  }
}

</style>