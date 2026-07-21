<template>
  <div class="containers-page" v-loading="loading">
    <!-- Page Header -->
    <header class="page-header">
      <div class="header-content">
        <h1 class="page-title">{{ t('container.title') }}</h1>
        <p class="page-subtitle">{{ t('container.subtitle') }}</p>
      </div>
      <div class="header-actions">
        <button class="btn btn-primary" @click="openCreate">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
          {{ t('container.deploy') }}
        </button>
        <button class="btn btn-secondary" @click="refreshData">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="23 4 23 10 17 10"></polyline>
            <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"></path>
          </svg>
          {{ t('common.refresh') }}
        </button>
      </div>
    </header>

    <!-- Toolbar -->
    <div class="toolbar">
      <div class="search-box">
        <svg class="search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="11" cy="11" r="8"></circle>
          <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
        </svg>
        <input 
          v-model="search" 
          type="text" 
          :placeholder="t('common.search')" 
          class="search-input"
        />
      </div>
      <div class="filter-tabs">
        <button 
          v-for="tab in filterTabs"
          :key="tab.value"
          class="filter-tab"
          :class="{ active: filter === tab.value }"
          @click="filter = tab.value"
        >
          <span class="tab-dot" :class="tab.color"></span>
          {{ tab.label }}
          <span class="tab-count">{{ getCount(tab.value) }}</span>
        </button>
      </div>
      <button 
        v-show="selected.length > 0"
        class="btn btn-danger btn-sm"
        @click="batchDelete"
      >
        {{ t('container.batchDelete') }} ({{ selected.length }})
      </button>
      <button 
        v-show="selected.length > 0 && selectedWithUpdates > 0"
        class="btn btn-primary btn-sm"
        @click="batchUpgrade"
        :disabled="upgradingContainers.size > 0"
      >
        {{ upgradingContainers.size > 0 ? t('container.upgrade.upgrading') : t('container.upgrade.batchUpgrade') }} ({{ selectedWithUpdates }})
      </button>
    </div>

    <!-- Data Table -->
    <div class="data-table" v-if="paginatedContainers.length > 0">
      <el-table
        :data="paginatedContainers"
        style="width: 100%"
        v-loading="loading"
        row-key="id"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="40" reserve-selection />

        <el-table-column :label="t('container.table.nameId')" min-width="200">
          <template #default="{ row }">
            <div class="td-name">
              <div class="status-dot" :class="row.state"></div>
              <div class="name-info">
                <span class="name" @click="viewDetail(row)">
                  {{ row.name || 'unnamed' }}
                </span>
                <code class="id">{{ row.id?.substring(0, 12) }}</code>
              </div>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('container.image')" min-width="200">
          <template #default="{ row }">
            <div class="td-image">
              <span class="image-text">{{ row.image }}</span>
              <span v-if="hasUpdateAvailable(row.id)" class="update-badge" :title="t('container.update.newVersionAvailable')">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="12" height="12">
                  <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                  <polyline points="7 10 12 15 17 10"></polyline>
                  <line x1="12" y1="15" x2="12" y2="3"></line>
                </svg>
                {{ t('container.update.hasUpdate') }}
              </span>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('common.status')" width="110" align="center">
          <template #default="{ row }">
            <span class="status-badge" :class="row.state">
              {{ getStatusText(row.state) }}
            </span>
          </template>
        </el-table-column>

        <!-- Usage Column -->
        <el-table-column :label="t('container.table.usage')" min-width="140">
          <template #default="{ row }">
            <div v-if="row.state === 'running'" class="usage-info-text">
              <template v-if="getContainerStats(row.id)">
                <span>CPU: {{ (getContainerStats(row.id)?.cpuStats?.percentCpu || 0).toFixed(1) }}%</span>
                <span>MEM: {{ formatBytes(getContainerStats(row.id)?.memoryStats?.usage || 0) }}</span>
              </template>
              <span v-else class="usage-loading">{{ t('container.update.fetching') }}</span>
            </div>
            <span v-else class="usage-none">-</span>
          </template>
        </el-table-column>

        <el-table-column :label="t('common.ports')" min-width="120" align="center">
          <template #default="{ row }">
            <el-popover
              v-if="normalizePorts(row.ports).length"
              placement="top"
              :width="280"
              trigger="hover"
              :show-after="200"
              popper-class="ports-popover"
            >
              <template #reference>
                <div class="ports-trigger">
                  <span class="ports-trigger-count">{{ normalizePorts(row.ports).length }} {{ t('container.ports.portsCount') }}</span>
                  <svg class="ports-trigger-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="6 9 12 15 18 9"></polyline>
                  </svg>
                </div>
              </template>
              <div class="ports-popover-content">
                <!-- 映射端口 -->
                <div v-if="getMappedPorts(normalizePorts(row.ports)).length" class="ports-section">
                  <div class="ports-section-title">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M22 12h-4l-3 9L9 3l-3 9H2"></path>
                    </svg>
                    {{ t('container.ports.mappedPorts') }} ({{ getMappedPorts(normalizePorts(row.ports)).length }})
                  </div>
                  <div class="ports-section-list">
                    <div v-for="(port, idx) in getMappedPorts(normalizePorts(row.ports))" :key="'m-'+idx" class="port-item mapped">
                      <span class="port-mapping">{{ port.publicPort }} → {{ port.privatePort }}</span>
                      <span class="port-proto">{{ (port.type || port.protocol || 'tcp').toUpperCase() }}</span>
                    </div>
                  </div>
                </div>
                <!-- 内部端口 -->
                <div v-if="getInternalPorts(normalizePorts(row.ports)).length" class="ports-section">
                  <div class="ports-section-title">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
                      <line x1="12" y1="8" x2="12" y2="16"></line>
                      <line x1="8" y1="12" x2="16" y2="12"></line>
                    </svg>
                    {{ t('container.ports.internalPorts') }} ({{ getInternalPorts(normalizePorts(row.ports)).length }})
                  </div>
                  <div class="ports-section-list">
                    <div v-for="(port, idx) in getInternalPorts(normalizePorts(row.ports))" :key="'i-'+idx" class="port-item internal">
                      <span class="port-mapping">{{ port.privatePort }}</span>
                      <span class="port-proto">{{ (port.type || port.protocol || 'tcp').toUpperCase() }}</span>
                    </div>
                  </div>
                </div>
                <!-- 反向代理 -->
                <div v-if="row.domainMappings && row.domainMappings.length" class="ports-section">
                  <div class="ports-section-title">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"></path>
                      <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"></path>
                    </svg>
                    {{ t('container.ports.reverseProxy') }} ({{ row.domainMappings.length }})
                  </div>
                  <div class="ports-section-list">
                    <div v-for="(mapping, idx) in row.domainMappings" :key="'p-'+idx" class="port-item proxy">
                      <span class="port-mapping proxy-domain">{{ mapping.domain }}</span>
                      <span class="port-proto" :class="{ ssl: mapping.enableSsl }">{{ mapping.enableSsl ? 'HTTPS' : 'HTTP' }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </el-popover>
            <span v-else class="no-ports">-</span>
          </template>
        </el-table-column>

        <el-table-column :label="t('common.created')" min-width="160">
          <template #default="{ row }">
            <span class="time">{{ formatDate(row.created) }}</span>
          </template>
        </el-table-column>

        <el-table-column :label="t('common.actions')" width="184" align="center" fixed="right">
          <template #default="{ row }">
            <div class="actions-cell">
              <el-button
                v-if="row.state !== 'running'"
                class="table-action-btn success"
                :class="{ loading: actionLoadingIds.has(row.id) }"
                :loading="actionLoadingIds.has(row.id)"
                :disabled="actionLoadingIds.has(row.id)"
                :icon="VideoPlay"
                :title="t('container.start')"
                @click="action(row, 'start')"
              />
              <el-button
                v-if="row.state === 'running'"
                class="table-action-btn warning"
                :class="{ loading: actionLoadingIds.has(row.id) }"
                :loading="actionLoadingIds.has(row.id)"
                :disabled="actionLoadingIds.has(row.id)"
                :icon="VideoPause"
                :title="t('container.stop')"
                @click="action(row, 'stop')"
              />
              <el-button
                class="table-action-btn"
                :icon="Document"
                :title="t('container.logs')"
                @click="router.push({ name: 'ContainerDetail', params: { id: row.id }, query: { tab: 'logs' } })"
              />
              <el-button
                class="table-action-btn"
                :title="t('container.terminal')"
                @click="router.push({ name: 'ContainerDetail', params: { id: row.id }, query: { tab: 'terminal' } })"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14"><polyline points="4 17 10 11 4 5"></polyline><line x1="12" y1="19" x2="20" y2="19"></line></svg>
              </el-button>
              <el-button
                class="table-action-btn danger"
                :icon="Delete"
                :title="t('container.remove')"
                @click="handleDelete(row)"
              />
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- Pagination -->
    <div class="pagination" v-if="totalPages > 1">
      <div class="page-info">
        {{ t('pagination.showing') }} {{ (currentPage - 1) * pageSize + 1 }} - {{ Math.min(currentPage * pageSize, filteredContainers.length) }} 
        {{ t('pagination.totalItems') }} {{ filteredContainers.length }} {{ t('pagination.itemsUnit') }}
      </div>
      <div class="page-controls">
        <button class="page-btn" :disabled="currentPage === 1" @click="currentPage = 1">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="11 17 6 12 11 7"></polyline><polyline points="18 17 13 12 18 7"></polyline></svg>
        </button>
        <button class="page-btn" :disabled="currentPage === 1" @click="currentPage--">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="15 18 9 12 15 6"></polyline></svg>
        </button>
        <div class="page-numbers">
          <button 
            v-for="page in visiblePages" 
            :key="page"
            class="page-num"
            :class="{ active: page === currentPage }"
            @click="currentPage = page"
          >
            {{ page }}
          </button>
        </div>
        <button class="page-btn" :disabled="currentPage === totalPages" @click="currentPage++">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
        </button>
        <button class="page-btn" :disabled="currentPage === totalPages" @click="currentPage = totalPages">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="13 17 18 12 13 7"></polyline><polyline points="6 17 11 12 6 7"></polyline></svg>
        </button>
      </div>
      <select v-model.number="pageSize" class="page-size">
        <option :value="10">10 {{ t('pagination.itemsPerPage') }}</option>
        <option :value="20">20 {{ t('pagination.itemsPerPage') }}</option>
        <option :value="50">50 {{ t('pagination.itemsPerPage') }}</option>
        <option :value="100">100 {{ t('pagination.itemsPerPage') }}</option>
      </select>
    </div>

    <!-- Empty State -->
    <div v-if="!loading && filteredContainers.length === 0" class="empty-state">
      <div class="empty-icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path>
        </svg>
      </div>
      <h3 class="empty-title">{{ t('container.empty.noContainers') }}</h3>
      <p class="empty-desc">{{ t('container.empty.createFirst') }}</p>
      <button class="btn btn-primary" @click="openCreate">{{ t('container.empty.createButton') }}</button>
    </div>

    <!-- Create Dialog -->
    <CreateContainerDialog v-model="showCreate" @success="refreshData" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useContainersStore } from '@/stores/containers'
