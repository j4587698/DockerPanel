using DockerPanel.API.Serialization;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using TinyDb;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 容器模板 Minimal API 端点（原 TemplatesController）。
    /// </summary>
    public static class TemplateEndpoints
    {
        /// <summary>
        /// 映射模板相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapTemplateEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/templates");

            group.MapGet("/", GetTemplates);
            group.MapGet("/{id}", GetTemplate);
            group.MapPost("/", CreateTemplate);
            group.MapPut("/{id}", UpdateTemplate);
            group.MapDelete("/{id}", DeleteTemplate);
            group.MapPost("/{id}/duplicate", DuplicateTemplate);
            group.MapGet("/{id}/export", ExportTemplate);
            group.MapPost("/import", ImportTemplate);

            return app;
        }

        private static IResult GetTemplates(string? type, TinyDbContext dbContext, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var templates = dbContext.ContainerTemplates.Query();

                if (!string.IsNullOrEmpty(type))
                {
                    templates = templates.Where(t => t.Type == type);
                }

                return TypedResults.Ok(templates.OrderByDescending(t => t.CreatedAt).ToList());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取模板列表失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "获取模板列表失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult GetTemplate(string id, TinyDbContext dbContext, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var template = dbContext.ContainerTemplates.FindById(id);
                if (template == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = "模板不存在" });
                }

                return TypedResults.Ok(template);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "获取模板失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = "获取模板失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult CreateTemplate(CreateTemplateRequest request, TinyDbContext dbContext, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var template = new ContainerTemplate
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = request.Name,
                    Type = request.Type,
                    Description = request.Description,
                    Image = request.Image,
                    Command = request.Command,
                    WorkingDir = request.WorkingDir,
                    User = request.User,
                    Ports = request.Ports,
                    Volumes = request.Volumes,
                    Environment = request.Environment,
                    Labels = request.Labels,
                    RestartPolicy = request.RestartPolicy,
                    NetworkMode = request.NetworkMode,
                    Networks = request.Networks,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dbContext.ContainerTemplates.Insert(template);
                logger.LogInformation("创建模板成功: {Name}", template.Name);
                return TypedResults.Created($"/api/templates/{template.Id}", template);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "创建模板失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "创建模板失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult UpdateTemplate(string id, UpdateTemplateRequest request, TinyDbContext dbContext, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var existing = dbContext.ContainerTemplates.FindById(id);
                if (existing == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = "模板不存在" });
                }

                existing.Name = request.Name;
                existing.Type = request.Type;
                existing.Description = request.Description;
                existing.Image = request.Image;
                existing.Command = request.Command;
                existing.WorkingDir = request.WorkingDir;
                existing.User = request.User;
                existing.Ports = request.Ports;
                existing.Volumes = request.Volumes;
                existing.Environment = request.Environment;
                existing.Labels = request.Labels;
                existing.RestartPolicy = request.RestartPolicy;
                existing.NetworkMode = request.NetworkMode;
                existing.Networks = request.Networks;
                existing.UpdatedAt = DateTime.UtcNow;

                dbContext.ContainerTemplates.Update(existing);
                logger.LogInformation("更新模板成功: {Id}", id);
                return TypedResults.Ok(existing);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "更新模板失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = "更新模板失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult DeleteTemplate(string id, TinyDbContext dbContext, ILocalizationService localization, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var existing = dbContext.ContainerTemplates.FindById(id);
                if (existing == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = "模板不存在" });
                }

                dbContext.ContainerTemplates.Delete(id);
                logger.LogInformation("删除模板成功: {Id}", id);
                return TypedResults.Ok(new MessageResponse { Message = localization.GetMessage("template.deleteSuccess") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "删除模板失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = "删除模板失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult DuplicateTemplate(string id, TinyDbContext dbContext, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var existing = dbContext.ContainerTemplates.FindById(id);
                if (existing == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = "模板不存在" });
                }

                var newTemplate = new ContainerTemplate
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"{existing.Name} (副本)",
                    Type = existing.Type,
                    Description = existing.Description,
                    Image = existing.Image,
                    Command = existing.Command?.ToList(),
                    WorkingDir = existing.WorkingDir,
                    User = existing.User,
                    Ports = existing.Ports?.Select(p => new TemplatePortMapping
                    {
                        HostIp = p.HostIp,
                        HostPort = p.HostPort,
                        ContainerPort = p.ContainerPort,
                        Protocol = p.Protocol
                    }).ToList(),
                    Volumes = existing.Volumes?.Select(v => new TemplateVolumeMapping
                    {
                        HostPath = v.HostPath,
                        ContainerPath = v.ContainerPath,
                        ReadOnly = v.ReadOnly
                    }).ToList(),
                    Environment = existing.Environment?.ToDictionary(k => k.Key, k => k.Value),
                    Labels = existing.Labels?.ToDictionary(k => k.Key, k => k.Value),
                    RestartPolicy = existing.RestartPolicy != null ? new TemplateRestartPolicy
                    {
                        Name = existing.RestartPolicy.Name,
                        MaximumRetryCount = existing.RestartPolicy.MaximumRetryCount
                    } : null,
                    NetworkMode = existing.NetworkMode,
                    Networks = existing.Networks?.Select(n => new TemplateNetworkConfig
                    {
                        NetworkId = n.NetworkId,
                        NetworkName = n.NetworkName,
                        Aliases = n.Aliases?.ToList(),
                        IpAddress = n.IpAddress
                    }).ToList(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dbContext.ContainerTemplates.Insert(newTemplate);
                logger.LogInformation("复制模板成功: {Name}", newTemplate.Name);
                return TypedResults.Created($"/api/templates/{newTemplate.Id}", newTemplate);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "复制模板失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = "复制模板失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult ExportTemplate(string id, TinyDbContext dbContext, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                var template = dbContext.ContainerTemplates.FindById(id);
                if (template == null)
                {
                    return TypedResults.NotFound(new ApiErrorResponse { Error = "模板不存在" });
                }

                return TypedResults.Ok(template);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导出模板失败: {Id}", id);
                return TypedResults.Json(new ApiErrorResponse { Error = "导出模板失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }

        private static IResult ImportTemplate(ContainerTemplate template, TinyDbContext dbContext, ILogger<SettingsEndpoints.LoggingTag> logger)
        {
            try
            {
                // 生成新ID，避免冲突
                template.Id = Guid.NewGuid().ToString();
                template.CreatedAt = DateTime.UtcNow;
                template.UpdatedAt = DateTime.UtcNow;

                dbContext.ContainerTemplates.Insert(template);
                logger.LogInformation("导入模板成功: {Name}", template.Name);
                return TypedResults.Created($"/api/templates/{template.Id}", template);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "导入模板失败");
                return TypedResults.Json(new ApiErrorResponse { Error = "导入模板失败", Message = ex.Message }, WebJsonContext.Default.ApiErrorResponse, statusCode: 500);
            }
        }
    }
}