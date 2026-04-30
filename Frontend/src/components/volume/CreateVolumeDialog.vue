<template>
  <el-dialog
    v-model="visible"
    :title="t('volume.createTitle')"
    width="600px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="120px"
      label-position="left"
    >
      <!-- 基本信息 -->
      <el-card class="form-section" :header="t('volume.basicInfo')">
        <el-form-item :label="t('volume.volumeName')" prop="name">
          <el-input
            v-model="form.name"
            :placeholder="t('volume.volumeNamePlaceholder')"
            clearable
          />
          <div class="form-tip">
            {{ t('volume.namePattern') }}
          </div>
        </el-form-item>

        <el-form-item :label="t('volume.driverType')" prop="driver">
          <el-select v-model="form.driver" style="width: 100%" :placeholder="t('volume.selectDriverType')">
            <el-option :label="t('volume.localDriver')" value="local" />
            <el-option :label="t('volume.nfsDriver')" value="nfs" />
            <el-option :label="t('volume.bindDriver')" value="bind" />
            <el-option :label="t('volume.tmpfsDriver')" value="tmpfs" />
          </el-select>
        </el-form-item>

        <el-form-item :label="t('volume.scope')" prop="scope">
          <el-radio-group v-model="form.scope">
            <el-radio label="local">{{ t('volume.localScope') }}</el-radio>
            <el-radio label="global">{{ t('volume.globalScope') }}</el-radio>
          </el-radio-group>
          <div class="form-tip">
            {{ t('volume.scopeHint') }}
          </div>
        </el-form-item>

        <el-form-item v-if="form.scope === 'local'" :label="t('volume.targetNode')" prop="nodeId">
          <el-select v-model="form.nodeId" style="width: 100%" :placeholder="t('volume.selectNode')">
            <el-option
              v-for="node in availableNodes"
              :key="node.id"
              :label="node.name"
              :value="node.id"
            />
          </el-select>
        </el-form-item>
      </el-card>

      <!-- 驱动选项 -->
      <el-card class="form-section" :header="t('volume.driverOptions')">
        <div class="driver-options">
          <div
            v-for="(option, index) in formArrays.options"
            :key="index"
            class="option-item"
          >
            <el-form-item
              :label="index === 0 ? t('common.key') : ''"
              :prop="`options.${index}.key`"
            >
              <el-input
                v-model="option.key"
                :placeholder="t('volume.optionKey')"
                clearable
              />
            </el-form-item>
            <el-form-item
              :label="index === 0 ? t('common.value') : ''"
              :prop="`options.${index}.value`"
            >
              <el-input
                v-model="option.value"
                :placeholder="t('volume.optionValue')"
                clearable
              />
            </el-form-item>
            <el-form-item :label="index === 0 ? t('volume.operation') : ''">
              <el-button
                type="danger"
                size="small"
                @click="removeOption(index)"
                :disabled="formArrays.options.length <= 1"
              >
                {{ t('common.delete') }}
              </el-button>
            </el-form-item>
          </div>
          <el-button type="primary" @click="addOption" style="margin-top: 12px">
            <el-icon><Plus /></el-icon>
            {{ t('volume.addDriverOption') }}
          </el-button>
        </div>

        <!-- 常用驱动选项预设 -->
        <el-divider content-position="left">{{ t('volume.commonPresets') }}</el-divider>
        <div class="option-presets">
          <el-button
            v-for="preset in driverPresets[form.driver] || []"
            :key="preset.name"
            size="small"
            @click="applyPreset(preset.options)"
          >
            {{ preset.name }}
          </el-button>
        </div>
      </el-card>

      <!-- 标签 -->
      <el-card class="form-section" :header="t('volume.labels')">
        <div class="label-list">
          <div
            v-for="(label, index) in formArrays.labels"
            :key="index"
            class="label-item"
          >
            <el-form-item
              :label="index === 0 ? t('common.key') : ''"
              :prop="`labels.${index}.key`"
            >
              <el-input
                v-model="label.key"
                :placeholder="t('volume.labelKey')"
                clearable
              />
            </el-form-item>
            <el-form-item
              :label="index === 0 ? t('common.value') : ''"
              :prop="`labels.${index}.value`"
            >
              <el-input
                v-model="label.value"
                :placeholder="t('volume.labelValue')"
                clearable
              />
            </el-form-item>
            <el-form-item :label="index === 0 ? t('volume.operation') : ''">
              <el-button
                type="danger"
                size="small"
                @click="removeLabel(index)"
                :disabled="formArrays.labels.length <= 1"
              >
                {{ t('common.delete') }}
              </el-button>
            </el-form-item>
          </div>
          <el-button type="primary" @click="addLabel" style="margin-top: 12px">
            <el-icon><Plus /></el-icon>
            {{ t('volume.addLabel') }}
          </el-button>
        </div>
      </el-card>

      <!-- 高级配置 -->
      <el-card class="form-section" :header="t('volume.advancedConfig')" v-if="form.driver === 'nfs'">
        <el-form-item :label="t('volume.nfsServer')">
          <el-input
            v-model="nfsConfig.server"
            :placeholder="t('volume.nfsServerPlaceholder')"
            clearable
          />
        </el-form-item>
        <el-form-item :label="t('volume.remotePath')">
          <el-input
            v-model="nfsConfig.path"
            :placeholder="t('volume.remotePathPlaceholder')"
            clearable
          />
        </el-form-item>
        <el-form-item :label="t('volume.mountOptions')">
          <el-input
            v-model="nfsConfig.options"
            :placeholder="t('volume.mountOptionsPlaceholder')"
            clearable
          />
        </el-form-item>
      </el-card>
    </el-form>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="loading">
          {{ t('volume.createVolume') }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage, type FormInstance } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { volumeApi, type CreateVolumeRequest } from '@/api/volume'