import { useSettingsStore } from '@/stores/settings'
import type { ContainerState } from '@/types/container'
import { ElMessage, ElMessageBox } from 'element-plus'
import { VideoPlay, VideoPause, Document, Delete } from '@element-plus/icons-vue'
import CreateContainerDialog from '@/components/container/CreateContainerDialog.vue'
import { autoUpdateApi, AutoUpdateStatus } from '@/api/autoUpdate'
import { formatLocalizedDate } from '@/utils/date'

const { t } = useI18n()
const router = useRouter()
const store = useContainersStore()
const settingsStore = useSettingsStore()
const loading = computed(() => store.loading)
const search = ref('')
const filter = ref('all')
const actionLoadingIds = ref<Set<string>>(new Set())
const selected = ref<any[]>([])
const showCreate = ref(false)
const upgradingContainers = ref<Set<string>>(new Set())
const updateStatusMap = ref<Map<string, AutoUpdateStatus>>(new Map())

// Pagination
const currentPage = ref(1)
const pageSize = ref(settingsStore.defaultPageSize)

const filterTabs = computed(() => [
  { value: 'all', label: t('common.all'), color: 'gray' },
  { value: 'running', label: t('container.running'), color: 'green' },
  { value: 'exited', label: t('container.exited'), color: 'red' }
])

