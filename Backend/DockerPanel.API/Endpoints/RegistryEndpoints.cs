using DockerPanel.API.Models;
using DockerPanel.API.Serialization;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 镜像仓库 Minimal API 端点（原 RegistryController）。
    /// </summary>
    public static class RegistryEndpoints
    {
        /// <summary>
        /// 映射镜像仓库相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapRegistryEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/registries")
                .RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });

            group.MapGet("", GetRegistries);
            group.MapGet("by-type/{type}", GetRegistriesByType);
            group.MapGet("mirrors", GetMirrors);
            group.MapGet("private", GetPrivateRegistries);
            group.MapGet("{id}", GetRegistry);
            group.MapPost("", CreateRegistry);
            group.MapPut("{id}", UpdateRegistry);
            group.MapDelete("{id}", DeleteRegistry);
            group.MapPost("{id}/test", TestRegistryConnection);
            group.MapPost("test-config", TestRegistryConfig);
            group.MapPost("{id}/search", SearchRegistryImages);
            group.MapPost("{id}/set-default", SetDefaultRegistry);
            group.MapPost("{id}/login", LoginToRegistry);
            group.MapPost("{id}/logout", LogoutFromRegistry);
            group.MapPost("{id}/validate-auth", ValidateRegistryAuth);
            group.MapPost("{id}/sync", SyncRegistryImages);
            group.MapGet("statistics", GetRegistryStatistics);

            return app;
        }

        private static async Task<IResult> GetRegistries(IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var registries = await registryService.GetRegistriesAsync();
                return TypedResults.Ok(registries);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像仓库列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetRegistriesByType(string type, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var registries = await registryService.GetRegistriesAsync();
                var filtered = string.Equals(type, "Private", StringComparison.OrdinalIgnoreCase)
                    ? registries.Where(r => r.Type != "Mirror" && r.Type != "DockerHub")
                    : registries.Where(r => string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase));
                return TypedResults.Ok(filtered);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像仓库列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.listFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetMirrors(IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var registries = await registryService.GetRegistriesAsync();
                var mirrors = registries.Where(r => r.Type == "Mirror");
                return TypedResults.Ok(mirrors);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像加速器列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.mirrorListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetPrivateRegistries(IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var registries = await registryService.GetRegistriesAsync();
                var privateRegistries = registries.Where(r => r.Type == "Private" || (r.Type != "Mirror" && r.Type != "DockerHub"));
                return TypedResults.Ok(privateRegistries);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取私有仓库列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.privateListFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetRegistry(string id, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var registry = await registryService.GetRegistryByIdAsync(id);
                if (registry == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("registry.notFound"), Message = $"仓库ID {id} 不存在" });
                }
                return TypedResults.Ok(registry);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取镜像仓库详情失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.detailFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> CreateRegistry(CreateRegistryRequest request, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var registry = await registryService.CreateRegistryAsync(request);
                return TypedResults.Created($"/api/registries/{registry.Id}", registry);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("error.invalidParams"), Message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建镜像仓库失败: {Name}", request.Name);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.createFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> UpdateRegistry(string id, UpdateRegistryRequest request, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var registry = await registryService.UpdateRegistryAsync(id, request);
                return TypedResults.Ok(registry);
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("error.invalidParams"), Message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新镜像仓库失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.updateFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> DeleteRegistry(string id, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var result = await registryService.DeleteRegistryAsync(id);
                if (!result)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("registry.notFound"), Message = $"仓库ID {id} 不存在" });
                }
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("registry.deleteSuccess") });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ApiErrorResponse { Error = localization.GetMessage("error.invalidOperation"), Message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除镜像仓库失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.deleteFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> TestRegistryConnection(string id, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var result = await registryService.TestRegistryConnectionAsync(id);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "测试仓库连接失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.testFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> TestRegistryConfig(TestRegistryConfigRequest request, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var result = await registryService.TestRegistryConfigAsync(request);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "测试仓库配置连接失败");
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.testConfigFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> SearchRegistryImages(string id, RegistrySearchRequest request, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var result = await registryService.SearchRegistryImagesAsync(
                    id,
                    request.Query ?? "",
                    request.Limit > 0 ? request.Limit : 20,
                    request.Offset > 0 ? request.Offset : 0
                );
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "搜索仓库镜像失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.searchFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> SetDefaultRegistry(string id, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var result = await registryService.SetDefaultRegistryAsync(id);
                if (!result)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = localization.GetMessage("registry.notFound"), Message = $"仓库ID {id} 不存在" });
                }
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("registry.setDefaultSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "设置默认仓库失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.setDefaultFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> LoginToRegistry(string id, RegistryLoginRequest? request, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var result = await registryService.LoginToRegistryAsync(id, request?.Username, request?.Password);
                return TypedResults.Ok(new ActionBooleanResponse
                {
                    Success = result,
                    Message = result ? localization.GetMessage("registry.loginSuccess") : localization.GetMessage("registry.loginFailed")
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "登录私有仓库失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.loginFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> LogoutFromRegistry(string id, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var result = await registryService.LogoutFromRegistryAsync(id);
                return TypedResults.Ok(new ActionBooleanResponse
                {
                    Success = result,
                    Message = result ? localization.GetMessage("registry.logoutSuccess") : localization.GetMessage("registry.logoutFailed")
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "从私有仓库登出失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.logoutFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> ValidateRegistryAuth(string id, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var result = await registryService.ValidateRegistryAuthAsync(id);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证仓库认证失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.validateAuthFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> SyncRegistryImages(string id, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var result = await registryService.SyncRegistryImagesAsync(id);
                return TypedResults.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "同步仓库镜像信息失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.syncFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static async Task<IResult> GetRegistryStatistics(string? id, IRegistryService registryService, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var statistics = await registryService.GetRegistryStatisticsAsync(id);
                return TypedResults.Ok(statistics);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取仓库统计数据失败: {RegistryId}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = localization.GetMessage("registry.statisticsFailed"), Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}