<template>
  <el-dialog
    v-model="dialogVisible"
    :title="t('network.createTitle')"
    width="700px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="120px"
      @submit.prevent="handleSubmit"
    >
      <!-- 基本信息 -->
      <div class="form-section">
        <h4>{{ t('network.basicInfo') }}</h4>
        <el-form-item :label="t('network.networkName')" prop="name">
          <el-input
            v-model="formData.name"
            :placeholder="t('network.enterNetworkName')"
            clearable
          />
        </el-form-item>

        <el-form-item :label="t('network.networkDriver')" prop="driver">
          <el-select v-model="formData.driver" :placeholder="t('network.selectNetworkDriver')">
            <el-option :label="t('network.bridgeOption')" value="bridge" />
            <el-option :label="t('network.overlayOption')" value="overlay" />
            <el-option :label="t('network.hostOption')" value="host" />
            <el-option :label="t('network.macvlanOption')" value="macvlan" />
            <el-option :label="t('network.noneOption')" value="none" />
          </el-select>
        </el-form-item>

        <el-form-item :label="t('network.scope')" prop="scope">
          <el-select v-model="formData.scope" :placeholder="t('network.selectScope')">
            <el-option :label="t('network.scopeLocal')" value="local" />
            <el-option :label="t('network.scopeSwarm')" value="swarm" />
            <el-option :label="t('network.scopeGlobal')" value="global" />
          </el-select>
        </el-form-item>
      </div>

      <!-- 网络选项 -->
      <div class="form-section">
        <h4>{{ t('network.networkOptions') }}</h4>
        <div class="options-grid">
          <el-checkbox v-model="formData.internal">
            {{ t('network.internalNetwork') }}
          </el-checkbox>
          <el-checkbox v-model="formData.enableIPv6" @change="handleIPv6Change">
            {{ t('network.enableIPv6') }}
          </el-checkbox>
          <el-checkbox v-model="formData.ingress" :disabled="formData.driver !== 'overlay'">
            {{ t('network.ingressNetworkOverlay') }}
          </el-checkbox>
          <el-checkbox v-model="formData.attachable">
            {{ t('network.attachable') }}
          </el-checkbox>
        </div>
      </div>

      <!-- IPAM 配置 -->
      <div class="form-section">
        <h4>
          {{ t('network.ipamConfig') }}
          <el-button size="small" text @click="addSubnetConfig" :icon="Plus">{{ t('network.addSubnet') }}</el-button>
        </h4>
        <div class="subnet-configs">
          <div
            v-for="(subnet, index) in formData.ipam.config"
            :key="index"
            class="subnet-config-item"
          >
            <el-card>
              <template #header>
                <div class="subnet-header">
                  <span>{{ t('network.subnetIndex', { index: index + 1 }) }}</span>
                  <el-button
                    size="small"
                    type="danger"
                    text
                    @click="removeSubnetConfig(index)"
                    :disabled="formData.ipam.config.length <= 1"
                   :icon="Delete">{{ t('common.delete') }}</el-button>
                </div>
              </template>
              <div class="subnet-fields">
                <el-form-item :label="t('network.subnetLabel')" :prop="`ipam.config.${index}.subnet`">
                  <el-input
                    v-model="subnet.subnet"
                    :placeholder="t('network.subnetPlaceholderExample')"
                  />
                </el-form-item>
                <el-form-item :label="t('network.gateway')" :prop="`ipam.config.${index}.gateway`">
                  <el-input
                    v-model="subnet.gateway"
                    :placeholder="t('network.gatewayPlaceholderExample')"
                  />
                </el-form-item>
                <el-form-item :label="t('network.ipRange')" :prop="`ipam.config.${index}.ipRange`">
                  <el-input
                    v-model="subnet.ipRange"
                    :placeholder="t('network.ipRangePlaceholderExample')"
                  />
                </el-form-item>
                <div class="aux-addresses">
                  <div class="aux-addresses-header">
                    <span>{{ t('network.auxiliaryAddresses') }}</span>
                    <el-button size="small" text @click="addAuxAddress(index)" :icon="Plus">{{ t('network.add') }}</el-button>
                  </div>
                  <div
                    v-for="(aux, auxIndex) in subnet.auxiliaryAddresses"
                    :key="auxIndex"
                    class="aux-address-item"
                  >
                    <el-input
                      v-model="aux.address"
                      :placeholder="t('network.auxAddressPlaceholderExample')"
                      style="width: 200px"
                    />
                    <el-button
                      size="small"
                      type="danger"
                      text
                      @click="removeAuxAddress(index, auxIndex)"
                     :icon="Delete" />
                  </div>
                </div>
              </div>
            </el-card>
          </div>
        </div>
      </div>

      <!-- 高级选项 -->
      <div class="form-section" v-if="showAdvanced">
        <h4>{{ t('network.advancedOptions') }}</h4>
        <el-form-item :label="t('network.labels')">
          <div class="tags-input">
            <div
              v-for="(tag, index) in formData.labels"
              :key="index"
              class="tag-item"
            >
              <el-input
                v-model="tag.key"
                :placeholder="t('network.keyPlaceholder')"
                style="width: 120px"
              />
              <span class="tag-separator">=</span>
              <el-input
                v-model="tag.value"
                :placeholder="t('network.valuePlaceholder')"
                style="width: 120px"
              />
              <el-button
                size="small"
                type="danger"
                text
                @click="removeLabel(index)"
               :icon="Delete" />
            </div>
            <el-button size="small" text @click="addLabel" :icon="Plus">{{ t('network.addLabel') }}</el-button>
          </div>
        </el-form-item>

        <el-form-item :label="t('network.options')">
          <div class="options-input">
            <div
              v-for="(option, index) in formData.options"
              :key="index"
              class="option-item"
            >
              <el-input
                v-model="option.key"
                :placeholder="t('network.keyPlaceholder')"
                style="width: 120px"
              />
              <span class="option-separator">=</span>
              <el-input
                v-model="option.value"
                :placeholder="t('network.valuePlaceholder')"
                style="width: 120px"
              />
              <el-button
                size="small"
                type="danger"
                text
                @click="removeOption(index)"
               :icon="Delete" />
            </div>
            <el-button size="small" text @click="addOption" :icon="Plus">{{ t('network.addOption') }}</el-button>
          </div>
        </el-form-item>
      </div>

      <!-- 显示高级选项切换 -->
      <div class="advanced-toggle">
        <el-button text @click="showAdvanced = !showAdvanced">
          {{ showAdvanced ? t('network.hide') : t('network.show') }}{{ t('network.advancedOptions') }}
          <el-icon class="el-icon--right">
            <ArrowDown v-if="!showAdvanced" />
            <ArrowUp v-else />
          </el-icon>
        </el-button>
      </div>
    </el-form>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('common.cancel') }}</el-button>
        <el-button @click="showCommand = !showCommand">
          {{ showCommand ? t('network.hide') : t('network.show') }}{{ t('network.command') }}
        </el-button>
        <el-button
          type="primary"
          @click="handleSubmit"
          :loading="loading"
        >
          {{ t('network.createNetwork') }}
        </el-button>
      </div>

      <!-- 生成的Docker命令 -->
      <div v-if="showCommand" class="docker-command">
        <h4>{{ t('network.dockerCommandPreview') }}</h4>
        <el-input
          :model-value="generatedCommand"
          type="textarea"
          :rows="3"
          readonly
          class="command-preview"
        />
        <el-button
          size="small"
          @click="copyCommand"
          style="margin-top: 8px"
         :icon="DocumentCopy">{{ t('network.copyCommand') }}</el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Plus, Delete, ArrowDown, ArrowUp, DocumentCopy } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { useNetworksStore } from '@/stores/networks'

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