const filteredContainers = computed(() => {
  return store.containers.filter((c: any) => {
    const matchesSearch = 
      c.name?.toLowerCase().includes(search.value.toLowerCase()) || 
      c.image.toLowerCase().includes(search.value.toLowerCase())
    const matchesStatus = 
      filter.value === 'all' || 
      (filter.value === 'running' && c.state === 'running') || 
      (filter.value === 'exited' && c.state !== 'running')
    return matchesSearch && matchesStatus
  })
})

const totalPages = computed(() => Math.ceil(filteredContainers.value.length / pageSize.value))

const paginatedContainers = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredContainers.value.slice(start, start + pageSize.value)
})

const visiblePages = computed(() => {
  const pages = []
  const total = totalPages.value
  const current = currentPage.value
  let start = Math.max(1, current - 2)
  let end = Math.min(total, start + 4)
  if (end - start < 4) start = Math.max(1, end - 4)
  for (let i = start; i <= end; i++) pages.push(i)
  return pages
})

// 选中容器中有更新的数量
const selectedWithUpdates = computed(() => {
  return selected.value.filter(c => hasUpdateAvailable(c.id)).length
})

// Reset page when filter changes
watch([search, filter, pageSize], () => {
  currentPage.value = 1
})

watch(() => settingsStore.defaultPageSize, (size) => {
  pageSize.value = size
  currentPage.value = 1
})

