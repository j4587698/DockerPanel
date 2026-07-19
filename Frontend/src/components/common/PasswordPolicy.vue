<template>
  <div class="password-policy">
    <div class="password-policy__header">
      <span>{{ t('common.passwordRequirements', '密码要求') }}</span>
      <span :class="['password-strength', passwordStrength.className]">{{ passwordStrength.text }}</span>
    </div>
    <div class="policy-grid">
      <span v-for="item in passwordPolicyItems" :key="item.label" :class="['policy-item', { passed: item.passed }]">
        <el-icon><component :is="item.passed ? Check : Close" /></el-icon>
        {{ item.label }}
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Check, Close } from '@element-plus/icons-vue'
import { useSettingsStore } from '@/stores/settings'

const props = defineProps<{
  password: string
}>()

const { t } = useI18n()
const settingsStore = useSettingsStore()

const passwordPolicyItems = computed(() => {
  const items = []
  const pwd = props.password || ''
  
  items.push({ 
    label: t('common.passwordMinLength', { length: settingsStore.passwordMinLength }, `至少 ${settingsStore.passwordMinLength} 位`), 
    passed: pwd.length >= settingsStore.passwordMinLength 
  })
  
  if (settingsStore.passwordRequireUppercase) {
    items.push({ label: t('common.passwordRequireUppercase', '包含大写字母'), passed: /[A-Z]/.test(pwd) })
  }
  if (settingsStore.passwordRequireLowercase) {
    items.push({ label: t('common.passwordRequireLowercase', '包含小写字母'), passed: /[a-z]/.test(pwd) })
  }
  if (settingsStore.passwordRequireNumbers) {
    items.push({ label: t('common.passwordRequireNumbers', '包含数字'), passed: /\d/.test(pwd) })
  }
  if (settingsStore.passwordRequireSpecialChars) {
    items.push({ label: t('common.passwordRequireSpecialChars', '包含特殊字符'), passed: /[^A-Za-z0-9]/.test(pwd) })
  }
  
  return items
})

const passwordStrength = computed(() => {
  const passedCount = passwordPolicyItems.value.filter(item => item.passed).length
  const totalCount = passwordPolicyItems.value.length
  
  if (!props.password) return { text: t('common.strengthNone', '未设置'), className: 'weak' }
  
  if (totalCount > 0) {
    const ratio = passedCount / totalCount
    if (ratio <= 0.4) return { text: t('common.strengthWeak', '较弱'), className: 'weak' }
    if (ratio <= 0.8) return { text: t('common.strengthMedium', '中等'), className: 'medium' }
    return { text: t('common.strengthStrong', '强'), className: 'strong' }
  }
  
  return { text: t('common.strengthNone', '未设置'), className: 'weak' }
})

defineExpose({
  isValid: computed(() => passwordPolicyItems.value.every(item => item.passed))
})
</script>

<style scoped>
.password-policy {
  margin: -6px 0 18px;
  padding: 14px;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-lg);
  background: var(--bg-subtle);
  color: var(--text-secondary);
  line-height: 1.5;
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

@media (max-width: 520px) {
  .policy-grid {
    grid-template-columns: 1fr;
  }
}
</style>
