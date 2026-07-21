<template>
  <div class="networks-page" v-loading="loading">
    <!-- Page Header -->
    <header class="page-header">
      <div class="header-content">
        <h1 class="page-title">{{ t('network.title') }}</h1>
        <p class="page-subtitle">{{ t('network.subtitle') }}</p>
      </div>
      <div class="header-actions">
        <button class="btn btn-primary" @click="showCreate = true">
          <svg class="btn-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="5" x2="12" y2="19"></line>
            <line x1="5" y1="12" x2="19" y2="12"></line>
          </svg>
          {{ t('network.create') }}
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
        <input v-model="search" type="text" :placeholder="t('network.searchPlaceholder')" class="search-input" />
      </div>
      <label class="hide-system-checkbox">
        <input type="checkbox" v-model="hideSystemNetworks" />
        <span>{{ t('network.hideDefault') }}</span>
      </label>
      <div class="stats">
        <span class="stat"><strong>{{ filtered.length }}</strong> {{ t('network.networksCount') }}</span>
      </div>
    </div>

    <!-- Data Table -->
    <div class="data-table" v-if="paginatedNetworks.length > 0">
      <el-table
        :data="paginatedNetworks"
        style="width: 100%"
        v-loading="loading"
        row-key="id"
      >
        <el-table-column :label="t('network.table.nameId')" min-width="250">
          <template #default="{ row }">
            <div class="td-name">
              <div class="network-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <circle cx="12" cy="12" r="10"></circle>
                  <line x1="2" y1="12" x2="22" y2="12"></line>
                  <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
                </svg>
              </div>
              <div class="name-info" @click="handleDetail(row)">
                <span class="name">{{ row.name }}</span>
                <code class="id">{{ row.id.substring(0, 12) }}</code>
              </div>
            </div>
          </template>
        </el-table-column>

        <el-table-column :label="t('network.driver')" width="140" align="center">
          <template #default="{ row }">
            <span class="driver-badge">{{ row.driver }}</span>
          </template>
        </el-table-column>

        <el-table-column :label="t('network.scope')" width="120" align="center">
          <template #default="{ row }">
            <span class="scope-badge" :class="row.scope">{{ row.scope }}</span>
          </template>
        </el-table-column>

        <el-table-column :label="t('network.internal')" width="120" align="center">
          <template #default="{ row }">
            <span class="internal-badge" :class="{ yes: row.internal }">
              {{ row.internal ? t('common.yes') : t('common.no') }}
            </span>
          </template>
        </el-table-column>

        <el-table-column :label="t('common.actions')" width="124" align="center" fixed="right">
          <template #default="{ row }">
            <div class="actions-cell">
              <el-button class="table-action-btn info" :icon="View" :title="t('network.inspectNetwork')" @click="handleDetail(row)" />
              <el-button class="table-action-btn primary" :icon="Link" :title="t('network.connectContainer')" @click="handleConnect(row)" />
              <el-button
                class="table-action-btn danger"
                :icon="Delete"
                :disabled="isSystemNetwork(row.name)"
                :title="isSystemNetwork(row.name) ? t('network.cannotDeleteDefault') : t('common.delete')"
                @click="handleDelete(row)"
              />
            </div>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- Pagination -->
    <div class="pagination" v-if="totalPages > 1">
      <div class="page-info">{{ t('pagination.totalItems') }} {{ filtered.length }} {{ t('pagination.itemsUnit') }}</div>
      <div class="page-controls">
        <button class="page-btn" :disabled="currentPage === 1" @click="currentPage--">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="15 18 9 12 15 6"></polyline></svg>
        </button>
        <span class="page-current">{{ currentPage }} / {{ totalPages }}</span>
        <button class="page-btn" :disabled="currentPage === totalPages" @click="currentPage++">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
        </button>
      </div>
    </div>

    <!-- Empty State -->
    <div v-if="!loading && filtered.length === 0" class="empty-state">
      <div class="empty-icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <circle cx="12" cy="12" r="10"></circle>
          <line x1="2" y1="12" x2="22" y2="12"></line>
          <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
        </svg>
      </div>
      <h3 class="empty-title">{{ t('network.empty.noNetworks') }}</h3>
      <p class="empty-desc">{{ t('network.empty.createFirst') }}</p>
      <button class="btn btn-primary" @click="showCreate = true">{{ t('network.create') }}</button>
    </div>

    <!-- Create Network Dialog -->
    <CreateNetworkDialog v-model="showCreate" @success="refreshData" />
    
    <!-- Connect Container Dialog -->
    <ConnectContainerDialog v-model="showConnect" :network="selectedNetwork" @success="refreshData" />
    
    <!-- Network Detail Drawer -->
    <NetworkDetailDrawer
      v-model="showDetail"
      :network-id="selectedNetworkId"
      @connect-container="handleConnectFromDrawer"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useNetworksStore } from '@/stores/networks'
