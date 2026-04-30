<template>
  <el-dialog
    v-model="visible"
    :title="editCompose ? t('compose.create.editTitle') : t('compose.create.createTitle')"
    width="800px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="formData"
      :rules="rules"
      label-width="120px"
      class="create-compose-form"
    >
      <el-form-item :label="t('compose.create.projectName')" prop="name">
        <el-input
          v-model="formData.name"
          :placeholder="t('compose.create.projectNamePlaceholder')"
          clearable
        />
      </el-form-item>

      <el-form-item :label="t('common.description')" prop="description">
        <el-input
          v-model="formData.description"
          type="textarea"
          :rows="3"
          :placeholder="t('compose.create.descriptionPlaceholder')"
        />
      </el-form-item>

      <el-form-item :label="t('compose.create.contentLabel')" prop="content">
        <el-tabs v-model="activeTab" class="compose-tabs">
          <el-tab-pane :label="t('compose.create.yamlEditor')" name="editor">
            <el-input
              v-model="formData.content"
              type="textarea"
              :rows="15"
              :placeholder="t('compose.create.contentPlaceholder')"
              class="compose-editor"
            />
          </el-tab-pane>

          <el-tab-pane :label="t('compose.create.visualEditor')" name="visual">
            <div class="visual-editor">
              <el-button
                type="primary"
                :icon="Plus"
                @click="addService"
                class="add-service-btn"
              >
                {{ t('compose.create.addService') }}
              </el-button>

              <div v-if="formData.services.length === 0" class="empty-state">
                <el-empty :description="t('compose.create.noService')" />
              </div>

              <div v-else class="services-list">
                <el-card
                  v-for="(service, index) in formData.services"
                  :key="index"
                  class="service-card"
                >
                  <template #header>
                    <div class="service-header">
                      <el-input
                        v-model="service.name"
                        :placeholder="t('compose.create.serviceName')"
                        style="width: 200px"
                      />
                      <el-button
                        type="danger"
                        :icon="Delete"
                        size="small"
                        @click="removeService(index)"
                      />
                    </div>
                  </template>

                  <!-- 基础配置 -->
                  <el-row :gutter="16">
                    <el-col :span="12">
                      <el-form-item label="镜像">
                        <el-input
                          v-model="service.image"
                          placeholder="例如: nginx:latest"
                        />
                      </el-form-item>
                    </el-col>
                    <el-col :span="12">
                      <el-form-item label="容器名">
                        <el-input
                          v-model="service.containerName"
                          placeholder="可选"
                        />
                      </el-form-item>
                    </el-col>
                  </el-row>

                  <!-- 端口和环境变量 -->
                  <el-row :gutter="16">
                    <el-col :span="12">
                      <el-form-item label="端口映射">
                        <el-input
                          v-model="service.ports"
                          type="textarea"
                          :rows="2"
                          placeholder="每行一个，如: 80:80"
                        />
                      </el-form-item>
                    </el-col>
                    <el-col :span="12">
                      <el-form-item label="环境变量">
                        <el-input
                          v-model="service.environment"
                          type="textarea"
                          :rows="2"
                          placeholder="每行一个，如: KEY=value"
                        />
                      </el-form-item>
                    </el-col>
                  </el-row>

                  <!-- 卷挂载 -->
                  <el-form-item label="卷挂载">
                    <el-input
                      v-model="service.volumes"
                      type="textarea"
                      :rows="2"
                      placeholder="每行一个，如: ./data:/app/data"
                    />
                  </el-form-item>

                  <!-- 高级配置 - 折叠面板 -->
                  <el-collapse class="advanced-config">
                    <el-collapse-item title="构建配置" name="build">
                      <el-row :gutter="16">
                        <el-col :span="12">
                          <el-form-item label="构建上下文">
                            <el-input v-model="service.context" placeholder="如: ./app" />
                          </el-form-item>
                        </el-col>
                        <el-col :span="12">
                          <el-form-item label="Dockerfile">
                            <el-input v-model="service.dockerfile" placeholder="如: Dockerfile.dev" />
                          </el-form-item>
                        </el-col>
                      </el-row>
                    </el-collapse-item>

                    <el-collapse-item title="运行配置" name="runtime">
                      <el-row :gutter="16">
                        <el-col :span="12">
                          <el-form-item label="命令">
                            <el-input v-model="service.command" placeholder="如: npm start" />
                          </el-form-item>
                        </el-col>
                        <el-col :span="12">
                          <el-form-item label="入口点">
                            <el-input v-model="service.entrypoint" placeholder="如: /bin/sh" />
                          </el-form-item>
                        </el-col>
                      </el-row>
                      <el-row :gutter="16">
                        <el-col :span="12">
                          <el-form-item label="工作目录">
                            <el-input v-model="service.workingDir" placeholder="如: /app" />
                          </el-form-item>
                        </el-col>
                        <el-col :span="12">
                          <el-form-item label="重启策略">
                            <el-select v-model="service.restart" placeholder="选择重启策略" clearable style="width: 100%">
                              <el-option label="always" value="always" />
                              <el-option label="unless-stopped" value="unless-stopped" />
                              <el-option label="on-failure" value="on-failure" />
                              <el-option label="no" value="no" />
                            </el-select>
                          </el-form-item>
                        </el-col>
                      </el-row>
                    </el-collapse-item>

                    <el-collapse-item title="依赖与网络" name="deps">
                      <el-row :gutter="16">
                        <el-col :span="12">
                          <el-form-item label="依赖服务">
                            <el-input
                              v-model="service.dependsOn"
                              placeholder="逗号分隔，如: db, redis"
                            />
                          </el-form-item>
                        </el-col>
                        <el-col :span="12">
                          <el-form-item label="网络">
                            <el-input
                              v-model="service.networks"
                              placeholder="逗号分隔"
                            />
                          </el-form-item>
                        </el-col>
                      </el-row>
                    </el-collapse-item>

                    <el-collapse-item title="资源限制" name="resources">
                      <el-row :gutter="16">
                        <el-col :span="12">
                          <el-form-item label="内存限制">
                            <el-input v-model="service.memLimit" placeholder="如: 512M" />
                          </el-form-item>
                        </el-col>
                        <el-col :span="12">
                          <el-form-item label="CPU 限制">
                            <el-input v-model="service.cpuCount" placeholder="如: 0.5" />
                          </el-form-item>
                        </el-col>
                      </el-row>
                    </el-collapse-item>
                  </el-collapse>
                </el-card>
              </div>
            </div>
          </el-tab-pane>
        </el-tabs>
      </el-form-item>

      <el-form-item :label="t('compose.create.tags')">
        <el-tag
          v-for="tag in formData.tags"
          :key="tag"
          closable
          @close="removeTag(tag)"
          class="tag-item"
        >
          {{ tag }}
        </el-tag>
        <el-input
          v-if="tagInputVisible"
          ref="tagInputRef"
          v-model="tagInputValue"
          size="small"
          style="width: 100px"
          @keyup.enter="handleTagInputConfirm"
          @blur="handleTagInputConfirm"
        />
        <el-button
          v-else
          size="small"
          @click="showTagInput"
        >
          + {{ t('compose.create.addTag') }}
        </el-button>
      </el-form-item>
    </el-form>

    <!-- 部署进度显示 -->
    <div v-if="deploying || deployStep" class="deploy-progress">
      <div class="progress-header">
        <el-icon class="is-loading" v-if="deploying"><Loading /></el-icon>
        <el-icon v-else-if="deployProgress === 100" style="color: var(--el-color-success)"><SuccessFilled /></el-icon>
        <span class="progress-text">{{ deployStep }}</span>
      </div>
      <el-progress 
        :percentage="deployProgress" 
        :status="deployProgress === 100 ? 'success' : ''"
        :stroke-width="8"
        :show-text="false"
      />
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">{{ t('common.cancel') }}</el-button>
        <el-button type="info" @click="validateCompose">{{ t('compose.create.validate') }}</el-button>
        <el-button v-if="editCompose" type="warning" @click="handleConfirm(true)" :loading="loading">
          {{ t('compose.create.saveAndDeploy') }}
        </el-button>
        <el-button type="primary" @click="handleConfirm(false)" :loading="loading">
          {{ editCompose ? t('compose.create.update') : t('compose.create.create') }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, reactive, nextTick, watch, onMounted, onUnmounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Loading, Plus, SuccessFilled } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { composeApi } from '@/api/compose'
