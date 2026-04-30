using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services.Acme;
using DockerPanel.API.Models;
using DockerPanel.API.Models.Acme;
using DockerPanel.API.Extensions;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Controllers.Acme
{
    /// <summary>
    /// 证书管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CertificateManagementController : ControllerBase
    {
        private readonly ICertificateManagementService _certificateManagementService;
        private readonly ILogger<CertificateManagementController> _logger;

        public CertificateManagementController(
            ICertificateManagementService certificateManagementService,
            ILogger<CertificateManagementController> logger)
        {
            _certificateManagementService = certificateManagementService;
            _logger = logger;
        }

        /// <summary>
        /// 获取证书列表
        /// </summary>
        /// <param name="includeExpired">是否包含过期证书</param>
        /// <param name="certificateType">证书类型过滤</param>
        /// <param name="statusFilter">状态过滤</param>
        /// <param name="domainFilter">域名过滤</param>
        /// <param name="pageIndex">页索引</param>
        /// <param name="pageSize">页大小</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书分页列表</returns>
        [HttpGet]
        public async Task<ActionResult<CertificateListResult>> GetCertificates(
            [FromQuery] bool includeExpired = false,
            [FromQuery] string? certificateType = null,
            [FromQuery] string? statusFilter = null,
            [FromQuery] string? domainFilter = null,
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书列表请求: IncludeExpired={IncludeExpired}, Type={Type}, Status={Status}, Domain={Domain}",
                    includeExpired, certificateType, statusFilter, domainFilter);

                var result = await _certificateManagementService.GetCertificatesAsync(
                    includeExpired, certificateType, statusFilter, domainFilter,
                    pageIndex, pageSize, cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书列表时发生异常");
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取证书详情
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书详情</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<CertificateDetails>> GetCertificateDetails(
            string id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书详情请求: {CertificateId}", id);

                var details = await _certificateManagementService.GetCertificateDetailsAsync(id, cancellationToken);

                if (details != null)
                {
                    return Ok(details);
                }
                else
                {
                    _logger.LogWarning("未找到证书: {CertificateId}", id);
                    return NotFound(new { Message = "未找到指定的证书" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书详情时发生异常: {CertificateId}", id);
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取即将到期的证书列表
        /// </summary>
        /// <param name="daysBeforeExpiry">到期前天数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>即将到期的证书列表</returns>
        [HttpGet("expiring")]
        public async Task<ActionResult<IEnumerable<ExpiringCertificate>>> GetExpiringCertificates(
            [FromQuery] int daysBeforeExpiry = 15,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取即将到期证书请求: 天数={Days}", daysBeforeExpiry);

                var expiringCertificates = await _certificateManagementService.GetExpiringCertificatesAsync(
                    daysBeforeExpiry, cancellationToken);

                return Ok(expiringCertificates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取即将到期证书时发生异常");
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 手动续期证书
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>续期结果</returns>
        [HttpPost("{id}/renew")]
        public async Task<ActionResult<CertificateRenewalResult>> RenewCertificate(
            string id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("手动续期证书请求: {CertificateId}", id);

                var result = await _certificateManagementService.RenewCertificateAsync(id, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("证书续期成功: {CertificateId}", id);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("证书续期失败: {CertificateId} - {Message}", id, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "续期证书时发生异常: {CertificateId}", id);
                return StatusCode(500, new CertificateRenewalResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    CertificateId = id,
                    RenewalStartedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 启用证书自动续期
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <param name="configuration">自动续期配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>配置结果</returns>
        [HttpPost("{id}/auto-renewal/enable")]
        public async Task<ActionResult<CertificateAutoRenewalConfigResult>> EnableAutoRenewal(
            string id,
            [FromBody] AutoRenewalConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("启用证书自动续期请求: {CertificateId}", id);

                var result = await _certificateManagementService.EnableAutoRenewalAsync(id, configuration, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("启用自动续期成功: {CertificateId}", id);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("启用自动续期失败: {CertificateId} - {Message}", id, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启用证书自动续期时发生异常: {CertificateId}", id);
                return StatusCode(500, new CertificateAutoRenewalConfigResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    CertificateId = id,
                    ConfiguredAt = DateTime.UtcNow,
                    ValidationErrors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 禁用证书自动续期
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作结果</returns>
        [HttpPost("{id}/auto-renewal/disable")]
        public async Task<ActionResult<bool>> DisableAutoRenewal(
            string id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("禁用证书自动续期请求: {CertificateId}", id);

                var result = await _certificateManagementService.DisableAutoRenewalAsync(id, cancellationToken);

                if (result)
                {
                    _logger.LogInformation("禁用自动续期成功: {CertificateId}", id);
                    return Ok(new { Success = true, Message = "自动续期禁用成功" });
                }
                else
                {
                    _logger.LogWarning("禁用自动续期失败: {CertificateId}", id);
                    return BadRequest(new { Success = false, Message = "自动续期禁用失败" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "禁用证书自动续期时发生异常: {CertificateId}", id);
                return StatusCode(500, new { Success = false, Message = "服务器内部错误", Error = ex.Message });
            }
        }

                /// <summary>
                /// 删除证书
                /// </summary>
                /// <param name="id">证书ID</param>
                /// <param name="force">是否强制删除</param>
                /// <param name="cancellationToken">取消令牌</param>
                /// <returns>删除结果</returns>
                [HttpDelete("{id}")]
                public async Task<ActionResult<CertificateDeletionResult>> DeleteCertificate(
                    string id,
                    [FromQuery] bool force = false,
                    CancellationToken cancellationToken = default)
                {
                    try
                    {
                        _logger.LogInformation("删除证书请求: {CertificateId}", id);
        
                        // 直接使用传入的ID查找证书
                        CertificateDetails? certificate = await _certificateManagementService.GetCertificateDetailsAsync(id);
                        if (certificate == null && !force)
                        {
                            _logger.LogWarning("证书不存在: {CertificateId}", id);
                            return NotFound(new CertificateDeletionResult
                            {
                                Success = false,
                                Message = "证书不存在",
                                CertificateId = id,
                                DeletedAt = DateTime.UtcNow
                            });
                        }
        
                        CertificateDeletionResult result;
                        if (force)
                        {
                            result = await _certificateManagementService.ForceDeleteCertificateAsync(id, cancellationToken);
                        }
                        else
                        {
                            result = await _certificateManagementService.DeleteCertificateAsync(id, cancellationToken);
                        }
        
                        if (result.Success)
                        {
                            _logger.LogInformation("证书删除成功: {CertificateId}, Force: {Force}", id, force);
                            return Ok(result);
                        }
                        else
                        {
                            _logger.LogWarning("证书删除失败: {CertificateId} - {Message}", id, result.Message);
                            return BadRequest(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "删除证书时发生异常: {CertificateId}", id);
                        return StatusCode(500, new CertificateDeletionResult
                        {
                            Success = false,
                            Message = "服务器内部错误",
                            CertificateId = id,
                            DeletedAt = DateTime.UtcNow,
                            Errors = new List<string> { ex.Message }
                        });
                    }
                }
        /// <summary>
        /// 导入证书
        /// </summary>
        /// <param name="request">导入请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导入结果</returns>
        [HttpPost("import")]
        public async Task<ActionResult<CertificateImportResult>> ImportCertificate(
            [FromBody] CertificateImportRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("导入证书请求: {Name}", request.Name);

                var result = await _certificateManagementService.ImportCertificateAsync(request, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("证书导入成功: {Name} - {CertificateId}", request.Name, result.CertificateId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("证书导入失败: {Name} - {Message}", request.Name, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入证书时发生异常: {Name}", request.Name);
                return StatusCode(500, new CertificateImportResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    ImportedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 导出证书
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <param name="format">导出格式</param>
        /// <param name="includePrivateKey">是否包含私钥</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导出结果</returns>
        [HttpGet("{id}/export")]
        public async Task<ActionResult<CertificateExportResult>> ExportCertificate(
            string id,
            [FromQuery] string format = "pem",
            [FromQuery] bool includePrivateKey = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("导出证书请求: {CertificateId}, Format={Format}, IncludeKey={IncludeKey}",
                    id, format, includePrivateKey);

                var result = await _certificateManagementService.ExportCertificateAsync(
                    id, format, includePrivateKey, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("证书导出成功: {CertificateId}", id);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("证书导出失败: {CertificateId} - {Message}", id, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出证书时发生异常: {CertificateId}", id);
                return StatusCode(500, new CertificateExportResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    CertificateId = id,
                    Format = format,
                    ExportedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 验证证书
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        [HttpPost("{id}/validate")]
        public async Task<ActionResult<CertificateValidationResult>> ValidateCertificate(
            string id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("验证证书请求: {CertificateId}", id);

                var result = await _certificateManagementService.ValidateCertificateAsync(id, cancellationToken);

                _logger.LogInformation("证书验证完成: {CertificateId} - {Valid}", id, result.IsValid);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证证书时发生异常: {CertificateId}", id);
                return StatusCode(500, new CertificateValidationResult
                {
                    IsValid = false,
                    Message = "服务器内部错误",
                    CertificateId = id,
                    ValidatedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 获取证书使用统计
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>使用统计</returns>
        [HttpGet("{id}/statistics")]
        public async Task<ActionResult<CertificateUsageStatistics>> GetCertificateUsageStatistics(
            string id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书使用统计请求: {CertificateId}", id);

                var statistics = await _certificateManagementService.GetCertificateUsageStatisticsAsync(id, cancellationToken);

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书使用统计时发生异常: {CertificateId}", id);
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取证书操作历史
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <param name="operationType">操作类型过滤</param>
        /// <param name="limit">限制数量</param>
        /// <param name="offset">偏移量</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>操作历史</returns>
        [HttpGet("{id}/history")]
        public async Task<ActionResult<IEnumerable<CertificateOperationHistory>>> GetCertificateOperationHistory(
            string id,
            [FromQuery] string? operationType = null,
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书操作历史请求: {CertificateId}, Type={Type}", id, operationType);

                var history = await _certificateManagementService.GetCertificateOperationHistoryAsync(
                    id, operationType, limit, offset, cancellationToken);

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书操作历史时发生异常: {CertificateId}", id);
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 批量操作证书
        /// </summary>
        /// <param name="request">批量操作请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>批量操作结果</returns>
        [HttpPost("batch")]
        public async Task<ActionResult<CertificateBatchOperationResult>> BatchOperateCertificates(
            [FromBody] CertificateBatchOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("批量操作证书请求: 操作={Operation}, 数量={Count}",
                    request.Operation, request.CertificateIds.Count);

                var result = await _certificateManagementService.BatchOperateCertificatesAsync(request, cancellationToken);

                _logger.LogInformation("批量操作完成: {Operation} - 成功={Success}, 失败={Failed}",
                    request.Operation, result.SuccessfulOperations, result.FailedOperations);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量操作证书时发生异常");
                return StatusCode(500, new CertificateBatchOperationResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Operation = request.Operation,
                    BatchStartedAt = DateTime.UtcNow,
                    BatchCompletedAt = DateTime.UtcNow,
                    TotalCertificates = request.CertificateIds.Count,
                    BatchErrors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 获取证书列表统计信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>统计信息</returns>
        [HttpGet("statistics")]
        public async Task<ActionResult<CertificateListStatistics>> GetCertificateListStatistics(
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书列表统计信息请求");

                var statistics = await _certificateManagementService.GetCertificateListStatisticsAsync(cancellationToken);

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书列表统计信息时发生异常");
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 搜索证书
        /// </summary>
        /// <param name="searchTerm">搜索词</param>
        /// <param name="searchFields">搜索字段</param>
        /// <param name="pageIndex">页索引</param>
        /// <param name="pageSize">页大小</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>搜索结果</returns>
        [HttpGet("search")]
        public async Task<ActionResult<CertificateSearchResult>> SearchCertificates(
            [FromQuery] string searchTerm,
            [FromQuery] IEnumerable<string>? searchFields = null,
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("搜索证书请求: 搜索词={SearchTerm}", searchTerm);

                var result = await _certificateManagementService.SearchCertificatesAsync(
                    searchTerm, searchFields, pageIndex, pageSize, cancellationToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索证书时发生异常: {SearchTerm}", searchTerm);
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 下载证书文件
        /// </summary>
        /// <param name="id">证书ID</param>
        /// <param name="format">导出格式</param>
        /// <param name="includePrivateKey">是否包含私钥</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书 ZIP 包（包含 cert.pem、privkey.pem、fullchain.pem）</returns>
        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadCertificate(
            string id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("下载证书请求: {CertificateId}", id);

                var certificate = await _certificateManagementService.GetCertificateDetailsAsync(id, cancellationToken);
                if (certificate == null)
                {
                    _logger.LogWarning("证书不存在: {CertificateId}", id);
                    return NotFound(new { Message = "证书不存在" });
                }

                // 获取域名作为文件名前缀
                var domainName = certificate.Domains?.FirstOrDefault()?.Replace("*.", "wildcard_") ?? "certificate";
                var fileNamePrefix = $"{domainName}_{DateTime.UtcNow:yyyyMMdd}";

                // 创建 ZIP 包
                using var memoryStream = new MemoryStream();
                using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    // cert.pem - 证书
                    if (!string.IsNullOrEmpty(certificate.CertificateData))
                    {
                        var certEntry = archive.CreateEntry("cert.pem");
                        using var entryStream = certEntry.Open();
                        using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync(certificate.CertificateData);
                    }

                    // privkey.pem - 私钥
                    if (!string.IsNullOrEmpty(certificate.PrivateKeyData))
                    {
                        var keyEntry = archive.CreateEntry("privkey.pem");
                        using var entryStream = keyEntry.Open();
                        using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync(certificate.PrivateKeyData);
                    }

                    // fullchain.pem - 完整证书链
                    if (!string.IsNullOrEmpty(certificate.CertificateChain))
                    {
                        var chainEntry = archive.CreateEntry("fullchain.pem");
                        using var entryStream = chainEntry.Open();
                        using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync(certificate.CertificateChain);
                    }
                    else if (!string.IsNullOrEmpty(certificate.CertificateData))
                    {
                        // 如果没有单独的 chain，用 cert 作为 fullchain
                        var chainEntry = archive.CreateEntry("fullchain.pem");
                        using var entryStream = chainEntry.Open();
                        using var writer = new StreamWriter(entryStream);
                        await writer.WriteAsync(certificate.CertificateData);
                    }
                }

                memoryStream.Position = 0;
                var zipBytes = memoryStream.ToArray();
                var zipFileName = $"{fileNamePrefix}.zip";

                _logger.LogInformation("证书下载成功: {CertificateId}, 文件大小: {Size} bytes", id, zipBytes.Length);
                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "下载证书时发生异常: {CertificateId}", id);
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取证书状态摘要
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>状态摘要</returns>
        [HttpGet("summary")]
        public async Task<ActionResult<object>> GetCertificateSummary(
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书状态摘要请求");

                var statistics = await _certificateManagementService.GetCertificateListStatisticsAsync(cancellationToken);
                var expiringSoon = await _certificateManagementService.GetExpiringCertificatesAsync(7, cancellationToken);

                var summary = new
                {
                    TotalCertificates = statistics.TotalCertificates,
                    ActiveCertificates = statistics.ActiveCertificates,
                    ExpiredCertificates = statistics.ExpiredCertificates,
                    ExpiringIn7Days = expiringSoon.Count(),
                    ExpiringIn30Days = statistics.ExpiringNext30Days,
                    CertificatesWithAutoRenewal = statistics.CertificatesWithAutoRenewal,
                    WildcardCertificates = statistics.WildcardCertificates,
                    LastUpdated = DateTime.UtcNow,
                    Status = "healthy", // 可以根据实际情况计算
                    UpcomingRenewals = expiringSoon.Take(5).Select(x => new
                    {
                        x.CertificateId,
                        x.Domains,
                        x.ExpiresAt,
                        x.DaysUntilExpiry,
                        x.AutoRenewalEnabled
                    })
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书状态摘要时发生异常");
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }
    }
}