const getCount = (filterValue: string) => {
  if (filterValue === 'all') return store.containers.length
  if (filterValue === 'running') return store.containers.filter((c: any) => c.state === 'running').length
  return store.containers.filter((c: any) => c.state !== 'running').length
}

const getStatusText = (state: string | ContainerState) => {
  const s = typeof state === 'string' ? state : state.status
  const texts: Record<string, string> = { 
    running: t('container.running'), 
    exited: t('container.exited'), 
    paused: t('container.paused'), 
    created: t('container.created') 
  }
  return texts[s] || s
}

// Normalize ports to consistent array format, deduplicating IPv4/IPv6 bindings
const normalizePorts = (ports: any[] | string | undefined | null) => {
  if (!ports) return []
  
  let portList: any[] = []
  
  if (Array.isArray(ports)) {
    portList = ports
  } else if (typeof ports === 'string') {
    // Handle legacy string format: "0.0.0.0:80->9998, :::80->9998"
    portList = ports.split(',').map(p => {
      p = p.trim()
      // Try to parse "IP:HostPort->ContainerPort" or similar
      const arrowParts = p.split('->')
      if (arrowParts.length === 2) {
        // e.g. "0.0.0.0:80" -> "9998"
        const left = arrowParts[0] || ''
        const right = arrowParts[1] || ''
        
        const hostParts = left.split(':')
        const hostPort = hostParts.length > 1 ? parseInt(hostParts[hostParts.length - 1]) : 0
        
        // right part might be "9998/tcp" or just "9998"
        const containerParts = right.split('/')
        const containerPort = parseInt(containerParts[0])
        const protocol = containerParts.length > 1 ? containerParts[1] : 'tcp'
        
        return {
          publicPort: hostPort,
          privatePort: containerPort,
          type: protocol,
          protocol: protocol
        }
      }
      return { protocol: 'tcp', privatePort: 0, publicPort: 0 } // Fallback
    }).filter(p => p.privatePort > 0 || p.publicPort > 0)
  }
  
  // 去重：Docker 会为同一端口返回 IPv4 (0.0.0.0) 和 IPv6 (::) 两条记录
  const seen = new Set<string>()
  return portList.filter(port => {
    const key = `${port.publicPort}:${port.privatePort}:${port.type || port.protocol}`
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })
}