import { signalrService } from '@/services/signalr'
import type { ComposeFile, ComposeValidationResult } from '@/types/compose'

const { t } = useI18n()

interface ComposeService {
  name: string
  image: string
  ports: string
  environment: string
  volumes: string
  // 构建配置
  build?: string
  context?: string
  dockerfile?: string
  // 运行配置
  containerName?: string
  command?: string
  entrypoint?: string
  workingDir?: string
  restart?: string
  // 依赖与网络
  dependsOn?: string
  networks?: string
  networkMode?: string
  // 资源限制
  memLimit?: string
  memReservation?: string
  cpuCount?: string
  cpuShares?: string
}

interface Props {
  modelValue: boolean
  editCompose?: ComposeFile
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'success', deployAfterSave?: boolean): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const visible = ref(false)
const loading = ref(false)
const activeTab = ref('editor')
const tagInputVisible = ref(false)
const tagInputValue = ref('')
const tagInputRef = ref()
const isSyncing = ref(false) // 防止循环同步
const parseLoading = ref(false)

// 部署进度状态
const deploying = ref(false)
const deployStep = ref('')
const deployProgress = ref(0)

const formRef = ref<FormInstance>()

const formDataInitialState = {
  name: '',
  description: '',
  content: '',
  tags: [] as string[],
  services: [] as ComposeService[]
}

