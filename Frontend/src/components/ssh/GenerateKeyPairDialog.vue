<template>
  <el-dialog
    v-model="visible"
    title="生成SSH密钥对"
    width="520px"
    :close-on-click-modal="false"
    @close="handleClose"
    class="keypair-dialog"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-width="100px">
      <el-form-item label="密钥名称" prop="name">
        <el-input v-model="form.name" placeholder="请输入密钥名称" />
      </el-form-item>

      <el-form-item label="密钥类型" prop="keyType">
        <el-select v-model="form.keyType" style="width: 100%">
          <el-option label="RSA (4096位)" value="rsa" />
          <el-option label="ED25519 (推荐)" value="ed25519" />
          <el-option label="ECDSA (256位)" value="ecdsa" />
        </el-select>
      </el-form-item>

      <el-form-item label="密钥位数" prop="keySize" v-if="form.keyType === 'rsa'">
        <el-select v-model="form.keySize" style="width: 100%">
          <el-option label="2048位" :value="2048" />
          <el-option label="4096位 (推荐)" :value="4096" />
        </el-select>
      </el-form-item>

      <el-form-item label="密码短语" prop="passphrase">
        <el-input
          v-model="form.passphrase"
          type="password"
          show-password
          placeholder="可选，用于加密私钥"
        />
      </el-form-item>

      <el-form-item label="备注" prop="comment">
        <el-input
          v-model="form.comment"
          type="textarea"
          :rows="2"
          placeholder="可选，密钥用途说明"
        />
      </el-form-item>
    </el-form>

    <!-- 生成结果 -->
    <div v-if="generatedKeyPair" class="generated-keys">
      <el-divider content-position="left">生成的密钥</el-divider>

      <el-alert
        title="请安全保存您的私钥，私钥不会被存储在服务器上"
        type="warning"
        :closable="false"
        show-icon
        style="margin-bottom: 16px"
      />

      <div class="key-section">
        <div class="key-header">
          <span>公钥 (Public Key)</span>
          <el-button size="small" text @click="copyKey('public')" :icon="CopyDocument">复制</el-button>
        </div>
        <el-input
          type="textarea"
          :model-value="generatedKeyPair.publicKey"
          readonly
          :rows="3"
          class="key-textarea"
        />
      </div>

      <div class="key-section">
        <div class="key-header">
          <span>私钥 (Private Key)</span>
          <div class="key-actions">
            <el-button size="small" text @click="copyKey('private')" :icon="CopyDocument">复制</el-button>
            <el-button size="small" text @click="downloadKey('private')" :icon="Download">下载</el-button>
          </div>
        </div>
        <el-input
          type="textarea"
          :model-value="generatedKeyPair.privateKey"
          readonly
          :rows="5"
          class="key-textarea"
        />
      </div>

      <div class="key-info">
        <el-descriptions :column="2" size="small" border>
          <el-descriptions-item label="密钥类型">{{ form.keyType.toUpperCase() }}</el-descriptions-item>
          <el-descriptions-item label="指纹">{{ generatedKeyPair.fingerprint }}</el-descriptions-item>
          <el-descriptions-item label="创建时间">{{ formatDate(generatedKeyPair.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="是否加密">{{ form.passphrase ? '是' : '否' }}</el-descriptions-item>
        </el-descriptions>
      </div>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">取消</el-button>
        <el-button
          v-if="!generatedKeyPair"
          type="primary"
          @click="generateKeyPair"
          :loading="generating"
        >
          生成密钥对
        </el-button>
        <el-button
          v-else
          type="success"
          @click="saveAndClose"
        >
          完成
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { CopyDocument, Download } from '@element-plus/icons-vue'
import { useSshStore } from '@/stores/ssh'
import type { FormInstance, FormRules } from 'element-plus'
import { formatLocalizedDateTime } from '@/utils/date'

interface KeyPairResult {
  publicKey: string
  privateKey: string
  fingerprint: string
  createdAt: string
}

const props = defineProps<{
  modelValue: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'success'): void
}>()

const sshStore = useSshStore()
const formRef = ref<FormInstance>()
const generating = ref(false)
const generatedKeyPair = ref<KeyPairResult | null>(null)

const visible = ref(props.modelValue)

watch(() => props.modelValue, (val) => {
  visible.value = val
})

watch(visible, (val) => {
  emit('update:modelValue', val)
})

const formInitialState = {
  name: '',
  keyType: 'ed25519',
  keySize: 4096,
  passphrase: '',
  comment: ''
}

const form = reactive({ ...formInitialState })

const rules: FormRules = {
  name: [
    { required: true, message: '请输入密钥名称', trigger: 'blur' },
    { min: 2, max: 50, message: '长度在 2 到 50 个字符', trigger: 'blur' }
  ],
  keyType: [
    { required: true, message: '请选择密钥类型', trigger: 'change' }
  ]
}

const generateKeyPair = async () => {
  if (!formRef.value) return

  try {
    await formRef.value.validate()
    generating.value = true

    const result = await sshStore.generateKeyPair({
      name: form.name,
      keyType: form.keyType,
      keySize: form.keyType === 'rsa' ? form.keySize : undefined,
      passphrase: form.passphrase || undefined,
      comment: form.comment || undefined
    })

    generatedKeyPair.value = result
    ElMessage.success('密钥对生成成功')
  } catch (error) {
    ElMessage.error('密钥对生成失败')
  } finally {
    generating.value = false
  }
}

const copyKey = async (type: 'public' | 'private') => {
  if (!generatedKeyPair.value) return

  const key = type === 'public' ? generatedKeyPair.value.publicKey : generatedKeyPair.value.privateKey
  try {
    await navigator.clipboard.writeText(key)
    ElMessage.success(`${type === 'public' ? '公钥' : '私钥'}已复制到剪贴板`)
  } catch {
    ElMessage.error('复制失败')
  }
}

const downloadKey = (type: 'private' | 'public') => {
  if (!generatedKeyPair.value) return

  const key = type === 'public' ? generatedKeyPair.value.publicKey : generatedKeyPair.value.privateKey
  const filename = type === 'public' ? `${form.name}.pub` : form.name

  const blob = new Blob([key], { type: 'text/plain' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)

  ElMessage.success(`${type === 'public' ? '公钥' : '私钥'}已下载`)
}

const formatDate = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

const resetFormState = () => {
  Object.keys(formInitialState).forEach(key => {
    delete (form as any)[key]
  })
  Object.assign(form, { ...formInitialState })
  generatedKeyPair.value = null
}

const handleClose = () => {
  resetFormState()
  visible.value = false
}

const saveAndClose = () => {
  emit('success')
  handleClose()
}
</script>

<style scoped>
.keypair-dialog :deep(.el-dialog__body) {
  padding: 20px 24px;
}

.generated-keys {
  margin-top: 20px;
}

.key-section {
  margin-bottom: 16px;
}

.key-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
  font-size: 14px;
  font-weight: 500;
  color: #303133;
}

.key-actions {
  display: flex;
  gap: 8px;
}

.key-textarea :deep(.el-textarea__inner) {
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  font-size: 12px;
  background-color: #f5f7fa;
}

.key-info {
  margin-top: 16px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

</style>

<style>
/* === Dark Mode === */
html.dark .key-header {
  color: #e5eaf3;
}

html.dark .key-textarea .el-textarea__inner {
  background-color: #1a1a1a;
}
</style>