const networksStore = useNetworksStore()

// 响应式数据
const loading = ref(false)
const showAdvanced = ref(false)
const showCommand = ref(false)
const formRef = ref<FormInstance>()

const dialogVisible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

// 表单初始状态
const formDataInitialState = {
  name: '',
  driver: 'bridge',
  scope: 'local',
  internal: false,
  enableIPv6: false,
  ingress: false,
  attachable: false,
  ipam: {
    driver: 'default',
    config: [
      {
        subnet: '',
        gateway: '',
        ipRange: '',
        auxiliaryAddresses: []
      }
    ]
  },
  labels: [],
  options: []
}

const formData = reactive({ ...formDataInitialState })

// 表单验证规则
const formRules: FormRules = {
  name: [
    { required: true, message: t('network.enterNetworkName'), trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9][a-zA-Z0-9_-]*$/, message: t('network.networkNameFormat'), trigger: 'blur' }
  ],
  driver: [
    { required: true, message: t('network.selectNetworkDriver'), trigger: 'change' }
  ],
  scope: [
    { required: true, message: t('network.selectScope'), trigger: 'change' }
  ]
}

// 监听对话框打开
watch(dialogVisible, (isOpen) => {
  if (isOpen) {
    resetFormState()
  }
})

// 重置表单
const resetFormState = () => {
  Object.keys(formDataInitialState).forEach(key => {
    delete (formData as any)[key]
  })
  Object.assign(formData, JSON.parse(JSON.stringify(formDataInitialState)))
  showAdvanced.value = false
  showCommand.value = false
}