// 获取映射端口（有公网端口）
const getMappedPorts = (ports: any[]) => {
  return ports.filter(p => p.publicPort && p.publicPort > 0)
}

// 获取内部端口（仅容器端口）
const getInternalPorts = (ports: any[]) => {
  return ports.filter(p => !p.publicPort || p.publicPort === 0)
}

const getContainerStats = (id: string) => store.stats[id]

const formatBytes = (bytes: number) => {
  if (bytes === null || bytes === undefined || isNaN(bytes) || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const absBytes = Math.abs(bytes)
  if (absBytes < 1) return absBytes.toFixed(1) + ' B'
  const i = Math.min(Math.max(0, Math.floor(Math.log(absBytes) / Math.log(k))), sizes.length - 1)
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}

const formatDate = (d: string) => {
  return formatLocalizedDate(d, '-', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
}

const isSelected = (container: any) => selected.value.some(c => c.id === container.id)
const isAllSelected = computed(() => 
  paginatedContainers.value.length > 0 && paginatedContainers.value.every(c => isSelected(c))
)

const handleSelectionChange = (selection: any[]) => {
  selected.value = selection
}

const toggleSelect = (container: any) => {
  const idx = selected.value.findIndex(c => c.id === container.id)
  if (idx > -1) selected.value.splice(idx, 1)
  else selected.value.push(container)
}

const toggleSelectAll = () => {
  if (isAllSelected.value) {
    paginatedContainers.value.forEach(c => {
      const idx = selected.value.findIndex(s => s.id === c.id)
      if (idx > -1) selected.value.splice(idx, 1)
    })
  } else {
    paginatedContainers.value.forEach(c => {
      if (!isSelected(c)) selected.value.push(c)
    })
  }
}

const refreshData = () => store.fetchContainers({ all: true })
const openCreate = () => showCreate.value = true
const viewDetail = (row: any) => router.push(`/containers/${row.id}`)

const action = async (row: any, type: string) => {
  const actionText = type === 'start' ? t('container.start') : type === 'stop' ? t('container.stop') : t('container.restart')
  actionLoadingIds.value.add(row.id)
  try {
    if (type === 'start') await store.startContainer(row.id)
    else if (type === 'stop') await store.stopContainer(row.id)
    else if (type === 'restart') await store.restartContainer(row.id)
    ElMessage.success(`${actionText}${t('common.success')}`)
    refreshData()
  } catch (error: any) {
    ElMessage.error(`${actionText}${t('common.error')}: ${error.message || t('common.unknown')}`)
  } finally {
    actionLoadingIds.value.delete(row.id)
  }
}

const handleDelete = async (row: any) => {
  try {
    await ElMessageBox.confirm(t('container.delete.confirm', { name: row.name || row.id }))
    try {
      await store.removeContainer(row.id)
      ElMessage.success(t('common.deleted'))
      refreshData()
    } catch (error: any) {
      if (error.needForce) {
        // 容器正在运行，询问是否强制删除
        try {
          await ElMessageBox.confirm(
            t('container.delete.forceConfirm'),
            t('container.delete.forceConfirmTitle'),
            { type: 'warning', confirmButtonText: t('container.delete.forceDeleteButton'), cancelButtonText: t('common.cancel') }
          )
          await store.removeContainer(row.id, true)
          ElMessage.success(t('container.delete.forceDeleted'))
          refreshData()
        } catch {
          // 用户取消
        }
      } else {
        ElMessage.error(`${t('container.delete.failed')}: ${error.message || error.error || t('common.unknown')}`)
      }
    }
  } catch {
    // 用户取消
  }
}

const batchDelete = async () => {
  try {
    await ElMessageBox.confirm(t('container.batch.deleteConfirm', { count: selected.value.length }))
    const forceDeleteIds: string[] = []
    const normalDeleteIds: string[] = []
    
    // 先尝试普通删除
    for (const c of selected.value) {
      try {
        await store.removeContainer(c.id)
        normalDeleteIds.push(c.id)
      } catch (error: any) {
        if (error.needForce) {
          forceDeleteIds.push(c.id)
        } else {
          ElMessage.error(`${t('container.delete.failed')} ${c.name || c.id}: ${error.message || t('common.unknown')}`)
        }
      }
    }
    
    // 如果有需要强制删除的容器，询问用户
    if (forceDeleteIds.length > 0) {
      try {
        await ElMessageBox.confirm(
          t('container.batch.forceDeleteConfirm', { count: forceDeleteIds.length }),
          t('container.delete.forceConfirmTitle'),
          { type: 'warning', confirmButtonText: t('container.delete.forceDeleteButton'), cancelButtonText: t('container.batch.skip') }
        )
        for (const id of forceDeleteIds) {
          try {
            await store.removeContainer(id, true)
          } catch (error: any) {
            ElMessage.error(`${t('container.delete.forceFailed')}: ${error.message || t('common.unknown')}`)
          }
        }
      } catch {
        // 用户选择跳过
        ElMessage.info(t('container.batch.skipRunning', { count: forceDeleteIds.length }))
      }
    }
    
    if (normalDeleteIds.length > 0 || forceDeleteIds.length > 0) {
      ElMessage.success(t('container.batch.deleted', { count: normalDeleteIds.length + forceDeleteIds.length }))
    }
    
    selected.value = []
    refreshData()
  } catch {
    // 用户取消
  }
}

const batchUpgrade = async () => {
  // 先检查选中容器的更新状态
  const containersWithUpdates = selected.value.filter(c => 
    c.state === 'running' && hasUpdateAvailable(c.id)
  )
  
  if (containersWithUpdates.length === 0) {
    ElMessage.warning(t('container.upgrade.noUpdatesAvailable'))
    return
  }
  
  try {
    await ElMessageBox.confirm(
      t('container.upgrade.confirmMessage', { count: containersWithUpdates.length }),
      t('container.upgrade.confirmTitle'),
      { confirmButtonText: t('container.upgrade.confirmButton'), cancelButtonText: t('common.cancel'), type: 'warning' }
    )
    
    // 标记正在升级的容器
    containersWithUpdates.forEach((c: any) => upgradingContainers.value.add(c.id))
    
    // 并行执行升级
    const results = await Promise.allSettled(
      containersWithUpdates.map(async (c: any) => {
        try {
          const result = await autoUpdateApi.updateContainer(c.id, false)
          return { containerId: c.id, name: c.name, ...result }
        } catch (err: any) {
          return { containerId: c.id, name: c.name, success: false, errorMessage: err.message }
        }
      })
    )
    
    // 统计结果
    let successCount = 0
    let failCount = 0
    results.forEach((r: any) => {
      if (r.value?.success) successCount++
      else failCount++
      upgradingContainers.value.delete(r.value?.containerId)
    })
    
    if (successCount > 0) {
      ElMessage.success(t('container.upgrade.successCount', { count: successCount }))
    }
    if (failCount > 0) {
      ElMessage.error(t('container.upgrade.failCount', { count: failCount }))
    }
    
    selected.value = []
    // 重新检查更新状态
    await checkAllUpdates()
    refreshData()
  } catch {
    // 用户取消
  }
}

// 检查所有容器的更新状态
const checkAllUpdates = async () => {
  try {
    const results = await autoUpdateApi.checkAllUpdates()
    results.forEach((r: any) => {
      updateStatusMap.value.set(r.containerId, r.hasUpdate ? AutoUpdateStatus.UpdateAvailable : AutoUpdateStatus.UpToDate)
    })
  } catch (err) {
    console.error(t('container.upgrade.checkFailed'), err)
  }
}

// 获取容器的更新状态
const getUpdateStatus = (containerId: string): AutoUpdateStatus | undefined => {
  return updateStatusMap.value.get(containerId)
}

// 判断容器是否有可用更新
const hasUpdateAvailable = (containerId: string): boolean => {
  return updateStatusMap.value.get(containerId) === AutoUpdateStatus.UpdateAvailable
}

onMounted(() => {
  refreshData()
  store.startStatsMonitoring()
  checkAllUpdates()
})

import { onUnmounted } from 'vue'
onUnmounted(() => {
  store.stopStatsMonitoring()
})
</script>

<style scoped>

/* === Usage Column === */
.usage-info-text { 
  display: flex; 
  flex-direction: column; 
  gap: 2px; 
  font-size: 11px; 
  color: var(--text-muted); 
  font-family: 'JetBrains Mono', monospace; 
}
.usage-loading { color: var(--text-muted); font-size: 11px; font-style: italic; }
.usage-none { color: var(--text-secondary); font-size: 12px; }
.ports-legacy { color: var(--text-muted); font-size: 11px; }

/* === Ports Column - Popover Trigger === */
.ports-trigger {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.08), rgba(139, 92, 246, 0.08));
  border: 1px solid rgba(59, 130, 246, 0.2);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.ports-trigger:hover {
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.15), rgba(139, 92, 246, 0.15));
  border-color: rgba(59, 130, 246, 0.4);
}

