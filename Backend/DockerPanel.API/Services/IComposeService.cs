using DockerPanel.API.Models;

namespace DockerPanel.API.Services;

/// <summary>
/// Docker Compose管理服务接口
/// </summary>
public interface IComposeService
{
    /// <summary>
    /// 获取所有Compose文件
    /// </summary>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <param name="includeContent">是否包含文件内容</param>
    /// <returns>Compose文件列表</returns>
    Task<IEnumerable<ComposeFile>> GetComposeFilesAsync(string? nodeId = null, bool includeContent = false);

    /// <summary>
    /// 根据ID获取Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="includeContent">是否包含文件内容</param>
    /// <returns>Compose文件信息</returns>
    Task<ComposeFile?> GetComposeFileAsync(string id, bool includeContent = true);

    /// <summary>
    /// 创建Compose文件
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <returns>创建的Compose文件</returns>
    Task<ComposeFile> CreateComposeFileAsync(CreateComposeFileRequest request);

    /// <summary>
    /// 更新Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新后的Compose文件</returns>
    Task<ComposeFile> UpdateComposeFileAsync(string id, UpdateComposeFileRequest request);

    /// <summary>
    /// 删除Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="force">是否强制删除</param>
    /// <returns>删除是否成功</returns>
    Task<bool> DeleteComposeFileAsync(string id, bool force = false);

    /// <summary>
    /// 验证Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="content">文件内容（可选，如果不提供则使用文件中的内容）</param>
    /// <returns>验证结果</returns>
    Task<ComposeValidationResult> ValidateComposeFileAsync(string id, string? content = null);

    /// <summary>
    /// 解析Compose文件内容
    /// </summary>
    /// <param name="content">Compose文件内容</param>
    /// <returns>解析结果</returns>
    Task<ComposeFile> ParseComposeContentAsync(string content);

    /// <summary>
    /// 部署Compose项目
    /// </summary>
    /// <param name="request">部署请求</param>
    /// <returns>部署结果</returns>
    Task<ComposeOperationResult> DeployComposeAsync(DeployComposeRequest request);

    /// <summary>
    /// 停止Compose项目
    /// </summary>
    /// <param name="request">停止请求</param>
    /// <returns>操作结果</returns>
    Task<ComposeOperationResult> StopComposeAsync(ComposeOperationRequest request);

    /// <summary>
    /// 启动Compose项目
    /// </summary>
    /// <param name="request">启动请求</param>
    /// <returns>操作结果</returns>
    Task<ComposeOperationResult> StartComposeAsync(ComposeOperationRequest request);

    /// <summary>
    /// 重启Compose项目
    /// </summary>
    /// <param name="request">重启请求</param>
    /// <returns>操作结果</returns>
    Task<ComposeOperationResult> RestartComposeAsync(ComposeOperationRequest request);

    /// <summary>
    /// 删除Compose项目
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <returns>操作结果</returns>
    Task<ComposeOperationResult> RemoveComposeAsync(ComposeOperationRequest request);

    /// <summary>
    /// 获取Compose项目状态
    /// </summary>
    /// <param name="composeFileId">Compose文件ID</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>Compose项目信息</returns>
    Task<ComposeProject?> GetComposeProjectStatusAsync(string composeFileId, string? nodeId = null);

    /// <summary>
    /// 获取Compose项目列表
    /// </summary>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>Compose项目列表</returns>
    Task<IEnumerable<ComposeProject>> GetComposeProjectsAsync(string? nodeId = null);

    /// <summary>
    /// 获取Compose日志
    /// </summary>
    /// <param name="request">日志请求</param>
    /// <returns>日志响应</returns>
    Task<ComposeLogsResponse> GetComposeLogsAsync(ComposeLogsRequest request);

    /// <summary>
    /// 获取Compose项目统计信息
    /// </summary>
    /// <param name="composeFileId">Compose文件ID</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>统计信息</returns>
    Task<ComposeProjectStats?> GetComposeProjectStatsAsync(string composeFileId, string? nodeId = null);

    /// <summary>
    /// 导出Compose文件
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="format">导出格式（yaml, json）</param>
    /// <returns>导出的文件内容</returns>
    Task<string> ExportComposeFileAsync(string id, string format = "yaml");

    /// <summary>
    /// 导入Compose文件
    /// </summary>
    /// <param name="content">文件内容</param>
    /// <param name="name">文件名称</param>
    /// <param name="description">描述</param>
    /// <param name="nodeId">节点ID（可选）</param>
    /// <returns>导入的Compose文件</returns>
    Task<ComposeFile> ImportComposeFileAsync(string content, string name, string? description = null, string? nodeId = null);

    /// <summary>
    /// 克隆Compose文件
    /// </summary>
    /// <param name="id">源Compose文件ID</param>
    /// <param name="newName">新文件名称</param>
    /// <param name="description">描述</param>
    /// <returns>克隆的Compose文件</returns>
    Task<ComposeFile> CloneComposeFileAsync(string id, string newName, string? description = null);

    /// <summary>
    /// 获取Compose模板列表
    /// </summary>
    /// <param name="category">分类（可选）</param>
    /// <param name="tags">标签（可选）</param>
    /// <returns>模板列表</returns>
    Task<IEnumerable<ComposeTemplate>> GetComposeTemplatesAsync(string? category = null, List<string>? tags = null);

    /// <summary>
    /// 根据模板创建Compose文件
    /// </summary>
    /// <param name="templateId">模板ID</param>
    /// <param name="variables">变量值</param>
    /// <param name="name">文件名称</param>
    /// <param name="description">描述</param>
    /// <returns>创建的Compose文件</returns>
    Task<ComposeFile> CreateFromTemplateAsync(string templateId, Dictionary<string, object> variables, string name, string? description = null);

    /// <summary>
    /// 批量操作Compose文件
    /// </summary>
    /// <param name="fileIds">文件ID列表</param>
    /// <param name="operation">操作类型</param>
    /// <param name="parameters">操作参数</param>
    /// <returns>批量操作结果</returns>
    Task<Dictionary<string, ComposeOperationResult>> BatchOperationAsync(List<string> fileIds, string operation, Dictionary<string, object>? parameters = null);

    /// <summary>
    /// 获取Compose文件历史版本
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <returns>历史版本列表</returns>
    Task<IEnumerable<ComposeFileVersion>> GetComposeFileHistoryAsync(string id);

    /// <summary>
    /// 恢复Compose文件到指定版本
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="versionId">版本ID</param>
    /// <returns>恢复后的Compose文件</returns>
    Task<ComposeFile> RestoreComposeFileVersionAsync(string id, string versionId);

    /// <summary>
    /// 同步Compose文件到节点
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <param name="nodeId">目标节点ID</param>
    /// <returns>同步结果</returns>
    Task<ComposeOperationResult> SyncComposeFileToNodeAsync(string id, string nodeId);

    /// <summary>
    /// 检查Compose文件依赖
    /// </summary>
    /// <param name="id">Compose文件ID</param>
    /// <returns>依赖检查结果</returns>
    Task<ComposeDependencyCheck> CheckComposeDependenciesAsync(string id);
}