const formData = reactive({ ...formDataInitialState })

const rules: FormRules = {
  name: [
    { required: true, message: t('compose.create.nameRequired'), trigger: 'blur' },
    { min: 2, max: 50, message: t('compose.create.nameLength'), trigger: 'blur' }
  ],
  content: [
    { required: true, message: t('compose.create.contentRequired'), trigger: 'blur' }
  ]
}

watch(() => props.modelValue, (newVal) => {
  visible.value = newVal
  if (newVal) {
    resetFormState()
    if (props.editCompose) {
      loadEditData()
    }
  }
})

// SignalR 部署进度监听
let unsubscribeDeployProgress: (() => void) | null = null

onMounted(() => {
  unsubscribeDeployProgress = signalrService.subscribe('compose-deploy-progress', (message) => {
    const data = message.data
    // 只处理当前项目的进度
    if (props.editCompose && data.projectId === props.editCompose.name) {
      deployStep.value = `${data.step}: ${data.detail || ''}`
      deployProgress.value = data.progress
      deploying.value = data.progress < 100
    }
  })
})

onUnmounted(() => {
  if (unsubscribeDeployProgress) {
    unsubscribeDeployProgress()
  }
})

// 监听 Tab 切换，实现联动
watch(activeTab, async (newTab, oldTab) => {
  if (isSyncing.value) return
  
  if (newTab === 'visual' && oldTab === 'editor') {
    // YAML → 可视化：解析 YAML 填充 services
    await parseYamlToServices()
  } else if (newTab === 'editor' && oldTab === 'visual') {
    // 可视化 → YAML：生成 YAML 更新 content
    syncServicesToYaml()
  }
})

// 监听 services 变化，实时同步到 YAML
watch(() => formData.services, () => {
  if (isSyncing.value || activeTab.value !== 'visual') return
  syncServicesToYaml()
}, { deep: true })

const resetFormState = () => {
  Object.keys(formDataInitialState).forEach(key => {
    delete (formData as any)[key]
  })
  Object.assign(formData, JSON.parse(JSON.stringify(formDataInitialState)))
  activeTab.value = 'editor'
  nextTick(() => {
    formRef.value?.clearValidate()
  })
}

const loadEditData = () => {
  if (!props.editCompose) return

  formData.name = props.editCompose.name
  formData.description = props.editCompose.description || ''
  formData.content = props.editCompose.content
  formData.tags = props.editCompose.tags ? [...props.editCompose.tags] : []
  formData.services = []
  activeTab.value = 'editor'
}

const handleClose = () => {
  emit('update:modelValue', false)
}