// 处理IPv6切换
const handleIPv6Change = (enabled: boolean) => {
  if (enabled && formData.ipam.config.length === 1 && !formData.ipam.config[0].subnet) {
    // 如果启用IPv6且没有配置子网，添加默认IPv6子网
    formData.ipam.config[0].subnet = '2001:db8::/64'
  }
}

// 添加子网配置
const addSubnetConfig = () => {
  formData.ipam.config.push({
    subnet: '',
    gateway: '',
    ipRange: '',
    auxiliaryAddresses: []
  })
}

// 移除子网配置
const removeSubnetConfig = (index: number) => {
  if (formData.ipam.config.length > 1) {
    formData.ipam.config.splice(index, 1)
  }
}

// 添加辅助地址
const addAuxAddress = (subnetIndex: number) => {
  formData.ipam.config[subnetIndex].auxiliaryAddresses.push({ address: '' })
}

// 移除辅助地址
const removeAuxAddress = (subnetIndex: number, auxIndex: number) => {
  formData.ipam.config[subnetIndex].auxiliaryAddresses.splice(auxIndex, 1)
}

// 添加标签
const addLabel = () => {
  formData.labels.push({ key: '', value: '' })
}

// 移除标签
const removeLabel = (index: number) => {
  formData.labels.splice(index, 1)
}

// 添加选项
const addOption = () => {
  formData.options.push({ key: '', value: '' })
}

// 移除选项
const removeOption = (index: number) => {
  formData.options.splice(index, 1)
}

// 生成Docker命令
const generatedCommand = computed(() => {
  let command = `docker network create`

  // 网络名称
  if (formData.name) {
    command += ` ${formData.name}`
  }

  // 驱动
  if (formData.driver && formData.driver !== 'bridge') {
    command += ` --driver ${formData.driver}`
  }

  // 作用域
  if (formData.scope && formData.scope !== 'local') {
    command += ` --scope ${formData.scope}`
  }

  // 选项
  if (formData.internal) {
    command += ' --internal'
  }
  if (formData.enableIPv6) {
    command += ' --ipv6'
  }
  if (formData.ingress) {
    command += ' --ingress'
  }
  if (formData.attachable) {
    command += ' --attachable'
  }

  // IPAM配置
  const validSubnets = formData.ipam.config.filter(subnet => subnet.subnet)
  if (validSubnets.length > 0) {
    command += ' --subnet'
    validSubnets.forEach(subnet => {
      command += ` ${subnet.subnet}`
      if (subnet.gateway) {
        command += ` --gateway ${subnet.gateway}`
      }
      if (subnet.ipRange) {
        command += ` --ip-range ${subnet.ipRange}`
      }
      subnet.auxiliaryAddresses.forEach(aux => {
        if (aux.address) {
          command += ` --aux-address ${aux.address}`
        }
      })
    })
  }

  // 标签
  formData.labels.forEach(label => {
    if (label.key && label.value) {
      command += ` --label ${label.key}="${label.value}"`
    }
  })

  // 选项
  formData.options.forEach(option => {
    if (option.key && option.value) {
      command += ` --opt ${option.key}="${option.value}"`
    }
  })

  return command
})

