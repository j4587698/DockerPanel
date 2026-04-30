using System.Collections.Concurrent;

namespace DockerPanel.API.Services;

/// <summary>
/// 多语言本地化服务接口
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// 获取当前请求的语言
    /// </summary>
    string GetCurrentLanguage();
    
    /// <summary>
    /// 设置当前请求的语言
    /// </summary>
    void SetCurrentLanguage(string language);
    
    /// <summary>
    /// 获取本地化的错误消息
    /// </summary>
    string GetErrorMessage(string code, params object[] args);
    
    /// <summary>
    /// 获取本地化的消息
    /// </summary>
    string GetMessage(string key, string? defaultValue = null);
}

/// <summary>
/// 多语言本地化服务实现
/// </summary>
public class LocalizationService : ILocalizationService
{
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _translations = new();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string DefaultLanguage = "zh-CN";
    private const string LanguageKey = "CurrentLanguage";

    public LocalizationService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        InitializeTranslations();
    }

    private void InitializeTranslations()
    {
        // 中文翻译
        var zhCn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 通用错误
            ["error.unauthorized"] = "未授权，请先登录",
            ["error.forbidden"] = "权限不足",
            ["error.notFound"] = "请求的资源不存在",
            ["error.invalidRequest"] = "请求参数无效",
            ["error.invalidParams"] = "参数错误",
            ["error.invalidOperation"] = "操作无效",
            ["error.serverError"] = "服务器内部错误",
            ["error.timeout"] = "请求超时",
            ["error.networkError"] = "网络连接失败",
            
            // 容器相关
            ["container.listFailed"] = "获取容器列表失败",
            ["container.notFound"] = "容器未找到",
            ["container.detailFailed"] = "获取容器详情失败",
            ["container.logsFailed"] = "获取容器日志失败",
            ["container.statsFailed"] = "获取容器统计信息失败",
            ["container.createFailed"] = "创建容器失败",
            ["container.startSuccess"] = "容器 {0} 启动成功",
            ["container.startFailed"] = "启动容器失败",
            ["container.stopSuccess"] = "容器 {0} 停止成功",
            ["container.stopFailed"] = "停止容器失败",
            ["container.restartSuccess"] = "容器 {0} 重启成功",
            ["container.restartFailed"] = "重启容器失败",
            ["container.deleteSuccess"] = "容器 {0} 删除成功",
            ["container.deleteFailed"] = "删除容器失败",
            ["container.runningCannotDelete"] = "容器正在运行，无法删除",
            ["container.pleaseStopFirst"] = "请先停止容器或使用强制删除",
            ["container.renameSuccess"] = "容器 {0} 重命名成功",
            ["container.renameFailed"] = "重命名容器失败",
            ["container.configUpdateSuccess"] = "容器 {0} 配置更新成功",
            ["container.configUpdateFailed"] = "更新容器配置失败",
            ["container.exportFailed"] = "导出容器失败",
            ["container.rebuildFailed"] = "重建容器失败",
            ["container.fileListFailed"] = "获取文件列表失败",
            ["container.mountInfoFailed"] = "获取挂载点信息失败",
            ["container.downloadFileFailed"] = "下载文件失败",
            ["container.uploadSuccess"] = "文件上传成功",
            ["container.uploadFailed"] = "上传文件失败",
            ["container.createFolderSuccess"] = "文件夹创建成功",
            ["container.createFolderFailed"] = "创建文件夹失败",
            ["container.renameFileSuccess"] = "重命名成功",
            ["container.renameFileFailed"] = "重命名失败",
            ["container.deleteFileSuccess"] = "删除成功",
            ["container.deleteFileFailed"] = "删除失败",
            ["container.getFileContentFailed"] = "获取文件内容失败",
            ["container.saveFileSuccess"] = "文件保存成功",
            ["container.saveFileFailed"] = "写入文件失败",
            ["container.chmodSuccess"] = "权限修改成功",
            ["container.chmodFailed"] = "修改权限失败",
            ["container.paramValidationFailed"] = "参数验证失败",
            ["container.requestBodyEmpty"] = "请求体为空",
            
            // 镜像相关
            ["image.notFound"] = "镜像不存在",
            ["image.pullStarted"] = "开始拉取镜像",
            ["image.deleteSuccess"] = "镜像删除成功",
            ["image.pushStarted"] = "推送任务已启动",
            ["image.importSuccess"] = "镜像导入成功",
            ["image.listFailed"] = "获取镜像列表失败",
            ["image.detailFailed"] = "获取镜像详情失败",
            ["image.testEndpointOk"] = "测试端点正常工作",
            
            // 网络相关
            ["network.listFailed"] = "获取网络列表失败",
            ["network.notFound"] = "网络不存在",
            ["network.detailFailed"] = "获取网络详情失败",
            ["network.createFailed"] = "创建网络失败",
            ["network.deleteSuccess"] = "网络删除成功",
            ["network.deleteFailed"] = "删除网络失败",
            ["network.connectSuccess"] = "容器连接网络成功",
            ["network.connectFailed"] = "容器连接网络失败",
            ["network.disconnectSuccess"] = "容器断开网络连接成功",
            ["network.disconnectFailed"] = "容器断开网络连接失败",
            ["network.containersFailed"] = "获取网络容器列表失败",
            ["network.pruneFailed"] = "清理网络失败",
            ["network.statisticsFailed"] = "获取网络统计信息失败",
            ["network.existsCheckFailed"] = "检查网络是否存在失败",
            ["network.ipamFailed"] = "获取网络IPAM信息失败",
            ["network.updateFailed"] = "更新网络配置失败",
            
            // 卷相关
            ["volume.listFailed"] = "获取卷列表失败",
            ["volume.notFound"] = "卷不存在",
            ["volume.detailFailed"] = "获取卷详情失败",
            ["volume.createFailed"] = "创建卷失败",
            ["volume.noArchiveSelected"] = "未选择归档文件",
            ["volume.restoreFailed"] = "恢复卷失败",
            ["volume.deleteSuccess"] = "卷删除成功",
            ["volume.deleteFailed"] = "删除卷失败",
            ["volume.updateFailed"] = "更新卷配置失败",
            ["volume.pruneFailed"] = "清理卷失败",
            ["volume.statsFailed"] = "获取卷统计信息失败",
            ["volume.existsCheckFailed"] = "检查卷是否存在失败",
            ["volume.usageFailed"] = "获取卷使用情况失败",
            ["volume.backupFailed"] = "备份卷失败",
            ["volume.backupListFailed"] = "获取卷备份列表失败",
            ["volume.backupDeleteSuccess"] = "卷备份删除成功",
            ["volume.backupNotFound"] = "卷备份不存在",
            ["volume.backupDeleteFailed"] = "删除卷备份失败",
            ["volume.fileListFailed"] = "获取文件列表失败",
            ["volume.downloadFileFailed"] = "下载文件失败",
            ["volume.noFileSelected"] = "未选择文件",
            ["volume.uploadSuccess"] = "上传成功",
            ["volume.uploadFailed"] = "上传文件失败",
            ["volume.createFolderSuccess"] = "创建成功",
            ["volume.createFolderFailed"] = "创建文件夹失败",
            ["volume.renameSuccess"] = "重命名成功",
            ["volume.renameFailed"] = "重命名文件失败",
            ["volume.deleteFileSuccess"] = "删除成功",
            ["volume.deleteFileFailed"] = "删除文件失败",
            ["volume.getFileContentFailed"] = "获取文件内容失败",
            ["volume.saveSuccess"] = "保存成功",
            ["volume.saveFileFailed"] = "保存文件内容失败",
            
            // 证书相关
            ["certificate.notFound"] = "证书不存在",
            ["certificate.notFoundConfirm"] = "证书未找到，请确保证书已成功申请",
            ["certificate.dataEmpty"] = "证书数据为空",
            ["certificate.orderNotFound"] = "证书订单不存在",
            ["certificate.cancelled"] = "证书申请已取消",
            ["certificate.retrySubmitted"] = "重试请求已提交，证书申请将在后台进行",
            ["certificate.revokeSuccess"] = "证书撤销成功",
            ["certificate.revokeFailed"] = "证书不存在或撤销失败",
            ["certificate.autoRenewEnabled"] = "已启用自动续期",
            ["certificate.autoRenewDisabled"] = "已禁用自动续期",
            ["certificate.accountNotFound"] = "账户不存在",
            ["certificate.accountDeleted"] = "账户删除成功",
            ["certificate.domainsEmpty"] = "域名列表不能为空",
            ["certificate.orderNotFound"] = "订单不存在",
            ["certificate.canOnlyRetryFailed"] = "只能重试失败或待处理的证书申请",
            
            // 节点相关
            ["node.listFailed"] = "获取节点列表失败",
            ["node.notFound"] = "节点不存在",
            ["node.getFailed"] = "获取节点失败",
            ["node.addFailed"] = "添加节点失败",
            ["node.updateFailed"] = "更新节点失败",
            ["node.deleteFailed"] = "删除节点失败",
            ["node.testConnectionFailed"] = "测试节点连接失败",
            ["node.statsFailed"] = "获取节点统计信息失败",
            ["node.detailFailed"] = "获取节点详细信息失败",
            ["node.healthFailed"] = "获取节点健康状态失败",
            ["node.batchOperationFailed"] = "批量节点操作失败",
            ["node.unsupportedOperation"] = "不支持的操作",
            
            // 代理相关
            ["proxy.configFailed"] = "获取代理配置失败",
            ["proxy.reloadSuccess"] = "配置重新加载成功",
            ["proxy.reloadFailed"] = "重新加载配置失败",
            ["proxy.routeAddSuccess"] = "路由添加成功",
            ["proxy.routeAddFailed"] = "路由添加失败",
            ["proxy.routeUpdateSuccess"] = "路由更新成功",
            ["proxy.routeUpdateFailed"] = "路由更新失败",
            ["proxy.routeDeleteSuccess"] = "路由删除成功",
            ["proxy.routeNotFound"] = "路由不存在",
            ["proxy.routeIdMismatch"] = "路由ID不匹配",
            ["proxy.clusterAddSuccess"] = "集群添加成功",
            ["proxy.clusterAddFailed"] = "集群添加失败",
            ["proxy.clusterUpdateSuccess"] = "集群更新成功",
            ["proxy.clusterUpdateFailed"] = "集群更新失败",
            ["proxy.clusterDeleteSuccess"] = "集群删除成功",
            ["proxy.clusterNotFound"] = "集群不存在",
            ["proxy.domainMappingAddSuccess"] = "域名映射添加成功",
            ["proxy.domainMappingUpdateSuccess"] = "域名映射更新成功",
            ["proxy.domainMappingDeleteSuccess"] = "域名映射删除成功",
            ["proxy.domainMappingNotFound"] = "域名映射不存在",
            ["proxy.certificateBindingUpdated"] = "证书绑定已更新",
            ["proxy.noAcmeAccount"] = "未找到可用的 ACME 账户，无法自动申请证书。请先在「设置 -> 证书管理」中配置 ACME 账户。",
            ["proxy.clusterIdMismatch"] = "集群ID不匹配",
            ["proxy.clusterDeleteFailed"] = "删除集群失败",
            ["proxy.getMappingsFailed"] = "获取域名映射失败",
            ["proxy.domainMappingDeleteSuccess"] = "域名映射删除成功",
            ["proxy.domainMappingDeleteFailed"] = "删除域名映射失败",
            ["proxy.certificateBindingUpdated"] = "证书绑定已更新",
            ["proxy.certificateBindingUpdateFailed"] = "更新证书绑定失败",
            ["proxy.statusFailed"] = "获取代理状态失败",
            ["proxy.routeDeleteFailed"] = "删除路由失败",
            
            // Compose 相关
            ["compose.notFound"] = "Compose文件不存在",
            ["compose.deleteSuccess"] = "Compose文件删除成功",
            ["compose.projectNotFound"] = "Compose项目不存在",
            ["compose.listFailed"] = "获取Compose文件列表失败",
            ["compose.detailFailed"] = "获取Compose文件详情失败",
            ["compose.createFailed"] = "创建Compose文件失败",
            ["compose.updateFailed"] = "更新Compose文件失败",
            ["compose.deleteFailed"] = "删除Compose文件失败",
            ["compose.validateFailed"] = "验证Compose文件失败",
            ["compose.contentEmpty"] = "内容不能为空",
            ["compose.parseFailed"] = "解析Compose内容失败",
            ["compose.deployFailed"] = "部署Compose项目失败",
            ["compose.stopFailed"] = "停止Compose项目失败",
            ["compose.startFailed"] = "启动Compose项目失败",
            ["compose.restartFailed"] = "重启Compose项目失败",
            ["compose.removeFailed"] = "删除Compose项目失败",
            ["compose.projectStatusFailed"] = "获取Compose项目状态失败",
            ["compose.projectListFailed"] = "获取Compose项目列表失败",
            ["compose.logsFailed"] = "获取Compose日志失败",
            ["compose.projectStatsNotFound"] = "Compose项目统计信息不存在",
            ["compose.projectStatsFailed"] = "获取Compose项目统计信息失败",
            ["compose.exportFailed"] = "导出Compose文件失败",
            ["compose.importFailed"] = "导入Compose文件失败",
            ["compose.templateListFailed"] = "获取Compose模板列表失败",
            ["compose.createFromTemplateFailed"] = "根据模板创建Compose文件失败",
            ["compose.batchOperationFailed"] = "批量操作Compose文件失败",
            ["compose.historyFailed"] = "获取Compose文件历史版本失败",
            ["compose.restoreFailed"] = "恢复Compose文件版本失败",
            ["compose.checkDependenciesFailed"] = "检查Compose文件依赖失败",
            
            // 仓库相关
            ["registry.notFound"] = "仓库不存在",
            ["registry.deleteSuccess"] = "仓库删除成功",
            ["registry.setDefaultSuccess"] = "默认仓库设置成功",
            ["registry.loginSuccess"] = "登录成功",
            ["registry.loginFailed"] = "登录失败",
            ["registry.logoutSuccess"] = "登出成功",
            ["registry.logoutFailed"] = "登出失败",
            ["registry.listFailed"] = "获取镜像仓库列表失败",
            ["registry.mirrorListFailed"] = "获取镜像加速器列表失败",
            ["registry.privateListFailed"] = "获取私有仓库列表失败",
            ["registry.detailFailed"] = "获取镜像仓库详情失败",
            ["registry.createFailed"] = "创建镜像仓库失败",
            ["registry.updateFailed"] = "更新镜像仓库失败",
            ["registry.deleteFailed"] = "删除镜像仓库失败",
            ["registry.testFailed"] = "测试仓库连接失败",
            ["registry.testConfigFailed"] = "测试仓库配置连接失败",
            ["registry.searchFailed"] = "搜索失败",
            ["registry.setDefaultFailed"] = "设置默认仓库失败",
            ["registry.validateAuthFailed"] = "验证仓库认证失败",
            ["registry.syncFailed"] = "同步仓库镜像信息失败",
            ["registry.statisticsFailed"] = "获取仓库统计数据失败",
            
            // SSH 相关
            ["ssh.testConnectionFailed"] = "测试SSH连接失败",
            ["ssh.generateKeyPairFailed"] = "生成SSH密钥对失败",
            ["ssh.validateKeyFailed"] = "验证SSH私钥失败",
            ["ssh.commandFailed"] = "SSH命令执行失败",
            ["ssh.uploadFailed"] = "SSH文件上传失败",
            ["ssh.downloadFailed"] = "SSH文件下载失败",
            ["ssh.batchTestFailed"] = "批量SSH连接测试失败",
            ["ssh.getConfigListFailed"] = "获取连接配置列表失败",
            ["ssh.configNotFound"] = "连接配置不存在",
            ["ssh.getConfigFailed"] = "获取连接配置失败",
            ["ssh.createConfigFailed"] = "创建连接配置失败",
            ["ssh.updateConfigFailed"] = "更新连接配置失败",
            ["ssh.deleteConfigFailed"] = "删除连接配置失败",
            ["ssh.getKeyPairListFailed"] = "获取密钥对列表失败",
            ["ssh.importKeyPairFailed"] = "导入密钥对失败",
            ["ssh.keyPairNotFound"] = "密钥对不存在",
            ["ssh.deleteKeyPairFailed"] = "删除密钥对失败",
            ["ssh.getSessionListFailed"] = "获取会话列表失败",
            ["ssh.sessionNotFound"] = "会话不存在",
            ["ssh.terminateSessionFailed"] = "终止会话失败",
            ["ssh.reconnectSessionFailed"] = "重连会话失败",
            ["ssh.listDirectoryFailed"] = "列出目录失败",
            ["ssh.deleteFileFailed"] = "删除文件失败",
            ["ssh.getStatsFailed"] = "获取统计信息失败",
            ["ssh.getLogsFailed"] = "获取操作日志失败",
            ["ssh.getSettingsFailed"] = "获取设置失败",
            ["ssh.updateSettingsFailed"] = "更新设置失败",
            
            // 设置相关
            ["settings.updateSuccess"] = "系统设置更新成功",
            ["settings.resetSuccess"] = "系统设置已重置为默认值",
            ["settings.importSuccess"] = "系统设置导入成功",
            
            // 任务相关
            ["task.deleted"] = "任务已删除",
            ["task.cleared"] = "已清理完成的任务",
            
            // 模板相关
            ["template.deleteSuccess"] = "删除成功",
            
            // 系统相关
            ["system.dockerDisconnected"] = "Docker 守护进程未启动或无法访问",
            
            // 挑战验证相关
            ["challenge.http01ConfigFailed"] = "配置HTTP-01挑战失败",
            ["challenge.http01ValidateFailed"] = "验证HTTP-01挑战失败",
            ["challenge.dns01ConfigFailed"] = "配置DNS-01挑战失败",
            ["challenge.dns01ValidateFailed"] = "验证DNS-01挑战失败",
            ["challenge.tlsAlpn01ConfigFailed"] = "配置TLS-ALPN-01挑战失败",
            ["challenge.tlsAlpn01ValidateFailed"] = "验证TLS-ALPN-01挑战失败",
            ["challenge.cleanupFailed"] = "清理挑战失败",
            ["challenge.getStatusFailed"] = "获取挑战状态失败",
            ["challenge.dnsProvidersFailed"] = "获取DNS提供商列表失败",
            ["challenge.dnsProviderTestFailed"] = "测试DNS提供商连接失败",
            ["challenge.autoConfigFailed"] = "自动配置挑战失败",
            ["challenge.batchCleanupFailed"] = "批量清理挑战失败",
            ["challenge.statsFailed"] = "获取挑战验证统计失败",
            
            // ACME 相关
            ["acme.statsFailed"] = "获取ACME统计信息失败",
            ["acme.providersFailed"] = "获取ACME提供商列表失败",
            ["acme.testConnectionFailed"] = "测试连接失败",
            ["acme.accountsFailed"] = "获取账户列表失败",
            ["acme.accountCreateFailed"] = "创建账户失败",
            ["acme.getAccountFailed"] = "获取账户失败",
            ["acme.accountDeleteFailed"] = "删除账户失败",
            ["acme.getOrderFailed"] = "获取订单失败",
            ["acme.orderCreateFailed"] = "证书申请失败：无法创建订单",
            ["acme.applyCertificateFailed"] = "申请证书失败",
            ["acme.certificatesFailed"] = "获取证书列表失败",
            ["acme.ordersFailed"] = "获取订单列表失败",
            ["acme.orderCancelFailed"] = "取消申请失败",
            ["acme.orderNotFound"] = "找不到指定的证书订单",
            ["acme.challengeCompleteFailed"] = "完成挑战验证失败",
            ["acme.challengeStatusFailed"] = "查询挑战状态失败",
            ["acme.downloadFailed"] = "下载证书失败",
            ["acme.pendingChallengesFailed"] = "获取待处理挑战失败",
            ["acme.renewFailed"] = "续期证书失败",
            ["acme.renewSuccess"] = "续期成功",
            ["acme.autoRenewEnableFailed"] = "启用自动续期失败",
            ["acme.autoRenewDisableFailed"] = "禁用自动续期失败",
            ["acme.retryFailed"] = "重试证书申请失败",
            ["acme.logsFailed"] = "获取操作日志失败",
            ["acme.expiryCheckFailed"] = "检查证书到期时间失败",
            ["acme.autoRenewRunFailed"] = "自动续期失败",
            ["acme.fixStatusFailed"] = "修复证书状态失败",
            ["acme.statusFixed"] = "成功修复了 {0} 个证书状态",
            ["acme.domainValidateFailed"] = "域名验证失败",
            ["acme.csrGenerateFailed"] = "生成CSR失败",
            ["acme.certificateValidateFailed"] = "验证证书失败",
            ["acme.keyInfoFailed"] = "获取密钥信息失败",
            ["acme.keyGenerateFailed"] = "生成密钥对失败",
            ["acme.keyExportFailed"] = "导出密钥失败",
            ["acme.keyImportFailed"] = "导入密钥失败",
            ["acme.batchRenewFailed"] = "批量续期失败",
            ["acme.batchRenewComplete"] = "批量续期完成: {0}/{1} 成功",
            ["acme.challengeStored"] = "挑战存储成功",
            ["acme.challengeNotFound"] = "挑战文件不存在",
            ["acme.renewedCount"] = "成功续期了 {0} 个证书",
            ["acme.userCancelled"] = "用户手动取消申请",
            ["acme.pendingOrderExists"] = "已存在相同域名的pending证书申请",
            ["acme.revokeFailed"] = "撤销证书失败",
            ["acme.certificateForceDeleted"] = "证书不存在（已确认删除）",
            ["acme.forceModeSkipCheck"] = "强制模式：跳过存在检查",
            ["acme.checkCertificateExists"] = "检查证书是否存在",
            ["acme.checkUsageStatus"] = "检查证书使用状态: 无关联域名映射",
            ["acme.certificateInUse"] = "无法删除证书，正在被多个域名映射使用。请先解除域名绑定后再删除证书。",
            ["acme.certificateUsedByDomains"] = "证书正在被以下域名使用",
            ["acme.mappingCertificateUnbound"] = "已解除域名映射的证书绑定",
            ["acme.forceModeUnbindMappings"] = "强制模式：解除域名映射的证书绑定",
            ["acme.deleteFromOrdersCollection"] = "从acme_orders集合中删除证书记录",
            ["acme.deleteFromCertificatesCollection"] = "从certificates集合中删除证书记录",
            ["acme.cleanOperationHistory"] = "清理相关操作历史",
            ["acme.cleanUsageStats"] = "清理使用统计",
            ["acme.certificateDeleted"] = "证书删除成功",
            ["acme.dbDeleteFailed"] = "数据库删除操作失败",
            ["acme.deleteFailed"] = "删除操作失败",
            ["acme.orderTimeoutMarkedFailed"] = "证书申请超时，已自动标记为失败",
            
            // 通配符证书相关
            ["wildcard.notFound"] = "未找到指定的通配符证书",
            
            // 节点资源相关
            ["nodeResource.overviewFailed"] = "获取节点资源概览失败",
            ["nodeResource.nodeNotFound"] = "节点不存在",
            ["nodeResource.detailFailed"] = "获取节点资源详情失败",
            ["nodeResource.trendFailed"] = "获取节点资源趋势失败",
            ["nodeResource.clusterStatsFailed"] = "获取集群资源统计失败",
            ["nodeResource.alertsFailed"] = "获取资源告警失败",
            ["nodeResource.alertCreateFailed"] = "创建资源告警规则失败",
            ["nodeResource.alertNotFound"] = "告警规则不存在",
            ["nodeResource.alertGetFailed"] = "获取资源告警规则失败",
            ["nodeResource.alertListFailed"] = "获取资源告警规则列表失败",
            ["nodeResource.alertUpdateFailed"] = "更新资源告警规则失败",
            ["nodeResource.alertDeleteFailed"] = "删除资源告警规则失败",
            ["nodeResource.realtimeFailed"] = "获取节点实时资源使用率失败",
            ["nodeResource.dashboardFailed"] = "获取集群仪表盘数据失败",
            ["nodeResource.performanceFailed"] = "获取节点性能指标失败",
            
            // 节点分组相关
            ["nodeGroup.listFailed"] = "获取节点分组失败",
            ["nodeGroup.notFound"] = "节点分组不存在",
            ["nodeGroup.createFailed"] = "创建节点分组失败",
            ["nodeGroup.updateFailed"] = "更新节点分组失败",
            ["nodeGroup.deleteFailed"] = "删除节点分组失败",
            ["nodeGroup.addNodeFailed"] = "添加节点到分组失败",
            ["nodeGroup.addNodeSuccess"] = "节点已添加到分组",
            ["nodeGroup.removeNodeFailed"] = "从分组移除节点失败",
            ["nodeGroup.removeNodeSuccess"] = "节点已从分组移除",
            ["nodeGroup.nodesFailed"] = "获取分组节点失败",
            ["nodeGroup.batchUpdateFailed"] = "批量更新节点分组失败",
            ["nodeGroup.batchUpdateSuccess"] = "批量更新节点分组成功",
            ["nodeGroup.statsFailed"] = "获取分组统计失败",
            ["nodeGroup.tagsFailed"] = "获取标签失败",
            ["nodeGroup.tagCreateFailed"] = "创建标签失败",
            ["nodeGroup.tagNotFound"] = "标签不存在",
            ["nodeGroup.tagGetFailed"] = "获取标签失败",
            ["nodeGroup.tagUpdateFailed"] = "更新标签失败",
            ["nodeGroup.tagDeleteFailed"] = "删除标签失败",
            ["nodeGroup.addTagFailed"] = "为节点添加标签失败",
            ["nodeGroup.addTagSuccess"] = "标签已添加到节点",
            ["nodeGroup.removeTagFailed"] = "从节点移除标签失败",
            ["nodeGroup.removeTagSuccess"] = "标签已从节点移除",
            ["nodeGroup.nodeTagsFailed"] = "获取节点标签失败",
            ["nodeGroup.tagNodesFailed"] = "获取标签节点失败",
            ["nodeGroup.batchTagUpdateFailed"] = "批量更新节点标签失败",
            ["nodeGroup.batchTagUpdateSuccess"] = "批量更新节点标签成功",
            ["nodeGroup.tagStatsFailed"] = "获取标签统计失败",
            ["nodeGroup.overviewFailed"] = "获取分组标签概览失败",
            
            // 进度相关
            ["progress.createFailed"] = "创建进度跟踪失败",
            ["progress.getFailed"] = "获取进度信息失败",
            ["progress.getByCertificateFailed"] = "获取证书进度信息失败",
            ["progress.listFailed"] = "获取所有进度列表失败",
            ["progress.stepUpdateSuccess"] = "进度步骤更新成功",
            ["progress.stepUpdateFailed"] = "更新进度步骤失败",
            ["progress.completeCurrentSuccess"] = "当前步骤完成成功",
            ["progress.completeCurrentFailed"] = "完成当前步骤失败",
            ["progress.addErrorSuccess"] = "错误信息添加成功",
            ["progress.addErrorFailed"] = "添加错误信息失败",
            ["progress.addWarningSuccess"] = "警告信息添加成功",
            ["progress.addWarningFailed"] = "添加警告信息失败",
            ["progress.markCompleteSuccess"] = "进度标记完成成功",
            ["progress.markCompleteFailed"] = "标记进度完成失败",
            ["progress.markFailSuccess"] = "进度标记失败成功",
            ["progress.markFailFailed"] = "标记进度失败失败",
            ["progress.deleteSuccess"] = "进度记录删除成功",
            ["progress.deleteFailed"] = "删除进度记录失败",
            ["progress.cleanupSuccess"] = "过期进度记录清理成功",
            ["progress.cleanupFailed"] = "清理过期进度记录失败",
            
            // 自动更新相关
            ["autoUpdate.getConfigFailed"] = "获取自动升级配置失败",
            ["autoUpdate.getContainerConfigFailed"] = "获取容器自动升级配置失败",
            ["autoUpdate.setContainerConfigFailed"] = "设置容器自动升级配置失败",
            ["autoUpdate.deleteContainerConfigFailed"] = "删除容器自动升级配置失败",
            ["autoUpdate.checkFailed"] = "检查容器镜像更新失败",
            ["autoUpdate.checkAllFailed"] = "检查所有容器更新失败",
            ["autoUpdate.availableUpdatesFailed"] = "获取可用更新列表失败",
            ["autoUpdate.updateFailed"] = "更新容器失败",
            ["autoUpdate.imageTagsFailed"] = "获取镜像标签失败",
            ["autoUpdate.rollbackFailed"] = "回滚容器失败",

            // SignalR 连接消息
            ["signalr.welcome"] = "欢迎连接到 DockerPanel 实时服务",
            ["signalr.error.containerList"] = "获取容器列表失败",
            ["signalr.error.systemStats"] = "获取系统统计失败",

            // 后台任务状态
            ["task.pending"] = "等待中",
            ["task.running"] = "进行中",
            ["task.completed"] = "已完成",
            ["task.failed"] = "失败",
            ["task.cancelled"] = "已取消",

            // 镜像构建
            ["imageBuild.preparing"] = "准备构建",
            ["imageBuild.convertingZip"] = "转换 ZIP 为 TAR 格式",
            ["imageBuild.building"] = "正在构建镜像",
            ["imageBuild.success"] = "镜像构建成功",
            ["imageBuild.failed"] = "镜像构建失败",
            ["imageBuild.pushing"] = "正在推送镜像",
            ["imageBuild.pushSuccess"] = "镜像推送成功",
            ["imageBuild.pushFailed"] = "镜像推送失败",
            ["imageBuild.pulling"] = "正在拉取镜像",
            ["imageBuild.pullSuccess"] = "镜像拉取成功",
            ["imageBuild.pullFailed"] = "镜像拉取失败",

            // 卷操作
            ["volumeArchiving"] = "正在打包卷",
            ["volumeArchiveSuccess"] = "卷打包成功",
            ["volumeArchiveFailed"] = "卷打包失败",

            // 镜像构建进度
            ["build.preparing"] = "准备构建",
            ["build.building"] = "构建中",
            ["build.completed"] = "构建完成",
            ["build.failed"] = "构建失败",

            // Compose 部署进度
            ["deploy.start"] = "开始部署",
            ["deploy.network"] = "创建网络",
            ["deploy.volume"] = "创建存储卷",
            ["deploy.pull"] = "拉取镜像",
            ["deploy.container"] = "创建容器",
            ["deploy.completed"] = "部署完成",
            ["deploy.failed"] = "部署失败",

            // Compose 操作进度
            ["operation.start"] = "开始操作",
            ["operation.stopping"] = "正在停止",
            ["operation.starting"] = "正在启动",
            ["operation.completed"] = "操作完成",
            ["operation.failed"] = "操作失败",

            // Volume 打包进度
            ["archive.preparing"] = "准备中",
            ["archive.packing"] = "打包中",
            ["archive.compressing"] = "压缩中",
            ["archive.cleaning"] = "清理中",
            ["archive.completed"] = "打包完成",
            ["archive.failed"] = "打包失败",

            // Volume 恢复进度
            ["restore.preparing"] = "准备中",
            ["restore.extracting"] = "解压中",
            ["restore.restoring"] = "恢复中",
            ["restore.cleaning"] = "清理中",
            ["restore.completed"] = "恢复完成",
            ["restore.failed"] = "恢复失败",

            // Compose 部署 (旧键，保持兼容)
            ["composeDeploy.preparing"] = "准备部署",
            ["composeDeploy.creatingNetworks"] = "创建网络",
            ["composeDeploy.creatingVolumes"] = "创建卷",
            ["composeDeploy.pullingImages"] = "拉取镜像",
            ["composeDeploy.creatingServices"] = "创建服务",
            ["composeDeploy.startingServices"] = "启动服务",
            ["composeDeploy.success"] = "部署成功",
            ["composeDeploy.failed"] = "部署失败",
            ["composeOperation.starting"] = "正在启动",
            ["composeOperation.stopping"] = "正在停止",
            ["composeOperation.restarting"] = "正在重启",
            ["composeOperation.success"] = "操作成功",
            ["composeOperation.failed"] = "操作失败"
        };
        
        // 英文翻译
        var enUs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // General errors
            ["error.unauthorized"] = "Unauthorized, please login first",
            ["error.forbidden"] = "Access denied",
            ["error.notFound"] = "Resource not found",
            ["error.invalidRequest"] = "Invalid request parameters",
            ["error.invalidParams"] = "Invalid parameters",
            ["error.invalidOperation"] = "Invalid operation",
            ["error.serverError"] = "Internal server error",
            ["error.timeout"] = "Request timeout",
            ["error.networkError"] = "Network connection failed",
            
            // Container errors
            ["error.containerNotFound"] = "Container not found",
            ["error.containerAlreadyRunning"] = "Container is already running",
            ["error.containerNotRunning"] = "Container is not running",
            ["error.containerStartFailed"] = "Failed to start container",
            ["error.containerStopFailed"] = "Failed to stop container",
            ["error.containerRemoveFailed"] = "Failed to remove container",
            ["error.containerCreateFailed"] = "Failed to create container",
            ["error.invalidContainerName"] = "Invalid container name",
            ["error.containerNameExists"] = "Container name already exists",
            
            // Image errors
            ["error.imageNotFound"] = "Image not found",
            ["error.imagePullFailed"] = "Failed to pull image",
            ["error.imageRemoveFailed"] = "Failed to remove image",
            ["error.imageTagExists"] = "Image tag already exists",
            ["error.invalidImageName"] = "Invalid image name",
            
            // Image messages
            ["image.listFailed"] = "Failed to get image list",
            ["image.detailFailed"] = "Failed to get image details",
            ["image.notFound"] = "Image not found",
            ["image.deleteSuccess"] = "Image deleted successfully",
            ["image.pullStarted"] = "Image pull started",
            ["image.pushStarted"] = "Image push task started",
            ["image.importSuccess"] = "Image imported successfully",
            ["image.testEndpointOk"] = "Test endpoint working",
            
            // Network errors
            ["error.networkNotFound"] = "Network not found",
            ["error.networkCreateFailed"] = "Failed to create network",
            ["error.networkRemoveFailed"] = "Failed to remove network",
            ["error.networkInUse"] = "Network is in use",
            ["error.containerNotInNetwork"] = "Container is not in this network",
            
            // Network messages
            ["network.listFailed"] = "Failed to get network list",
            ["network.notFound"] = "Network not found",
            ["network.detailFailed"] = "Failed to get network details",
            ["network.createFailed"] = "Failed to create network",
            ["network.deleteSuccess"] = "Network deleted successfully",
            ["network.deleteFailed"] = "Failed to delete network",
            ["network.connectSuccess"] = "Container connected to network successfully",
            ["network.connectFailed"] = "Failed to connect container to network",
            ["network.disconnectSuccess"] = "Container disconnected from network successfully",
            ["network.disconnectFailed"] = "Failed to disconnect container from network",
            ["network.containersFailed"] = "Failed to get network containers",
            ["network.pruneFailed"] = "Failed to prune networks",
            ["network.statisticsFailed"] = "Failed to get network statistics",
            ["network.existsCheckFailed"] = "Failed to check network existence",
            ["network.ipamFailed"] = "Failed to get network IPAM info",
            ["network.updateFailed"] = "Failed to update network config",
            
            // Volume errors
            ["error.volumeNotFound"] = "Volume not found",
            ["error.volumeCreateFailed"] = "Failed to create volume",
            ["error.volumeRemoveFailed"] = "Failed to remove volume",
            ["error.volumeInUse"] = "Volume is in use",
            
            // Certificate errors
            ["error.certificateNotFound"] = "Certificate not found",
            ["error.certificateExpired"] = "Certificate has expired",
            ["error.certificateInvalid"] = "Invalid certificate",
            ["error.certificateRequestFailed"] = "Failed to request certificate",
            ["error.dnsChallengeFailed"] = "DNS challenge failed",
            
            // Certificate messages
            ["certificate.notFound"] = "Certificate not found",
            ["certificate.notFoundConfirm"] = "Certificate not found, please ensure the certificate has been successfully applied",
            ["certificate.dataEmpty"] = "Certificate data is empty",
            ["certificate.orderNotFound"] = "Certificate order not found",
            ["certificate.cancelled"] = "Certificate application cancelled",
            ["certificate.retrySubmitted"] = "Retry request submitted, certificate application will proceed in background",
            ["certificate.revokeSuccess"] = "Certificate revoked successfully",
            ["certificate.revokeFailed"] = "Certificate not found or revocation failed",
            ["certificate.autoRenewEnabled"] = "Auto-renewal enabled",
            ["certificate.autoRenewDisabled"] = "Auto-renewal disabled",
            ["certificate.accountNotFound"] = "Account not found",
            ["certificate.accountDeleted"] = "Account deleted successfully",
            ["certificate.domainsEmpty"] = "Domain list cannot be empty",
            ["certificate.canOnlyRetryFailed"] = "Can only retry failed or pending certificate applications",
            
            // ACME messages
            ["acme.statsFailed"] = "Failed to get ACME statistics",
            ["acme.providersFailed"] = "Failed to get ACME providers list",
            ["acme.testConnectionFailed"] = "Connection test failed",
            ["acme.accountsFailed"] = "Failed to get account list",
            ["acme.accountCreateFailed"] = "Failed to create account",
            ["acme.getAccountFailed"] = "Failed to get account",
            ["acme.accountDeleteFailed"] = "Failed to delete account",
            ["acme.getOrderFailed"] = "Failed to get order",
            ["acme.orderCreateFailed"] = "Certificate application failed: unable to create order",
            ["acme.applyCertificateFailed"] = "Failed to apply for certificate",
            ["acme.certificatesFailed"] = "Failed to get certificate list",
            ["acme.ordersFailed"] = "Failed to get order list",
            ["acme.orderCancelFailed"] = "Failed to cancel application",
            ["acme.orderNotFound"] = "Certificate order not found",
            ["acme.challengeCompleteFailed"] = "Failed to complete challenge verification",
            ["acme.challengeStatusFailed"] = "Failed to query challenge status",
            ["acme.downloadFailed"] = "Failed to download certificate",
            ["acme.pendingChallengesFailed"] = "Failed to get pending challenges",
            ["acme.renewFailed"] = "Failed to renew certificate",
            ["acme.renewSuccess"] = "Renewal successful",
            ["acme.autoRenewEnableFailed"] = "Failed to enable auto-renewal",
            ["acme.autoRenewDisableFailed"] = "Failed to disable auto-renewal",
            ["acme.retryFailed"] = "Failed to retry certificate application",
            ["acme.logsFailed"] = "Failed to get operation logs",
            ["acme.expiryCheckFailed"] = "Failed to check certificate expiry",
            ["acme.autoRenewRunFailed"] = "Auto-renewal failed",
            ["acme.fixStatusFailed"] = "Failed to fix certificate status",
            ["acme.statusFixed"] = "Successfully fixed {0} certificate statuses",
            ["acme.domainValidateFailed"] = "Domain validation failed",
            ["acme.csrGenerateFailed"] = "Failed to generate CSR",
            ["acme.certificateValidateFailed"] = "Failed to validate certificate",
            ["acme.keyInfoFailed"] = "Failed to get key information",
            ["acme.keyGenerateFailed"] = "Failed to generate key pair",
            ["acme.keyExportFailed"] = "Failed to export key",
            ["acme.keyImportFailed"] = "Failed to import key",
            ["acme.batchRenewFailed"] = "Batch renewal failed",
            ["acme.batchRenewComplete"] = "Batch renewal completed: {0}/{1} successful",
            ["acme.challengeStored"] = "Challenge stored successfully",
            ["acme.challengeNotFound"] = "Challenge file not found",
            ["acme.renewedCount"] = "Successfully renewed {0} certificates",
            ["acme.userCancelled"] = "User manually cancelled application",
            ["acme.pendingOrderExists"] = "A pending certificate application already exists for this domain",
            ["acme.revokeFailed"] = "Failed to revoke certificate",
            ["acme.certificateForceDeleted"] = "Certificate not found (deletion confirmed)",
            ["acme.forceModeSkipCheck"] = "Force mode: skipping existence check",
            ["acme.checkCertificateExists"] = "Checking if certificate exists",
            ["acme.checkUsageStatus"] = "Checking certificate usage status: no associated domain mappings",
            ["acme.certificateInUse"] = "Cannot delete certificate, it is being used by multiple domain mappings. Please unbind the domains first.",
            ["acme.certificateUsedByDomains"] = "Certificate is being used by the following domains",
            ["acme.mappingCertificateUnbound"] = "Certificate binding removed from domain mapping",
            ["acme.forceModeUnbindMappings"] = "Force mode: removing certificate binding from domain mappings",
            ["acme.deleteFromOrdersCollection"] = "Deleting certificate record from acme_orders collection",
            ["acme.deleteFromCertificatesCollection"] = "Deleting certificate record from certificates collection",
            ["acme.cleanOperationHistory"] = "Cleaning up operation history",
            ["acme.cleanUsageStats"] = "Cleaning up usage statistics",
            ["acme.certificateDeleted"] = "Certificate deleted successfully",
            ["acme.dbDeleteFailed"] = "Database deletion operation failed",
            ["acme.deleteFailed"] = "Deletion operation failed",
            ["acme.orderTimeoutMarkedFailed"] = "Certificate application timed out, automatically marked as failed",
            
            // Node errors
            ["error.nodeNotFound"] = "Node not found",
            ["error.nodeConnectionFailed"] = "Node connection failed",
            ["error.nodeAlreadyExists"] = "Node already exists",
            
            // Node messages
            ["node.listFailed"] = "Failed to get node list",
            ["node.notFound"] = "Node not found",
            ["node.getFailed"] = "Failed to get node",
            ["node.addFailed"] = "Failed to add node",
            ["node.updateFailed"] = "Failed to update node",
            ["node.deleteFailed"] = "Failed to delete node",
            ["node.testConnectionFailed"] = "Failed to test node connection",
            ["node.statsFailed"] = "Failed to get node statistics",
            ["node.detailFailed"] = "Failed to get node details",
            ["node.healthFailed"] = "Failed to get node health status",
            ["node.batchOperationFailed"] = "Failed to perform batch node operation",
            ["node.unsupportedOperation"] = "Unsupported operation",
            
            // Registry messages
            ["registry.notFound"] = "Registry not found",
            ["registry.deleteSuccess"] = "Registry deleted successfully",
            ["registry.setDefaultSuccess"] = "Default registry set successfully",
            ["registry.loginSuccess"] = "Login successful",
            ["registry.loginFailed"] = "Login failed",
            ["registry.logoutSuccess"] = "Logout successful",
            ["registry.logoutFailed"] = "Logout failed",
            ["registry.listFailed"] = "Failed to get registry list",
            ["registry.mirrorListFailed"] = "Failed to get mirror list",
            ["registry.privateListFailed"] = "Failed to get private registry list",
            ["registry.detailFailed"] = "Failed to get registry details",
            ["registry.createFailed"] = "Failed to create registry",
            ["registry.updateFailed"] = "Failed to update registry",
            ["registry.deleteFailed"] = "Failed to delete registry",
            ["registry.testFailed"] = "Failed to test registry connection",
            ["registry.testConfigFailed"] = "Failed to test registry config connection",
            ["registry.searchFailed"] = "Search failed",
            ["registry.setDefaultFailed"] = "Failed to set default registry",
            ["registry.validateAuthFailed"] = "Failed to validate registry authentication",
            ["registry.syncFailed"] = "Failed to sync registry images",
            ["registry.statisticsFailed"] = "Failed to get registry statistics",
            
            // Challenge validation messages
            ["challenge.http01ConfigFailed"] = "Failed to configure HTTP-01 challenge",
            ["challenge.http01ValidateFailed"] = "Failed to validate HTTP-01 challenge",
            ["challenge.dns01ConfigFailed"] = "Failed to configure DNS-01 challenge",
            ["challenge.dns01ValidateFailed"] = "Failed to validate DNS-01 challenge",
            ["challenge.tlsAlpn01ConfigFailed"] = "Failed to configure TLS-ALPN-01 challenge",
            ["challenge.tlsAlpn01ValidateFailed"] = "Failed to validate TLS-ALPN-01 challenge",
            ["challenge.cleanupFailed"] = "Failed to cleanup challenge",
            ["challenge.getStatusFailed"] = "Failed to get challenge status",
            ["challenge.dnsProvidersFailed"] = "Failed to get DNS providers list",
            ["challenge.dnsProviderTestFailed"] = "Failed to test DNS provider connection",
            ["challenge.autoConfigFailed"] = "Failed to auto-configure challenge",
            ["challenge.batchCleanupFailed"] = "Failed to batch cleanup challenges",
            ["challenge.statsFailed"] = "Failed to get challenge validation statistics",
            
            // Proxy errors
            ["error.proxyConfigFailed"] = "Failed to configure proxy",
            ["error.domainMappingFailed"] = "Failed to map domain",
            ["error.invalidDomain"] = "Invalid domain",
            
            // Auth errors
            ["error.loginFailed"] = "Login failed",
            ["error.invalidCredentials"] = "Invalid username or password",
            ["error.tokenExpired"] = "Session expired, please login again",
            ["error.accountDisabled"] = "Account is disabled",
            
            // SSH messages
            ["ssh.testConnectionFailed"] = "Failed to test SSH connection",
            ["ssh.generateKeyPairFailed"] = "Failed to generate SSH key pair",
            ["ssh.validateKeyFailed"] = "Failed to validate SSH private key",
            ["ssh.commandFailed"] = "SSH command execution failed",
            ["ssh.uploadFailed"] = "SSH file upload failed",
            ["ssh.downloadFailed"] = "SSH file download failed",
            ["ssh.batchTestFailed"] = "Batch SSH connection test failed",
            ["ssh.getConfigListFailed"] = "Failed to get connection config list",
            ["ssh.configNotFound"] = "Connection config not found",
            ["ssh.getConfigFailed"] = "Failed to get connection config",
            ["ssh.createConfigFailed"] = "Failed to create connection config",
            ["ssh.updateConfigFailed"] = "Failed to update connection config",
            ["ssh.deleteConfigFailed"] = "Failed to delete connection config",
            ["ssh.getKeyPairListFailed"] = "Failed to get key pair list",
            ["ssh.importKeyPairFailed"] = "Failed to import key pair",
            ["ssh.keyPairNotFound"] = "Key pair not found",
            ["ssh.deleteKeyPairFailed"] = "Failed to delete key pair",
            ["ssh.getSessionListFailed"] = "Failed to get session list",
            ["ssh.sessionNotFound"] = "Session not found",
            ["ssh.terminateSessionFailed"] = "Failed to terminate session",
            ["ssh.reconnectSessionFailed"] = "Failed to reconnect session",
            ["ssh.listDirectoryFailed"] = "Failed to list directory",
            ["ssh.deleteFileFailed"] = "Failed to delete file",
            ["ssh.getStatsFailed"] = "Failed to get statistics",
            ["ssh.getLogsFailed"] = "Failed to get operation logs",
            ["ssh.getSettingsFailed"] = "Failed to get settings",
            ["ssh.updateSettingsFailed"] = "Failed to update settings",
            
            // Settings messages
            ["settings.updateSuccess"] = "System settings updated successfully",
            ["settings.resetSuccess"] = "System settings have been reset to default",
            ["settings.importSuccess"] = "System settings imported successfully",
            
            // Task messages
            ["task.deleted"] = "Task deleted",
            ["task.cleared"] = "Completed tasks cleared",
            
            // Template messages
            ["template.deleteSuccess"] = "Deleted successfully",
            
            // System messages
            ["system.dockerDisconnected"] = "Docker daemon is not running or inaccessible",
            
            // Node resource messages
            ["nodeResource.overviewFailed"] = "Failed to get node resource overview",
            ["nodeResource.nodeNotFound"] = "Node not found",
            ["nodeResource.detailFailed"] = "Failed to get node resource details",
            ["nodeResource.trendFailed"] = "Failed to get node resource trend",
            ["nodeResource.clusterStatsFailed"] = "Failed to get cluster resource statistics",
            ["nodeResource.alertsFailed"] = "Failed to get resource alerts",
            ["nodeResource.alertCreateFailed"] = "Failed to create resource alert rule",
            ["nodeResource.alertNotFound"] = "Alert rule not found",
            ["nodeResource.alertGetFailed"] = "Failed to get resource alert rule",
            ["nodeResource.alertListFailed"] = "Failed to get resource alert rule list",
            ["nodeResource.alertUpdateFailed"] = "Failed to update resource alert rule",
            ["nodeResource.alertDeleteFailed"] = "Failed to delete resource alert rule",
            ["nodeResource.realtimeFailed"] = "Failed to get node realtime resource usage",
            ["nodeResource.dashboardFailed"] = "Failed to get cluster dashboard data",
            ["nodeResource.performanceFailed"] = "Failed to get node performance metrics",
            
            // Node group messages
            ["nodeGroup.listFailed"] = "Failed to get node group list",
            ["nodeGroup.notFound"] = "Node group not found",
            ["nodeGroup.createFailed"] = "Failed to create node group",
            ["nodeGroup.updateFailed"] = "Failed to update node group",
            ["nodeGroup.deleteFailed"] = "Failed to delete node group",
            ["nodeGroup.addNodeFailed"] = "Failed to add node to group",
            ["nodeGroup.addNodeSuccess"] = "Node added to group",
            ["nodeGroup.removeNodeFailed"] = "Failed to remove node from group",
            ["nodeGroup.removeNodeSuccess"] = "Node removed from group",
            ["nodeGroup.nodesFailed"] = "Failed to get group nodes",
            ["nodeGroup.batchUpdateFailed"] = "Failed to batch update node groups",
            ["nodeGroup.batchUpdateSuccess"] = "Node groups batch updated successfully",
            ["nodeGroup.statsFailed"] = "Failed to get group statistics",
            ["nodeGroup.tagsFailed"] = "Failed to get tags",
            ["nodeGroup.tagCreateFailed"] = "Failed to create tag",
            ["nodeGroup.tagNotFound"] = "Tag not found",
            ["nodeGroup.tagGetFailed"] = "Failed to get tag",
            ["nodeGroup.tagUpdateFailed"] = "Failed to update tag",
            ["nodeGroup.tagDeleteFailed"] = "Failed to delete tag",
            ["nodeGroup.addTagFailed"] = "Failed to add tag to node",
            ["nodeGroup.addTagSuccess"] = "Tag added to node",
            ["nodeGroup.removeTagFailed"] = "Failed to remove tag from node",
            ["nodeGroup.removeTagSuccess"] = "Tag removed from node",
            ["nodeGroup.nodeTagsFailed"] = "Failed to get node tags",
            ["nodeGroup.tagNodesFailed"] = "Failed to get tag nodes",
            ["nodeGroup.batchTagUpdateFailed"] = "Failed to batch update node tags",
            ["nodeGroup.batchTagUpdateSuccess"] = "Node tags batch updated successfully",
            ["nodeGroup.tagStatsFailed"] = "Failed to get tag statistics",
            ["nodeGroup.overviewFailed"] = "Failed to get group and tag overview",
            
            // Success messages
            ["success.saved"] = "Saved successfully",
            ["success.deleted"] = "Deleted successfully",
            ["success.created"] = "Created successfully",
            ["success.updated"] = "Updated successfully",
            ["success.operation"] = "Operation completed",

            // SignalR connection messages
            ["signalr.welcome"] = "Welcome to DockerPanel real-time service",
            ["signalr.error.containerList"] = "Failed to get container list",
            ["signalr.error.systemStats"] = "Failed to get system statistics",

            // Background task status
            ["task.pending"] = "Pending",
            ["task.running"] = "Running",
            ["task.completed"] = "Completed",
            ["task.failed"] = "Failed",
            ["task.cancelled"] = "Cancelled",

            // Image build
            ["imageBuild.preparing"] = "Preparing build",
            ["imageBuild.convertingZip"] = "Converting ZIP to TAR format",
            ["imageBuild.building"] = "Building image",
            ["imageBuild.success"] = "Image build succeeded",
            ["imageBuild.failed"] = "Image build failed",
            ["imageBuild.pushing"] = "Pushing image",
            ["imageBuild.pushSuccess"] = "Image push succeeded",
            ["imageBuild.pushFailed"] = "Image push failed",
            ["imageBuild.pulling"] = "Pulling image",
            ["imageBuild.pullSuccess"] = "Image pull succeeded",
            ["imageBuild.pullFailed"] = "Image pull failed",

            // Volume operations
            ["volumeArchiving"] = "Archiving volume",
            ["volumeArchiveSuccess"] = "Volume archive succeeded",
            ["volumeArchiveFailed"] = "Volume archive failed",
            
            // Volume messages
            ["volume.listFailed"] = "Failed to get volume list",
            ["volume.notFound"] = "Volume not found",
            ["volume.detailFailed"] = "Failed to get volume details",
            ["volume.createFailed"] = "Failed to create volume",
            ["volume.noArchiveSelected"] = "No archive file selected",
            ["volume.restoreFailed"] = "Failed to restore volume",
            ["volume.deleteSuccess"] = "Volume deleted successfully",
            ["volume.deleteFailed"] = "Failed to delete volume",
            ["volume.updateFailed"] = "Failed to update volume config",
            ["volume.pruneFailed"] = "Failed to prune volumes",
            ["volume.statsFailed"] = "Failed to get volume statistics",
            ["volume.existsCheckFailed"] = "Failed to check volume existence",
            ["volume.usageFailed"] = "Failed to get volume usage",
            ["volume.backupFailed"] = "Failed to backup volume",
            ["volume.backupListFailed"] = "Failed to get volume backup list",
            ["volume.backupDeleteSuccess"] = "Volume backup deleted successfully",
            ["volume.backupNotFound"] = "Volume backup not found",
            ["volume.backupDeleteFailed"] = "Failed to delete volume backup",
            ["volume.fileListFailed"] = "Failed to get file list",
            ["volume.downloadFileFailed"] = "Failed to download file",
            ["volume.noFileSelected"] = "No file selected",
            ["volume.uploadSuccess"] = "Upload successful",
            ["volume.uploadFailed"] = "Failed to upload file",
            ["volume.createFolderSuccess"] = "Created successfully",
            ["volume.createFolderFailed"] = "Failed to create folder",
            ["volume.renameSuccess"] = "Renamed successfully",
            ["volume.renameFailed"] = "Failed to rename file",
            ["volume.deleteFileSuccess"] = "Deleted successfully",
            ["volume.deleteFileFailed"] = "Failed to delete file",
            ["volume.getFileContentFailed"] = "Failed to get file content",
            ["volume.saveSuccess"] = "Saved successfully",
            ["volume.saveFileFailed"] = "Failed to save file content",

            // Image build progress
            ["build.preparing"] = "Preparing build",
            ["build.building"] = "Building",
            ["build.completed"] = "Build completed",
            ["build.failed"] = "Build failed",

            // Compose deploy progress
            ["deploy.start"] = "Starting deployment",
            ["deploy.network"] = "Creating networks",
            ["deploy.volume"] = "Creating volumes",
            ["deploy.pull"] = "Pulling images",
            ["deploy.container"] = "Creating containers",
            ["deploy.completed"] = "Deployment completed",
            ["deploy.failed"] = "Deployment failed",

            // Compose operation progress
            ["operation.start"] = "Starting operation",
            ["operation.stopping"] = "Stopping",
            ["operation.starting"] = "Starting",
            ["operation.completed"] = "Operation completed",
            ["operation.failed"] = "Operation failed",

            // Volume archive progress
            ["archive.preparing"] = "Preparing",
            ["archive.packing"] = "Packing",
            ["archive.compressing"] = "Compressing",
            ["archive.cleaning"] = "Cleaning up",
            ["archive.completed"] = "Archive completed",
            ["archive.failed"] = "Archive failed",

            // Volume restore progress
            ["restore.preparing"] = "Preparing",
            ["restore.extracting"] = "Extracting",
            ["restore.restoring"] = "Restoring",
            ["restore.cleaning"] = "Cleaning up",
            ["restore.completed"] = "Restore completed",
            ["restore.failed"] = "Restore failed",

            // Compose messages
            ["compose.notFound"] = "Compose file not found",
            ["compose.deleteSuccess"] = "Compose file deleted successfully",
            ["compose.projectNotFound"] = "Compose project not found",
            ["compose.listFailed"] = "Failed to get compose file list",
            ["compose.detailFailed"] = "Failed to get compose file details",
            ["compose.createFailed"] = "Failed to create compose file",
            ["compose.updateFailed"] = "Failed to update compose file",
            ["compose.deleteFailed"] = "Failed to delete compose file",
            ["compose.validateFailed"] = "Failed to validate compose file",
            ["compose.contentEmpty"] = "Content cannot be empty",
            ["compose.parseFailed"] = "Failed to parse compose content",
            ["compose.deployFailed"] = "Failed to deploy compose project",
            ["compose.stopFailed"] = "Failed to stop compose project",
            ["compose.startFailed"] = "Failed to start compose project",
            ["compose.restartFailed"] = "Failed to restart compose project",
            ["compose.removeFailed"] = "Failed to remove compose project",
            ["compose.projectStatusFailed"] = "Failed to get compose project status",
            ["compose.projectListFailed"] = "Failed to get compose project list",
            ["compose.logsFailed"] = "Failed to get compose logs",
            ["compose.projectStatsNotFound"] = "Compose project statistics not found",
            ["compose.projectStatsFailed"] = "Failed to get compose project statistics",
            ["compose.exportFailed"] = "Failed to export compose file",
            ["compose.importFailed"] = "Failed to import compose file",
            ["compose.templateListFailed"] = "Failed to get compose template list",
            ["compose.createFromTemplateFailed"] = "Failed to create compose file from template",
            ["compose.batchOperationFailed"] = "Failed to perform batch operation on compose files",
            ["compose.historyFailed"] = "Failed to get compose file history",
            ["compose.restoreFailed"] = "Failed to restore compose file version",
            ["compose.checkDependenciesFailed"] = "Failed to check compose file dependencies",

            // Compose deploy (legacy keys, kept for compatibility)
            ["composeDeploy.preparing"] = "Preparing deployment",
            ["composeDeploy.creatingNetworks"] = "Creating networks",
            ["composeDeploy.creatingVolumes"] = "Creating volumes",
            ["composeDeploy.pullingImages"] = "Pulling images",
            ["composeDeploy.creatingServices"] = "Creating services",
            ["composeDeploy.startingServices"] = "Starting services",
            ["composeDeploy.success"] = "Deployment succeeded",
            ["composeDeploy.failed"] = "Deployment failed",
            ["composeOperation.starting"] = "Starting",
            ["composeOperation.stopping"] = "Stopping",
            ["composeOperation.restarting"] = "Restarting",
            ["composeOperation.success"] = "Operation succeeded",
            ["composeOperation.failed"] = "Operation failed"
        };

        _translations["zh-CN"] = zhCn;
        _translations["zh"] = zhCn;
        _translations["en-US"] = enUs;
        _translations["en"] = enUs;
    }

    /// <summary>
    /// 静态方法：根据指定语言获取翻译消息（供 SignalR Hub 等无法依赖注入的场景使用）
    /// </summary>
    public static string GetTranslatedMessage(string key, string language, string? defaultValue = null)
    {
        var normalizedLang = NormalizeLanguageStatic(language);
        
        if (_translations.TryGetValue(normalizedLang, out var translations))
        {
            if (translations.TryGetValue(key, out var message))
            {
                return message;
            }
        }
        
        // 尝试使用默认语言
        if (_translations.TryGetValue(DefaultLanguage, out var defaultTranslations))
        {
            if (defaultTranslations.TryGetValue(key, out var message))
            {
                return message;
            }
        }
        
        return defaultValue ?? key;
    }

    private static string NormalizeLanguageStatic(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return DefaultLanguage;
        }
        
        var lang = language.ToLowerInvariant();
        
        if (lang.StartsWith("zh"))
        {
            return "zh-CN";
        }
        
        if (lang.StartsWith("en"))
        {
            return "en-US";
        }
        
        return DefaultLanguage;
    }

    public string GetCurrentLanguage()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(LanguageKey, out var language) == true && language is string lang)
        {
            return lang;
        }
        return DefaultLanguage;
    }

    public void SetCurrentLanguage(string language)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items[LanguageKey] = NormalizeLanguage(language);
        }
    }

    public string GetErrorMessage(string code, params object[] args)
    {
        var key = $"error.{code}";
        var message = GetMessage(key);
        
        if (args.Length > 0 && message != key)
        {
            try
            {
                return string.Format(message, args);
            }
            catch
            {
                return message;
            }
        }
        
        return message;
    }

    public string GetMessage(string key, string? defaultValue = null)
    {
        var language = GetCurrentLanguage();
        
        if (_translations.TryGetValue(language, out var translations))
        {
            if (translations.TryGetValue(key, out var message))
            {
                return message;
            }
        }
        
        // 尝试使用默认语言
        if (_translations.TryGetValue(DefaultLanguage, out var defaultTranslations))
        {
            if (defaultTranslations.TryGetValue(key, out var message))
            {
                return message;
            }
        }
        
        return defaultValue ?? key;
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return DefaultLanguage;
        }
        
        // 标准化语言代码
        var lang = language.ToLowerInvariant();
        
        if (lang.StartsWith("zh"))
        {
            return "zh-CN";
        }
        
        if (lang.StartsWith("en"))
        {
            return "en-US";
        }
        
        // 查找精确匹配
        foreach (var supportedLang in _translations.Keys)
        {
            if (supportedLang.Equals(language, StringComparison.OrdinalIgnoreCase))
            {
                return supportedLang;
            }
        }
        
        return DefaultLanguage;
    }
}

/// <summary>
/// 本地化扩展方法
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// 添加本地化服务
    /// </summary>
    public static IServiceCollection AddLocalizationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ILocalizationService, LocalizationService>();
        return services;
    }
    
    /// <summary>
    /// 使用本地化中间件
    /// </summary>
    public static IApplicationBuilder UseLocalization(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LocalizationMiddleware>();
    }
}

/// <summary>
/// 本地化中间件 - 解析 Accept-Language 头
/// </summary>
public class LocalizationMiddleware
{
    private readonly RequestDelegate _next;

    public LocalizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILocalizationService localizationService)
    {
        // 从 Accept-Language 头获取语言
        var acceptLanguage = context.Request.Headers.AcceptLanguage.FirstOrDefault();
        
        if (!string.IsNullOrEmpty(acceptLanguage))
        {
            // 解析 Accept-Language 头，格式: "zh-CN, zh-CN;q=0.9, en-US;q=0.8"
            var languages = acceptLanguage.Split(',')
                .Select(l => l.Split(';')[0].Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();
            
            if (languages.Count > 0)
            {
                localizationService.SetCurrentLanguage(languages[0]);
            }
        }
        
        await _next(context);
    }
}
