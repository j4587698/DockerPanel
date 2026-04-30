<template>
  <el-dialog
    v-model="visible"
    :title="editMode ? t('createContainer.editContainer') : t('createContainer.createContainer')"
    width="960px"
    :close-on-click-modal="false"
    :close-on-press-escape="!pulling"
    @close="handleClose"
    top="5vh"
    class="create-container-dialog"
  >
    <!-- 进度展示层 -->
    <PullProgressDisplay 
      v-if="pulling" 
      :title="t('createContainer.imageMissingPulling') + form.image" 
      :progressMap="progressMap" 
    />

    <!-- 配置表单层 -->
    <el-form
      v-else
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="100px"
      label-position="left"
    >
      <el-tabs v-model="activeTab" type="border-card">
        <!-- ==================== 基本配置 ==================== -->
        <el-tab-pane name="basic">
          <template #label>
            <span class="tab-label"><el-icon><Setting /></el-icon> {{ t('createContainer.basicConfig') }}</span>
          </template>
          <div class="tab-content">
            <el-row :gutter="20">
              <el-col :span="12">
                <el-form-item :label="t('createContainer.containerName')" prop="name">
                  <el-input v-model="form.name" :placeholder="t('createContainer.containerNamePlaceholder')" clearable />
                  <div class="field-hint">{{ t('createContainer.containerNameHint') }}</div>
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item :label="t('createContainer.imageLabel')" prop="image">
                  <div class="image-select-wrapper">
                    <el-select
                      v-model="form.image"
                      filterable
                      :placeholder="t('createContainer.imagePlaceholder')"
                      style="width: 100%"
                      @change="handleImageSelectChange"
                    >
                      <el-option
                        v-for="img in localImageOptions"
                        :key="img.value"
                        :label="img.label"
                        :value="img.value"
                      >
                        <div class="image-option-item">
                          <span class="image-option-name">{{ img.label }}</span>
                          <span class="image-option-size">{{ img.size }}</span>
                        </div>
                      </el-option>
                      <template #empty>
                        <div class="image-select-empty">
                          <span>{{ t('createContainer.noLocalImages') }}</span>
                        </div>
                      </template>
                    </el-select>
                    <el-button class="pull-trigger-btn" @click="showPullDialog = true">
                      <el-icon><Download /></el-icon>
                    </el-button>
                  </div>
                </el-form-item>
              </el-col>
            </el-row>

            <el-row :gutter="20">
              <el-col :span="8">
                <el-form-item :label="t('createContainer.restartPolicy')">
                  <el-select v-model="form.restartPolicy.name" style="width: 100%">
                    <el-option :label="t('createContainer.restartNo')" value="no" />
                    <el-option :label="t('createContainer.restartAlways')" value="always" />
                    <el-option :label="t('createContainer.restartUnlessStopped')" value="unless-stopped" />
                    <el-option :label="t('createContainer.restartOnFailure')" value="on-failure" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="8" v-if="form.restartPolicy.name === 'on-failure'">
                <el-form-item :label="t('createContainer.retryCount')">
                  <el-input v-model.number="form.restartPolicy.maximumRetryCount" type="number" placeholder="0" min="0" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.autoRemove')">
                  <el-switch v-model="form.autoRemove" />
                </el-form-item>
              </el-col>
            </el-row>

            <el-divider content-position="left">{{ t('createContainer.runConfig') }}</el-divider>

            <el-row :gutter="20">
              <el-col :span="8">
                <el-form-item :label="t('createContainer.hostname')">
                  <el-input v-model="form.hostname" :placeholder="t('createContainer.hostnamePlaceholder')" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.user')">
                  <el-input v-model="form.user" placeholder="uid:gid" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.workingDir')">
                  <el-input v-model="form.workingDir" placeholder="/app" />
                </el-form-item>
              </el-col>
            </el-row>

            <el-row :gutter="20">
              <el-col :span="12">
                <el-form-item :label="t('createContainer.entrypoint')">
                  <el-input v-model="form.entrypoint" placeholder="Entrypoint" />
                </el-form-item>
              </el-col>
              <el-col :span="12">
                <el-form-item :label="t('createContainer.command')">
                  <el-input v-model="commandStr" placeholder="CMD" />
                </el-form-item>
              </el-col>
            </el-row>

            <el-row :gutter="20">
              <el-col :span="6">
                <el-form-item :label="t('createContainer.allocateTty')">
                  <el-switch v-model="form.tty" />
                </el-form-item>
              </el-col>
              <el-col :span="6">
                <el-form-item :label="t('createContainer.interactiveMode')">
                  <el-switch v-model="form.stdinOpen" />
                </el-form-item>
              </el-col>
              <el-col :span="6">
                <el-form-item :label="t('createContainer.stopSignal')">
                  <el-select v-model="form.stopSignal" placeholder="SIGTERM" clearable style="width: 100%">
                    <el-option label="SIGTERM" value="SIGTERM" />
                    <el-option label="SIGKILL" value="SIGKILL" />
                    <el-option label="SIGINT" value="SIGINT" />
                    <el-option label="SIGQUIT" value="SIGQUIT" />
                    <el-option label="SIGHUP" value="SIGHUP" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="6">
                <el-form-item :label="t('createContainer.stopTimeout')">
                  <el-input v-model.number="form.stopTimeout" placeholder="10" type="number" />
                </el-form-item>
              </el-col>
            </el-row>
          </div>
        </el-tab-pane>

        <!-- ==================== 网络与端口 ==================== -->
        <el-tab-pane name="network">
          <template #label>
            <span class="tab-label"><el-icon><Connection /></el-icon> {{ t('createContainer.network') }}</span>
          </template>
          <div class="tab-content">
            <!-- 网络设置 -->
            <el-row :gutter="20">
              <el-col :span="12">
                <el-form-item :label="t('createContainer.network')">
                  <div class="network-options">
                    <el-checkbox v-model="disableNetwork" @change="handleDisableNetworkChange">
                      {{ t('createContainer.disableNetwork') }}
                    </el-checkbox>
                    <el-checkbox v-model="useHostNetwork" @change="handleHostNetworkChange" :disabled="disableNetwork">
                      {{ t('createContainer.useHostNetwork') }}
                    </el-checkbox>
                  </div>
                </el-form-item>
              </el-col>
              <el-col :span="12" v-if="!disableNetwork && !useHostNetwork">
                <el-form-item :label="t('createContainer.additionalNetworks')">
                  <el-select 
                    v-model="form.network.networkIds" 
                    multiple 
                    :placeholder="t('createContainer.selectAdditionalNetworks')" 
                    style="width: 100%"
                    @change="handleAdditionalNetworksChange"
                  >
                    <el-option 
                      v-for="net in otherNetworks" 
                      :key="net.id" 
                      :label="net.name" 
                      :value="net.id"
                    >
                      <div class="network-option-item">
                        <span>{{ net.name }}</span>
                        <el-tag size="small" type="info">{{ net.driver }}</el-tag>
                      </div>
                    </el-option>
                  </el-select>
                </el-form-item>
              </el-col>
            </el-row>

            <!-- 网络提示 -->
            <el-alert v-if="useHostNetwork" type="warning" :closable="false" style="margin-bottom: 16px">
              <template #title>
                {{ t('createContainer.hostNetworkWarning') }}
              </template>
            </el-alert>
            <el-alert v-if="disableNetwork" type="info" :closable="false" style="margin-bottom: 16px">
              <template #title>
                {{ t('createContainer.disableNetworkWarning') }}
              </template>
            </el-alert>
            <el-alert v-if="!disableNetwork && !useHostNetwork && defaultNetwork" type="success" :closable="false" style="margin-bottom: 16px">
              <template #title>
                {{ t('createContainer.defaultNetworkInfo', { network: defaultNetwork.name }) }}
              </template>
            </el-alert>

            <!-- 附加网络参数配置 -->
            <div v-if="!disableNetwork && !useHostNetwork && form.network.networkIds.length > 0" class="network-config-section">
              <div class="network-config-header">
                <span>{{ t('createContainer.additionalNetworkParams') }}</span>
                <span class="hint">{{ t('createContainer.additionalNetworkHint') }}</span>
              </div>
              <div v-for="networkId in form.network.networkIds" :key="networkId" class="additional-network-config">
                <div class="network-name">
                  {{ getNetworkName(networkId) }}
                  <el-tag size="small" type="info">{{ getNetworkDriver(networkId) }}</el-tag>
                </div>
                <el-row :gutter="16">
                  <el-col :span="8">
                    <el-form-item :label="t('createContainer.ipv4Address')">
                      <el-input
                        v-model="form.network.configs[networkId].ipAddress"
                        :placeholder="t('createContainer.ipv4Placeholder')"
                      />
                    </el-form-item>
                  </el-col>
                  <el-col :span="8">
                    <el-form-item :label="t('createContainer.ipv6Address')">
                      <el-input
                        v-model="form.network.configs[networkId].ipv6Address"
                        :placeholder="t('createContainer.ipv6Placeholder')"
                      />
                    </el-form-item>
                  </el-col>
                  <el-col :span="8">
                    <el-form-item :label="t('createContainer.networkAliases')">
                      <el-input
                        v-model="form.network.configs[networkId].aliases"
                        :placeholder="t('createContainer.networkAliasesPlaceholder')"
                      />
                    </el-form-item>
                  </el-col>
                </el-row>
              </div>
            </div>

            <template v-if="!disableNetwork && !useHostNetwork">
              <el-divider content-position="left">{{ t('createContainer.portMapping') }}</el-divider>

              <el-table :data="formArrays.ports" border size="small" v-if="formArrays.ports.length > 0" class="array-table" style="width: 100%">
                <el-table-column :label="t('createContainer.bindIp')" min-width="130">
                  <template #default="{ row }">
                    <el-input v-model="row.hostIp" :placeholder="t('createContainer.bindIpPlaceholder')" size="small" />
                  </template>
                </el-table-column>
                <el-table-column :label="t('createContainer.hostPort')" min-width="120">
                  <template #default="{ row }">
                    <el-input v-model="row.hostPort" :placeholder="t('createContainer.hostPortPlaceholder')" size="small" />
                  </template>
                </el-table-column>
                <el-table-column :label="t('createContainer.containerPort')" min-width="110">
                  <template #default="{ row }">
                    <el-input v-model="row.containerPort" :placeholder="t('createContainer.containerPortPlaceholder')" size="small" />
                  </template>
                </el-table-column>
                <el-table-column :label="t('createContainer.protocol')" width="100">
                  <template #default="{ row }">
                    <el-select v-model="row.protocol" size="small">
                      <el-option label="TCP" value="tcp" />
                      <el-option label="UDP" value="udp" />
                    </el-select>
                  </template>
                </el-table-column>
                <el-table-column :label="t('common.actions')" width="60" align="center">
                  <template #default="{ $index }">
                    <el-button type="danger" link :icon="Delete" @click="removePort($index)" />
                  </template>
                </el-table-column>
              </el-table>
              <div style="margin-top: 8px; display: flex; gap: 8px;">
                <el-button type="primary" link :icon="Plus" @click="addPort">{{ t('createContainer.addPortMapping') }}</el-button>
                <el-button type="success" link :icon="Search" @click="autoDetectPorts" :disabled="!form.image">{{ t('createContainer.detectImagePorts') }}</el-button>
              </div>
            </template>

            <template v-if="!disableNetwork">
              <el-divider content-position="left">{{ t('createContainer.dnsAndHosts') }}</el-divider>

              <el-row :gutter="20">
                <el-col :span="8">
                  <el-form-item :label="t('createContainer.dnsServer')">
                    <el-input v-model="form.dns" :placeholder="t('createContainer.dnsPlaceholder')" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item :label="t('createContainer.dnsSearchDomain')">
                    <el-input v-model="form.dnsSearch" :placeholder="t('createContainer.dnsSearchPlaceholder')" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item :label="t('createContainer.extraHosts')">
                    <el-input v-model="form.extraHosts" :placeholder="t('createContainer.extraHostsPlaceholder')" />
                  </el-form-item>
                </el-col>
              </el-row>
            </template>
          </div>
        </el-tab-pane>

        <!-- ==================== 存储挂载 ==================== -->
        <el-tab-pane name="storage">
          <template #label>
            <span class="tab-label"><el-icon><Folder /></el-icon> {{ t('createContainer.storageTab') }}</span>
          </template>
          <div class="tab-content">
            <div class="sub-section">
              <div class="sub-title">{{ t('createContainer.volumeMapping') }}</div>
              <div v-for="(vol, idx) in formArrays.volumes" :key="idx" class="volume-row">
                <div class="volume-type-select">
                  <el-select v-model="vol.type" size="small" :placeholder="t('createContainer.volumeType')" style="width: 100px">
                    <el-option :label="t('createContainer.hostPath')" value="bind" />
                    <el-option :label="t('createContainer.volumeType')" value="volume" />
                  </el-select>
                </div>
                <!-- 主机路径模式 -->
                <template v-if="vol.type === 'bind'">
                  <el-input v-model="vol.hostPath" :placeholder="t('createContainer.hostPathPlaceholder')" size="small" style="flex: 1" />
                </template>
                <!-- 存储卷模式 -->
                <template v-else>
                  <el-select 
                    v-model="vol.volumeName" 
                    size="small" 
                    :placeholder="t('createContainer.selectVolume')" 
                    style="flex: 1"
                    filterable
                  >
                    <el-option value="__create_new__" :label="t('createContainer.createNewVolume')">
                      <span style="color: var(--color-primary)">
                        <el-icon style="margin-right: 4px; vertical-align: middle"><Plus /></el-icon>
                        {{ t('createContainer.createNewVolume') }}
                      </span>
                    </el-option>
                    <el-option 
                      v-for="v in availableVolumes" 
                      :key="v.name" 
                      :label="v.name" 
                      :value="v.name"
                    >
                      <span style="float: left">{{ v.name }}</span>
                      <span style="float: right; color: var(--text-muted); font-size: 12px">{{ v.driver }}</span>
                    </el-option>
                  </el-select>
                </template>
                <span class="separator">:</span>
                <el-input v-model="vol.containerPath" :placeholder="t('createContainer.containerPathPlaceholder')" size="small" style="width: 140px" />
                <el-checkbox v-model="vol.readOnly" size="small" :title="t('createContainer.readOnly')">{{ t('createContainer.readOnly') }}</el-checkbox>
                <el-button type="danger" link :icon="Delete" @click="removeVolume(idx)" />
              </div>
              <div style="margin-top: 8px; display: flex; gap: 8px;">
                <el-button type="primary" link :icon="Plus" @click="addVolume">{{ t('createContainer.addVolume') }}</el-button>
                <el-button type="success" link :icon="Search" @click="autoDetectVolumes" :disabled="!form.image">{{ t('createContainer.detectImageVolumes') }}</el-button>
              </div>
            </div>

            <el-divider />

            <div class="sub-section">
              <div class="sub-title">{{ t('createContainer.tmpfsMount') }}</div>
              <div v-for="(t, idx) in formArrays.tmpfs" :key="idx" class="inline-form-row">
                <el-input v-model="t.containerPath" :placeholder="t('createContainer.tmpfsPathPlaceholder')" size="small" style="width: 40%" />
                <el-input v-model="t.options" :placeholder="t('createContainer.tmpfsOptionsPlaceholder')" size="small" style="width: 45%" />
                <el-button type="danger" link :icon="Delete" @click="formArrays.tmpfs.splice(idx, 1)" />
              </div>
              <el-button type="primary" link :icon="Plus" @click="formArrays.tmpfs.push({ containerPath: '', options: '' })">{{ t('createContainer.addTmpfs') }}</el-button>
            </div>

            <el-divider />

            <div class="sub-section">
              <div class="sub-title">{{ t('createContainer.deviceMapping') }}</div>
              <div v-for="(dev, idx) in formArrays.devices" :key="idx" class="inline-form-row">
                <el-input v-model="dev.host" placeholder="/dev/video0" size="small" style="width: 40%" />
                <span class="separator">-></span>
                <el-input v-model="dev.container" placeholder="/dev/video0" size="small" style="width: 40%" />
                <el-button type="danger" link :icon="Delete" @click="formArrays.devices.splice(idx, 1)" />
              </div>
              <el-button type="primary" link :icon="Plus" @click="formArrays.devices.push({ host: '', container: '' })">{{ t('createContainer.addDevice') }}</el-button>
            </div>
          </div>
        </el-tab-pane>

        <!-- ==================== 环境与标签 ==================== -->
        <el-tab-pane name="env">
          <template #label>
            <span class="tab-label"><el-icon><List /></el-icon> {{ t('createContainer.envTab') }}</span>
          </template>
          <div class="tab-content">
            <div class="sub-section">
              <div class="sub-title">{{ t('createContainer.environmentVars') }}</div>
              <div v-for="(env, idx) in formArrays.environment" :key="idx" class="inline-form-row">
                <el-input v-model="env.key" :placeholder="t('createContainer.envKeyPlaceholder')" size="small" style="width: 35%" />
                <span class="separator">=</span>
                <el-input v-model="env.value" :placeholder="t('createContainer.envValuePlaceholder')" size="small" style="width: 50%" />
                <el-button type="danger" link :icon="Delete" @click="removeEnv(idx)" />
              </div>
              <el-button type="primary" link :icon="Plus" @click="addEnv">{{ t('createContainer.addVariable') }}</el-button>
            </div>

            <el-divider />

            <div class="sub-section">
              <div class="sub-title">{{ t('createContainer.labels') }}</div>
              <div v-for="(label, idx) in formArrays.labels" :key="idx" class="inline-form-row">
                <el-input v-model="label.key" :placeholder="t('createContainer.labelKeyPlaceholder')" size="small" style="width: 35%" />
                <span class="separator">=</span>
                <el-input v-model="label.value" :placeholder="t('createContainer.labelValuePlaceholder')" size="small" style="width: 50%" />
                <el-button type="danger" link :icon="Delete" @click="formArrays.labels.splice(idx, 1)" />
              </div>
              <el-button type="primary" link :icon="Plus" @click="formArrays.labels.push({ key: '', value: '' })">{{ t('createContainer.addLabel') }}</el-button>
            </div>
          </div>
        </el-tab-pane>

        <!-- ==================== 安全与资源 ==================== -->
        <el-tab-pane name="security">
          <template #label>
            <span class="tab-label"><el-icon><Lock /></el-icon> {{ t('createContainer.securityTab') }}</span>
          </template>
          <div class="tab-content">
            <div class="sub-section">
              <div class="sub-title">{{ t('createContainer.securityOptions') }}</div>
              <div class="switch-group">
                <el-checkbox v-model="form.privileged">{{ t('createContainer.privilegedMode') }}</el-checkbox>
                <el-checkbox v-model="form.readOnlyRootfs">{{ t('createContainer.readOnlyRootfs') }}</el-checkbox>
                <el-checkbox v-model="form.hostPid">{{ t('createContainer.hostPidNamespace') }}</el-checkbox>
                <el-checkbox v-model="form.init">{{ t('createContainer.enableInit') }}</el-checkbox>
              </div>

              <el-row :gutter="20" style="margin-top: 16px">
                <el-col :span="12">
                  <el-form-item :label="t('createContainer.addCapabilities')">
                    <el-select v-model="form.capAdd" multiple collapse-tags collapse-tags-tooltip style="width: 100%" placeholder="Cap Add">
                      <el-option v-for="cap in allCapabilities" :key="cap" :label="cap" :value="cap" />
                    </el-select>
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item :label="t('createContainer.dropCapabilities')">
                    <el-select v-model="form.capDrop" multiple collapse-tags collapse-tags-tooltip style="width: 100%" placeholder="Cap Drop">
                      <el-option v-for="cap in allCapabilities" :key="cap" :label="cap" :value="cap" />
                    </el-select>
                  </el-form-item>
                </el-col>
              </el-row>

              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item :label="t('createContainer.additionalUserGroups')">
                    <el-input v-model="form.groupAdd" :placeholder="t('createContainer.userGroupsPlaceholder')" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item :label="t('createContainer.runtime')">
                    <el-select v-model="form.runtime" :placeholder="t('createContainer.runtimeDefault')" clearable style="width: 100%">
                      <el-option label="runc" value="" />
                      <el-option label="nvidia" value="nvidia" />
                    </el-select>
                  </el-form-item>
                </el-col>
              </el-row>
            </div>

            <el-divider content-position="left">{{ t('createContainer.resourceLimits') }}</el-divider>

            <el-row :gutter="20">
              <el-col :span="8">
                <el-form-item :label="t('createContainer.cpuCores')">
                  <el-input v-model.number="form.resources.cpuQuota" type="number" :placeholder="t('createContainer.cpuCoresPlaceholder')" min="0" step="0.5" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.cpuWeight')">
                  <el-input v-model.number="form.resources.cpuShares" type="number" :placeholder="t('createContainer.cpuWeightPlaceholder')" min="0" max="1024" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.cpuBinding')">
                  <el-input v-model="form.resources.cpusetCpus" :placeholder="t('createContainer.cpuBindingPlaceholder')" />
                </el-form-item>
              </el-col>
            </el-row>

            <el-row :gutter="20">
              <el-col :span="8">
                <el-form-item :label="t('createContainer.memoryLimit')">
                  <el-input v-model="form.resources.memoryLimit" placeholder="MB">
                    <template #append>MB</template>
                  </el-input>
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.memorySwap')">
                  <el-input v-model="form.resources.memorySwap" :placeholder="t('createContainer.memorySwapPlaceholder')">
                    <template #append>MB</template>
                  </el-input>
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.memoryReservation')">
                  <el-input v-model="form.resources.memoryReservation" :placeholder="t('createContainer.memoryReservationPlaceholder')">
                    <template #append>MB</template>
                  </el-input>
                </el-form-item>
              </el-col>
            </el-row>

            <el-row :gutter="20">
              <el-col :span="8">
                <el-form-item :label="t('createContainer.shmSize')">
                  <el-input v-model="form.shmSize" :placeholder="t('createContainer.shmSizePlaceholder')" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.pidLimit')">
                  <el-input v-model.number="form.resources.pidsLimit" type="number" :placeholder="t('createContainer.pidLimitPlaceholder')" min="-1" />
                </el-form-item>
              </el-col>
            </el-row>
          </div>
        </el-tab-pane>

        <!-- ==================== 健康检查与日志 ==================== -->
        <el-tab-pane name="health">
          <template #label>
            <span class="tab-label"><el-icon><Checked /></el-icon> {{ t('createContainer.healthTab') }}</span>
          </template>
          <div class="tab-content">
            <div class="sub-section">
              <div class="sub-title">{{ t('createContainer.healthCheck') }}</div>
              <el-row :gutter="16">
                <el-col :span="24">
                  <el-form-item :label="t('createContainer.healthCheckCommand')">
                    <el-input v-model="form.healthCheck.test" :placeholder="t('createContainer.healthCheckPlaceholder')" />
                  </el-form-item>
                </el-col>
              </el-row>
              <el-row :gutter="16">
                <el-col :span="8">
                  <el-form-item :label="t('createContainer.healthInterval')">
                    <el-input v-model.number="form.healthCheck.interval" type="number" placeholder="30" min="1" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item :label="t('createContainer.healthTimeout')">
                    <el-input v-model.number="form.healthCheck.timeout" type="number" placeholder="10" min="1" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item :label="t('createContainer.healthRetries')">
                    <el-input v-model.number="form.healthCheck.retries" type="number" placeholder="3" min="1" />
                  </el-form-item>
                </el-col>
              </el-row>
              <el-row :gutter="16">
                <el-col :span="8">
                  <el-form-item :label="t('createContainer.healthStartPeriod')">
                    <el-input v-model.number="form.healthCheck.startPeriod" type="number" placeholder="0" min="0" />
                  </el-form-item>
                </el-col>
                <el-col :span="8">
                  <el-form-item :label="t('createContainer.healthStartInterval')">
                    <el-input v-model.number="form.healthCheck.startInterval" type="number" placeholder="5" min="1" />
                  </el-form-item>
                </el-col>
              </el-row>
            </div>

            <el-divider content-position="left">{{ t('createContainer.logConfig') }}</el-divider>

            <el-row :gutter="20">
              <el-col :span="8">
                <el-form-item :label="t('createContainer.logDriver')">
                  <el-select v-model="form.logConfig.driver" style="width: 100%">
                    <el-option :label="t('createContainer.logDriverDefault')" value="json-file" />
                    <el-option label="local" value="local" />
                    <el-option label="syslog" value="syslog" />
                    <el-option label="journald" value="journald" />
                    <el-option :label="t('createContainer.logDriverNone')" value="none" />
                  </el-select>
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.logMaxSize')">
                  <el-input v-model="form.logConfig.maxSize" :placeholder="t('createContainer.logMaxSizePlaceholder')" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item :label="t('createContainer.logMaxFile')">
                  <el-input v-model.number="form.logConfig.maxFile" type="number" placeholder="3" min="1" />
                </el-form-item>
              </el-col>
            </el-row>
          </div>
        </el-tab-pane>

        <!-- ==================== Web 反向代理 ==================== -->
        <el-tab-pane name="web">
          <template #label>
            <span class="tab-label">
              <el-icon><Monitor /></el-icon> {{ t('createContainer.webTab') }}
              <el-badge v-if="isWebApp" is-dot type="success" style="margin-left: 4px" />
            </span>
          </template>
          <div class="tab-content">
            <!-- 无网络提示 -->
            <el-alert v-if="disableNetwork" type="warning" :closable="false" style="margin-bottom: 16px">
              <template #title>
                {{ t('createContainer.disableNetworkWarning') }}
              </template>
            </el-alert>
            
            <el-form-item :label="t('createContainer.enableDomainAccess')">
              <el-switch v-model="isWebApp" :disabled="disableNetwork || useHostNetwork" />
            </el-form-item>
            <template v-if="isWebApp">
              <el-row :gutter="20">
                <el-col :span="12">
                  <el-form-item :label="t('createContainer.domain')">
                    <el-input v-model="form.domainMapping.domain" :placeholder="t('createContainer.domainPlaceholder')" />
                  </el-form-item>
                </el-col>
                <el-col :span="6">
                  <el-form-item :label="t('createContainer.domainContainerPort')">
                    <el-input v-model.number="form.domainMapping.containerPort" type="number" placeholder="80" min="1" max="65535" />
                  </el-form-item>
                </el-col>
                <el-col :span="6">
                  <el-form-item :label="t('createContainer.pathPrefix')">
                    <el-input v-model="form.domainMapping.pathPrefix" :placeholder="t('createContainer.pathPrefixPlaceholder')" />
                  </el-form-item>
                </el-col>
              </el-row>
              <el-form-item :label="t('createContainer.sslMode')">
                <el-radio-group v-model="sslMode">
                  <el-radio-button value="none">{{ t('createContainer.sslModeNone') }}</el-radio-button>
                  <el-radio-button value="existing">{{ t('createContainer.sslModeExisting') }}</el-radio-button>
                  <el-radio-button value="auto">{{ t('createContainer.sslModeAuto') }}</el-radio-button>
                </el-radio-group>
              </el-form-item>
              <el-form-item :label="t('createContainer.selectCertificate')" v-if="sslMode === 'existing'">
                <el-select v-model="form.domainMapping.certificateId" :placeholder="t('createContainer.selectCertificatePlaceholder')" style="width: 100%" clearable>
                  <el-option v-for="cert in certificates" :key="cert.id" :label="getCertLabel(cert)" :value="cert.id" />
                </el-select>
              </el-form-item>
              <el-form-item :label="t('createContainer.selectAcmeAccount')" v-if="sslMode === 'auto'" required>
                <el-select v-model="form.domainMapping.accountId" :placeholder="t('createContainer.selectAcmeAccountPlaceholder')" style="width: 100%">
                  <el-option v-for="acc in accounts" :key="acc.id" :label="`${acc.email} (${acc.provider})`" :value="acc.id" />
                </el-select>
                <div v-if="accounts.length === 0" class="field-hint" style="color: var(--el-color-warning);">
                  {{ t('createContainer.noAvailableAccount') }}
                </div>
              </el-form-item>
            </template>
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-form>

    <template #footer>
      <div class="dialog-footer">
        <div class="footer-left">
          <el-button @click="showTemplateDialog = true" :icon="Document">{{ t('createContainer.templateLibrary') }}</el-button>
          <el-button @click="showSaveTemplateDialog = true" :icon="FolderAdd">{{ t('createContainer.saveAsTemplate') }}</el-button>
        </div>
        <div class="footer-spacer"></div>
        <div class="footer-right">
          <el-button @click="handleClose">{{ t('common.cancel') }}</el-button>
          <el-button type="primary" @click="handleSubmit" :loading="creating">
            {{ pulling ? t('createContainer.pullingImage') : (editMode ? t('createContainer.saveButton') : t('createContainer.createButton')) }}
          </el-button>
        </div>
      </div>
    </template>
  </el-dialog>

  <!-- 镜像拉取对话框 -->
  <el-dialog
    v-model="showPullDialog"
    :title="t('createContainer.pullImageTitle')"
    width="600px"
    :close-on-click-modal="!manualPulling"
    :close-on-press-escape="!manualPulling"
    class="pull-image-dialog"
  >
    <el-tabs v-model="pullTab">
      <!-- 直接输入 -->
      <el-tab-pane :label="t('createContainer.directInput')" name="direct">
        <div class="pull-direct-section">
          <el-input
            v-model="pullImageName"
            :placeholder="t('createContainer.pullImagePlaceholder')"
            :disabled="manualPulling"
            @keyup.enter="handlePullImage"
          >
            <template #append>
              <el-button @click="handlePullImage" :loading="manualPulling" :disabled="!pullImageName.trim()">
                {{ t('createContainer.pullButton') }}
              </el-button>
            </template>
          </el-input>
          <div class="pull-hint">
            <el-icon><InfoFilled /></el-icon>
            <span>{{ t('createContainer.pullHint') }}</span>
          </div>
          <!-- 拉取进度 -->
          <div v-if="manualPulling" class="pull-progress-section">
            <PullProgressDisplay 
              :title="t('createContainer.pullingLabel', { image: pullImageName })" 
              :progressMap="progressMap" 
            />
          </div>
        </div>
      </el-tab-pane>
      
      <!-- 搜索 Docker Hub -->
      <el-tab-pane :label="t('createContainer.dockerHub')" name="dockerhub">
        <div class="pull-search-section">
          <div class="search-row">
            <el-input
              v-model="searchKeyword"
              :placeholder="t('createContainer.searchDockerHub')"
              @keyup.enter="searchDockerHub"
              :disabled="searching"
            >
              <template #append>
                <el-button @click="searchDockerHub" :loading="searching">
                  <el-icon><Search /></el-icon>
                </el-button>
              </template>
            </el-input>
          </div>
          <div v-if="searchResults.length > 0" class="search-results">
            <div 
              v-for="img in searchResults" 
              :key="img.name" 
              class="search-result-item"
              @click="selectSearchResult(img)"
            >
              <div class="result-name">{{ img.name }}</div>
              <div class="result-desc">{{ img.description || t('createContainer.noDescription') }}</div>
              <div class="result-meta">
                <el-tag size="small" type="info">⭐ {{ img.stars || 0 }}</el-tag>
              </div>
            </div>
          </div>
          <el-empty v-else-if="searched && !searching" :description="t('createContainer.noResults')" />
          <el-empty v-else-if="!searched" :description="t('createContainer.searchPlaceholder')" />
        </div>
      </el-tab-pane>
      
      <!-- 其他注册表 -->
      <el-tab-pane :label="t('createContainer.privateRegistry')" name="registry">
        <div class="pull-registry-section">
          <el-select v-model="selectedRegistryId" :placeholder="t('createContainer.selectRegistry')" style="width: 100%; margin-bottom: 12px">
            <el-option 
              v-for="reg in registries" 
              :key="reg.id" 
              :label="reg.name" 
              :value="reg.id"
            >
              <span>{{ reg.name }}</span>
              <span style="color: var(--text-muted); margin-left: 8px; font-size: 12px">{{ reg.url }}</span>
            </el-option>
          </el-select>
          <el-input
            v-model="registryImageName"
            :placeholder="t('createContainer.registryImagePlaceholder')"
            :disabled="!selectedRegistryId || manualPulling"
            @keyup.enter="pullFromRegistry"
          >
            <template #append>
              <el-button 
                @click="pullFromRegistry" 
                :loading="manualPulling" 
                :disabled="!selectedRegistryId || !registryImageName.trim()"
              >
                {{ t('createContainer.pullButton') }}
              </el-button>
            </template>
          </el-input>
          <div class="pull-hint">
            <el-icon><InfoFilled /></el-icon>
            <span>{{ t('createContainer.registryPullHint') }}</span>
          </div>
          <div v-if="manualPulling && selectedRegistryId" class="pull-progress-section">
            <PullProgressDisplay 
              :title="t('createContainer.pullingLabel', { image: registryImageName })" 
              :progressMap="progressMap" 
            />
          </div>
        </div>
      </el-tab-pane>
    </el-tabs>

    <template #footer>
      <el-button @click="showPullDialog = false" :disabled="manualPulling">{{ t('common.close') }}</el-button>
    </template>
  </el-dialog>

  <ContainerTemplateDialog v-model="showTemplateDialog" @template-selected="handleTemplateSelect" />

  <!-- 存为模板对话框 -->
  <el-dialog
    v-model="showSaveTemplateDialog"
    :title="t('createContainer.saveAsTemplateTitle')"
    width="500px"
    :close-on-click-modal="false"
    @close="resetSaveTemplateForm"
  >
    <el-form label-width="80px">
      <el-form-item :label="t('createContainer.templateName')" required>
        <el-input v-model="saveTemplateName" :placeholder="t('createContainer.templateNamePlaceholder')" />
      </el-form-item>
      <el-form-item :label="t('createContainer.templateType')">
        <el-select v-model="saveTemplateType" :placeholder="t('createContainer.templateType')">
          <el-option :label="t('createContainer.templateTypeWeb')" value="web" />
          <el-option :label="t('createContainer.templateTypeDatabase')" value="database" />
          <el-option :label="t('createContainer.templateTypeCache')" value="cache" />
          <el-option :label="t('createContainer.templateTypeQueue')" value="queue" />
          <el-option :label="t('createContainer.templateTypeMonitoring')" value="monitoring" />
          <el-option :label="t('createContainer.templateTypeDevelopment')" value="development" />
          <el-option :label="t('createContainer.templateTypeCustom')" value="custom" />
        </el-select>
      </el-form-item>
      <el-form-item :label="t('createContainer.templateDescription')">
        <el-input
          v-model="saveTemplateDescription"
          type="textarea"
          :rows="3"
          :placeholder="t('createContainer.templateDescriptionPlaceholder')"
        />
      </el-form-item>
    </el-form>
    <div class="template-preview-info">
      <el-alert type="info" :closable="false">
        <template #title>
          {{ t('createContainer.templatePreviewInfo') }}
        </template>
      </el-alert>
    </div>
    <template #footer>
      <div class="dialog-footer-buttons">
        <el-button @click="showSaveTemplateDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="handleSaveAsTemplate" :loading="savingTemplate">
          {{ t('common.save') }}
        </el-button>
      </div>
    </template>
  </el-dialog>