import { useNodesStore } from '@/stores/nodes'
import type { NodeInfo } from '@/api/nodes'

const { t } = useI18n()

interface Props {
  modelValue: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'success'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const nodesStore = useNodesStore()

// 响应式数据
const formRef = ref<FormInstance>()
const loading = ref(false)

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

// 可用节点列表
const availableNodes = ref<NodeInfo[]>([])

// 表单初始状态
const formInitialState: CreateVolumeRequest = {
  name: '',
  driver: 'local',
  scope: 'local',
  nodeId: '',
  labels: {},
  options: {}
}

const formArraysInitialState = {
  labels: [{ key: '', value: '' }],
  options: [{ key: '', value: '' }]
}

const nfsConfigInitialState = {
  server: '',
  path: '',
  options: 'rw,soft,timeo=30'
}

// 表单数据
const form = reactive<CreateVolumeRequest>({ ...formInitialState })

// 为了方便绑定，使用数组形式管理选项和标签
const formArrays = reactive({ ...formArraysInitialState })

// NFS配置（用于快速配置）
const nfsConfig = reactive({ ...nfsConfigInitialState })

// 驱动预设
const driverPresets: Record<string, Array<{ name: string; options: Record<string, string> }>> = {
  local: [
    {
      name: t('volume.basicConfig'),
      options: {}
    },
    {
      name: t('volume.performanceOptimized'),
      options: {
        'type': 'none',
        'o': 'size=100m'
      }
    }
  ],
  nfs: [
    {
      name: t('volume.standardNfs'),
      options: {
        'type': 'nfs',
        'o': 'addr=SERVER_IP,rw'
      }
    },
    {
      name: t('volume.highPerfNfs'),
      options: {
        'type': 'nfs',
        'o': 'addr=SERVER_IP,rw,hard,nolock'
      }
    }
  ],
  tmpfs: [
    {
      name: t('volume.memoryStorage'),
      options: {
        'type': 'tmpfs',
        'device': 'tmpfs'
      }
    },
    {
      name: t('volume.largeMemStorage'),
      options: {
        'type': 'tmpfs',
        'device': 'tmpfs',
        'size': '1g'
      }
    }
  ]
}

// 表单验证规则
const rules = {
  name: [
    { required: true, message: t('volume.nameRequired'), trigger: 'blur' },
    {
      pattern: /^[a-zA-Z0-9][a-zA-Z0-9_-]*$/,
      message: t('volume.namePattern'),
      trigger: 'blur'
    },
    { min: 1, max: 63, message: t('volume.nameLengthRange'), trigger: 'blur' }
  ],
  driver: [
    { required: true, message: t('volume.driverRequired'), trigger: 'change' }
  ],
  scope: [
    { required: true, message: t('volume.scopeRequired'), trigger: 'change' }
  ],
  nodeId: [
    {
      validator: (rule: any, value: string, callback: Function) => {
        if (form.scope === 'local' && !value) {
          callback(new Error(t('volume.localVolumeNodeRequired')))
        } else {
          callback()
        }
      },
      trigger: 'change'
    }
  ]
}

// 监听表单数组变化
watch(formArrays.labels, (labels) => {
  form.labels = {}
  labels.forEach(label => {
    if (label.key && label.value) {
      form.labels[label.key] = label.value
    }
  })
}, { deep: true })

watch(formArrays.options, (options) => {
  form.options = {}
  options.forEach(option => {
    if (option.key && option.value) {
      form.options[option.key] = option.value
    }
  })
}, { deep: true })

// 监听NFS配置变化
watch(() => [nfsConfig.server, nfsConfig.path, nfsConfig.options], () => {
  if (form.driver === 'nfs' && nfsConfig.server && nfsConfig.path) {
    form.options = {
      'type': 'nfs',
      'device': `:${nfsConfig.path}`,
      'o': `addr=${nfsConfig.server},${nfsConfig.options}`
    }

    // 更新选项数组
    const optionEntries = Object.entries(form.options)
    formArrays.options = optionEntries.map(([key, value]) => ({
      key,
      value
    }))
  }
}, { deep: true })

// 监听对话框打开
watch(visible, async (newValue) => {
  if (newValue) {
    await loadAvailableNodes()
  }
})

// 方法
const loadAvailableNodes = async () => {
  try {
    availableNodes.value = await nodesStore.fetchNodes()
  } catch (error) {
    console.error(t('volume.fetchNodesFailed'), ':', error)
  }
}

const addOption = () => {
  formArrays.options.push({ key: '', value: '' })
}

const removeOption = (index: number) => {
  formArrays.options.splice(index, 1)
}

const addLabel = () => {
  formArrays.labels.push({ key: '', value: '' })
}

const removeLabel = (index: number) => {
  formArrays.labels.splice(index, 1)
}

const applyPreset = (presetOptions: Record<string, string>) => {
  formArrays.options = Object.entries(presetOptions).map(([key, value]) => ({
    key,
    value
  }))

  // 如果是NFS预设，解析并填充到NFS配置中
  if (form.driver === 'nfs') {
    const device = presetOptions.device || ''
    const options = presetOptions.o || ''

    if (device.startsWith(':')) {
      nfsConfig.path = device.substring(1)
    }

    const addrMatch = options.match(/addr=([^,]+)/)
    if (addrMatch) {
      nfsConfig.server = addrMatch[1]
    }

    const otherOptions = options.replace(/addr=[^,]+,?/, '')
    if (otherOptions) {
      nfsConfig.options = otherOptions
    }
  }
}

const handleSubmit = async () => {
  if (!formRef.value) return

  try {
    await formRef.value.validate()
    loading.value = true

    await volumeApi.createVolume(form)

    ElMessage.success(t('volume.createSuccess'))
    emit('success')
    handleClose()
  } catch (error: any) {
    console.error(t('volume.createFailed'), ':', error)
    ElMessage.error(error.response?.data?.message || t('volume.createFailed'))
  } finally {
    loading.value = false
  }
}

const handleClose = () => {
  visible.value = false
  resetFormState()
}

const resetFormState = () => {
  // 清除验证
  formRef.value?.clearValidate?.()
  
  // 重置 form
  Object.keys(formInitialState).forEach(key => {
    delete (form as any)[key]
  })
  Object.assign(form, JSON.parse(JSON.stringify(formInitialState)))
  
  // 重置 formArrays
  Object.keys(formArraysInitialState).forEach(key => {
    delete (formArrays as any)[key]
  })
  Object.assign(formArrays, JSON.parse(JSON.stringify(formArraysInitialState)))
  
  // 重置 nfsConfig
  Object.assign(nfsConfig, nfsConfigInitialState)
}
</script>

<style scoped>
.form-section {
  margin-bottom: 16px;
}

.driver-options,
.label-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.option-item,
.label-item {
  display: grid;
  grid-template-columns: 1fr 1fr 80px;
  gap: 12px;
  align-items: center;
}

.option-presets {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.form-tip {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
  line-height: 1.4;
}

.dialog-footer {
  text-align: right;
}

@media (max-width: 768px) {
  .option-item,
  .label-item {
    grid-template-columns: 1fr;
    gap: 8px;
  }
}
</style>