// 解析 YAML 内容到 services 数组
const parseYamlToServices = async () => {
  if (!formData.content.trim()) {
    formData.services = []
    return
  }

  try {
    isSyncing.value = true
    parseLoading.value = true
    
    // 调用后端使用 Compose.NET 解析
    const result = await composeApi.parseComposeContent(formData.content)
    
    // 使用后端返回的服务详情
    if (result.serviceDetails && result.serviceDetails.length > 0) {
      formData.services = result.serviceDetails.map(s => ({
        name: s.name,
        image: s.image,
        ports: s.ports.join('\n'),
        environment: Object.entries(s.environment || {})
          .map(([k, v]) => `${k}=${v || ''}`)
          .join('\n'),
        volumes: s.volumes.join('\n'),
        // 新增字段
        containerName: s.containerName || '',
        context: s.context || '',
        dockerfile: s.dockerfile || '',
        command: s.command || '',
        entrypoint: s.entrypoint || '',
        workingDir: s.workingDir || '',
        restart: s.restart || '',
        dependsOn: (s.dependsOn || []).join(', '),
        networks: (s.networks || []).join(', '),
        networkMode: s.networkMode || '',
        memLimit: s.memLimit ? `${s.memLimit}` : '',
        cpuCount: s.cpuCount ? `${s.cpuCount}` : ''
      }))
      ElMessage.success(t('compose.create.parseSuccess') || `成功解析 ${result.serviceDetails.length} 个服务`)
    } else {
      formData.services = []
      ElMessage.warning(t('compose.create.parseFailed') || '未找到服务定义')
    }
  } catch (error) {
    console.error('解析 YAML 失败:', error)
    ElMessage.warning(t('compose.create.parseFailed') || 'YAML 解析失败，请检查格式')
  } finally {
    isSyncing.value = false
    parseLoading.value = false
  }
}