import { useSettingsStore } from '@/stores/settings'
import { ElMessage, ElMessageBox } from 'element-plus'
import { View, Link, Delete } from '@element-plus/icons-vue'
import CreateNetworkDialog from '@/components/network/CreateNetworkDialog.vue'
import ConnectContainerDialog from '@/components/network/ConnectContainerDialog.vue'
import NetworkDetailDrawer from '@/components/network/NetworkDetailDrawer.vue'

const { t } = useI18n()
const store = useNetworksStore()
const settingsStore = useSettingsStore()
const loading = ref(false)
const search = ref('')
const showCreate = ref(false)
const showConnect = ref(false)
const showDetail = ref(false)
const selectedNetwork = ref<any>(null)
const selectedNetworkId = ref('')
const hideSystemNetworks = ref(true)
const currentPage = ref(1)
const pageSize = computed(() => settingsStore.defaultPageSize)

const isSystemNetwork = (name: string) => ['bridge', 'host', 'none', 'dockerpanel-network'].includes(name)

const filtered = computed(() => {
  let networks = store.networks.filter((n: any) => 
    n.name.toLowerCase().includes(search.value.toLowerCase())
  )
  
  // 根据选项过滤系统网络
  if (hideSystemNetworks.value) {
    networks = networks.filter((n: any) => !isSystemNetwork(n.name))
  }
  
  // 排序：自定义网络在前，系统网络在后
  return networks.sort((a: any, b: any) => {
    const aIsSystem = isSystemNetwork(a.name)
    const bIsSystem = isSystemNetwork(b.name)
    if (aIsSystem === bIsSystem) {
      // 同类型按名称排序
      return a.name.localeCompare(b.name)
    }
    return aIsSystem ? 1 : -1  // 系统网络排后面
  })
})

const totalPages = computed(() => Math.ceil(filtered.value.length / pageSize.value))
const paginatedNetworks = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filtered.value.slice(start, start + pageSize.value)
})

watch(search, () => { currentPage.value = 1 })
watch(hideSystemNetworks, () => { currentPage.value = 1 })
watch(pageSize, () => { currentPage.value = 1 })

const refreshData = async () => {
  loading.value = true
  try { await store.fetchNetworks() } finally { loading.value = false }
}

const handleDelete = (row: any) => {
  if (isSystemNetwork(row.name)) {
    ElMessage.warning(t('network.cannotDeleteDefault'))
    return
  }
  ElMessageBox.confirm(t('network.deleteConfirm', { name: row.name })).then(async () => {
    await store.deleteNetwork(row.id)
    ElMessage.success(t('common.deleted'))
    refreshData()
  })
}

const handleConnect = (network: any) => {
  selectedNetwork.value = network
  showConnect.value = true
}

const handleDetail = (network: any) => {
  selectedNetworkId.value = network.id
  showDetail.value = true
}

const handleConnectFromDrawer = (network: any) => {
  selectedNetwork.value = network
  showConnect.value = true
}

onMounted(() => refreshData())
</script>

<style scoped>

.networks-page {
  padding: 24px 32px;
  max-width: 1600px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
}

.page-subtitle { margin: 6px 0 0 0; color: var(--text-muted); font-size: 14px; }
.header-actions { display: flex; gap: 10px; }

