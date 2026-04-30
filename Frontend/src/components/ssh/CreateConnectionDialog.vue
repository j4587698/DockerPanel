<!--
  创建SSH连接对话框
-->
<template>
  <el-dialog
    v-model="dialogVisible"
    :title="t('ssh.createConnection')"
    width="600px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="120px"
      @submit.prevent
    >
      <el-form-item :label="t('ssh.hostAddress')" prop="host">
        <el-input
          v-model="formData.host"
          :placeholder="t('ssh.hostPlaceholder')"
          clearable
        />
      </el-form-item>

      <el-form-item :label="t('ssh.portLabel')" prop="port">
        <el-input-number
          v-model="formData.port"
          :min="1"
          :max="65535"
          :placeholder="t('ssh.portPlaceholder')"
          style="width: 100%"
        />
      </el-form-item>

      <el-form-item :label="t('ssh.usernameLabel')" prop="username">
        <el-input
          v-model="formData.username"
          :placeholder="t('ssh.usernamePlaceholder')"
          clearable
        />
      </el-form-item>

      <el-form-item :label="t('ssh.authMethodLabel')" prop="authMethod">
        <el-radio-group v-model="formData.authMethod" @change="handleAuthMethodChange">
          <el-radio value="password">{{ t('ssh.passwordAuth') }}</el-radio>
          <el-radio value="privatekey">{{ t('ssh.keyAuth') }}</el-radio>
          <el-radio value="both">{{ t('ssh.bothAuth') }}</el-radio>
        </el-radio-group>
      </el-form-item>

      <el-form-item
        v-if="formData.authMethod === 'password' || formData.authMethod === 'both'"
        :label="t('ssh.passwordLabel')"
        prop="password"
      >
        <el-input
          v-model="formData.password"
          type="password"
          :placeholder="t('ssh.passwordPlaceholder')"
          show-password
          clearable
        />
      </el-form-item>

      <el-form-item
        v-if="formData.authMethod === 'privatekey' || formData.authMethod === 'both'"
        :label="t('ssh.privateKeyPath')"
        prop="privateKeyPath"
      >
        <el-input
          v-model="formData.privateKeyPath"
          :placeholder="t('ssh.privateKeyPathPlaceholder')"
          clearable
        >
          <template #append>
            <el-button @click="selectPrivateKeyFile">{{ t('ssh.selectFile') }}</el-button>
          </template>
        </el-input>
      </el-form-item>

      <el-form-item
        v-if="formData.authMethod === 'privatekey' || formData.authMethod === 'both'"
        :label="t('ssh.privateKeyPassphrase')"
        prop="privateKeyPassphrase"
      >
        <el-input
          v-model="formData.privateKeyPassphrase"
          type="password"
          :placeholder="t('ssh.privateKeyPassphrasePlaceholder')"
          show-password
          clearable
        />
      </el-form-item>

      <el-divider>{{ t('ssh.advancedSettings') }}</el-divider>

      <el-form-item :label="t('ssh.connectionTimeout')" prop="connectionTimeout">
        <el-input-number
          v-model="formData.connectionTimeout"
          :min="5"
          :max="300"
          :placeholder="t('common.seconds')"
          style="width: 100%"
        >
          <template #append>{{ t('common.seconds') }}</template>
        </el-input-number>
      </el-form-item>

      <el-form-item :label="t('ssh.commandTimeout')" prop="commandTimeout">
        <el-input-number
          v-model="formData.commandTimeout"
          :min="10"
          :max="3600"
          :placeholder="t('common.seconds')"
          style="width: 100%"
        >
          <template #append>{{ t('common.seconds') }}</template>
        </el-input-number>
      </el-form-item>

      <el-form-item :label="t('ssh.strictHostKeyChecking')" prop="strictHostKeyChecking">
        <el-switch
          v-model="formData.strictHostKeyChecking"
          :active-text="t('common.enable')"
          :inactive-text="t('common.disable')"
        />
        <div class="form-tip">
          {{ t('ssh.strictHostKeyCheckingHint') }}
        </div>
      </el-form-item>

      <el-form-item :label="t('ssh.connectionNameLabel')" prop="connectionName">
        <el-input
          v-model="formData.connectionName"
          :placeholder="t('ssh.connectionNamePlaceholder')"
          clearable
        />
      </el-form-item>

      <el-form-item :label="t('ssh.description')" prop="description">
        <el-input
          v-model="formData.description"
          type="textarea"
          :rows="3"
          :placeholder="t('ssh.descriptionPlaceholder')"
          maxlength="200"
          show-word-limit
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('ssh.cancel') }}</el-button>
        <el-button @click="testConnection" :loading="testing">
          {{ t('ssh.testConnection') }}
        </el-button>
        <el-button type="primary" @click="handleSubmit" :loading="submitting">
          {{ t('ssh.create') }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { useSshStore } from '@/stores/ssh'
import type { SshConnectionConfig } from '@/types/ssh'
import { useI18n } from 'vue-i18n'

// Props
interface Props {
  modelValue: boolean
}

// Emits
interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'success'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// 状态管理
const sshStore = useSshStore()

// i18n
const { t } = useI18n()

// 响应式数据
const formRef = ref<FormInstance>()
const testing = ref(false)
const submitting = ref(false)

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const formDataInitialState = {
  host: '',
  port: 22,
  username: '',
  authMethod: 'password',
  password: '',
  privateKeyPath: '',
  privateKeyPassphrase: '',
  connectionTimeout: 30,
  commandTimeout: 60,
  strictHostKeyChecking: true,
  connectionName: '',
  description: ''
}

const formData = reactive({ ...formDataInitialState })

// 表单验证规则
const formRules: FormRules = {
  host: [
    { required: true, message: t('ssh.validation.hostRequired'), trigger: 'blur' },
    { pattern: /^[\w\.-]+$/, message: t('ssh.validation.hostInvalid'), trigger: 'blur' }
  ],
  port: [
    { required: true, message: t('ssh.validation.portRequired'), trigger: 'blur' },
    { type: 'number', min: 1, max: 65535, message: t('ssh.validation.portRange'), trigger: 'blur' }
  ],
  username: [
    { required: true, message: t('ssh.validation.usernameRequired'), trigger: 'blur' },
    { min: 1, max: 50, message: t('ssh.validation.usernameLength'), trigger: 'blur' }
  ],
  authMethod: [
    { required: true, message: t('ssh.validation.authMethodRequired'), trigger: 'change' }
  ],
  password: [
    { required: true, message: t('ssh.validation.passwordRequired'), trigger: 'blur' },
    { min: 1, message: t('ssh.validation.passwordEmpty'), trigger: 'blur' }
  ],
  privateKeyPath: [
    { required: true, message: t('ssh.validation.privateKeyPathRequired'), trigger: 'blur' }
  ],
  connectionTimeout: [
    { required: true, message: t('ssh.validation.connectionTimeoutRequired'), trigger: 'blur' },
    { type: 'number', min: 5, max: 300, message: t('ssh.validation.connectionTimeoutRange'), trigger: 'blur' }
  ],
  commandTimeout: [
    { required: true, message: t('ssh.validation.commandTimeoutRequired'), trigger: 'blur' },
    { type: 'number', min: 10, max: 3600, message: t('ssh.validation.commandTimeoutRange'), trigger: 'blur' }
  ]
}

// 监听认证方式变化，动态调整验证规则
watch(() => formData.authMethod, (newMethod) => {
  // 动态更新密码验证规则
  if (newMethod === 'password' || newMethod === 'both') {
    formRules.password = [
      { required: true, message: t('ssh.validation.passwordRequired'), trigger: 'blur' },
      { min: 1, message: t('ssh.validation.passwordEmpty'), trigger: 'blur' }
    ]
  } else {
    delete formRules.password
  }

  // 动态更新私钥路径验证规则
  if (newMethod === 'privatekey' || newMethod === 'both') {
    formRules.privateKeyPath = [
      { required: true, message: t('ssh.validation.privateKeyPathRequired'), trigger: 'blur' }
    ]
  } else {
    delete formRules.privateKeyPath
  }
}, { immediate: true })

// 方法
const handleAuthMethodChange = () => {
  // 切换认证方式时清空相关字段
  if (formData.authMethod !== 'password' && formData.authMethod !== 'both') {
    formData.password = ''
  }
  if (formData.authMethod !== 'privatekey' && formData.authMethod !== 'both') {
    formData.privateKeyPath = ''
    formData.privateKeyPassphrase = ''
  }
}

const selectPrivateKeyFile = () => {
  // 创建文件输入元素
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = '.pem,.key'
  input.onchange = (event: any) => {
    const file = event.target.files[0]
    if (file) {
      // 这里可以上传文件或获取文件路径
      // 为了简化，这里只是设置文件名
      formData.privateKeyPath = file.name
      ElMessage.success(t('ssh.privateKeySelected'))
    }
  }
  input.click()
}

const testConnection = async () => {
  try {
    await formRef.value?.validate()
    testing.value = true

    const testRequest = {
      host: formData.host,
      port: formData.port,
      username: formData.username,
      password: formData.password || undefined,
      privateKeyPath: formData.privateKeyPath || undefined
    }

    await sshStore.testConnection(testRequest)
    ElMessage.success(t('ssh.testSuccess'))
  } catch (error: any) {
    ElMessage.error(`${t('ssh.testFailed')}: ${error.message}`)
  } finally {
    testing.value = false
  }
}

const handleSubmit = async () => {
  try {
    await formRef.value?.validate()
    submitting.value = true

    const connectionConfig: SshConnectionConfig = {
      host: formData.host,
      port: formData.port,
      username: formData.username,
      password: formData.password,
      privateKeyPath: formData.privateKeyPath,
      privateKeyPassphrase: formData.privateKeyPassphrase,
      connectionTimeout: formData.connectionTimeout,
      commandTimeout: formData.commandTimeout,
      strictHostKeyChecking: formData.strictHostKeyChecking
    }

    await sshStore.createConnectionConfig(connectionConfig)
    ElMessage.success(t('ssh.connectionCreated'))
    handleClose()
    emit('success')
  } catch (error: any) {
    ElMessage.error(`${t('ssh.connectionCreateFailed')}: ${error.message}`)
  } finally {
    submitting.value = false
  }
}

const handleClose = () => {
  dialogVisible.value = false
  // 重置表单
  formRef.value?.resetFields()
  Object.keys(formDataInitialState).forEach(key => {
    delete (formData as any)[key]
  })
  Object.assign(formData, { ...formDataInitialState })
}
</script>

<style scoped>
.dialog-footer {
  text-align: right;
}

.form-tip {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
  line-height: 1.4;
}

:deep(.el-divider) {
  margin: 24px 0 20px 0;
}

:deep(.el-input-number) {
  width: 100%;
}

:deep(.el-input-number .el-input__inner) {
  text-align: left;
}
</style>