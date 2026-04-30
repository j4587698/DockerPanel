using Microsoft.AspNetCore.Mvc;
using DockerPanel.API.Services.Acme;
using DockerPanel.API.Models;
using DockerPanel.API.Models.Acme;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Controllers.Acme
{
    /// <summary>
    /// 通配符证书管理控制器
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class WildcardCertificateController : ControllerBase
    {
        private readonly IWildcardCertificateService _wildcardCertificateService;
        private readonly ILogger<WildcardCertificateController> _logger;

        public WildcardCertificateController(
            IWildcardCertificateService wildcardCertificateService,
            ILogger<WildcardCertificateController> logger)
        {
            _wildcardCertificateService = wildcardCertificateService;
            _logger = logger;
        }

        /// <summary>
        /// 申请通配符证书
        /// </summary>
        /// <param name="request">通配符证书申请请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>申请结果</returns>
        [HttpPost("request")]
        public async Task<ActionResult<WildcardCertificateResult>> RequestWildcardCertificate(
            [FromBody] WildcardCertificateRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("开始申请通配符证书: {Domains}", string.Join(", ", request.Domains));

                var result = await _wildcardCertificateService.RequestWildcardCertificateAsync(request, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("通配符证书申请成功: {CertificateId}", result.CertificateId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通配符证书申请失败: {Message}", result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "申请通配符证书时发生异常");
                return StatusCode(500, new WildcardCertificateResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    RequestedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 续期通配符证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>续期结果</returns>
        [HttpPost("{certificateId}/renew")]
        public async Task<ActionResult<WildcardCertificateResult>> RenewWildcardCertificate(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("开始续期通配符证书: {CertificateId}", certificateId);

                var result = await _wildcardCertificateService.RenewWildcardCertificateAsync(certificateId, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("通配符证书续期成功: {CertificateId}", certificateId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通配符证书续期失败: {CertificateId} - {Message}", certificateId, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "续期通配符证书时发生异常: {CertificateId}", certificateId);
                return StatusCode(500, new WildcardCertificateResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    CertificateId = certificateId,
                    RequestedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 验证通配符证书请求
        /// </summary>
        /// <param name="request">验证请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        [HttpPost("validate")]
        public async Task<ActionResult<WildcardCertificateValidationResult>> ValidateWildcardRequest(
            [FromBody] WildcardCertificateRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("开始验证通配符证书请求: {Domains}", string.Join(", ", request.Domains));

                var result = await _wildcardCertificateService.ValidateWildcardRequestAsync(request, cancellationToken);

                _logger.LogInformation("通配符证书请求验证完成: {Passed} - {Message}", result.Passed, result.Message);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证通配符证书请求时发生异常");
                return StatusCode(500, new WildcardCertificateValidationResult
                {
                    Passed = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    ValidatedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 配置通配符证书DNS挑战
        /// </summary>
        /// <param name="request">DNS挑战配置请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>配置结果</returns>
        [HttpPost("configure-dns-challenge")]
        public async Task<ActionResult<DnsChallengeConfigurationResult>> ConfigureWildcardDnsChallenge(
            [FromBody] WildcardDnsChallengeRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("开始配置通配符证书DNS挑战: {Domain} - {Provider}", request.Domain, request.DnsProvider);

                var result = await _wildcardCertificateService.ConfigureWildcardDnsChallengeAsync(request, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("通配符证书DNS挑战配置成功: {Domain}", request.Domain);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通配符证书DNS挑战配置失败: {Domain} - {Message}", request.Domain, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配置通配符证书DNS挑战时发生异常: {Domain}", request.Domain);
                return StatusCode(500, new DnsChallengeConfigurationResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    Domain = request.Domain,
                    ConfiguredAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 清理通配符证书DNS挑战
        /// </summary>
        /// <param name="request">清理请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>清理结果</returns>
        [HttpPost("cleanup-dns-challenge")]
        public async Task<ActionResult<DnsChallengeCleanupResult>> CleanupWildcardDnsChallenge(
            [FromBody] WildcardDnsChallengeCleanupRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("开始清理通配符证书DNS挑战: {Domain} - {Provider}", request.Domain, request.DnsProvider);

                var result = await _wildcardCertificateService.CleanupWildcardDnsChallengeAsync(request, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("通配符证书DNS挑战清理成功: {Domain}", request.Domain);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通配符证书DNS挑战清理失败: {Domain} - {Message}", request.Domain, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理通配符证书DNS挑战时发生异常: {Domain}", request.Domain);
                return StatusCode(500, new DnsChallengeCleanupResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    Domain = request.Domain,
                    CleanedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 获取通配符证书详情
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书详情</returns>
        [HttpGet("{certificateId}")]
        public async Task<ActionResult<WildcardCertificateDetails>> GetWildcardCertificateDetails(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取通配符证书详情: {CertificateId}", certificateId);

                var details = await _wildcardCertificateService.GetWildcardCertificateDetailsAsync(certificateId, cancellationToken);

                if (details != null)
                {
                    return Ok(details);
                }
                else
                {
                    _logger.LogWarning("未找到通配符证书: {CertificateId}", certificateId);
                    return NotFound(new { Message = "未找到指定的通配符证书" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取通配符证书详情时发生异常: {CertificateId}", certificateId);
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取通配符证书列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书列表</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<WildcardCertificateSummary>>> GetWildcardCertificates(
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取通配符证书列表");

                var certificates = await _wildcardCertificateService.GetWildcardCertificatesAsync(null, cancellationToken);

                return Ok(certificates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取通配符证书列表时发生异常");
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 删除通配符证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{certificateId}")]
        public async Task<ActionResult<WildcardCertificateDeletionResult>> DeleteWildcardCertificate(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("删除通配符证书: {CertificateId}", certificateId);

                var result = await _wildcardCertificateService.DeleteWildcardCertificateAsync(certificateId, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("通配符证书删除成功: {CertificateId}", certificateId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通配符证书删除失败: {CertificateId}", certificateId);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除通配符证书时发生异常: {CertificateId}", certificateId);
                return StatusCode(500, new WildcardCertificateDeletionResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    CertificateId = certificateId,
                    DeletedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 强制删除通配符证书（用于处理超时或异常状态的证书）
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除结果</returns>
        [HttpDelete("{certificateId}/force")]
        public async Task<ActionResult<WildcardCertificateDeletionResult>> ForceDeleteWildcardCertificate(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("强制删除通配符证书: {CertificateId}", certificateId);

                var result = await _wildcardCertificateService.ForceDeleteWildcardCertificateAsync(certificateId, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("通配符证书强制删除成功: {CertificateId}", certificateId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通配符证书强制删除失败: {CertificateId}", certificateId);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "强制删除通配符证书时发生异常: {CertificateId}", certificateId);
                return StatusCode(500, new WildcardCertificateDeletionResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    CertificateId = certificateId,
                    DeletedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// 导入通配符证书
        /// </summary>
        /// <param name="request">导入请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导入结果</returns>
        [HttpPost("import")]
        public async Task<ActionResult<WildcardCertificateImportResult>> ImportWildcardCertificate(
            [FromBody] WildcardCertificateImportRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("导入通配符证书: {Domains}", string.Join(", ", request.Domains));

                var result = await _wildcardCertificateService.ImportWildcardCertificateAsync(request, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("通配符证书导入成功: {CertificateId}", result.CertificateId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通配符证书导入失败: {Message}", result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入通配符证书时发生异常");
                return StatusCode(500, new WildcardCertificateImportResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    ImportedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 导出通配符证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="format">导出格式</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>导出结果</returns>
        [HttpGet("{certificateId}/export")]
        public async Task<ActionResult<WildcardCertificateExportResult>> ExportWildcardCertificate(
            string certificateId,
            [FromQuery] string format = "pem",
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("导出通配符证书: {CertificateId} - {Format}", certificateId, format);

                var result = await _wildcardCertificateService.ExportWildcardCertificateAsync(certificateId, format, true, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("通配符证书导出成功: {CertificateId}", certificateId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通配符证书导出失败: {CertificateId} - {Message}", certificateId, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出通配符证书时发生异常: {CertificateId}", certificateId);
                return StatusCode(500, new WildcardCertificateExportResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    ExportedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 验证通配符证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>验证结果</returns>
        [HttpPost("{certificateId}/validate")]
        public async Task<ActionResult<WildcardCertificateValidationResult>> ValidateWildcardCertificate(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("验证通配符证书: {CertificateId}", certificateId);

                var result = await _wildcardCertificateService.ValidateWildcardCertificateAsync(certificateId, cancellationToken);

                _logger.LogInformation("通配符证书验证完成: {CertificateId} - {Valid}", certificateId, result.ValidationStatus.IsValid);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证通配符证书时发生异常: {CertificateId}", certificateId);
                return StatusCode(500, new WildcardCertificateValidationResult
                {
                    IsValid = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    ValidatedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 测试通配符证书申请流程
        /// </summary>
        /// <param name="request">测试请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>测试结果</returns>
        [HttpPost("test")]
        public async Task<ActionResult<WildcardCertificateTestResult>> TestWildcardCertificateFlow(
            [FromBody] WildcardCertificateRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("测试通配符证书申请流程: {Domains}", string.Join(", ", request.Domains));

                var result = await _wildcardCertificateService.TestWildcardCertificateFlowAsync(request, cancellationToken);

                _logger.LogInformation("通配符证书申请流程测试完成: {Success} - {Message}", result.Success, result.Message);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试通配符证书申请流程时发生异常");
                return StatusCode(500, new WildcardCertificateTestResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    TestStartedAt = DateTime.UtcNow,
                    TestCompletedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 获取支持的DNS提供商列表
        /// </summary>
        /// <returns>DNS提供商列表</returns>
        [HttpGet("dns-providers")]
        public ActionResult<IEnumerable<DnsProviderInfo>> GetSupportedDnsProviders()
        {
            try
            {
                _logger.LogInformation("获取支持的DNS提供商列表");

                var providers = _wildcardCertificateService.GetSupportedDnsProviders();

                return Ok(providers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取DNS提供商列表时发生异常");
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 获取通配符证书统计信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>统计信息</returns>
        [HttpGet("statistics")]
        public async Task<ActionResult<WildcardCertificateStatistics>> GetWildcardCertificateStatistics(
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取通配符证书统计信息");

                var statistics = await _wildcardCertificateService.GetWildcardCertificateStatisticsAsync(cancellationToken);

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取通配符证书统计信息时发生异常");
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 批量操作通配符证书
        /// </summary>
        /// <param name="request">批量操作请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>批量操作结果</returns>
        [HttpPost("batch")]
        public async Task<ActionResult<WildcardCertificateBatchResult>> BatchOperationWildcardCertificates(
            [FromBody] WildcardCertificateBatchRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("批量操作通配符证书: {Operation} - {Count}", request.Operation, request.CertificateIds.Count);

                var result = await _wildcardCertificateService.BatchOperationWildcardCertificatesAsync(request, cancellationToken);

                _logger.LogInformation("通配符证书批量操作完成: {Success} - {Successful}/{Total}",
                    result.Success, result.SuccessCount, result.TotalCertificates);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量操作通配符证书时发生异常");
                return StatusCode(500, new WildcardCertificateBatchResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    BatchStartedAt = DateTime.UtcNow,
                    BatchCompletedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 检查通配符证书状态
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>证书状态</returns>
        [HttpGet("{certificateId}/status")]
        public async Task<ActionResult<WildcardCertificateStatus>> CheckWildcardCertificateStatus(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("检查通配符证书状态: {CertificateId}", certificateId);

                var status = await _wildcardCertificateService.CheckWildcardCertificateStatusAsync(certificateId, cancellationToken);

                if (status != null)
                {
                    return Ok(status);
                }
                else
                {
                    _logger.LogWarning("未找到通配符证书状态: {CertificateId}", certificateId);
                    return NotFound(new { Message = "未找到指定的通配符证书" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查通配符证书状态时发生异常: {CertificateId}", certificateId);
                return StatusCode(500, new { Message = "服务器内部错误", Error = ex.Message });
            }
        }

        /// <summary>
        /// 自动配置通配符证书挑战
        /// </summary>
        /// <param name="request">自动配置请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>配置结果</returns>
        [HttpPost("auto-configure-challenge")]
        public async Task<ActionResult<WildcardAutoChallengeResult>> AutoConfigureWildcardChallenge(
            [FromBody] WildcardAutoChallengeRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("自动配置通配符证书挑战: {Domain}", request.Domain);

                var result = await _wildcardCertificateService.AutoConfigureWildcardChallengeAsync(request, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("通配符证书挑战自动配置成功: {Domain}", request.Domain);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("通配符证书挑战自动配置失败: {Domain} - {Message}", request.Domain, result.Message);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动配置通配符证书挑战时发生异常: {Domain}", request.Domain);
                return StatusCode(500, new WildcardAutoChallengeResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Errors = new List<string> { ex.Message },
                    ConfiguredAt = DateTime.UtcNow
                });
            }
        }
    }
}