// 从 YAML 内容解析服务详情（前端备用解析 - 已弃用，保留以防后端失败）
const parseServicesFromYaml = (content: string): ComposeService[] => {
  const services: ComposeService[] = []
  
  try {
    const lines = content.split('\n')
    let currentService: ComposeService | null = null
    let inServices = false
    let currentIndent = 0
    
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i]
      const trimmed = line.trim()
      
      if (trimmed === 'services:') {
        inServices = true
        continue
      }
      
      if (trimmed.startsWith('networks:') || trimmed.startsWith('volumes:')) {
        inServices = false
        continue
      }
      
      if (!inServices) continue
      
      // 检测服务名（2空格缩进，以冒号结尾）
      if (line.startsWith('  ') && !line.startsWith('    ') && trimmed.endsWith(':')) {
        if (currentService) {
          services.push(currentService)
        }
        currentService = {
          name: trimmed.slice(0, -1),
          image: '',
          ports: '',
          environment: '',
          volumes: ''
        }
        currentIndent = line.indexOf(trimmed)
        continue
      }
      
      if (!currentService) continue
      
      // 解析服务属性
      if (trimmed.startsWith('image:')) {
        currentService.image = trimmed.replace('image:', '').trim().replace(/['"]/g, '')
      } else if (trimmed.startsWith('ports:')) {
        // 继续读取后续的端口列表
        const ports: string[] = []
        let j = i + 1
        while (j < lines.length && lines[j].match(/^\s{6,}-/)) {
          const portLine = lines[j].trim().replace(/^-\s*/, '').replace(/['"]/g, '')
          ports.push(portLine)
          j++
        }
        currentService.ports = ports.join('\n')
      } else if (trimmed.startsWith('environment:')) {
        // 读取环境变量
        const envs: string[] = []
        let j = i + 1
        while (j < lines.length) {
          const envLine = lines[j]
          if (envLine.match(/^\s{6,}-/)) {
            // 数组格式
            const env = envLine.trim().replace(/^-\s*/, '').replace(/['"]/g, '')
            envs.push(env)
            j++
          } else if (envLine.match(/^\s{6,}[A-Za-z_]/)) {
            // 对象格式
            const [key, ...vals] = envLine.trim().split(':')
            const value = vals.join(':').trim().replace(/['"]/g, '')
            envs.push(`${key}=${value}`)
            j++
          } else {
            break
          }
        }
        currentService.environment = envs.join('\n')
      } else if (trimmed.startsWith('volumes:')) {
        // 读取卷挂载
        const volumes: string[] = []
        let j = i + 1
        while (j < lines.length && lines[j].match(/^\s{6,}-/)) {
          const volLine = lines[j].trim().replace(/^-\s*/, '').replace(/['"]/g, '')
          volumes.push(volLine)
          j++
        }
        currentService.volumes = volumes.join('\n')
      }
    }
    
    if (currentService) {
      services.push(currentService)
    }
  } catch (error) {
    console.error('解析服务失败:', error)
  }
  
  return services
}

// 同步 services 到 YAML
const syncServicesToYaml = () => {
  isSyncing.value = true
  formData.content = generateComposeYaml()
  nextTick(() => {
    isSyncing.value = false
  })
}

const generateComposeYaml = () => {
  if (formData.services.length === 0) return formData.content || ''

  const servicesLines: string[] = []
  
  formData.services.forEach(service => {
    if (!service.name) return
    
    const lines: string[] = [`  ${service.name}:`]
    
    if (service.image) {
      lines.push(`    image: ${service.image}`)
    }
    
    // 端口
    if (service.ports) {
      const ports = service.ports.split('\n')
        .map(p => p.trim())
        .filter(p => p)
      if (ports.length > 0) {
        lines.push('    ports:')
        ports.forEach(port => {
          lines.push(`      - "${port}"`)
        })
      }
    }
    
    // 环境变量
    if (service.environment) {
      const envs = service.environment.split('\n')
        .map(e => e.trim())
        .filter(e => e)
      if (envs.length > 0) {
        lines.push('    environment:')
        envs.forEach(env => {
          const [key, ...vals] = env.split('=')
          const value = vals.join('=')
          lines.push(`      - ${key}=${value}`)
        })
      }
    }
    
    // 卷挂载
    if (service.volumes) {
      const volumes = service.volumes.split('\n')
        .map(v => v.trim())
        .filter(v => v)
      if (volumes.length > 0) {
        lines.push('    volumes:')
        volumes.forEach(vol => {
          lines.push(`      - ${vol}`)
        })
      }
    }
    
    // 容器名
    if (service.containerName) {
      lines.push(`    container_name: ${service.containerName}`)
    }
    
    // 构建配置
    if (service.context) {
      lines.push(`    build:`)
      lines.push(`      context: ${service.context}`)
      if (service.dockerfile) {
        lines.push(`      dockerfile: ${service.dockerfile}`)
      }
    }
    
    // 命令
    if (service.command) {
      lines.push(`    command: ${service.command}`)
    }
    
    // 入口点
    if (service.entrypoint) {
      lines.push(`    entrypoint: ${service.entrypoint}`)
    }
    
    // 工作目录
    if (service.workingDir) {
      lines.push(`    working_dir: ${service.workingDir}`)
    }
    
    // 重启策略
    if (service.restart) {
      lines.push(`    restart: ${service.restart}`)
    }
    
    // 依赖
    if (service.dependsOn) {
      const deps = service.dependsOn.split(',').map(d => d.trim()).filter(d => d)
      if (deps.length > 0) {
        lines.push('    depends_on:')
        deps.forEach(dep => {
          lines.push(`      - ${dep}`)
        })
      }
    }
    
    // 网络
    if (service.networks) {
      const nets = service.networks.split(',').map(n => n.trim()).filter(n => n)
      if (nets.length > 0) {
        lines.push('    networks:')
        nets.forEach(net => {
          lines.push(`      - ${net}`)
        })
      }
    }
    
    // 资源限制
    if (service.memLimit || service.cpuCount) {
      lines.push('    deploy:')
      lines.push('      resources:')
      lines.push('        limits:')
      if (service.memLimit) {
        lines.push(`          memory: ${service.memLimit}`)
      }
      if (service.cpuCount) {
        lines.push(`          cpus: '${service.cpuCount}'`)
      }
    }
    
    servicesLines.push(lines.join('\n'))
  })

  return `version: '3.8'
services:
${servicesLines.join('\n\n')}
`
}

const addService = () => {
  formData.services.push({
    name: '',
    image: '',
    ports: '',
    environment: '',
    volumes: '',
    containerName: '',
    context: '',
    dockerfile: '',
    command: '',
    entrypoint: '',
    workingDir: '',
    restart: '',
    dependsOn: '',
    networks: '',
    networkMode: '',
    memLimit: '',
    cpuCount: ''
  })
}

const removeService = (index: number) => {
  formData.services.splice(index, 1)
}

const removeTag = (tag: string) => {
  const index = formData.tags.indexOf(tag)
  if (index > -1) {
    formData.tags.splice(index, 1)
  }
}

const showTagInput = () => {
  tagInputVisible.value = true
  nextTick(() => {
    tagInputRef.value?.focus()
  })
}

const handleTagInputConfirm = () => {
  const value = tagInputValue.value.trim()
  if (value && !formData.tags.includes(value)) {
    formData.tags.push(value)
  }
  tagInputVisible.value = false
  tagInputValue.value = ''
}

const validateCompose = async () => {
  try {
    const content = activeTab.value === 'editor' ? formData.content : generateComposeYaml()

    if (!content.trim()) {
      ElMessage.warning(t('compose.create.contentRequired'))
      return
    }

    loading.value = true
    const result = await composeApi.validateCompose({ content })

    if (result.isValid) {
      ElMessage.success(t('compose.create.validateSuccess'))
    } else {
      ElMessage.error(`${t('compose.create.validateFailed')}: ${result.errors?.join(', ') || t('compose.create.formatError')}`)
    }
  } catch (error) {
    console.error('验证Compose文件失败:', error)
    ElMessage.error(t('compose.create.validateFailedMsg'))
  } finally {
    loading.value = false
  }
}

const handleConfirm = async (deployAfterSave = false) => {
  if (!formRef.value) return

  try {
    await formRef.value.validate()

    const content = activeTab.value === 'editor' ? formData.content : generateComposeYaml()

    if (!content.trim()) {
      ElMessage.warning(t('compose.create.contentRequired'))
      return
    }

    loading.value = true

    const composeData: Partial<ComposeFile> = {
      name: formData.name,
      description: formData.description,
      content: content,
      tags: formData.tags
    }

    if (props.editCompose) {
      // 编辑模式 - 保存
      deployStep.value = t('compose.create.saving')
      deployProgress.value = 20
      await composeApi.updateComposeFile(props.editCompose.id, composeData)
      
      if (deployAfterSave) {
        // 开始部署
        deploying.value = true
        deployStep.value = t('compose.create.deploying')
        deployProgress.value = 50
        
        try {
          await composeApi.deployCompose({ composeFileId: props.editCompose.id })
          deployStep.value = t('compose.create.deploySuccess')
          deployProgress.value = 100
          ElMessage.success(t('compose.create.saveAndDeploySuccess'))
        } catch (deployError: any) {
          deployStep.value = t('compose.create.deployFailed')
          deployProgress.value = 100
          ElMessage.error(`${t('compose.create.deployFailed')}: ${deployError.message || t('common.unknown')}`)
        }
      } else {
        ElMessage.success(t('compose.create.updateSuccess'))
      }
    } else {
      // 创建模式
      await composeApi.createComposeFile(composeData as any)
      ElMessage.success(t('compose.create.createSuccess'))
    }

    emit('success', deployAfterSave)
    handleClose()
  } catch (error: any) {
    console.error(props.editCompose ? '更新Compose项目失败:' : '创建Compose项目失败:', error)
    ElMessage.error(props.editCompose ? t('compose.create.updateFailed') : t('compose.create.createFailed'))
  } finally {
    loading.value = false
    deploying.value = false
    deployStep.value = ''
    deployProgress.value = 0
  }
}
</script>

<style scoped>
.create-compose-form {
  padding: 16px 0;
}

.compose-tabs {
  width: 100%;
}

.compose-editor {
  font-family: 'JetBrains Mono', 'Monaco', 'Menlo', monospace;
  font-size: 13px;
  line-height: 1.6;
}

.visual-editor {
  min-height: 400px;
}

.add-service-btn {
  margin-bottom: 20px;
  height: 40px;
  padding: 0 20px;
  border-radius: var(--border-radius, 8px);
  font-weight: 500;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.2);
  transition: all 0.2s ease;
}

.add-service-btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.3);
}

.deploy-progress {
  margin-top: 16px;
  padding: 16px;
  background: var(--bg-subtle, #f1f5f9);
  border-radius: var(--border-radius, 8px);
  border: 1px solid var(--border-color, #e2e8f0);
}

.progress-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.progress-header .el-icon {
  font-size: 18px;
}

.progress-text {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-main, #1e293b);
}

.empty-state {
  text-align: center;
  padding: 60px 0;
  background: var(--bg-subtle, #f1f5f9);
  border-radius: var(--border-radius-lg, 12px);
  border: 2px dashed var(--border-color, #e2e8f0);
}

.services-list {
  max-height: 500px;
  overflow-y: auto;
  padding-right: 4px;
}

.service-card {
  margin-bottom: 16px;
  border-radius: var(--border-radius-lg, 12px) !important;
  border: 1px solid var(--border-color, #e2e8f0) !important;
  overflow: hidden;
  transition: all 0.2s ease;
}

.service-card:hover {
  border-color: var(--color-secondary, #3b82f6) !important;
  box-shadow: 0 4px 16px rgba(59, 130, 246, 0.1);
}

.service-card :deep(.el-card__header) {
  padding: 14px 20px !important;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.08), rgba(59, 130, 246, 0.02));
  border-bottom: 1px solid var(--border-color, #e2e8f0);
}

.service-card :deep(.el-card__body) {
  padding: 20px !important;
}

.service-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.service-header .el-input {
  flex: 1;
  max-width: 240px;
}

.service-header .el-input :deep(.el-input__wrapper) {
  font-weight: 600;
  font-size: 15px;
}

.service-header .el-input :deep(.el-input__inner) {
  color: var(--text-main, #0f172a);
}

.service-collapse {
  border: none !important;
  --el-collapse-header-bg-color: transparent;
  --el-collapse-content-bg-color: transparent;
}

.service-collapse :deep(.el-collapse-item__header) {
  height: 40px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary, #475569);
  border-bottom: none !important;
  padding: 0 4px;
}

.service-collapse :deep(.el-collapse-item__header:hover) {
  color: var(--color-secondary, #3b82f6);
}

.service-collapse :deep(.el-collapse-item__wrap) {
  border-bottom: none !important;
}

.service-collapse :deep(.el-collapse-item__content) {
  padding-bottom: 8px;
}

.tag-item {
  margin-right: 8px;
  margin-bottom: 8px;
  border-radius: var(--border-radius-full, 9999px);
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

/* Form Items */
:deep(.el-form-item) {
  margin-bottom: 18px;
}

:deep(.el-form-item__label) {
  font-weight: 500;
  font-size: 13px;
  color: var(--text-secondary, #475569);
}

/* Inputs */
:deep(.el-input__wrapper),
:deep(.el-textarea__inner) {
  border-radius: var(--border-radius, 8px) !important;
  transition: all 0.2s ease;
}

:deep(.el-input__wrapper:hover),
:deep(.el-textarea__inner:hover) {
  border-color: var(--text-muted, #94a3b8) !important;
}

:deep(.el-input__wrapper.is-focus),
:deep(.el-textarea__inner:focus) {
  border-color: var(--color-secondary, #3b82f6) !important;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1) !important;
}

/* Tabs */
:deep(.el-tabs__content) {
  padding: 16px 0;
}

:deep(.el-tabs__item) {
  font-weight: 500;
  font-size: 14px;
  padding: 0 20px;
}

:deep(.el-tabs__item.is-active) {
  color: var(--color-secondary, #3b82f6);
}

:deep(.el-tabs__active-bar) {
  background-color: var(--color-secondary, #3b82f6);
}

/* Scrollbar */
.services-list::-webkit-scrollbar {
  width: 6px;
}

.services-list::-webkit-scrollbar-track {
  background: transparent;
}

.services-list::-webkit-scrollbar-thumb {
  background: var(--border-color, #e2e8f0);
  border-radius: 3px;
}

.services-list::-webkit-scrollbar-thumb:hover {
  background: var(--text-muted, #94a3b8);
}

/* Buttons in form */
:deep(.el-button--primary) {
  background: var(--color-secondary, #3b82f6);
  border-color: var(--color-secondary, #3b82f6);
}

:deep(.el-button--primary:hover) {
  background: #2563eb;
  border-color: #2563eb;
}

:deep(.el-button--danger) {
  background: var(--color-danger, #ef4444);
  border-color: var(--color-danger, #ef4444);
}

:deep(.el-button--danger:hover) {
  background: #dc2626;
  border-color: #dc2626;
}
</style>