.ports-trigger-count {
  font-size: 12px;
  font-weight: 600;
  color: #3b82f6;
}

.ports-trigger-icon {
  width: 14px;
  height: 14px;
  color: var(--text-muted);
  transition: transform 0.2s ease;
}

.ports-trigger:hover .ports-trigger-icon {
  color: #3b82f6;
}

.no-ports { color: var(--text-secondary); }

.containers-page {
  padding: 24px 32px;
  max-width: 1600px;
  margin: 0 auto;
}

/* === Page Header === */
.page-header {
  display: flex;
  justify-content: space-between;
    align-items: flex-start;
    margin-bottom: 24px;
  }
  
  .page-subtitle { margin: 6px 0 0 0; color: var(--text-muted); font-size: 14px; }

.header-actions { display: flex; gap: 10px; }

/* === Buttons === */
.btn-icon { width: 16px; height: 16px; }

.btn-sm {
  padding: 4px 10px;
  font-size: 12px;
  font-weight: 500;
  height: 28px;
  line-height: 1;
}

/* === Toolbar === */
.toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
  flex-wrap: wrap;
}

.search-box {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  max-width: 320px;
  min-width: 200px;
  padding: 8px 12px;
  background: var(--bg-glass-dark);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.search-box:focus-within { border-color: #3b82f6; }

.search-icon { width: 16px; height: 16px; color: var(--text-muted); }

.search-input {
  flex: 1;
  border: none;
  background: transparent;
  outline: none;
  font-size: 13px;
  color: var(--text-main);
  min-width: 0;
}

.filter-tabs { display: flex; gap: 6px; flex-wrap: wrap; }

.filter-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-radius: 6px;
  border: none;
  background: transparent;
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.filter-tab:hover { background: var(--bg-subtle); }
.filter-tab.active { background: var(--color-secondary); color: #fff; }

.tab-dot { width: 6px; height: 6px; border-radius: 50%; }
.tab-dot.gray { background: var(--text-muted); }
.tab-dot.green { background: #22c55e; }
.tab-dot.red { background: #ef4444; }
.filter-tab.active .tab-dot { background: rgba(255, 255, 255, 0.8); }

.tab-count {
  font-size: 11px;
  padding: 1px 6px;
  background: rgba(0, 0, 0, 0.08);
  border-radius: 8px;
}

.filter-tab.active .tab-count { background: rgba(255, 255, 255, 0.2); }

/* === Data Table === */
.data-table {
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
  overflow: hidden;
}

.td-name {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.status-dot.running { background: #22c55e; box-shadow: 0 0 6px rgba(34, 197, 94, 0.5); }
.status-dot.exited, .status-dot.stopped { background: var(--text-muted); }
.status-dot.paused { background: #f59e0b; }

.name-info { 
  display: flex; 
  flex-direction: column; 
  gap: 2px; 
  min-width: 0; 
  overflow: hidden;
  flex: 1;
}

.name {
  font-weight: 600;
  color: var(--text-main);
  cursor: pointer;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  display: block;
}

.name:hover { color: #3b82f6; }

.id {
  font-size: 10px;
  font-family: 'JetBrains Mono', monospace;
  color: var(--text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.td-image {
  min-width: 0;
  overflow: hidden;
}

.image-text {
  font-family: 'JetBrains Mono', monospace;
  font-size: 12px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  display: block;
}

.update-badge {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  margin-left: 6px;
  padding: 2px 6px;
  font-size: 10px;
  font-weight: 500;
  color: #fff;
  background: linear-gradient(135deg, #f59e0b, #d97706);
  border-radius: 4px;
  white-space: nowrap;
  animation: pulse 2s infinite;
}

.update-badge svg {
  flex-shrink: 0;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}

.td-status {
  flex-shrink: 0;
}

.status-badge {
  display: inline-flex;
  padding: 3px 8px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}

.status-badge.running { background: rgba(34, 197, 94, 0.1); color: #16a34a; }
.status-badge.created { background: rgba(59, 130, 246, 0.1); color: #3b82f6; }
.status-badge.exited, .status-badge.stopped { background: var(--bg-subtle); color: var(--text-muted); }

.td-usage {
  min-width: 0;
  overflow: hidden;
}

.ports-list { display: flex; gap: 4px; flex-wrap: wrap; }

.port {
  font-family: 'JetBrains Mono', monospace;
  font-size: 10px;
  padding: 2px 6px;
  background: var(--bg-subtle);
  border-radius: 4px;
}

.td-ports {
  min-width: 0;
  overflow: hidden;
}

.time {
  font-size: 12px;
  color: var(--text-muted);
  white-space: nowrap;
}

/* 操作列固定设计 */







/* === Pagination === */
.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 0;
  font-size: 13px;
  color: var(--text-muted);
  flex-wrap: wrap;
  gap: 12px;
}

.page-info { font-size: 12px; }

.page-controls { display: flex; align-items: center; gap: 4px; }

.page-btn {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  background: var(--bg-surface);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: var(--text-muted);
  transition: all 0.15s ease;
}

.page-btn svg { width: 16px; height: 16px; }
.page-btn:hover:not(:disabled) { border-color: #3b82f6; color: #3b82f6; }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.page-numbers { display: flex; gap: 4px; margin: 0 8px; }

.page-num {
  min-width: 32px;
  height: 32px;
  border-radius: 6px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-muted);
  transition: all 0.15s ease;
}

.page-num:hover { background: var(--bg-subtle); }
.page-num.active { background: var(--color-secondary); color: #fff; }

.page-size {
  padding: 6px 10px;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  font-size: 12px;
  color: var(--text-secondary);
  background: var(--bg-surface);
  cursor: pointer;
}

/* === Empty State === */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 60px 40px;
  text-align: center;
}

.empty-icon {
  width: 64px;
  height: 64px;
  margin-bottom: 16px;
  color: var(--text-secondary);
}

.empty-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-secondary);
  margin: 0 0 8px 0;
}

.empty-desc {
  font-size: 14px;
  color: var(--text-muted);
  margin: 0 0 24px 0;
}

/* === Responsive === */
@media (max-width: 768px) {
  .containers-page { padding: 16px; }
  .page-header { flex-direction: column; gap: 12px; align-items: stretch; }
  .header-actions { justify-content: flex-end; }
  .toolbar { padding: 10px 12px; }
  .search-box { max-width: none; min-width: 0; width: 100%; }
  .filter-tabs { width: 100%; }
}


</style>

<style>

/* === Dark Mode === */
html.dark .toolbar, html.dark .data-table { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-box { background: #0f172a; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-input { color: #f1f5f9; }
html.dark .name { color: #f1f5f9; }
html.dark .page-btn { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
html.dark .port { background: rgba(255, 255, 255, 0.1); }

</style>
