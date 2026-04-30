<template>
  <el-dialog
    v-model="visible"
    :title="t('containerTemplate.title')"
    width="1000px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <div class="template-content">
      <!-- 模板列表 -->
      <div class="template-list-section">
        <div class="section-header">
          <h3>{{ t('containerTemplate.savedTemplates') }}</h3>
          <div class="header-buttons">
            <el-button @click="showImportDialog = true">
              <el-icon><Upload /></el-icon>
              {{ t('containerTemplate.import') }}
            </el-button>
            <el-button type="primary" @click="showCreateDialog = true">
              <el-icon><Plus /></el-icon>
              {{ t('containerTemplate.createTemplate') }}
            </el-button>
          </div>
        </div>

        <div class="template-grid">
          <div
            v-for="template in templates"
            :key="template.id"
            class="template-card"
            :class="{ 'selected': selectedTemplate?.id === template.id }"
            @click="selectTemplate(template)"
          >
            <div class="template-header">
              <div class="template-title">
                <el-tag :type="getTemplateTypeColor(template.type)" size="small">
                  {{ getTemplateTypeLabel(template.type) }}
                </el-tag>
                <h4>{{ template.name }}</h4>
              </div>
              <div class="template-actions">
                <el-button
                  type="primary"
                  size="small"
                  @click.stop="useTemplate(template)"
                >
                  {{ t('containerTemplate.use') }}
                </el-button>
                <el-dropdown @command="handleTemplateAction" trigger="click">
                  <el-button size="small">
                    <el-icon><MoreFilled /></el-icon>
                  </el-button>
                  <template #dropdown>
                    <el-dropdown-menu>
                      <el-dropdown-item :command="`edit-${template.id}`">{{ t('common.edit') }}</el-dropdown-item>
                      <el-dropdown-item :command="`duplicate-${template.id}`">{{ t('containerTemplate.duplicate') }}</el-dropdown-item>
                      <el-dropdown-item :command="`export-${template.id}`">{{ t('containerTemplate.export') }}</el-dropdown-item>
                      <el-dropdown-item :command="`delete-${template.id}`" divided>{{ t('common.delete') }}</el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
              </div>
            </div>

            <div class="template-info">
              <div class="info-item">
                <span class="label">{{ t('containerTemplate.image') }}:</span>
                <span class="value">{{ template.image }}</span>
              </div>
              <div class="info-item" v-if="template.ports && template.ports.length > 0">
                <span class="label">{{ t('containerTemplate.ports') }}:</span>
                <span class="value">
                  <el-tag
                    v-for="port in template.ports.slice(0, 2)"
                    :key="`${port.containerPort}/${port.protocol}`"
                    size="small"
                  >
                    {{ port.hostPort }}:{{ port.containerPort }}
                  </el-tag>
                  <span v-if="template.ports.length > 2" class="more-text">
                    +{{ template.ports.length - 2 }}
                  </span>
                </span>
              </div>
              <div class="info-item" v-if="template.environment && Object.keys(template.environment).length > 0">
                <span class="label">{{ t('containerTemplate.environmentVars') }}:</span>
                <span class="value">
                  <el-tag size="small">
                    {{ Object.keys(template.environment).length }} {{ t('containerTemplate.count') }}
                  </el-tag>
                </span>
              </div>
              <div class="info-item">
                <span class="label">{{ t('containerTemplate.restartPolicy') }}:</span>
                <span class="value">{{ template.restartPolicy?.name || 'no' }}</span>
              </div>
            </div>

            <div class="template-description">
              <p>{{ template.description || t('containerTemplate.noDescription') }}</p>
            </div>

            <div class="template-footer">
              <span class="created-time">
                {{ t('containerTemplate.createdAt') }} {{ formatDateTime(template.createdAt) }}
              </span>
              <span class="updated-time">
                {{ t('containerTemplate.updatedAt') }} {{ formatDateTime(template.updatedAt) }}
              </span>
            </div>
          </div>

          <div v-if="templates.length === 0" class="empty-state">
            <el-empty :description="t('containerTemplate.noTemplates')">
              <el-button type="primary" @click="showCreateDialog = true">
                {{ t('containerTemplate.createFirst') }}
              </el-button>
            </el-empty>
          </div>
        </div>
      </div>

      <!-- 模板预览 -->
      <div v-if="selectedTemplate" class="template-preview-section">
        <div class="section-header">
          <h3>{{ t('containerTemplate.preview') }} - {{ selectedTemplate.name }}</h3>
          <el-button @click="useTemplate(selectedTemplate)" type="primary">
            <el-icon><Check /></el-icon>
            {{ t('containerTemplate.useThis') }}
          </el-button>
        </div>

        <div class="preview-content">
          <el-descriptions :column="2" border>
            <el-descriptions-item :label="t('containerTemplate.name')">
              {{ selectedTemplate.name }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('common.type')">
              <el-tag :type="getTemplateTypeColor(selectedTemplate.type)">
                {{ getTemplateTypeLabel(selectedTemplate.type) }}
              </el-tag>
            </el-descriptions-item>
            <el-descriptions-item :label="t('containerTemplate.image')" :span="2">
              {{ selectedTemplate.image }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('common.description')" :span="2">
              {{ selectedTemplate.description || t('containerTemplate.noDescription') }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('containerTemplate.command')" :span="2" v-if="selectedTemplate.command">
              <code>{{ selectedTemplate.command.join(' ') }}</code>
            </el-descriptions-item>
            <el-descriptions-item :label="t('containerTemplate.workingDir')" :span="2" v-if="selectedTemplate.workingDir">
              {{ selectedTemplate.workingDir }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('containerTemplate.user')" :span="2" v-if="selectedTemplate.user">
              {{ selectedTemplate.user }}
            </el-descriptions-item>
            <el-descriptions-item :label="t('containerTemplate.restartPolicy')" :span="2" v-if="selectedTemplate.restartPolicy">
              {{ selectedTemplate.restartPolicy.name }} ({{ selectedTemplate.restartPolicy.maximumRetryCount }} {{ t('containerTemplate.times') }})
            </el-descriptions-item>
          </el-descriptions>

          <div v-if="selectedTemplate.ports && selectedTemplate.ports.length > 0" class="section-block">
            <h4>{{ t('containerTemplate.portMapping') }}</h4>
            <el-table :data="selectedTemplate.ports" size="small">
              <el-table-column prop="containerPort" :label="t('containerTemplate.containerPort')" width="100" />
              <el-table-column prop="hostPort" :label="t('containerTemplate.hostPort')" width="100" />
              <el-table-column prop="protocol" :label="t('containerTemplate.protocol')" width="80" />
              <el-table-column prop="hostIp" :label="t('containerTemplate.hostIp')" />
            </el-table>
          </div>

          <div v-if="selectedTemplate.volumes && selectedTemplate.volumes.length > 0" class="section-block">
            <h4>{{ t('containerTemplate.volumeMapping') }}</h4>
            <el-table :data="selectedTemplate.volumes" size="small">
              <el-table-column prop="hostPath" :label="t('containerTemplate.hostPath')" />
              <el-table-column prop="containerPath" :label="t('containerTemplate.containerPath')" />
              <el-table-column prop="readOnly" :label="t('containerTemplate.readOnly')" width="80">
                <template #default="{ row }">
                  <el-tag :type="row.readOnly ? 'info' : 'success'" size="small">
                    {{ row.readOnly ? t('common.yes') : t('common.no') }}
                  </el-tag>
                </template>
              </el-table-column>
            </el-table>
          </div>

          <div v-if="selectedTemplate.environment && Object.keys(selectedTemplate.environment).length > 0" class="section-block">
            <h4>{{ t('containerTemplate.environmentVars') }}</h4>
            <div class="env-list">
              <div
                v-for="(value, key) in selectedTemplate.environment"
                :key="key"
                class="env-item"
              >
                <el-tag size="small">{{ key }}</el-tag>
                <span>=</span>
                <span class="env-value">{{ value }}</span>
                </div>
            </div>
          </div>

          <div v-if="selectedTemplate.labels && Object.keys(selectedTemplate.labels).length > 0" class="section-block">
            <h4>{{ t('containerTemplate.labels') }}</h4>
            <div class="label-list">
              <div
                v-for="(value, key) in selectedTemplate.labels"
                :key="key"
                class="label-item"
              >
                <el-tag size="small">{{ key }}: {{ value }}</el-tag>
                </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 导入模板对话框 -->
    <el-dialog
      v-model="showImportDialog"
      :title="t('containerTemplate.importTemplate')"
      width="500px"
      append-to-body
      @close="importForm.templateJson = ''"
    >
      <el-form ref="importFormRef" :model="importForm" :rules="importRules">
        <el-form-item :label="t('containerTemplate.templateJson')" prop="templateJson">
          <el-input
            v-model="importForm.templateJson"
            type="textarea"
            :rows="10"
            :placeholder="t('containerTemplate.templateJsonPlaceholder')"
          />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showImportDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="importTemplate" :loading="importing">
          {{ t('containerTemplate.import') }}
        </el-button>
      </template>
    </el-dialog>

    <!-- 新建/编辑模板对话框 -->
    <el-dialog
      v-model="showCreateDialog"
      :title="isEditing ? t('containerTemplate.editTemplate') : t('containerTemplate.createTemplate')"
      width="700px"
      append-to-body
      @close="resetTemplateForm"
    >
      <el-form ref="templateFormRef" :model="templateForm" :rules="templateRules">
        <el-tabs v-model="createDialogActiveTab">
          <!-- 基本信息 -->
          <el-tab-pane :label="t('containerTemplate.basicInfo')" name="basic">
            <el-form-item :label="t('containerTemplate.name')" prop="name">
              <el-input v-model="templateForm.name" :placeholder="t('containerTemplate.namePlaceholder')" />
            </el-form-item>
            <el-form-item :label="t('containerTemplate.type')" prop="type">
              <el-select v-model="templateForm.type" :placeholder="t('containerTemplate.selectType')">
                <el-option :label="t('containerTemplate.typeWeb')" value="web" />
                <el-option :label="t('containerTemplate.typeDatabase')" value="database" />
                <el-option :label="t('containerTemplate.typeCache')" value="cache" />
                <el-option :label="t('containerTemplate.typeQueue')" value="queue" />
                <el-option :label="t('containerTemplate.typeMonitoring')" value="monitoring" />
                <el-option :label="t('containerTemplate.typeDevelopment')" value="development" />
                <el-option :label="t('containerTemplate.typeCustom')" value="custom" />
              </el-select>
            </el-form-item>
            <el-form-item :label="t('common.description')" prop="description">
              <el-input
                v-model="templateForm.description"
                type="textarea"
                :rows="3"
                :placeholder="t('containerTemplate.descriptionPlaceholder')"
              />
            </el-form-item>
          </el-tab-pane>

          <!-- 容器配置 -->
          <el-tab-pane :label="t('containerTemplate.containerConfig')" name="container">
            <el-form-item :label="t('containerTemplate.image')" prop="image">
              <el-input v-model="templateForm.image" :placeholder="t('containerTemplate.imagePlaceholder')" />
            </el-form-item>
            <el-form-item :label="t('containerTemplate.command')">
              <el-input
                v-model="templateForm.commandStr"
                :placeholder="t('containerTemplate.commandPlaceholder')"
              />
            </el-form-item>
            <el-form-item :label="t('containerTemplate.workingDir')">
              <el-input v-model="templateForm.workingDir" :placeholder="t('containerTemplate.workingDirOptional')" />
            </el-form-item>
            <el-form-item :label="t('containerTemplate.user')">
              <el-input v-model="templateForm.user" :placeholder="t('containerTemplate.userOptional')" />
            </el-form-item>
            <el-form-item :label="t('containerTemplate.restartPolicy')">
              <el-row :gutter="16">
                <el-col :span="12">
                  <el-select v-model="templateForm.restartPolicyName" :placeholder="t('containerTemplate.selectRestartPolicy')">
                    <el-option :label="t('containerTemplate.restartNo')" value="no" />
                    <el-option :label="t('containerTemplate.restartOnFailure')" value="on-failure" />
                    <el-option :label="t('containerTemplate.restartAlways')" value="always" />
                    <el-option :label="t('containerTemplate.restartUnlessStopped')" value="unless-stopped" />
                  </el-select>
                </el-col>
                <el-col :span="12">
                  <el-input-number
                    v-model="templateForm.maximumRetryCount"
                    :min="0"
                    :placeholder="t('containerTemplate.maxRetryCount')"
                  />
                </el-col>
              </el-row>
            </el-form-item>
          </el-tab-pane>

          <!-- 端口配置 -->
          <el-tab-pane :label="t('containerTemplate.portMapping')" name="ports">
            <div class="ports-list">
              <div
                v-for="(port, index) in templateForm.ports"
                :key="index"
                class="port-item"
              >
                <el-row :gutter="8">
                  <el-col :span="6">
                    <el-input-number
                      v-model="port.containerPort"
                      :placeholder="t('containerTemplate.containerPort')"
                      :min="1"
                      :max="65535"
                    />
                  </el-col>
                  <el-col :span="6">
                    <el-input-number
                      v-model="port.hostPort"
                      :placeholder="t('containerTemplate.hostPort')"
                      :min="1"
                      :max="65535"
                    />
                  </el-col>
                  <el-col :span="6">
                    <el-select v-model="port.protocol">
                      <el-option label="TCP" value="tcp" />
                      <el-option label="UDP" value="udp" />
                    </el-select>
                  </el-col>
                  <el-col :span="6">
                    <el-input v-model="port.hostIp" :placeholder="t('containerTemplate.hostIp')" />
                  </el-col>
                  <el-col :span="2">
                    <el-button
                      type="danger"
                      size="small"
                      @click="removePort(index)"
                      :disabled="templateForm.ports.length <= 0"
                    >
                      {{ t('common.delete') }}
                    </el-button>
                  </el-col>
                </el-row>
              </div>
              <el-button type="primary" size="small" @click="addPort">
                <el-icon><Plus /></el-icon>
                {{ t('containerTemplate.addPortMapping') }}
              </el-button>
            </div>
          </el-tab-pane>

          <!-- 卷配置 -->
          <el-tab-pane :label="t('containerTemplate.volumeMapping')" name="volumes">
            <div class="volumes-list">
              <div
                v-for="(volume, index) in templateForm.volumes"
                :key="index"
                class="volume-item"
              >
                <el-row :gutter="8">
                  <el-col :span="10">
                    <el-input v-model="volume.hostPath" :placeholder="t('containerTemplate.hostPath')" />
                  </el-col>
                  <el-col :span="10">
                    <el-input v-model="volume.containerPath" :placeholder="t('containerTemplate.containerPath')" />
                  </el-col>
                  <el-col :span="4">
                    <el-checkbox v-model="volume.readOnly">{{ t('containerTemplate.readOnly') }}</el-checkbox>
                  </el-col>
                  <el-col :span="2">
                    <el-button
                      type="danger"
                      size="small"
                      @click="removeVolume(index)"
                      :disabled="templateForm.volumes.length <= 0"
                    >
                      {{ t('common.delete') }}
                    </el-button>
                  </el-col>
                </el-row>
              </div>
              <el-button type="primary" size="small" @click="addVolume">
                <el-icon><Plus /></el-icon>
                {{ t('containerTemplate.addVolumeMapping') }}
              </el-button>
            </div>
          </el-tab-pane>

          <!-- 环境变量 -->
          <el-tab-pane :label="t('containerTemplate.environmentVars')" name="environment">
            <div class="env-list">
              <div
                v-for="(env, index) in templateForm.envList"
                :key="index"
                class="env-item"
              >
                <el-row :gutter="8">
                  <el-col :span="8">
                    <el-input v-model="env.key" :placeholder="t('containerTemplate.varName')" />
                  </el-col>
                  <el-col :span="8">
                    <el-input v-model="env.value" :placeholder="t('containerTemplate.varValue')" />
                  </el-col>
                  <el-col :span="6">
                    <el-button
                      type="danger"
                      size="small"
                      @click="removeEnv(index)"
                      :disabled="templateForm.envList.length <= 1"
                    >
                      {{ t('common.delete') }}
                    </el-button>
                  </el-col>
                </el-row>
              </div>
              <el-button type="primary" size="small" @click="addEnv">
                <el-icon><Plus /></el-icon>
                {{ t('containerTemplate.addEnvVar') }}
              </el-button>
            </div>
          </el-tab-pane>

          <!-- 标签 -->
          <el-tab-pane :label="t('containerTemplate.labels')" name="labels">
            <div class="labels-list">
              <div
                v-for="(label, index) in templateForm.labelList"
                :key="index"
                class="label-item"
              >
                <el-row :gutter="8">
                  <el-col :span="10">
                    <el-input v-model="label.key" :placeholder="t('containerTemplate.labelKey')" />
                  </el-col>
                  <el-col :span="10">
                    <el-input v-model="label.value" :placeholder="t('containerTemplate.labelValue')" />
                  </el-col>
                  <el-col :span="4">
                    <el-button
                      type="danger"
                      size="small"
                      @click="removeLabel(index)"
                      :disabled="templateForm.labelList.length <= 1"
                    >
                      {{ t('common.delete') }}
                    </el-button>
                  </el-col>
                </el-row>
              </div>
              <el-button type="primary" size="small" @click="addLabel">
                <el-icon><Plus /></el-icon>
                {{ t('containerTemplate.addLabel') }}
              </el-button>
            </div>
          </el-tab-pane>
        </el-tabs>
      </el-form>

      <template #footer>
        <el-button @click="showCreateDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="saveTemplate" :loading="saving">
          {{ isEditing ? t('containerTemplate.update') : t('common.save') }}
        </el-button>
      </template>
    </el-dialog>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance } from 'element-plus'
import {
  Plus,
  MoreFilled,
  Check,
  Upload
} from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import type {
  PortMapping,
  VolumeMapping,
  RestartPolicy
} from '@/api/containers'
import templateApi, { type ContainerTemplate, type TemplateType } from '@/api/templates'
import { formatLocalizedDateTime } from '@/utils/date'

const { t } = useI18n()

interface Props {
  modelValue: boolean
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'template-selected', template: ContainerTemplate): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// 响应式数据
const templates = ref<ContainerTemplate[]>([])
const selectedTemplate = ref<ContainerTemplate | null>(null)
const showCreateDialog = ref(false)
const showImportDialog = ref(false)
const isEditing = ref(false)
const saving = ref(false)
const importing = ref(false)
const createDialogActiveTab = ref('basic')

const templateFormRef = ref<FormInstance>()
const importFormRef = ref<FormInstance>()

const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

// 模板表单初始状态
const templateFormInitialState = {
  name: '',
  type: 'custom' as TemplateType,
  description: '',
  image: '',
  commandStr: '',
  command: [] as string[],
  workingDir: '',
  user: '',
  restartPolicyName: 'no' as string,
  maximumRetryCount: 0,
  ports: [] as PortMapping[],
  volumes: [] as VolumeMapping[],
  envList: [{ key: '', value: '' }],
  environment: {} as Record<string, string>,
  labelList: [{ key: '', value: '' }],
  labels: {} as Record<string, string>
}

// 导入表单初始状态
const importFormInitialState = {
  templateJson: ''
}

// 模板表单数据
const templateForm = reactive({ ...templateFormInitialState })

// 导入表单数据
const importForm = reactive({ ...importFormInitialState })

// 表单验证规则
const templateRules = {
  name: [
    { required: true, message: t('containerTemplate.validation.nameRequired'), trigger: 'blur' },
    { min: 2, max: 50, message: t('containerTemplate.validation.nameLength'), trigger: 'blur' }
  ],
  type: [
    { required: true, message: t('containerTemplate.validation.typeRequired'), trigger: 'change' }
  ],
  image: [
    { required: true, message: t('containerTemplate.validation.imageRequired'), trigger: 'blur' }
  ]
}

const importRules = {
  templateJson: [
    { required: true, message: t('containerTemplate.validation.jsonRequired'), trigger: 'blur' }
  ]
}

// 监听命令字符串变化
watch(() => templateForm.commandStr, (value: string) => {
  templateForm.command = value ? value.trim().split(/\s+/) : []
})

// 监听环境变量列表变化
watch(templateForm.envList, (envList) => {
  templateForm.environment = {}
  envList.forEach(env => {
    if (env.key && env.value) {
      templateForm.environment[env.key as string] = env.value
    }
  })
}, { deep: true })

// 监听标签列表变化
watch(templateForm.labelList, (labelList) => {
  templateForm.labels = {}
  labelList.forEach(label => {
    if (label.key && label.value) {
      templateForm.labels[label.key as string] = label.value
    }
  })
}, { deep: true })

// 监听重启策略变化
watch(() => templateForm.restartPolicyName, (value: string) => {
  const retryCountMap: Record<string, number> = {
    'no': 0,
    'on-failure': 0,
    'always': 0,
    'unless-stopped': 0
  }
  templateForm.maximumRetryCount = retryCountMap[value] || 0
})

// 监听对话框打开
watch(visible, (newValue) => {
  if (newValue) {
    loadTemplates()
  }
})

// 方法
const loading = ref(false)

const loadTemplates = async () => {
  loading.value = true
  try {
    const response = await templateApi.getTemplates()
    // api 拦截器已经返回 response.data，所以这里直接用 response
    templates.value = response || []
  } catch (error: any) {
    console.error('加载模板失败:', error)
    ElMessage.error(t('containerTemplate.loadFailed'))
    templates.value = []
  } finally {
    loading.value = false
  }
}

const selectTemplate = (template: ContainerTemplate) => {
  selectedTemplate.value = template
}

const useTemplate = (template: ContainerTemplate) => {
  emit('template-selected', template)
  visible.value = false
}

const getTemplateTypeColor = (type: TemplateType) => {
  const colorMap: Record<TemplateType, string> = {
    web: 'success',
    database: 'primary',
    cache: 'warning',
    queue: 'danger',
    monitoring: 'info',
    development: 'warning',
    custom: 'info'
  }
  return colorMap[type] || 'info'
}

const getTemplateTypeLabel = (type: TemplateType) => {
  const labelMap: Record<TemplateType, string> = {
    web: 'Web应用',
    database: '数据库',
    cache: '缓存服务',
    queue: '消息队列',
    monitoring: '监控工具',
    development: '开发环境',
    custom: '自定义'
  }
  return labelMap[type] || '自定义'
}

const formatDateTime = (dateString: string) => {
  return formatLocalizedDateTime(dateString, '--')
}

const handleTemplateAction = async (command: string) => {
  // 使用 indexOf 找到第一个 '-'，后面的都是 templateId
  const dashIndex = command.indexOf('-')
  const action = command.substring(0, dashIndex)
  const templateId = command.substring(dashIndex + 1)
  const template = templates.value.find(t => t.id === templateId)

  if (!template) return

  switch (action) {
    case 'edit':
      editTemplate(template)
      break
    case 'duplicate':
      duplicateTemplate(template)
      break
    case 'export':
      exportTemplate(template)
      break
    case 'delete':
      await deleteTemplate(template)
      break
  }
}

const editTemplate = (template: ContainerTemplate) => {
  isEditing.value = true
  showCreateDialog.value = true

  // 填充表单数据
  Object.assign(templateForm, {
    name: template.name,
    type: template.type,
    description: template.description || '',
    image: template.image,
    commandStr: template.command?.join(' ') || '',
    command: template.command || [],
    workingDir: template.workingDir || '',
    user: template.user || '',
    restartPolicyName: template.restartPolicy?.name || 'no',
    maximumRetryCount: template.restartPolicy?.maximumRetryCount || 0,
    ports: template.ports ? [...template.ports] : [],
    volumes: template.volumes ? [...template.volumes] : [],
    envList: template.environment
      ? Object.entries(template.environment).map(([key, value]) => ({ key, value }))
      : [{ key: '', value: '' }],
    environment: template.environment || {},
    labelList: template.labels
      ? Object.entries(template.labels).map(([key, value]) => ({ key, value }))
      : [{ key: '', value: '' }],
    labels: template.labels || {}
  })
}

const duplicateTemplate = async (template: ContainerTemplate) => {
  try {
    const response = await templateApi.duplicateTemplate(template.id)
    templates.value.unshift(response)
    ElMessage.success(t('containerTemplate.duplicateSuccess'))
  } catch (error: any) {
    console.error('复制模板失败:', error)
    ElMessage.error(t('containerTemplate.duplicateFailed'))
  }
}

const exportTemplate = async (template: ContainerTemplate) => {
  try {
    const response = await templateApi.exportTemplate(template.id)
    const templateJson = JSON.stringify(response, null, 2)

    navigator.clipboard.writeText(templateJson).then(() => {
      ElMessage.success(t('containerTemplate.copyToClipboardSuccess'))
    }).catch(() => {
      // 复制失败时显示下载对话框
      const blob = new Blob([templateJson], { type: 'application/json' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${template.name}.json`
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      URL.revokeObjectURL(url)
      ElMessage.success(t('containerTemplate.downloadSuccess'))
    })
  } catch (error: any) {
    console.error('导出模板失败:', error)
    ElMessage.error(t('containerTemplate.exportFailed'))
  }
}

const deleteTemplate = async (template: ContainerTemplate) => {
  try {
    await ElMessageBox.confirm(
      t('containerTemplate.deleteConfirm', { name: template.name }),
      t('common.deleteConfirm'),
      {
        confirmButtonText: t('common.delete'),
        cancelButtonText: t('common.cancel'),
        type: 'warning'
      }
    )

    await templateApi.deleteTemplate(template.id)
    
    const index = templates.value.findIndex(t => t.id === template.id)
    if (index > -1) {
      templates.value.splice(index, 1)
    }

    if (selectedTemplate.value?.id === template.id) {
      selectedTemplate.value = null
    }

    ElMessage.success(t('containerTemplate.deleteSuccess'))
  } catch (error: any) {
    if (error !== 'cancel') {
      console.error('删除模板失败:', error)
      ElMessage.error(t('containerTemplate.deleteFailed'))
    }
  }
}

const saveTemplate = async () => {
  if (!templateFormRef.value) return

  try {
    await templateFormRef.value.validate()

    saving.value = true

    const templateData = {
      name: templateForm.name,
      type: templateForm.type,
      description: templateForm.description || undefined,
      image: templateForm.image,
      command: templateForm.command.length > 0 ? templateForm.command : undefined,
      workingDir: templateForm.workingDir || undefined,
      user: templateForm.user || undefined,
      ports: templateForm.ports.length > 0 ? templateForm.ports.map(p => ({
        hostIp: p.hostIp || undefined,
        hostPort: p.hostPort,
        containerPort: p.containerPort,
        protocol: p.protocol || 'tcp'
      })) : undefined,
      volumes: templateForm.volumes.length > 0 ? templateForm.volumes.map(v => ({
        hostPath: v.hostPath || undefined,
        containerPath: v.containerPath,
        readOnly: v.readOnly || false
      })) : undefined,
      environment: Object.keys(templateForm.environment).length > 0 ? templateForm.environment : undefined,
      labels: Object.keys(templateForm.labels).length > 0 ? templateForm.labels : undefined,
      restartPolicy: {
        name: templateForm.restartPolicyName,
        maximumRetryCount: templateForm.maximumRetryCount || 0
      }
    }

    if (isEditing.value && selectedTemplate.value) {
      const response = await templateApi.updateTemplate(selectedTemplate.value.id, templateData)
      const index = templates.value.findIndex(t => t.id === selectedTemplate.value!.id)
      if (index > -1) {
        templates.value[index] = response
      }
    } else {
      const response = await templateApi.createTemplate(templateData)
      templates.value.unshift(response)
    }

    showCreateDialog.value = false
    resetTemplateForm()
    ElMessage.success(isEditing.value ? t('containerTemplate.updateSuccess') : t('containerTemplate.saveSuccess'))
  } catch (error: any) {
    console.error('保存模板失败:', error)
    ElMessage.error(t('containerTemplate.saveFailed') + ': ' + (error.message || t('containerTemplate.serverError')))
  } finally {
    saving.value = false
  }
}

const importTemplate = async () => {
  if (!importFormRef.value) return

  try {
    await importFormRef.value.validate()

    importing.value = true

    // 尝试解析 JSON
    let templateData: ContainerTemplate
    try {
      templateData = JSON.parse(importForm.templateJson)
    } catch {
      ElMessage.error(t('template.invalidJson'))
      importing.value = false
      return
    }

    // 验证模板数据
    if (!templateData.name) {
      ElMessage.error(t('template.missingNameField'))
      importing.value = false
      return
    }
    if (!templateData.image) {
      ElMessage.error(t('template.missingImageField'))
      importing.value = false
      return
    }

    const response = await templateApi.importTemplate(templateData)
    templates.value.unshift(response)
    
    showImportDialog.value = false
    importForm.templateJson = ''
    ElMessage.success(t('containerTemplate.importSuccess'))
  } catch (error: any) {
    ElMessage.error(t('containerTemplate.importFailed') + '：' + (error.message || t('containerTemplate.serverError')))
  } finally {
    importing.value = false
  }
}

const addPort = () => {
  templateForm.ports.push({
    containerPort: 80,
    hostPort: 80,
    protocol: 'tcp',
    hostIp: ''
  })
}

const removePort = (index: number) => {
  templateForm.ports.splice(index, 1)
}

const addVolume = () => {
  templateForm.volumes.push({
    hostPath: '',
    containerPath: '',
    readOnly: false
  })
}

const removeVolume = (index: number) => {
  templateForm.volumes.splice(index, 1)
}

const addEnv = () => {
  templateForm.envList.push({ key: '', value: '' })
}

const removeEnv = (index: number) => {
  templateForm.envList.splice(index, 1)
}

const addLabel = () => {
  templateForm.labelList.push({ key: '', value: '' })
}

const removeLabel = (index: number) => {
  templateForm.labelList.splice(index, 1)
}

const resetTemplateForm = () => {
  // 重置表单到初始状态
  Object.keys(templateFormInitialState).forEach(key => {
    delete (templateForm as any)[key]
  })
  Object.assign(templateForm, JSON.parse(JSON.stringify(templateFormInitialState)))
  
  isEditing.value = false
  createDialogActiveTab.value = 'basic'
}

const resetImportForm = () => {
  importForm.templateJson = ''
}

const handleClose = () => {
  visible.value = false
  selectedTemplate.value = null
  resetTemplateForm()
  resetImportForm()
}
</script>

<style scoped>
.template-content {
  display: flex;
  flex-direction: column;
  gap: 20px;
  max-height: 70vh;
  overflow-y: auto;
}

.template-list-section,
.template-preview-section {
  background: var(--bg-surface, #f8f9fa);
  border-radius: 8px;
  padding: 16px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.section-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-main, #303133);
}

.header-buttons {
  display: flex;
  gap: 8px;
}

.template-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}

.template-card {
  background: var(--bg-surface, #fff);
  border: 1px solid var(--border-color, #e4e7ed);
  border-radius: 8px;
  padding: 16px;
  cursor: pointer;
  transition: all 0.3s ease;
}

.template-card:hover {
  border-color: #409eff;
  box-shadow: 0 2px 8px rgba(64, 158, 255, 0.1);
}

.template-card.selected {
  border-color: #67c23a;
  background-color: var(--bg-subtle, #f0f9ff);
}

.template-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.template-title {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.template-title h4 {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main, #303133);
}

.template-actions {
  display: flex;
  gap: 8px;
}

.template-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 12px;
}

.info-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
}

.info-item .label {
  color: var(--text-secondary, #606266);
  font-weight: 500;
  min-width: 60px;
}

.info-item .value {
  color: var(--text-main, #303133);
  flex: 1;
}

.more-text {
  color: var(--text-muted, #909399);
  font-size: 11px;
}

.template-description {
  margin-bottom: 12px;
}

.template-description p {
  margin: 0;
  font-size: 12px;
  color: var(--text-secondary, #606266);
  line-height: 1.4;
}

.template-footer {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  color: var(--text-muted, #909399);
}

.empty-state {
  grid-column: 1 / -1;
  padding: 40px 20px;
}

.preview-content {
  background: var(--bg-surface);
  border-radius: 8px;
  padding: 16px;
}

.section-block {
  margin-top: 16px;
}

.section-block h4 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-main, #303133);
}

.ports-list,
.volumes-list,
.env-list,
.label-list {
  margin-bottom: 16px;
}

.port-item,
.volume-item,
.env-item,
.label-item {
  margin-bottom: 8px;
}

.env-value {
  color: var(--text-main, #303133);
}

@media (max-width: 768px) {
  .template-content {
    flex-direction: column;
  }

  .template-grid {
    grid-template-columns: 1fr;
  }

  .template-header {
    flex-direction: column;
    gap: 12px;
  }

  .section-header {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }

  .info-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 4px;
  }

  .template-footer {
    flex-direction: column;
    gap: 4px;
  }
}
</style>