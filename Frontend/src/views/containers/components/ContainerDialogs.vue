<template>
  <!-- Edit Container Name Dialog (simple rename) -->
  <el-dialog v-model="editVisible" :title="t('container.dialogs.renameContainer')" width="450px" class="glass-dialog">
    <el-form :model="editForm" label-position="top" size="small">
      <el-form-item :label="t('container.dialogs.containerName')">
        <el-input v-model="editForm.name" :placeholder="t('container.dialogs.newNamePlaceholder')" />
      </el-form-item>
      
      <div class="config-info" v-if="containerImage">
        <span class="label">{{ t('common.image') }}:</span>
        <span class="value font-mono">{{ containerImage }}</span>
      </div>
      <div class="config-info" v-if="containerState">
        <span class="label">{{ t('common.status') }}:</span>
        <el-tag :type="containerState === 'running' ? 'success' : 'info'" size="small">
          {{ containerState === 'running' ? t('container.running') : t('container.stopped') }}
        </el-tag>
      </div>
      
      <el-alert type="info" :closable="false" style="margin-top: 16px">
        {{ t('container.dialogs.editConfigHint') }}
      </el-alert>

      <el-divider />

      <el-form-item>
        <el-checkbox v-model="editOptions.pullLatest">
          {{ t('container.pullLatest') }}
        </el-checkbox>
        <div class="option-hint">{{ t('container.dialogs.pullLatestOnEditHint') }}</div>
      </el-form-item>
    </el-form>
    
    <template #footer>
      <el-button @click="editVisible = false">{{ t('common.cancel') }}</el-button>
      <el-button type="primary" @click="handleSaveEdit" :loading="loading">
        {{ editOptions.pullLatest ? t('container.dialogs.saveAndRecreate') : t('common.save') }}
      </el-button>
    </template>
  </el-dialog>

  <!-- Recreate Dialog -->
  <el-dialog v-model="recreateVisible" :title="t('container.recreate')" width="450px" class="glass-dialog" :close-on-click-modal="recreatePhase === 'idle'">
    <div class="recreate-dialog-content">
      <el-alert type="warning" :closable="false" style="margin-bottom: 16px">
        {{ t('container.dialogs.recreateWarning') }}
      </el-alert>
      
      <div class="container-info" v-if="containerName">
        <span class="label">{{ t('container.dialogs.containerNameLabel') }}:</span>
        <span class="value font-mono">{{ containerName }}</span>
      </div>
      <div class="container-info" v-if="containerImage">
        <span class="label">{{ t('common.image') }}:</span>
        <span class="value font-mono">{{ containerImage }}</span>
      </div>
      
      <el-divider v-if="recreatePhase === 'idle'" />
      
      <!-- 选项区域（idle 时显示） -->
      <el-form v-if="recreatePhase === 'idle'" label-position="top" size="small">
        <el-form-item>
          <el-checkbox v-model="recreateOptions.pullLatest">
            {{ t('container.pullLatest') }}
          </el-checkbox>
          <div class="option-hint">{{ t('container.dialogs.pullLatestHint') }}</div>
        </el-form-item>
        
        <el-form-item>
          <el-checkbox v-model="recreateOptions.autoStart">
            {{ t('container.autoStart') }}
          </el-checkbox>
          <div class="option-hint">{{ t('container.dialogs.autoStartHint') }}</div>
        </el-form-item>
        
        <el-form-item>
          <el-checkbox v-model="recreateOptions.keepVolumes" disabled>
            {{ t('container.keepVolumes') }}
          </el-checkbox>
          <div class="option-hint">{{ t('container.dialogs.keepVolumesHint') }}</div>
        </el-form-item>
      </el-form>
      
      <!-- 进度区域（running 时显示） -->
      <div v-if="recreatePhase === 'running'" class="recreate-progress-area">
        <el-progress 
          :percentage="recreateProgress" 
          :stroke-width="8"
          :status="recreateProgress >= 100 ? 'success' : ''"
          style="margin: 16px 0"
        />
        <div class="recreate-progress-detail">{{ recreateDetail }}</div>
      </div>
    </div>
    
    <template #footer>
      <el-button v-if="recreatePhase === 'idle'" @click="recreateVisible = false">{{ t('common.cancel') }}</el-button>
      <el-button 
        v-if="recreatePhase === 'idle'"
        type="primary" 
        @click="handleConfirmRecreate" 
        :loading="loading"
      >
        {{ t('container.dialogs.confirmRecreate') }}
      </el-button>
      <span v-if="recreatePhase === 'running'" class="text-muted text-sm">{{ t('container.dialogs.recreateInProgress') }}</span>
    </template>
  </el-dialog>

  <!-- Connect Network Dialog -->
  <el-dialog v-model="connectNetworkVisible" :title="t('container.connectNetwork')" width="450px" class="glass-dialog">
    <div class="connect-network-form">
      <el-form label-position="top">
        <el-form-item :label="t('container.dialogs.selectNetwork')">
          <el-select v-model="selectedNetworkId" :placeholder="t('container.dialogs.selectNetworkPlaceholder')" class="w-full" filterable>
            <el-option 
              v-for="net in availableNetworks" 
              :key="net.id" 
              :label="net.name" 
              :value="net.id"
            >
              <div class="network-option">
                <span class="network-option-name">{{ net.name }}</span>
                <span class="network-option-driver">{{ net.driver }}</span>
              </div>
            </el-option>
          </el-select>
        </el-form-item>
      </el-form>
      <div v-if="availableNetworks.length === 0" class="no-networks-hint">
        <el-empty :description="t('container.dialogs.noAvailableNetworks')" :image-size="60" />
      </div>
    </div>
    <template #footer>
      <el-button @click="connectNetworkVisible = false">{{ t('common.cancel') }}</el-button>
      <el-button 
        type="primary" 
        @click="handleConnectNetwork" 
        :loading="connectingNetwork"
        :disabled="!selectedNetworkId"
      >
        {{ t('container.dialogs.connect') }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, watch, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import type { NetworkInfo } from '@/types/network'

const { t } = useI18n()

const props = defineProps<{
  editDialogVisible: boolean
  recreateDialogVisible: boolean
  connectNetworkDialogVisible: boolean
  availableNetworks: NetworkInfo[]
  connectingNetwork: boolean
  loading?: boolean
  containerName?: string
  containerImage?: string
  containerState?: string
  autoStart?: boolean
  recreatePhase?: 'idle' | 'running' | 'completed' | 'failed'
  recreateProgress?: number
  recreateDetail?: string
}>()

const emit = defineEmits<{
  'update:editDialogVisible': [value: boolean]
  'update:recreateDialogVisible': [value: boolean]
  'update:connectNetworkDialogVisible': [value: boolean]
  'save-edit': [payload: { name: string; pullLatest: boolean }]
  'confirm-recreate': [options: { pullLatest: boolean; autoStart: boolean; keepVolumes: boolean }]
  'connect-network': [networkId: string]
}>()

const selectedNetworkId = ref('')

// 编辑表单
const editForm = reactive({
  name: ''
})

// 编辑时是否同时拉取最新镜像并重建
const editOptions = reactive({
  pullLatest: false
})

// 重建选项
const recreateOptions = reactive({
  pullLatest: false,
  autoStart: true,
  keepVolumes: true
})

// 双向绑定
const editVisible = ref(props.editDialogVisible)
const recreateVisible = ref(props.recreateDialogVisible)
const connectNetworkVisible = ref(props.connectNetworkDialogVisible)

watch(() => props.editDialogVisible, v => {
  editVisible.value = v
  if (v) {
    editForm.name = props.containerName?.replace(/^\//, '') || ''
    editOptions.pullLatest = false
  }
})
watch(() => props.recreateDialogVisible, v => recreateVisible.value = v)
watch(() => props.connectNetworkDialogVisible, v => connectNetworkVisible.value = v)

watch(editVisible, v => emit('update:editDialogVisible', v))
watch(recreateVisible, v => emit('update:recreateDialogVisible', v))
watch(connectNetworkVisible, v => emit('update:connectNetworkDialogVisible', v))

// 初始化自动启动选项
watch(() => props.autoStart, v => {
  if (v !== undefined) {
    recreateOptions.autoStart = v
  }
}, { immediate: true })

// 事件处理
const handleSaveEdit = () => {
  if (!editForm.name.trim()) return
  emit('save-edit', { name: editForm.name.trim(), pullLatest: editOptions.pullLatest })
}

const handleConfirmRecreate = () => {
  emit('confirm-recreate', { ...recreateOptions })
}

const handleConnectNetwork = () => {
  emit('connect-network', selectedNetworkId.value)
  selectedNetworkId.value = ''
}
</script>

<style>

.recreate-dialog-content {
  padding: 0 4px;
}

.container-info,
.config-info {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.container-info .label,
.config-info .label {
  color: var(--text-muted);
  font-size: 13px;
}

.container-info .value,
.config-info .value {
  color: var(--text-main);
  font-size: 13px;
}

.option-hint {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 4px;
  margin-left: 24px;
}

.network-option {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.network-option-name {
  font-weight: 500;
}

.network-option-driver {
  font-size: 12px;
  color: var(--text-muted);
}

.no-networks-hint {
  padding: 20px 0;
}

.w-full {
  width: 100%;
}

.font-mono {
  font-family: 'JetBrains Mono', monospace;
}

.recreate-progress-area {
  padding: 8px 4px;
}

.recreate-progress-detail {
  font-size: 12px;
  color: var(--text-muted);
  text-align: center;
  word-break: break-all;
}

.text-sm {
  font-size: 13px;
}

</style>