.btn-icon { width: 16px; height: 16px; }

.toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding: 12px 16px;
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
}

.search-box {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  max-width: 320px;
  padding: 8px 12px;
  background: var(--bg-glass-dark);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.search-icon { width: 16px; height: 16px; color: var(--text-muted); }
.search-input { flex: 1; border: none; background: transparent; outline: none; font-size: 13px; color: var(--text-main); }

.hide-system-checkbox {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--text-muted);
  cursor: pointer;
  user-select: none;
}

.hide-system-checkbox input {
  width: 16px;
  height: 16px;
  cursor: pointer;
  accent-color: var(--color-primary);
}

.stats { display: flex; gap: 16px; margin-left: auto; font-size: 13px; color: var(--text-muted); }
.stats strong { color: var(--text-main); }

.data-table {
  background: var(--bg-surface);
  border-radius: 12px;
  border: 1px solid var(--border-color);
  overflow: hidden;
}

.td-name { display: flex; align-items: center; gap: 12px; }

.network-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: linear-gradient(135deg, #06b6d4, #0891b2);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.network-icon svg { width: 18px; height: 18px; color: #fff; }

.name-info { display: flex; flex-direction: column; gap: 2px; cursor: pointer; }
.name { font-weight: 600; font-size: 14px; color: var(--text-main); }
.name-info:hover .name { color: var(--color-primary); }
.id { font-size: 10px; font-family: 'JetBrains Mono', monospace; color: var(--text-muted); }

.driver-badge {
  font-family: 'JetBrains Mono', monospace;
  font-size: 11px;
  padding: 4px 10px;
  background: var(--bg-subtle);
  border-radius: 6px;
  color: var(--text-secondary);
}

.scope-badge {
  font-size: 11px;
  padding: 4px 10px;
  border-radius: 6px;
  font-weight: 600;
}

.scope-badge.local { background: var(--color-success-bg); color: var(--color-success); }
.scope-badge.swarm { background: var(--color-warning-bg); color: var(--color-warning); }

.internal-badge {
  font-size: 11px;
  padding: 4px 8px;
  border-radius: 4px;
  color: var(--text-muted);
}

.internal-badge.yes {
  background: var(--color-info-bg);
  color: var(--color-info);
}

.td-actions { display: flex; gap: 4px; justify-content: center; width: 100%; }








.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 0;
  font-size: 13px;
  color: var(--text-muted);
}

.page-controls { display: flex; align-items: center; gap: 8px; }
.page-current { font-weight: 500; }

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
}

.page-btn svg { width: 16px; height: 16px; }
.page-btn:hover:not(:disabled) { border-color: var(--color-primary); color: var(--color-primary); }
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 60px 40px;
  text-align: center;
}

.empty-icon { width: 64px; height: 64px; margin-bottom: 16px; color: var(--text-secondary); }
.empty-title { font-size: 18px; font-weight: 600; color: var(--text-secondary); margin: 0 0 8px 0; }
.empty-desc { font-size: 14px; color: var(--text-muted); margin: 0 0 24px 0; }

@media (max-width: 768px) {
  .networks-page { padding: 16px; }
  .page-header { flex-direction: column; gap: 12px; }
  .toolbar { flex-wrap: wrap; }
  .search-box { max-width: none; width: 100%; }
}

</style>

<style>

/* === Dark Mode === */
html.dark .toolbar, html.dark .data-table { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-box { background: #0f172a; border-color: rgba(255, 255, 255, 0.1); }
html.dark .search-input { color: #f1f5f9; }
html.dark .table-header { background: #0f172a; color: #94a3b8; }
html.dark .table-row { border-color: rgba(255, 255, 255, 0.05); }
html.dark .table-row:hover { background: rgba(255, 255, 255, 0.03); }
html.dark .name { color: #f1f5f9; }
html.dark .stats strong { color: #f1f5f9; }
html.dark .driver-badge { background: rgba(255, 255, 255, 0.1); }
html.dark .action-btn, html.dark .page-btn { background: #1e293b; border-color: rgba(255, 255, 255, 0.1); }

</style>