</template>

<script setup lang="ts">
import { ref, reactive, computed, onBeforeUnmount, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'

const { t } = useI18n()
import { Search, Plus, Delete, Document, Setting, Connection, Folder, Monitor, Tools, ArrowDown, Lock, Checked, List, Download, InfoFilled, FolderAdd } from '@element-plus/icons-vue'
import { containerApi } from '@/api/containers'
import { imageApi } from '@/api/image'
import { registryApi } from '@/api/registry'
import { useImagesStore } from '@/stores/images'
import { useVolumesStore } from '@/stores/volumes'
import { networkApi } from '@/api/network'
import { certificateApi } from '@/api/certificate'
import { acmeApi } from '@/api/acme'
import type { AcmeAccount } from '@/api/certificate'
import templateApi from '@/api/templates'
import PullProgressDisplay from '../image/PullProgressDisplay.vue'
import ContainerTemplateDialog from './ContainerTemplateDialog.vue'
import * as signalR from '@microsoft/signalr'

const props = defineProps<{ 
  modelValue: boolean,
  editContainer?: any,  // 编辑模式：传入要编辑的容器信息
  editMode?: boolean,   // 是否为编辑模式
  preselectedImage?: any  // 预选镜像：从镜像页面传入
}>()
const emit = defineEmits(['update:modelValue', 'success'])

const imagesStore = useImagesStore()
const volumesStore = useVolumesStore()
const formRef = ref()
const creating = ref(false)
const pulling = ref(false)
const manualPulling = ref(false)
const progressMap = ref<Record<string, any>>({})
const showPullDialog = ref(false)
const pullTab = ref('direct')
const pullImageName = ref('')
const searchKeyword = ref('')
const searching = ref(false)
const searched = ref(false)
const searchResults = ref<any[]>([])
const registries = ref<any[]>([])
const selectedRegistryId = ref('')
const registryImageName = ref('')
const showTemplateDialog = ref(false)
const selectedTemplate = ref<any>(null)
const showSaveTemplateDialog = ref(false)
const saveTemplateName = ref('')
const saveTemplateType = ref<'web' | 'database' | 'cache' | 'queue' | 'monitoring' | 'development' | 'custom'>('custom')
const saveTemplateDescription = ref('')
const savingTemplate = ref(false)
const activeTab = ref('basic')

const availableNetworks = ref<any[]>([])
const certificates = ref<any[]>([])
const accounts = ref<AcmeAccount[]>([])

// 可加入的网络（排除系统网络 none, host, bridge）
const joinableNetworks = computed(() => {
  const systemNetworks = ['none', 'host', 'bridge']
  return availableNetworks.value.filter(n => !systemNetworks.includes(n.name))
})

// 默认网络（dockerpanel-network）
const defaultNetwork = computed(() => {
  return availableNetworks.value.find(n => n.name === 'dockerpanel-network')
})

// 其他可加入的网络（排除默认网络和 host/none 模式）
const otherNetworks = computed(() => {
  return availableNetworks.value.filter(n => 
    n.name !== 'dockerpanel-network' && 
    n.name !== 'host' && 
    n.name !== 'none'
  )
})

// 可用的存储卷列表
const availableVolumes = computed(() => {
  return volumesStore.volumes || []
})

// 选择卷变化时的处理（不再立即创建，只是标记）
const handleVolumeSelectChange = (val: string, idx: number) => {
  // 选择 "__create_new__" 时保持这个值，提交时再创建
  // 不做任何操作
}

// 处理附加网络选择变化
const handleAdditionalNetworksChange = (networkIds: string[]) => {
  // 为新选择的网络初始化配置
  networkIds.forEach(id => {
    if (!form.network.configs[id]) {
      form.network.configs[id] = {
        ipAddress: '',
        ipv6Address: '',
        aliases: ''
      }
    }
  })
}

// 获取网络名称
const getNetworkName = (networkId: string) => {
  return availableNetworks.value.find(n => n.id === networkId)?.name || networkId
}

// 获取网络驱动
const getNetworkDriver = (networkId: string) => {
  return availableNetworks.value.find(n => n.id === networkId)?.driver || ''
}

const isWebApp = ref(false)
const sslMode = ref('none')
const cachedImageDetail = ref<any>(null)  // 缓存当前镜像详情
const commandStr = ref('')
const originalCmd = ref<string[] | null>(null)  // 缓存原始 cmd 数组
const originalEntrypoint = ref<string[] | null>(null)  // 缓存原始 entrypoint 数组

// 网络控制
const disableNetwork = ref(false)
const useHostNetwork = ref(false)

const handleDisableNetworkChange = (val: boolean) => {
  if (val) {
    useHostNetwork.value = false
  }
}

const handleHostNetworkChange = (val: boolean) => {
  if (val) {
    disableNetwork.value = false
  }
}

// 计算实际的网络模式
// 只有 host/none 需要显式指定，其他情况由 network.networkId 决定
const networkModeValue = computed(() => {
  if (disableNetwork.value) return 'none'
  if (useHostNetwork.value) return 'host'
  return undefined  // 不传 'bridge'，让后端使用 network.networkId
})

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

// 监听对话框打开，预选镜像时自动填充
watch(visible, async (val) => {
  if (val) {
    // 加载存储卷列表
    loadVolumes()
    // 预选镜像处理
    if (props.preselectedImage && !props.editMode) {
      // 预选镜像：格式为 repository:tag 或镜像ID
      const imageRef = props.preselectedImage.repository && props.preselectedImage.repository !== '<none>'
        ? `${props.preselectedImage.repository}:${props.preselectedImage.tag || 'latest'}`
        : props.preselectedImage.id
      form.image = imageRef
      // 调用 prefetchImageConfig 填充镜像的默认配置（端口、环境变量、卷等）
      await prefetchImageConfig(imageRef)
    }
  }
})

// Linux Capabilities 完整列表
const allCapabilities = [
  'ALL', 'AUDIT_CONTROL', 'AUDIT_READ', 'AUDIT_WRITE', 'BLOCK_SUSPEND',
  'BPF', 'CHECKPOINT_RESTORE', 'CHOWN', 'DAC_OVERRIDE', 'DAC_READ_SEARCH',
  'FOWNER', 'FSETID', 'IPC_LOCK', 'IPC_OWNER', 'KILL', 'LEASE',
  'LINUX_IMMUTABLE', 'MAC_ADMIN', 'MAC_OVERRIDE', 'MKNOD',
  'NET_ADMIN', 'NET_BIND_SERVICE', 'NET_BROADCAST', 'NET_RAW',
  'PERFMON', 'SETFCAP', 'SETGID', 'SETPCAP', 'SETUID',
  'SYS_ADMIN', 'SYS_BOOT', 'SYS_CHROOT', 'SYS_MODULE', 'SYS_NICE',
  'SYS_PACCT', 'SYS_PTRACE', 'SYS_RAWIO', 'SYS_RESOURCE', 'SYS_TIME',
  'SYS_TTY_CONFIG', 'SYSLOG', 'WAKE_ALARM'
]

// 本地镜像选项列表
const formatSize = (bytes: number): string => {
  if (bytes >= 1024 * 1024 * 1024) return (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB'
  if (bytes >= 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(0) + ' MB'
  return (bytes / 1024).toFixed(0) + ' KB'
}

const localImageOptions = computed(() => {
  return imagesStore.images
    .filter(i => i.repository && !i.repository.includes('<none>'))
    .map(i => ({
      value: `${i.repository}:${i.tag || 'latest'}`,
      label: `${i.repository}:${i.tag || 'latest'}`,
      size: formatSize(i.size || 0)
    }))
})

// 表单初始状态
const formInitialState = {
  name: '', image: '', hostname: '', workingDir: '', user: '',
  tty: false, stdinOpen: false, privileged: false, autoRemove: false, readOnlyRootfs: false,
  hostPid: false, init: false,
  entrypoint: '', command: '', shmSize: '', groupAdd: '',
  dns: '', dnsSearch: '', extraHosts: '',
  stopSignal: '', stopTimeout: null as number | null,
  runtime: '',
  capAdd: [] as string[],
  capDrop: [] as string[],
  logConfig: { driver: 'json-file', maxSize: '', maxFile: 3 },
  restartPolicy: { name: 'no', maximumRetryCount: 0 },
  network: { networkIds: [] as string[], configs: {} as Record<string, any>, primaryNetworkId: '' as string, macAddress: '' as string },
  domainMapping: { domain: '', containerPort: 80, enableSsl: false, certificateId: '', accountId: '', autoRequestCertificate: false, pathPrefix: '/' },
  resources: { cpuShares: 0, memoryLimit: '', memorySwap: '', memoryReservation: '', cpuQuota: 0, cpusetCpus: '', pidsLimit: null as number | null },
  healthCheck: { test: '', interval: 30, timeout: 10, retries: 3, startPeriod: 0, startInterval: 5 }
}

const formArraysInitialState = { 
  ports: [] as any[], 
  volumes: [] as any[], 
  environment: [] as any[],
  devices: [] as { host: string; container: string }[],
  labels: [] as { key: string; value: string }[],
  tmpfs: [] as { containerPath: string; options: string }[]
}

const form = reactive({ ...formInitialState })
const formArrays = reactive({ ...formArraysInitialState })

// 编辑模式
const editMode = computed(() => props.editMode && props.editContainer)
const editContainerId = ref<string | null>(null)

// 从容器信息填充表单
const populateFormFromContainer = (container: any) => {
  if (!container) return
  
  editContainerId.value = container.id
  
  // 基本信息
  form.name = container.name?.replace(/^\//, '') || ''
  form.image = container.image || ''
  form.hostname = container.hostName || ''
  form.workingDir = container.workingDir || ''
  form.user = container.user || ''
  
  // 开关选项
  form.tty = container.tty || false
  form.stdinOpen = container.openStdin || false
  form.privileged = container.hostConfig?.Privileged || false
  form.autoRemove = container.hostConfig?.AutoRemove || false
  form.readOnlyRootfs = container.hostConfig?.ReadonlyRootfs || false
  form.hostPid = container.hostConfig?.PidMode === 'host'
  form.init = container.hostConfig?.Init || false
  
  // 命令
  if (container.entrypoint && Array.isArray(container.entrypoint)) {
    form.entrypoint = container.entrypoint.join(' ')
  } else if (container.entrypoint) {
    form.entrypoint = String(container.entrypoint)
  }
  if (container.command) {
    if (Array.isArray(container.command)) {
      commandStr.value = container.command.join(' ')
    } else {
      commandStr.value = String(container.command)
    }
  }
  
  // 重启策略
  if (container.restartPolicy) {
    form.restartPolicy.name = container.restartPolicy.name || 'no'
    form.restartPolicy.maximumRetryCount = container.restartPolicy.maximumRetryCount || 0
  }
  
  // 环境变量
  if (container.environment && container.environment.length > 0) {
    formArrays.environment = container.environment.map((env: string) => {
      const idx = env.indexOf('=')
      return {
        key: idx > 0 ? env.substring(0, idx) : env,
        value: idx > 0 ? env.substring(idx + 1) : ''
      }
    })
  }
  
  // 标签
  if (container.labels) {
    formArrays.labels = Object.entries(container.labels).map(([key, value]) => ({
      key,
      value: String(value)
    }))
  }
  
  // 端口映射
  if (container.ports && container.ports.length > 0) {
    formArrays.ports = container.ports.map((p: any) => ({
      hostIp: p.ip || '',
      hostPort: p.publicPort || '',
      containerPort: p.privatePort || '',
      protocol: p.type || 'tcp'
    }))
  }
  
  // 卷挂载
  if (container.mounts && container.mounts.length > 0) {
    formArrays.volumes = container.mounts.map((m: any) => ({
      hostPath: m.source || '',
      containerPath: m.destination || '',
      readOnly: !m.rw
    }))
  }
  
  // 网络模式
  const networkMode = container.hostConfig?.NetworkMode || 'bridge'
  if (networkMode === 'none') {
    disableNetwork.value = true
  } else if (networkMode === 'host') {
    useHostNetwork.value = true
  }
  
  // 资源限制
  if (container.hostConfig) {
    form.resources.memoryLimit = container.hostConfig.Memory ? String(Math.round(container.hostConfig.Memory / 1024 / 1024)) : ''
    form.resources.memorySwap = container.hostConfig.MemorySwap ? String(Math.round(container.hostConfig.MemorySwap / 1024 / 1024)) : ''
    form.resources.cpuQuota = container.hostConfig.CpuQuota ? container.hostConfig.CpuQuota / 100000 : 0
    form.resources.cpusetCpus = container.hostConfig.CpusetCpus || ''
    form.resources.cpuShares = container.hostConfig.CpuShares || 0
  }
  
  // Capabilities
  if (container.hostConfig?.CapAdd) {
    form.capAdd = container.hostConfig.CapAdd
  }
  if (container.hostConfig?.CapDrop) {
    form.capDrop = container.hostConfig.CapDrop
  }
  
  // DNS
  if (container.hostConfig?.Dns && container.hostConfig.Dns.length > 0) {
    form.dns = container.hostConfig.Dns.join(', ')
  }
  if (container.hostConfig?.DnsSearch && container.hostConfig.DnsSearch.length > 0) {
    form.dnsSearch = container.hostConfig.DnsSearch.join(', ')
  }
  if (container.hostConfig?.ExtraHosts && container.hostConfig.ExtraHosts.length > 0) {
    form.extraHosts = container.hostConfig.ExtraHosts.join(', ')
  }
}

// 创建重置函数
const resetFormState = () => {
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
  
  // 重置其他状态
  commandStr.value = ''
  selectedTemplate.value = null
  isWebApp.value = false
  sslMode.value = 'none'
  disableNetwork.value = false
  useHostNetwork.value = false
}

const rules = {
  image: [{ required: true, message: '请选择镜像', trigger: ['blur', 'change'] }]
}

let hubConnection: signalR.HubConnection | null = null

const startSignalR = async () => {
  hubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/dockerpanelHub', {
      accessTokenFactory: () => localStorage.getItem('token') || ''
    })
    .withAutomaticReconnect()
    .build()
  hubConnection.on('ImagePullUpdate', (update) => {
    // 手动拉取时不设置 pulling（那是创建时自动拉取的标志）
    if (!manualPulling.value) {
      pulling.value = true
    }
    progressMap.value[update.id] = update
  })
  await hubConnection.start()
  return hubConnection.connectionId
}

const handleSubmit = async () => {
  if (!formRef.value) return
  
  try {
    await formRef.value.validate()
  } catch {
    // 验证失败，Element Plus 会自动显示红色边框和错误信息
    ElMessage.warning('请检查表单中的必填项')
    return
  }

  creating.value = true
  try {
    // 先创建标记为"新卷"的卷
    for (const vol of formArrays.volumes) {
      if (vol.type === 'volume' && vol.volumeName === '__create_new__') {
        try {
          const createdVolume = await volumesStore.createVolume({
            name: '',  // 空名称，让 Docker 自动生成
            driver: 'local'
          })
          vol.volumeName = createdVolume.name
          ElMessage.success(`已创建卷: ${createdVolume.name}`)
        } catch (error: any) {
          ElMessage.error(`创建卷失败: ${error.message}`)
          creating.value = false
          return
        }
      }
    }

    const connectionId = await startSignalR()
    
    const allNetIds = form.network.networkIds || []
    const defaultNet = availableNetworks.value.find(n => n.name === 'dockerpanel-network')
    
    // 用户选择的主网络，如果没有选择则使用默认网络
    const primaryNetId = form.network.primaryNetworkId || (defaultNet ? defaultNet.id : '')
    const primaryNetObj = availableNetworks.value.find(n => n.id === primaryNetId)
    // Docker API NetworkMode 需要网络名而不是 ID
    const primaryNetName = primaryNetObj ? primaryNetObj.name : ''
    // 附加网络也转为名称
    const additionalNetNames = allNetIds
      .filter(id => id !== primaryNetId)
      .map(id => availableNetworks.value.find(n => n.id === id)?.name)
      .filter((name): name is string => !!name)

    const rawAliases = form.network.configs[primaryNetId]?.aliases || ''
    const aliasList = [form.name, ...rawAliases.split(',').map((a: string) => a.trim())].filter(a => a)

    const request = {
      name: form.name,
      image: form.image,
      connectionId,
      hostname: form.hostname || form.name,
      workingDir: form.workingDir || undefined,
      user: form.user || undefined,
      tty: form.tty,
      interactive: form.stdinOpen,
      autoRemove: form.autoRemove,
      privileged: form.privileged,
      readOnlyRootfs: form.readOnlyRootfs,
      hostPid: form.hostPid,
      init: form.init,
      entrypoint: form.entrypoint 
        ? (form.entrypoint === originalEntrypoint.value?.join(' ') 
            ? originalEntrypoint.value 
            : form.entrypoint.trim().split(/\s+/))
        : undefined,
      command: commandStr.value 
        ? (commandStr.value === originalCmd.value?.join(' ') 
            ? originalCmd.value 
            : commandStr.value.trim().split(/\s+/))
        : [],
      shmSize: form.shmSize || undefined,
      stopSignal: form.stopSignal || undefined,
      stopTimeout: form.stopTimeout ?? undefined,
      runtime: form.runtime || undefined,
      groupAdd: form.groupAdd ? form.groupAdd.split(',').map(g => g.trim()) : undefined,
      dns: form.dns ? form.dns.split(',').map(d => d.trim()) : undefined,
      dnsSearch: form.dnsSearch ? form.dnsSearch.split(',').map(d => d.trim()) : undefined,
      extraHosts: form.extraHosts ? form.extraHosts.split(',').map(h => h.trim()) : undefined,
      ...(networkModeValue.value ? { networkMode: networkModeValue.value } : {}),
      capAdd: form.capAdd.length > 0 ? form.capAdd : undefined,
      capDrop: form.capDrop.length > 0 ? form.capDrop : undefined,

      restartPolicy: form.restartPolicy.name !== 'no' || form.restartPolicy.maximumRetryCount > 0 ? {
        name: form.restartPolicy.name,
        maximumRetryCount: form.restartPolicy.maximumRetryCount
      } : undefined,

      resources: (form.resources.memoryLimit || form.resources.memorySwap || form.resources.memoryReservation ||
                  form.resources.cpuQuota > 0 || form.resources.cpuShares > 0 || form.resources.cpusetCpus ||
                  form.shmSize || form.resources.pidsLimit !== null) ? {
        memoryLimit: form.resources.memoryLimit ? (Number(form.resources.memoryLimit) * 1024 * 1024).toString() : undefined,
        memorySwap: form.resources.memorySwap ? (Number(form.resources.memorySwap) === -1 ? '-1' : (Number(form.resources.memorySwap) * 1024 * 1024).toString()) : undefined,
        memoryReservation: form.resources.memoryReservation ? (Number(form.resources.memoryReservation) * 1024 * 1024).toString() : undefined,
        cpuQuota: form.resources.cpuQuota > 0 ? (Number(form.resources.cpuQuota) * 100000).toString() : undefined,
        cpuPeriod: form.resources.cpuQuota > 0 ? "100000" : undefined,
        cpuShares: form.resources.cpuShares > 0 ? form.resources.cpuShares.toString() : undefined,
        cpusetCpus: form.resources.cpusetCpus || undefined,
        shmSize: form.shmSize || undefined,
        pidsLimit: form.resources.pidsLimit ?? undefined
      } : undefined,

      logConfig: form.logConfig.driver !== 'json-file' || form.logConfig.maxSize ? {
        driver: form.logConfig.driver,
        maxSize: form.logConfig.maxSize || undefined,
        maxFile: form.logConfig.maxFile || undefined
      } : undefined,

      healthCheck: form.healthCheck.test ? {
        test: form.healthCheck.test,
        interval: form.healthCheck.interval,
        timeout: form.healthCheck.timeout,
        retries: form.healthCheck.retries,
        startPeriod: form.healthCheck.startPeriod,
        startInterval: form.healthCheck.startInterval
      } : undefined,

      network: primaryNetName ? {
          networkId: primaryNetName,
          ipAddress: form.network.configs[primaryNetId]?.ipAddress || undefined,
          aliases: aliasList,
          additionalNetworks: additionalNetNames,
          macAddress: form.network.macAddress || undefined
      } : undefined,

      domainMapping: isWebApp.value ? {
        domain: form.domainMapping.domain,
        containerPort: form.domainMapping.containerPort,
        enableSsl: sslMode.value !== 'none',
        autoRequestCertificate: sslMode.value === 'auto',
        certificateId: form.domainMapping.certificateId,
        accountId: form.domainMapping.accountId,
        pathPrefix: form.domainMapping.pathPrefix
      } : undefined,

      ports: formArrays.ports.filter(p => p.containerPort).map(p => ({
        hostPort: p.hostPort.toString(),
        containerPort: p.containerPort.toString(),
        protocol: p.protocol,
        hostIp: p.hostIp || undefined
      })),

      volumes: formArrays.volumes.filter(v => v.containerPath).map(v => ({
        hostPath: v.type === 'volume' ? v.volumeName : v.hostPath,
        containerPath: v.containerPath,
        readOnly: v.readOnly
      })),

      tmpfs: formArrays.tmpfs.filter(t => t.containerPath).map(t => ({
        containerPath: t.containerPath,
        options: t.options || undefined
      })),

      devices: formArrays.devices.filter(d => d.host).map(d => ({
        hostPath: d.host,
        containerPath: d.container || d.host
      })),

      labels: formArrays.labels.filter(l => l.key).reduce((acc: any, curr) => {
        acc[curr.key] = curr.value
        return acc
      }, {}),

      environment: formArrays.environment.reduce((acc: any, curr: any) => {
        if (curr.key) acc[curr.key] = curr.value
        return acc
      }, {})
    }

    // 编辑模式：先删除旧容器，再创建新容器
    if (editMode.value && editContainerId.value) {
      const wasRunning = props.editContainer?.state === 'running'
      
      // 删除旧容器
      await containerApi.removeContainer(editContainerId.value, true)
      
      // 创建新容器
      await containerApi.createContainer(request as any)
      
      // 如果原来在运行，启动新容器
      if (wasRunning) {
        // 获取新创建的容器ID（通过名称查找）
        const containers = await containerApi.getContainers()
        const newContainer = (containers as any).data?.find((c: any) => 
          c.name === form.name || c.name === '/' + form.name
        )
        if (newContainer) {
          await containerApi.startContainer(newContainer.id)
        }
      }
      
      ElMessage.success('容器已更新')
    } else {
      // 创建模式
      await containerApi.createContainer(request as any)
      ElMessage.success('容器创建成功')
    }
    
    emit('success')
    handleClose()
  } catch (error: any) {
    console.error('创建失败:', error)
    const errorMsg = error.response?.data?.message || error.message || '创建失败'
    ElMessage.error(errorMsg)
  } finally {
    creating.value = false
    pulling.value = false
  }
}

// 保存为模板
const handleSaveAsTemplate = async () => {
  if (!form.image) {
    ElMessage.warning('请先选择镜像')
    return
  }
  if (!saveTemplateName.value.trim()) {
    ElMessage.warning('请输入模板名称')
    return
  }

  savingTemplate.value = true
  try {
    const templateData = {
      name: saveTemplateName.value.trim(),
      type: saveTemplateType.value,
      description: saveTemplateDescription.value.trim() || undefined,
      image: form.image,
      command: commandStr.value 
        ? (commandStr.value === originalCmd.value?.join(' ') 
            ? originalCmd.value 
            : commandStr.value.trim().split(/\s+/))
        : undefined,
      workingDir: form.workingDir || undefined,
      user: form.user || undefined,
      ports: formArrays.ports.length > 0 ? formArrays.ports.map(p => ({
        hostIp: p.hostIp || undefined,
        hostPort: p.hostPort || undefined,
        containerPort: p.containerPort,
        protocol: p.protocol || 'tcp'
      })) : undefined,
      volumes: formArrays.volumes.length > 0 ? formArrays.volumes.map(v => ({
        hostPath: v.hostPath || undefined,
        containerPath: v.containerPath,
        readOnly: v.readOnly || false
      })) : undefined,
      environment: formArrays.environment.length > 0 
        ? formArrays.environment.reduce((acc, e) => { if (e.key) acc[e.key] = e.value; return acc }, {} as Record<string, string>)
        : undefined,
      labels: formArrays.labels.length > 0
        ? formArrays.labels.reduce((acc, l) => { if (l.key) acc[l.key] = l.value; return acc }, {} as Record<string, string>)
        : undefined,
      restartPolicy: {
        name: form.restartPolicy.name,
        maximumRetryCount: form.restartPolicy.maximumRetryCount || 0
      },
      networkMode: networkModeValue.value,
      networks: form.network.networkIds.length > 0 
        ? form.network.networkIds.map(id => ({
            networkId: id,
            networkName: availableNetworks.value.find(n => n.id === id)?.name,
            aliases: form.network.configs[id]?.aliases?.split(',').map((a: string) => a.trim()).filter(Boolean) || undefined,
            ipAddress: form.network.configs[id]?.ipAddress || undefined
          }))
        : undefined
    }

    await templateApi.createTemplate(templateData)
    ElMessage.success('模板保存成功')
    showSaveTemplateDialog.value = false
    resetSaveTemplateForm()
  } catch (error: any) {
    console.error('保存模板失败:', error)
    ElMessage.error('保存模板失败: ' + (error.response?.data?.message || error.message))
  } finally {
    savingTemplate.value = false
  }
}

const resetSaveTemplateForm = () => {
  saveTemplateName.value = ''
  saveTemplateDescription.value = ''
  saveTemplateType.value = 'custom'
}

const handleClose = () => {
  if (hubConnection) hubConnection.stop()
  visible.value = false
  pulling.value = false
  manualPulling.value = false
  creating.value = false
  progressMap.value = {}
  activeTab.value = 'basic'
  pullImageName.value = ''
  cachedImageDetail.value = null
  resetFormState()
  
  // 重新加载初始数据（恢复默认网络等）
  loadInitialData()
}

const addPort = () => formArrays.ports.push({ hostIp: '', hostPort: '', containerPort: '', protocol: 'tcp' })
const removePort = (idx: number) => formArrays.ports.splice(idx, 1)
const addVolume = () => formArrays.volumes.push({ type: 'bind', hostPath: '', volumeName: '', containerPath: '', readOnly: false })
const removeVolume = (idx: number) => formArrays.volumes.splice(idx, 1)
const addEnv = () => formArrays.environment.push({ key: '', value: '' })
const removeEnv = (idx: number) => formArrays.environment.splice(idx, 1)

// 获取证书显示标签
const getCertLabel = (cert: any) => {
  const domains = cert.domains?.join(', ') || cert.domain || cert.name || cert.id
  return domains.length > 40 ? domains.substring(0, 40) + '...' : domains
}

const loadInitialData = async () => {
  try {
    const [nets, certsRes, accountsRes] = await Promise.all([
      networkApi.getNetworks(), 
      certificateApi.getCertificates({ status: 'valid' }),
      acmeApi.getAccounts()
    ])
    availableNetworks.value = (nets as any).data || nets
    // 证书API返回 { items: [], total, page, pageSize } 格式
    const certsData = (certsRes as any).data || certsRes
    certificates.value = certsData.items || certsData || []
    // 账户列表
    accounts.value = (accountsRes as any).data || accountsRes || []

    // 加载存储卷列表
    loadVolumes()

    // 初始化所有网络的配置对象
    availableNetworks.value.forEach(n => {
      if (!form.network.configs[n.id]) {
        form.network.configs[n.id] = { ipAddress: '', aliases: '' }
      }
    })
  } catch (e) { console.error('加载初始化数据失败', e) }
}

// 加载存储卷列表
const loadVolumes = async () => {
  try {
    await volumesStore.fetchVolumes()
  } catch (e) { console.error('加载存储卷列表失败', e) }
}

watch(visible, (val) => { 
  if (val) { 
    loadInitialData(); 
    imagesStore.fetchImages();
    // 编辑模式时填充表单
    if (editMode.value && props.editContainer) {
      populateFormFromContainer(props.editContainer);
    }
  } 
})

const handleImageSelectChange = async (val: string) => {
  form.image = val
  await prefetchImageConfig(val)
}

// 拉取镜像（直接输入）
const handlePullImage = async () => {
  const input = pullImageName.value.trim()
  if (!input) return

  // 解析镜像名和 tag
  let imageName = input
  let tag = 'latest'
  const colonIdx = input.lastIndexOf(':')
  if (colonIdx > 0 && !input.substring(colonIdx).includes('/')) {
    imageName = input.substring(0, colonIdx)
    tag = input.substring(colonIdx + 1)
  }

  manualPulling.value = true
  progressMap.value = {}

  try {
    const connectionId = await startSignalR()
    await imageApi.pullImage({ imageName, tag, connectionId })

    // 拉取成功后刷新镜像列表
    await imagesStore.fetchImages()

    // 自动选中刚拉取的镜像
    const fullName = `${imageName}:${tag}`
    form.image = fullName
    showPullDialog.value = false
    pullImageName.value = ''

    ElMessage.success(`镜像 ${fullName} 拉取成功`)
    await prefetchImageConfig(fullName)
  } catch (error: any) {
    console.error('拉取镜像失败:', error)
    const errorMsg = error.response?.data?.message || error.message || '拉取镜像失败'
    ElMessage.error(errorMsg)
  } finally {
    manualPulling.value = false
    progressMap.value = {}
    if (hubConnection) {
      hubConnection.stop()
      hubConnection = null
    }
  }
}

// 搜索 Docker Hub
const searchDockerHub = async () => {
  const keyword = searchKeyword.value.trim()
  if (!keyword) return

  searching.value = true
  searched.value = false
  try {
    // 响应拦截器已经返回 response.data，所以这里直接获取数据
    const data = await imageApi.searchImages(keyword, 25) as any[]
    
    // 处理返回数据
    const rawData = data || []
    
    // 后端返回的字段可能是大写开头或小写开头，需要兼容处理
    searchResults.value = rawData.map((item: any) => ({
      name: item.Name || item.name || '',
      description: item.Description || item.description || '',
      stars: item.Stars || item.stars || item.StarCount || item.starCount || 0,
      isOfficial: item.IsOfficial || item.isOfficial || item.Official || item.official || false,
      isAutomated: item.IsAutomated || item.isAutomated || item.Automated || item.automated || false
    }))
    
    searched.value = true
  } catch (error: any) {
    console.error('搜索镜像失败:', error)
    ElMessage.error('搜索失败: ' + (error.message || '请重试'))
  } finally {
    searching.value = false
  }
}

// 选择搜索结果
const selectSearchResult = (img: any) => {
  pullImageName.value = img.name + ':latest'
  pullTab.value = 'direct'
  // 直接开始拉取
  handlePullImage()
}

// 从私有仓库拉取
const pullFromRegistry = async () => {
  const imageName = registryImageName.value.trim()
  if (!imageName || !selectedRegistryId.value) return

  manualPulling.value = true
  progressMap.value = {}

  try {
    const connectionId = await startSignalR()
    await registryApi.pullImage({
      registryId: selectedRegistryId.value,
      image: imageName,
      connectionId
    })

    await imagesStore.fetchImages()

    // 获取注册表信息用于构建完整镜像名
    const registry = registries.value.find(r => r.id === selectedRegistryId.value)
    const fullName = registry ? `${registry.url}/${imageName}` : imageName
    form.image = fullName
    showPullDialog.value = false
    registryImageName.value = ''
    selectedRegistryId.value = ''

    ElMessage.success(`镜像 ${fullName} 拉取成功`)
    await prefetchImageConfig(fullName)
  } catch (error: any) {
    console.error('从私有仓库拉取失败:', error)
    const errorMsg = error.response?.data?.message || error.message || '拉取失败'
    ElMessage.error(errorMsg)
  } finally {
    manualPulling.value = false
    progressMap.value = {}
    if (hubConnection) {
      hubConnection.stop()
      hubConnection = null
    }
  }
}

// 加载注册表列表
const loadRegistries = async () => {
  try {
    const res = await registryApi.getRegistries()
    registries.value = res.data || []
  } catch (e) {
    console.error('加载注册表列表失败', e)
  }
}

// 监听弹窗打开，加载注册表列表
watch(showPullDialog, (val) => {
  if (val) {
    loadRegistries()
    searchKeyword.value = ''
    searchResults.value = []
    searched.value = false
  }
})

const handleTemplateSelect = (tpl: any) => {
  selectedTemplate.value = tpl
  
  // 基本信息
  form.image = tpl.image
  if (tpl.command && tpl.command.length > 0) {
    commandStr.value = tpl.command.join(' ')
  }
  if (tpl.workingDir) form.workingDir = tpl.workingDir
  if (tpl.user) form.user = tpl.user
  
  // 重启策略
  if (tpl.restartPolicy) {
    form.restartPolicy = {
      name: tpl.restartPolicy.name || 'no',
      maximumRetryCount: tpl.restartPolicy.maximumRetryCount || 0
    }
  }
  
  // 网络模式
  if (tpl.networkMode) {
    disableNetwork.value = tpl.networkMode === 'none'
    useHostNetwork.value = tpl.networkMode === 'host'
  }
  
  // 端口映射
  if (tpl.ports && tpl.ports.length > 0) {
    formArrays.ports = tpl.ports.map((p: any) => ({
      hostIp: p.hostIp || '',
      hostPort: p.hostPort || null,
      containerPort: p.containerPort,
      protocol: p.protocol || 'tcp'
    }))
  }
  
  // 卷映射
  if (tpl.volumes && tpl.volumes.length > 0) {
    formArrays.volumes = tpl.volumes.map((v: any) => ({
      hostPath: v.hostPath || '',
      containerPath: v.containerPath,
      readOnly: v.readOnly || false
    }))
  }
  
  // 环境变量
  if (tpl.environment && Object.keys(tpl.environment).length > 0) {
    formArrays.environment = Object.entries(tpl.environment).map(([key, value]) => ({
      key,
      value: String(value)
    }))
  }
  
  // 标签
  if (tpl.labels && Object.keys(tpl.labels).length > 0) {
    formArrays.labels = Object.entries(tpl.labels).map(([key, value]) => ({
      key,
      value: String(value)
    }))
  }
  
  // 网络配置
  if (tpl.networks && tpl.networks.length > 0) {
    form.network.networkIds = tpl.networks.map((n: any) => n.networkId)
    tpl.networks.forEach((n: any) => {
      if (n.networkId) {
        form.network.configs[n.networkId] = {
          aliases: n.aliases ? n.aliases.join(',') : '',
          ipAddress: n.ipAddress || ''
        }
      }
    })
  }
  
  ElMessage.success(`已应用模板: ${tpl.name}`)
}

const prefetchImageConfig = async (imageName: string) => {
  try {
    // 匹配逻辑需要跟 localImageOptions 保持一致
    const imageInfo = imagesStore.images.find(i => 
      `${i.repository}:${i.tag || 'latest'}` === imageName || 
      i.repository === imageName ||
      i.id === imageName
    )
    
    if (!imageInfo) return
    
    const response = await imageApi.getImage(imageInfo.id)
    const detail = response.data || response
    
    if (!detail) return
    
    // 缓存镜像详情，供后续使用（如检测端口按钮）
    cachedImageDetail.value = detail
    
    // 清空之前的自动填充数据，重新填充新镜像的配置
    // 只清空由自动填充添加的数据，保留用户手动添加的
    formArrays.volumes = formArrays.volumes.filter(v => v.hostPath || v.volumeName)
    formArrays.environment = formArrays.environment.filter(e => e.key && e.value !== undefined && e.value !== '')
    
    // 自动填充卷挂载（镜像定义的容器路径）
    if (detail.volumes && detail.volumes.length > 0) {
      detail.volumes.forEach((volPath: string) => {
        // 检查是否已存在相同的容器路径
        if (!formArrays.volumes.some(v => v.containerPath === volPath)) {
          formArrays.volumes.push({ type: 'volume', hostPath: '', volumeName: '', containerPath: volPath, readOnly: false })
        }
      })
    }
    
    // 自动填充环境变量（镜像定义的默认环境变量）
    if (detail.env && detail.env.length > 0) {
      detail.env.forEach((envStr: string) => {
        const [key, ...valueParts] = envStr.split('=')
        if (key && !formArrays.environment.some(e => e.key === key)) {
          formArrays.environment.push({ key, value: valueParts.join('=') })
        }
      })
    }
    
    // 更新工作目录、用户、入口点和命令
    form.workingDir = detail.workingDir || ''
    form.user = detail.user || ''
    if (detail.entrypoint?.length > 0) {
      form.entrypoint = detail.entrypoint.join(' ')
      originalEntrypoint.value = detail.entrypoint
    } else {
      form.entrypoint = ''
      originalEntrypoint.value = []
    }
    if (detail.cmd?.length > 0) {
      commandStr.value = detail.cmd.join(' ')
      originalCmd.value = detail.cmd
    } else {
      commandStr.value = ''
      originalCmd.value = []
    }
  } catch (error) {
    console.warn('获取镜像配置失败:', error)
  }
}

// 自动检测镜像端口
const autoDetectPorts = async () => {
  if (!form.image) {
    ElMessage.warning('请先选择镜像')
    return
  }
  
  try {
    // 优先使用缓存的镜像详情
    let detail = cachedImageDetail.value
    
    // 如果没有缓存，则从 API 获取
    if (!detail) {
      // 匹配逻辑需要跟 localImageOptions 保持一致
      const imageInfo = imagesStore.images.find(i => 
        `${i.repository}:${i.tag || 'latest'}` === form.image || 
        i.repository === form.image ||
        i.id === form.image
      )
      
      if (!imageInfo) {
        ElMessage.warning('找不到镜像信息')
        return
      }
      
      const response = await imageApi.getImage(imageInfo.id)
      detail = response.data || response
      cachedImageDetail.value = detail
    }
    
    if (!detail || !detail.exposedPorts || detail.exposedPorts.length === 0) {
      ElMessage.info('该镜像没有暴露端口')
      return
    }
    
    // 获取已添加的容器端口
    const existingPorts = new Set(formArrays.ports.map(p => `${p.containerPort}/${p.protocol}`))
    let addedCount = 0
    
    detail.exposedPorts.forEach((portStr: string) => {
      const match = portStr.match(/^(\d+)\/?(tcp|udp)?$/i)
      if (match) {
        const port = match[1]
        const protocol = (match[2] || 'tcp').toLowerCase()
        const portKey = `${port}/${protocol}`
        
        // 只添加不存在的端口
        if (!existingPorts.has(portKey)) {
          formArrays.ports.push({
            hostIp: '',
            hostPort: port,
            containerPort: port,
            protocol: protocol
          })
          addedCount++
        }
      }
    })
    
    if (addedCount > 0) {
      ElMessage.success(`已添加 ${addedCount} 个端口映射`)
    } else {
      ElMessage.info('所有端口已存在，无需添加')
    }
  } catch (error) {
    console.error('检测端口失败:', error)
    ElMessage.error('检测端口失败')
  }
}

// 自动检测镜像卷
const autoDetectVolumes = async () => {
  if (!form.image) {
    ElMessage.warning('请先选择镜像')
    return
  }
  
  try {
    // 优先使用缓存的镜像详情
    let detail = cachedImageDetail.value
    
    // 如果没有缓存，则从 API 获取
    if (!detail) {
      const imageInfo = imagesStore.images.find(i => 
        `${i.repository}:${i.tag || 'latest'}` === form.image || 
        i.repository === form.image ||
        i.id === form.image
      )
      
      if (!imageInfo) {
        ElMessage.warning('找不到镜像信息')
        return
      }
      
      const response = await imageApi.getImage(imageInfo.id)
      detail = response.data || response
      cachedImageDetail.value = detail
    }
    
    if (!detail || !detail.volumes || detail.volumes.length === 0) {
      ElMessage.info('该镜像没有定义卷')
      return
    }
    
    // 获取已添加的容器路径
    const existingPaths = new Set(formArrays.volumes.map(v => v.containerPath))
    let addedCount = 0
    
    detail.volumes.forEach((volPath: string) => {
      // 只添加不存在的卷
      if (!existingPaths.has(volPath)) {
        formArrays.volumes.push({
          type: 'volume',
          hostPath: '',
          volumeName: '',
          containerPath: volPath,
          readOnly: false
        })
        addedCount++
      }
    })
    
    if (addedCount > 0) {
      ElMessage.success(`已添加 ${addedCount} 个卷映射`)
    } else {
      ElMessage.info('所有卷已存在，无需添加')
    }
  } catch (error) {
    console.error('检测卷失败:', error)
    ElMessage.error('检测卷失败')
  }
}

onBeforeUnmount(() => { if (hubConnection) hubConnection.stop() })
</script>

<style scoped>
.create-container-dialog :deep(.el-dialog__body) {
  padding: 12px 20px;
  max-height: 70vh;
  overflow-y: auto;
}

.tab-label {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.tab-content {
  padding: 16px 8px;
  min-height: 350px;
}

.array-table {
  width: 100%;
}

.sub-section {
  margin-bottom: 16px;
}

.sub-section:last-child {
  margin-bottom: 0;
}

.network-selection {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.default-network-info {
  display: flex;
  align-items: center;
  gap: 8px;
}

.default-network-tag {
  display: flex;
  align-items: center;
  gap: 8px;
}

.default-network-tag .el-tag {
  display: flex;
  align-items: center;
  gap: 4px;
}

.network-hint {
  font-size: 12px;
  color: var(--text-secondary);
}

.network-options {
  display: flex;
  gap: 24px;
}

.network-option-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.network-config-section {
  margin-top: 16px;
  padding: 16px;
  background: var(--bg-subtle, #f5f7fa);
  border-radius: 8px;
  border: 1px solid var(--border-color-light, #e4e7ed);
}

.network-config-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
  font-weight: 500;
  color: var(--text-secondary, #606266);
}

.network-config-header .hint {
  font-weight: normal;
  font-size: 12px;
  color: var(--text-muted, #909399);
}

.sub-title {
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 10px;
  font-weight: 500;
}

.inline-form-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.volume-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
  padding: 8px;
  background: var(--bg-subtle);
  border-radius: 6px;
}

.separator {
  color: var(--text-muted);
  font-weight: 500;
  flex-shrink: 0;
}

.switch-group {
  display: flex;
  flex-wrap: wrap;
  gap: 24px;
}

.dialog-footer {
  display: flex;
  align-items: center;
  width: 100%;
}

.footer-left {
  display: flex;
  gap: 12px;
  margin-right: auto;
}

.footer-spacer {
  display: none;
}

.footer-right {
  display: flex;
  gap: 12px;
}

.template-preview-info {
  margin-top: 16px;
}

.dialog-footer-buttons {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

/* 镜像选择器 */
.image-select-wrapper {
  display: flex;
  gap: 8px;
  width: 100%;
}

.image-select-wrapper .el-select {
  flex: 1;
}

.pull-trigger-btn {
  flex-shrink: 0;
}

.image-option-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.image-option-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.image-option-size {
  font-size: 12px;
  color: var(--text-muted);
  margin-left: 12px;
  flex-shrink: 0;
}

.image-select-empty {
  padding: 12px;
  text-align: center;
  color: var(--text-muted);
  font-size: 13px;
}

/* 镜像拉取对话框样式 */
.pull-image-dialog :deep(.el-tabs__content) {
  padding: 16px 0;
}

.pull-direct-section,
.pull-search-section,
.pull-registry-section {
  min-height: 200px;
}

.pull-hint {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 12px;
  padding: 10px 12px;
  background: var(--bg-subtle);
  border-radius: 6px;
  font-size: 12px;
  color: var(--text-secondary);
}

.pull-progress-section {
  margin-top: 16px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 12px;
  background: var(--bg-subtle);
}

.search-row {
  margin-bottom: 16px;
}

.search-results {
  max-height: 300px;
  overflow-y: auto;
  border: 1px solid var(--border-color);
  border-radius: 8px;
}

.search-result-item {
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-color);
  cursor: pointer;
  transition: background 0.2s;
}

.search-result-item:last-child {
  border-bottom: none;
}

.search-result-item:hover {
  background: var(--bg-subtle);
}

.search-result-item .result-name {
  font-weight: 500;
  color: var(--text-main);
  margin-bottom: 4px;
}

.search-result-item .result-desc {
  font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 6px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.search-result-item .result-meta {
  display: flex;
  gap: 8px;
}

.field-hint {
  font-size: 12px;
  color: var(--text-secondary);
  margin-top: 4px;
}
</style>