// 复制命令
const copyCommand = async () => {
  try {
    await navigator.clipboard.writeText(generatedCommand.value)
    ElMessage.success(t('network.commandCopied'))
  } catch (error) {
    ElMessage.error(t('common.copyFailed'))
  }
}

// 处理提交
const handleSubmit = async () => {
  if (!formRef.value) return

  try {
    const valid = await formRef.value.validate()
    if (!valid) return

    loading.value = true

    // 构建网络配置
    const networkConfig: any = {
      name: formData.name,
      driver: formData.driver,
      scope: formData.scope,
      internal: formData.internal,
      enableIPv6: formData.enableIPv6,
      ingress: formData.ingress,
      attachable: formData.attachable,
      ipam: {
        driver: formData.ipam.driver,
        config: formData.ipam.config.filter(config => config.subnet).map(config => ({
          subnet: config.subnet,
          gateway: config.gateway || undefined,
          ipRange: config.ipRange || undefined,
          auxiliaryAddresses: config.auxiliaryAddresses
            .filter(aux => aux.address)
            .map(aux => aux.address)
        }))
      },
      labels: {},
      options: {}
    }

    // 处理标签
    formData.labels.forEach(label => {
      if (label.key && label.value) {
        networkConfig.labels[label.key] = label.value
      }
    })

    // 处理选项
    formData.options.forEach(option => {
      if (option.key && option.value) {
        networkConfig.options[option.key] = option.value
      }
    })

    // 创建网络
    await networksStore.createNetwork(networkConfig)

    ElMessage.success(t('network.networkCreated'))
    emit('success')
    handleClose()
  } catch (error: any) {
    console.error('创建网络失败:', error)
    ElMessage.error(error.message || t('network.networkCreateFailed'))
  } finally {
    loading.value = false
  }
}

// 处理关闭
const handleClose = () => {
  dialogVisible.value = false
}
</script>

<style scoped>
.form-section {
  margin-bottom: 24px;
  padding-bottom: 16px;
  border-bottom: 1px solid #f0f0f0;
}

.form-section:last-child {
  border-bottom: none;
}

.form-section h4 {
  margin: 0 0 16px 0;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  display: flex;
  align-items: center;
  gap: 8px;
}

.options-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 12px;
}

.subnet-configs {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.subnet-config-item {
  border: 1px solid #e4e7ed;
  border-radius: 8px;
}

.subnet-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: 500;
}

.subnet-fields {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.aux-addresses {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.aux-addresses-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 8px;
}

.aux-address-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.tags-input,
.options-input {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.tag-item,
.option-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.tag-separator,
.option-separator {
  color: #909399;
  font-weight: 500;
}

.advanced-toggle {
  margin-top: 16px;
  text-align: center;
}

.docker-command {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #e4e7ed;
}

.docker-command h4 {
  margin: 0 0 8px 0;
  font-size: 14px;
  color: #606266;
}

.command-preview {
  font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
  font-size: 12px;
  line-height: 1.4;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  align-items: center;
}

:deep(.el-form-item__label) {
  font-weight: 500;
}

:deep(.el-checkbox__label) {
  font-weight: normal;
}

:deep(.el-card__body) {
  padding: 16px;
}

@media (max-width: 768px) {
  .options-grid {
    grid-template-columns: 1fr;
  }

  .tag-item,
  .option-item {
    flex-direction: column;
    align-items: stretch;
  }

  .aux-address-item {
    flex-direction: column;
    align-items: stretch;
  }

  .dialog-footer {
    flex-direction: column;
  }
}
</style>