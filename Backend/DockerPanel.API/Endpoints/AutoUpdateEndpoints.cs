using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 容器自动升级 Minimal API 端点（原 AutoUpdateController）。
    /// </summary>
    public static class AutoUpdateEndpoints
    {
        /// <summary>
        /// 映射自动升级相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapAutoUpdateEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/auto-update");

            group.MapGet("/configs", GetAllConfigs);
            group.MapGet("/configs/{containerId}", GetConfig);
            group.MapPut("/configs/{containerId}", SetConfig);
            group.MapDelete("/configs/{containerId}", DeleteConfig);
            group.MapPost("/check/{containerId}", CheckUpdate);
            group.MapPost("/check-all", CheckAllUpdates);
            group.MapGet("/available-updates", GetAvailableUpdates);
            group.MapPost("/update/{containerId}", UpdateContainer);
            group.MapGet("/settings", GetGlobalSettings);
            group.MapPut("/settings", SetGlobalSettings);
            group.MapGet("/image-tags", GetImageTags);
            group.MapPost("/rollback/{containerId}", RollbackContainer);

            return app;
        }

        private static async Task<IResult> GetAllConfigs(IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var configs = await service.GetAllConfigsAsync();
                return TypedResults.Ok(configs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取自动升级配置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.getConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetConfig(string containerId, IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var config = await service.GetConfigAsync(containerId);
                if (config == null)
                {
                    return TypedResults.Ok(new ContainerAutoUpdateConfig
                    {
                        ContainerId = containerId,
                        EnableUpdateCheck = true,
                        EnableAutoPull = false,
                        EnableAutoRestart = false,
                        CheckIntervalHours = 6,
                        Status = AutoUpdateStatus.Disabled
                    });
                }

                return TypedResults.Ok(config);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取容器 {ContainerId} 的自动升级配置失败", containerId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.getContainerConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> SetConfig(string containerId, ContainerAutoUpdateConfig config, IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await service.SetConfigAsync(containerId, config);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "设置容器 {ContainerId} 的自动升级配置失败", containerId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.setContainerConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteConfig(string containerId, IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                await service.DeleteConfigAsync(containerId);
                return TypedResults.NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除容器 {ContainerId} 的自动升级配置失败", containerId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.deleteContainerConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CheckUpdate(string containerId, IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await service.CheckUpdateAsync(containerId);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "检查容器 {ContainerId} 的镜像更新失败", containerId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.checkFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CheckAllUpdates(IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var results = await service.CheckAllUpdatesAsync();
                return TypedResults.Ok(results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "检查所有容器更新失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.checkAllFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetAvailableUpdates(IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var configs = await service.GetContainersWithUpdatesAsync();
                return TypedResults.Ok(configs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取可用更新列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.availableUpdatesFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateContainer(string containerId, bool pullOnly, IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await service.UpdateContainerAsync(containerId, pullOnly);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新容器 {ContainerId} 失败", containerId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.updateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetGlobalSettings(IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var settings = await service.GetGlobalSettingsAsync();
                return TypedResults.Ok(settings);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取全局设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.getGlobalSettingsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> SetGlobalSettings(GlobalAutoUpdateSettings settings, IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var result = await service.SetGlobalSettingsAsync(settings);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "设置全局设置失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.setGlobalSettingsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetImageTags(string imageName, IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                var tags = await service.GetImageTagsAsync(imageName);
                return TypedResults.Ok(tags);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像标签失败: {ImageName}", imageName);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.imageTagsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> RollbackContainer(string containerId, string? targetTag, IAutoUpdateService service, ILogger<SettingsEndpoints.LoggingTag> logger, ILocalizationService localization)
        {
            try
            {
                if (string.IsNullOrEmpty(targetTag))
                {
                    return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.targetTagRequired") });
                }

                var result = await service.RollbackContainerAsync(containerId, targetTag);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "回滚容器 {ContainerId} 失败", containerId);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("autoUpdate.rollbackFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}