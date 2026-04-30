export default {
  title: '存储卷管理',
  subtitle: '管理持久化数据存储',
  create: '创建卷',
  prune: '清理未使用',
  mountpoint: '挂载点',
  driver: '驱动',
  // 卷详情
  volumeName: '卷名称',
  volumeDriver: '驱动类型',
  volumePath: '存储路径',
  usageCount: '使用次数',
  labels: '标签',
  options: '选项',
  // 创建卷
  createTitle: '创建卷',
  volumeNamePlaceholder: '请输入卷名称',
  volumeDriverPlaceholder: '默认 local',
  // 卷操作
  removeVolume: '删除卷',
  inspectVolume: '查看详情',
  backupVolume: '备份卷',
  restoreVolume: '恢复卷',
  duplicateVolume: '复制卷',
  // 清理
  pruneTitle: '清理未使用卷',
  pruneDescription: '将删除所有未被容器使用的存储卷',
  pruneWarning: '注意：这将永久删除数据',
  pruneSuccess: '清理完成',
  pruneCount: '已删除 {count} 个卷，释放 {size}',
  // 新增翻译
  searchPlaceholder: '搜索存储卷...',
  volumesCount: '存储卷',
  pruneUnused: '清理未使用',
  usage: '使用',
  detailTitle: '卷详情',
  loadDetailFailed: '获取卷详情失败',
  deleteConfirm: '确认删除存储卷 {name}?',
  pruneConfirm: '确定清理所有未使用的存储卷?',
  pruneFailed: '清理失败',
  createForm: {
    namePlaceholder: '留空则自动生成随机名称'
  },
  usageInfo: '使用情况',
  containers: '个容器',
  noContainersUsing: '暂无容器使用此卷',
  // 日期格式
  date: {
    today: '今天',
    yesterday: '昨天',
    daysAgo: '{count} 天前'
  },
  // 空状态
  empty: {
    noVolumes: '暂无存储卷',
    createFirst: '创建存储卷来持久化您的容器数据'
  },
  // 文件管理器 - 卷特定
  fileManager: {
    info: '存储卷: {name}'
  },
  browseFiles: '浏览文件',
  downloadVolume: '打包下载',
  // 从归档恢复
  restoreFromArchive: '从归档恢复',
  archiveFile: '归档文件',
  selectArchive: '选择归档文件',
  archiveFileTip: '支持 .tar.gz 或 .tgz 格式的归档文件',
  restoreVolumeNamePlaceholder: '留空则自动生成随机名称',
  archiveFileRequired: '请选择归档文件',
  restoreSuccess: '恢复成功',
  restoreFailed: '恢复卷失败',
  createSuccess: '创建成功',
  createFailed: '创建失败',
  // 清理卷对话框
  cleanupVolumes: {
    dialogTitle: '清理未使用卷',
    cleanupType: '清理类型',
    unusedVolumes: '未使用的卷',
    danglingVolumes: '匿名卷',
    allVolumes: '所有卷',
    selectNode: '请选择节点',
    forceDelete: '强制删除',
    forceEnable: '启用',
    forceDisable: '禁用',
    warningTitle: '警告',
    warningMessageUnused: '将删除所有未被容器使用的存储卷，这可能包括一些重要数据。',
    warningMessageDangling: '将删除所有匿名卷（没有名称的卷），这些卷通常是由容器自动创建的。',
    warningMessageAll: '将删除所有存储卷，包括正在使用的卷！此操作不可逆。',
    validationNodeRequired: '请选择节点',
    confirmCleanupTitle: '确认清理',
    confirmCleanupMessage: '确定要清理{type}吗？此操作不可撤销。',
    startCleanup: '开始清理',
    progressTitle: '清理进度',
    progressCleaning: '正在清理...',
    progressPercent: '清理进度: {percent}%',
    progressComplete: '清理完成',
    progressFailed: '清理失败: {error}',
    resultDeleted: '已删除卷',
    resultSpaceReclaimed: '释放空间',
    resultError: '错误信息',
    cleanupSuccess: '清理完成',
    noVolumesToClean: '没有可清理的卷',
    cleanupFailed: '清理失败',
    loadNodesFailed: '加载节点列表失败'
  }
}
