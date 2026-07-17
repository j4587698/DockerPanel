using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DockerPanel.API.Extensions;
using DockerPanel.API.Models.Acme;
using TinyDb;
using TinyDb.Attributes;
using TinyDb.Bson;
using TinyDb.Core;
using TinyDb.Collections;
using Microsoft.Extensions.Logging;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// 证书管理服务实现
    /// </summary>
    public class CertificateManagementService : ICertificateManagementService
    {
        private readonly IAcmeService _acmeService;
        private readonly IWildcardCertificateService _wildcardCertificateService;
        private readonly ICertificateAutoService _certificateAutoService;
        private readonly ILogger<CertificateManagementService> _logger;
        private readonly TinyDbEngine _database;
        private readonly ITinyCollection<CertificateRecord> _certificateCollection;
        private readonly ITinyCollection<CertificateOperationRecord> _operationCollection;
        private readonly ConcurrentDictionary<string, CertificateUsageStatistics> _usageStatistics;

        public CertificateManagementService(
            IAcmeService acmeService,
            IWildcardCertificateService wildcardCertificateService,
            ICertificateAutoService certificateAutoService,
            ILogger<CertificateManagementService> logger,
            TinyDbEngine database)
        {
            _acmeService = acmeService;
            _wildcardCertificateService = wildcardCertificateService;
            _certificateAutoService = certificateAutoService;
            _logger = logger;
            _database = database;
            _certificateCollection = database.GetCollection<CertificateRecord>(DbCollections.Certificates);
            _operationCollection = database.GetCollection<CertificateOperationRecord>(DbCollections.CertificateOperations);
            _usageStatistics = new ConcurrentDictionary<string, CertificateUsageStatistics>();

            // TinyDb 使用属性自动创建索引
        }

        public async Task<CertificateListResult> GetCertificatesAsync(
            bool includeExpired = false,
            string? certificateType = null,
            string? statusFilter = null,
            string? domainFilter = null,
            int pageIndex = 0,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书列表: IncludeExpired={IncludeExpired}, Type={Type}, Status={Status}, Domain={Domain}",
                    includeExpired, certificateType, statusFilter, domainFilter);

                var query = _certificateCollection.Query();

                // 应用过滤条件
                if (!includeExpired)
                {
                    query = query.Where(x => x.ExpiresAt > DateTime.UtcNow);
                }

                if (!string.IsNullOrEmpty(certificateType))
                {
                    query = query.Where(x => x.Type.Equals(certificateType, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    query = query.Where(x => x.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(domainFilter))
                {
                    query = query.Where(x => x.Domains.Any(d => d.Contains(domainFilter, StringComparison.OrdinalIgnoreCase)));
                }

                // 获取总数
                var totalCount = query.Count();

                // 分页查询
                var certificates = query
                    .OrderByDescending(x => x.CreatedAt)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .ToList();

                var certificateListItems = certificates.Select(ConvertToListItem).ToList();

                var result = new CertificateListResult
                {
                    Certificates = certificateListItems,
                    TotalCount = totalCount,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                };

                _logger.LogInformation("获取证书列表完成: 总数={TotalCount}, 当前页={Count}",
                    totalCount, certificateListItems.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书列表时发生异常");
                return new CertificateListResult
                {
                    Certificates = new List<CertificateListItem>(),
                    TotalCount = 0,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                };
            }
        }

        public async Task<CertificateDetails?> GetCertificateDetailsAsync(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书详情: {CertificateId}", certificateId);

                var certificateRecord = _certificateCollection
                    .FindAll().FirstOrDefault(c => c.Id.ToString() == certificateId);

                if (certificateRecord == null)
                {
                    _logger.LogWarning("未找到证书: {CertificateId}", certificateId);
                    return null;
                }

                var details = ConvertToDetails(certificateRecord);

                // 获取使用统计
                details.UsageStatistics = await GetCertificateUsageStatisticsAsync(certificateId, cancellationToken);

                _logger.LogInformation("获取证书详情完成: {CertificateId}", certificateId);
                return details;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书详情时发生异常: {CertificateId}", certificateId);
                return null;
            }
        }

        public async Task<IEnumerable<ExpiringCertificate>> GetExpiringCertificatesAsync(
            int daysBeforeExpiry = 15,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取即将到期的证书: 天数={Days}", daysBeforeExpiry);

                var expiryDate = DateTime.UtcNow.AddDays(daysBeforeExpiry);
                var expiringRecords = _certificateCollection
                    .Find(x => x.ExpiresAt <= expiryDate && x.ExpiresAt > DateTime.UtcNow && x.Status != "revoked")
                    .ToList();

                var expiringCertificates = expiringRecords.Select(record => new ExpiringCertificate
                {
                    CertificateId = record.Id.ToString(),
                    Domains = record.Domains,
                    ExpiresAt = record.ExpiresAt,
                    DaysUntilExpiry = (int)(record.ExpiresAt - DateTime.UtcNow).TotalDays,
                    AutoRenewalEnabled = record.AutoRenewalEnabled,
                    NextRenewalAttempt = record.NextRenewalAttempt,
                    Status = record.Status,
                    AccountId = record.AccountId,
                    NotificationEmails = record.NotificationEmails ?? new List<string>()
                }).ToList();

                _logger.LogInformation("获取即将到期证书完成: 数量={Count}", expiringCertificates.Count);
                return expiringCertificates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取即将到期证书时发生异常");
                return new List<ExpiringCertificate>();
            }
        }

        public async Task<CertificateRenewalResult> RenewCertificateAsync(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("续期证书: {CertificateId}", certificateId);

                var certificateRecord = _certificateCollection.FindAll().FirstOrDefault(c => c.Id.ToString() == certificateId);
                if (certificateRecord == null)
                {
                    return new CertificateRenewalResult
                    {
                        Success = false,
                        Message = "证书不存在",
                        CertificateId = certificateId,
                        RenewalStartedAt = DateTime.UtcNow,
                        Errors = new List<string> { "未找到指定的证书" }
                    };
                }

                // 记录操作开始
                await RecordOperationAsync(certificateId, "renewal", "开始续期", true);

                var renewalSteps = new List<string>();
                var errors = new List<string>();
                var warnings = new List<string>();

                try
                {
                    renewalSteps.Add("验证证书状态");

                    // 根据证书类型选择续期方式
                    CertificateRenewalResult renewalResult;
                    if (certificateRecord.Type == "wildcard")
                    {
                        renewalSteps.Add("使用通配符证书服务续期");
                        var wildcardResult = await _wildcardCertificateService.RenewWildcardCertificateAsync(certificateId, cancellationToken);
                        renewalResult = new CertificateRenewalResult
                        {
                            Success = wildcardResult.Success,
                            Message = wildcardResult.Message,
                            CertificateId = wildcardResult.CertificateId ?? string.Empty,
                            NewCertificateId = wildcardResult.CertificateId,
                            RenewalStartedAt = wildcardResult.RequestedAt,
                            RenewalCompletedAt = wildcardResult.CompletedAt,
                            RenewalSteps = wildcardResult.ValidationSteps,
                            Errors = wildcardResult.Errors,
                            Warnings = wildcardResult.Warnings,
                            RenewalDetails = wildcardResult.Metadata
                        };
                    }
                    else
                    {
                        renewalSteps.Add("使用自动证书服务续期");
                        var autoRenewalResult = await _certificateAutoService.AutoRenewCertificateAsync(certificateId, cancellationToken);
                        renewalResult = new CertificateRenewalResult
                        {
                            Success = autoRenewalResult.Success,
                            Message = autoRenewalResult.Message,
                            CertificateId = autoRenewalResult.CertificateId,
                            NewCertificateId = autoRenewalResult.NewCertificateId,
                            RenewalStartedAt = autoRenewalResult.RenewalStartedAt,
                            RenewalCompletedAt = autoRenewalResult.RenewalCompletedAt,
                            Errors = autoRenewalResult.Errors,
                            Warnings = autoRenewalResult.Warnings
                        };
                    }

                    renewalSteps.AddRange(renewalResult.RenewalSteps);
                    errors.AddRange(renewalResult.Errors);
                    warnings.AddRange(renewalResult.Warnings);

                    await RecordOperationAsync(certificateId, "renewal",
                        renewalResult.Success ? "续期成功" : "续期失败",
                        renewalResult.Success, renewalResult.Errors);

                    return renewalResult;
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    await RecordOperationAsync(certificateId, "renewal", "续期异常", false, new List<string> { ex.Message });

                    return new CertificateRenewalResult
                    {
                        Success = false,
                        Message = "续期过程中发生异常",
                        CertificateId = certificateId,
                        RenewalStartedAt = DateTime.UtcNow,
                        RenewalSteps = renewalSteps,
                        Errors = errors,
                        Warnings = warnings
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "续期证书时发生异常: {CertificateId}", certificateId);
                return new CertificateRenewalResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    CertificateId = certificateId,
                    RenewalStartedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<CertificateAutoRenewalConfigResult> EnableAutoRenewalAsync(
            string certificateId,
            AutoRenewalConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("启用证书自动续期: {CertificateId}", certificateId);

                var result = await _certificateAutoService.SetAutoRenewalConfigurationAsync(certificateId, configuration);

                await RecordOperationAsync(certificateId, "enable-auto-renewal",
                    result.Success ? "启用自动续期成功" : "启用自动续期失败",
                    result.Success, result.ValidationErrors);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启用证书自动续期时发生异常: {CertificateId}", certificateId);
                return new CertificateAutoRenewalConfigResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    CertificateId = certificateId,
                    ConfiguredAt = DateTime.UtcNow,
                    ValidationErrors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<bool> DisableAutoRenewalAsync(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("禁用证书自动续期: {CertificateId}", certificateId);

                var result = await _certificateAutoService.DisableAutoRenewalAsync(certificateId);

                await RecordOperationAsync(certificateId, "disable-auto-renewal",
                    result ? "禁用自动续期成功" : "禁用自动续期失败",
                    result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "禁用证书自动续期时发生异常: {CertificateId}", certificateId);
                return false;
            }
        }

        public async Task<CertificateDeletionResult> DeleteCertificateAsync(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("删除证书: {CertificateId}", certificateId);

                // 智能处理ID格式：支持UUID和ObjectId两种格式
                var certificateRecord = FindCertificateById(certificateId);
                if (certificateRecord == null)
                {
                    return new CertificateDeletionResult
                    {
                        Success = false,
                        Message = "证书不存在",
                        CertificateId = certificateId,
                        DeletedAt = DateTime.UtcNow,
                        Errors = new List<string> { "未找到指定的证书" }
                    };
                }

                var deletionSteps = new List<string>();
                var errors = new List<string>();
                var warnings = new List<string>();

                try
                {
                    deletionSteps.Add("检查证书使用情况");

                    // 检查证书是否在使用中
                    var usageStatistics = await GetCertificateUsageStatisticsAsync(certificateId, cancellationToken);
                    if (usageStatistics.UsedByServices.Any())
                    {
                        warnings.Add($"证书正在被 {usageStatistics.UsedByServices.Count} 个服务使用");
                        deletionSteps.Add("记录使用中的服务");
                    }

                    deletionSteps.Add("从数据库删除证书记录");
                    var certificateToDelete = FindCertificateById(certificateId);
                    var deleted = certificateToDelete != null && DeleteCertificateById(certificateId) > 0;

                    if (deleted)
                    {
                        deletionSteps.Add("清理相关操作历史");
                        _operationCollection.DeleteMany(x => x.CertificateId == certificateId);

                        deletionSteps.Add("清理使用统计");
                        _usageStatistics.TryRemove(certificateId, out _);

                        await RecordOperationAsync(certificateId, "deletion", "证书删除成功", true);

                        return new CertificateDeletionResult
                        {
                            Success = true,
                            Message = "证书删除成功",
                            CertificateId = certificateId,
                            DeletedAt = DateTime.UtcNow,
                            DeletionSteps = deletionSteps,
                            Warnings = warnings
                        };
                    }
                    else
                    {
                        errors.Add("数据库删除操作失败");
                        return new CertificateDeletionResult
                        {
                            Success = false,
                            Message = "删除证书失败",
                            CertificateId = certificateId,
                            DeletedAt = DateTime.UtcNow,
                            DeletionSteps = deletionSteps,
                            Errors = errors
                        };
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    return new CertificateDeletionResult
                    {
                        Success = false,
                        Message = "删除过程中发生异常",
                        CertificateId = certificateId,
                        DeletedAt = DateTime.UtcNow,
                        DeletionSteps = deletionSteps,
                        Errors = errors
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除证书时发生异常: {CertificateId}", certificateId);
                return new CertificateDeletionResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    CertificateId = certificateId,
                    DeletedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        /// <summary>
        /// 强制删除证书（用于处理超时或异常状态的证书）
        /// </summary>
        public async Task<CertificateDeletionResult> ForceDeleteCertificateAsync(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("强制删除证书: {CertificateId}", certificateId);

                var deletionSteps = new List<string>();
                var errors = new List<string>();
                var warnings = new List<string>();

                // 强制删除，跳过大部分检查
                deletionSteps.Add("强制模式：跳过使用检查");

                try
                {
                    // 尝试从数据库删除，不管是否存在
                    var certificateToDelete = FindCertificateById(certificateId);
                    var deleted = certificateToDelete != null && DeleteCertificateById(certificateId) > 0;

                    if (deleted || certificateToDelete == null)
                    {
                        deletionSteps.Add(certificateToDelete != null ? "从数据库删除证书记录" : "证书记录不存在");

                        // 强制清理所有相关数据
                        deletionSteps.Add("强制清理相关操作历史");
                        _operationCollection.DeleteMany(x => x.CertificateId == certificateId);

                        deletionSteps.Add("强制清理使用统计");
                        _usageStatistics.TryRemove(certificateId, out _);

                        // 记录强制删除操作
                        await RecordOperationAsync(certificateId, "force_deletion", "证书强制删除成功", true);

                        return new CertificateDeletionResult
                        {
                            Success = true,
                            Message = certificateToDelete != null ? "证书强制删除成功" : "证书不存在（已确认删除）",
                            CertificateId = certificateId,
                            DeletedAt = DateTime.UtcNow,
                            DeletionSteps = deletionSteps,
                            Warnings = warnings,
                            DeletionDetails = new Dictionary<string, object>
                            {
                                ["ForceDeleted"] = true,
                                ["CertificateExisted"] = certificateToDelete != null
                            }
                        };
                    }
                    else
                    {
                        errors.Add("强制删除失败");
                        return new CertificateDeletionResult
                        {
                            Success = false,
                            Message = "强制删除失败",
                            CertificateId = certificateId,
                            DeletedAt = DateTime.UtcNow,
                            DeletionSteps = deletionSteps,
                            Errors = errors
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "强制删除证书时发生异常: {CertificateId}", certificateId);
                    errors.Add($"强制删除异常: {ex.Message}");

                    return new CertificateDeletionResult
                    {
                        Success = false,
                        Message = "强制删除过程中发生异常",
                        CertificateId = certificateId,
                        DeletedAt = DateTime.UtcNow,
                        DeletionSteps = deletionSteps,
                        Errors = errors
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "强制删除证书时发生系统异常: {CertificateId}", certificateId);
                return new CertificateDeletionResult
                {
                    Success = false,
                    Message = "系统内部错误",
                    CertificateId = certificateId,
                    DeletedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message },
                    DeletionDetails = new Dictionary<string, object>
                    {
                        ["ForceDeletion"] = true,
                        ["SystemError"] = true
                    }
                };
            }
        }

        public async Task<CertificateImportResult> ImportCertificateAsync(
            CertificateImportRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("导入证书: {Name}", request.Name);

                var importSteps = new List<string>();
                var errors = new List<string>();
                var warnings = new List<string>();

                try
                {
                    importSteps.Add("验证证书格式");

                    // 解析证书
                    X509Certificate2 certificate;
                    try
                    {
                        if (request.Format.Equals("pfx", StringComparison.OrdinalIgnoreCase))
                        {
                            var certBytes = Convert.FromBase64String(request.CertificateData);
                            var passwordBytes = System.Text.Encoding.UTF8.GetBytes(request.Password ?? "");
                            certificate = X509CertificateLoader.LoadPkcs12(certBytes, request.Password, X509KeyStorageFlags.Exportable);
                        }
                        else
                        {
                            certificate = LoadCertificateFromStoredData(request.CertificateData);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"证书解析失败: {ex.Message}");
                        return new CertificateImportResult
                        {
                            Success = false,
                            Message = "证书格式无效",
                            ImportedAt = DateTime.UtcNow,
                            ImportSteps = importSteps,
                            Errors = errors
                        };
                    }

                    importSteps.Add("提取证书信息");

                    // 提取证书信息
                    var domains = new List<string>();
                    if (!string.IsNullOrEmpty(certificate.Subject))
                    {
                        var cnMatch = System.Text.RegularExpressions.Regex.Match(certificate.Subject, @"CN=([^,]+)");
                        if (cnMatch.Success)
                        {
                            domains.Add(cnMatch.Groups[1].Value);
                        }
                    }

                    // 添加SAN域名
                    var sanExtensions = certificate.Extensions
                        .OfType<X509SubjectAlternativeNameExtension>()
                        .FirstOrDefault();
                    if (sanExtensions != null)
                    {
                        // 使用正确的API获取SAN名称
                        var sanNames = new List<string>();
                        foreach (var name in sanExtensions.EnumerateDnsNames())
                        {
                            sanNames.Add(name);
                        }
                        domains.AddRange(sanNames);
                    }

                    if (!domains.Any())
                    {
                        domains = request.Domains;
                    }

                    // 创建证书记录
                    var certificateRecord = new CertificateRecord
                    {
                        Id = ObjectId.NewObjectId().ToString(),
                        Name = request.Name,
                        Type = DetermineCertificateType(domains),
                        Domains = domains.Distinct().ToList(),
                        Status = certificate.NotAfter > DateTime.UtcNow ? "active" : "expired",
                        IssuedAt = certificate.NotBefore,
                        ExpiresAt = certificate.NotAfter,
                        Issuer = certificate.Issuer,
                        CertificateData = request.CertificateData,
                        PrivateKeyData = request.PrivateKeyData,
                        CertificateChain = request.CertificateChain ?? string.Empty,
                        KeyAlgorithm = certificate.PublicKey.Oid.FriendlyName ?? "Unknown",
                        KeySize = GetKeySize(certificate),
                        SignatureAlgorithm = certificate.SignatureAlgorithm.FriendlyName ?? "Unknown",
                        SerialNumber = certificate.SerialNumber ?? string.Empty,
                        Fingerprint = certificate.Thumbprint ?? string.Empty,
                        AccountId = "imported",
                        AutoRenewalEnabled = request.EnableAutoRenewal,
                        Metadata = request.Metadata,
                        Tags = request.Tags,
                        NotificationEmails = new List<string>(),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    importSteps.Add("保存证书到数据库");
                    _certificateCollection.Insert(certificateRecord);

                    // 启用自动续期（如果需要）
                    if (request.EnableAutoRenewal && request.AutoRenewalConfiguration != null)
                    {
                        importSteps.Add("配置自动续期");
                        await _certificateAutoService.SetAutoRenewalConfigurationAsync(
                            certificateRecord.Id.ToString(),
                            request.AutoRenewalConfiguration);
                    }

                    await RecordOperationAsync(certificateRecord.Id.ToString(), "import", "证书导入成功", true);

                    var importedCertificate = ConvertToDetails(certificateRecord);

                    return new CertificateImportResult
                    {
                        Success = true,
                        Message = "证书导入成功",
                        CertificateId = certificateRecord.Id.ToString(),
                        ImportedAt = DateTime.UtcNow,
                        ImportSteps = importSteps,
                        Warnings = warnings,
                        ImportedCertificate = importedCertificate
                    };
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    return new CertificateImportResult
                    {
                        Success = false,
                        Message = "导入过程中发生异常",
                        ImportedAt = DateTime.UtcNow,
                        ImportSteps = importSteps,
                        Errors = errors
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入证书时发生异常: {Name}", request.Name);
                return new CertificateImportResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    ImportedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<CertificateExportResult> ExportCertificateAsync(
            string certificateId,
            string format = "pem",
            bool includePrivateKey = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("导出证书: {CertificateId}, Format={Format}, IncludeKey={IncludeKey}",
                    certificateId, format, includePrivateKey);

                var certificateRecord = _certificateCollection.FindAll().FirstOrDefault(c => c.Id.ToString() == certificateId);
                if (certificateRecord == null)
                {
                    return new CertificateExportResult
                    {
                        Success = false,
                        Message = "证书不存在",
                        CertificateId = certificateId,
                        Format = format,
                        ExportedAt = DateTime.UtcNow,
                        Errors = new List<string> { "未找到指定的证书" }
                    };
                }

                var exportSteps = new List<string>();
                var errors = new List<string>();
                var warnings = new List<string>();

                try
                {
                    exportSteps.Add("准备证书数据");

                    string exportedData;
                    switch (format.ToLowerInvariant())
                    {
                        case "pem":
                            exportedData = ExportToPem(certificateRecord, includePrivateKey);
                            break;
                        case "der":
                            exportedData = ExportToDer(certificateRecord, includePrivateKey);
                            break;
                        case "pfx":
                            if (string.IsNullOrEmpty(certificateRecord.PrivateKeyData))
                            {
                                errors.Add("导出PFX格式需要私钥，但该证书没有私钥");
                                return new CertificateExportResult
                                {
                                    Success = false,
                                    Message = "缺少私钥",
                                    CertificateId = certificateId,
                                    Format = format,
                                    ExportedAt = DateTime.UtcNow,
                                    ExportSteps = exportSteps,
                                    Errors = errors
                                };
                            }
                            exportedData = await ExportToPfx(certificateRecord, cancellationToken);
                            break;
                        default:
                            errors.Add($"不支持的导出格式: {format}");
                            return new CertificateExportResult
                            {
                                Success = false,
                                Message = "格式不支持",
                                CertificateId = certificateId,
                                Format = format,
                                ExportedAt = DateTime.UtcNow,
                                ExportSteps = exportSteps,
                                Errors = errors
                            };
                    }

                    exportSteps.Add("证书数据准备完成");

                    await RecordOperationAsync(certificateId, "export", "证书导出成功", true);

                    return new CertificateExportResult
                    {
                        Success = true,
                        Message = "证书导出成功",
                        CertificateId = certificateId,
                        Format = format,
                        ExportedData = exportedData,
                        ExportedAt = DateTime.UtcNow,
                        ExportSteps = exportSteps,
                        Warnings = warnings
                    };
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    return new CertificateExportResult
                    {
                        Success = false,
                        Message = "导出过程中发生异常",
                        CertificateId = certificateId,
                        Format = format,
                        ExportedAt = DateTime.UtcNow,
                        ExportSteps = exportSteps,
                        Errors = errors
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出证书时发生异常: {CertificateId}", certificateId);
                return new CertificateExportResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    CertificateId = certificateId,
                    Format = format,
                    ExportedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<CertificateValidationResult> ValidateCertificateAsync(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("验证证书: {CertificateId}", certificateId);

                var certificateRecord = _certificateCollection.FindAll().FirstOrDefault(c => c.Id.ToString() == certificateId);
                if (certificateRecord == null)
                {
                    return new CertificateValidationResult
                    {
                        IsValid = false,
                        Message = "证书不存在",
                        CertificateId = certificateId,
                        ValidatedAt = DateTime.UtcNow,
                        Errors = new List<string> { "未找到指定的证书" }
                    };
                }

                var validationChecks = new List<ValidationCheck>();
                var errors = new List<string>();
                var warnings = new List<string>();

                try
                {
                    // 解析证书
                    using var certificate = LoadCertificateFromStoredData(certificateRecord.CertificateData);

                    // 检查证书是否过期
                    var expiryCheck = new ValidationCheck
                    {
                        Name = "证书有效期",
                        Passed = DateTime.UtcNow <= certificate.NotAfter,
                        Message = DateTime.UtcNow <= certificate.NotAfter ? "证书未过期" : "证书已过期",
                        Details = $"有效期: {certificate.NotBefore} 至 {certificate.NotAfter}",
                        CheckedAt = DateTime.UtcNow
                    };
                    validationChecks.Add(expiryCheck);

                    // 检查证书是否被吊销
                    var revocationCheck = new ValidationCheck
                    {
                        Name = "证书吊销状态",
                        Passed = certificateRecord.Status != "revoked",
                        Message = certificateRecord.Status != "revoked" ? "证书未被吊销" : "证书已被吊销",
                        Details = certificateRecord.Status == "revoked" ? certificateRecord.RevocationReason : null,
                        CheckedAt = DateTime.UtcNow
                    };
                    validationChecks.Add(revocationCheck);

                    // 检查域名匹配
                    var domainCheck = new ValidationCheck
                    {
                        Name = "域名验证",
                        Passed = certificateRecord.Domains.Any(),
                        Message = certificateRecord.Domains.Any() ? "域名配置正确" : "没有配置域名",
                        Details = string.Join(", ", certificateRecord.Domains),
                        CheckedAt = DateTime.UtcNow
                    };
                    validationChecks.Add(domainCheck);

                    // 检查私钥匹配（如果有私钥）
                    if (!string.IsNullOrEmpty(certificateRecord.PrivateKeyData))
                    {
                        var privateKeyValidation = ValidatePrivateKeyMatchesCertificate(
                            certificateRecord.CertificateData,
                            certificateRecord.PrivateKeyData);

                        var keyCheck = new ValidationCheck
                        {
                            Name = "私钥匹配",
                            Passed = privateKeyValidation.Passed,
                            Message = privateKeyValidation.Message,
                            Details = privateKeyValidation.Details,
                            CheckedAt = DateTime.UtcNow
                        };
                        validationChecks.Add(keyCheck);

                        if (!privateKeyValidation.Passed)
                        {
                            errors.Add(privateKeyValidation.Message);
                        }
                    }

                    var isValid = validationChecks.All(x => x.Passed);

                    await RecordOperationAsync(certificateId, "validation",
                        isValid ? "证书验证通过" : "证书验证失败",
                        isValid, errors);

                    return new CertificateValidationResult
                    {
                        IsValid = isValid,
                        Message = isValid ? "证书验证通过" : "证书验证失败",
                        CertificateId = certificateId,
                        ValidatedAt = DateTime.UtcNow,
                        ValidationChecks = validationChecks,
                        Errors = errors,
                        Warnings = warnings
                    };
                }
                catch (Exception ex)
                {
                    errors.Add($"证书解析失败: {ex.Message}");
                    return new CertificateValidationResult
                    {
                        IsValid = false,
                        Message = "证书验证失败",
                        CertificateId = certificateId,
                        ValidatedAt = DateTime.UtcNow,
                        ValidationChecks = validationChecks,
                        Errors = errors
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证证书时发生异常: {CertificateId}", certificateId);
                return new CertificateValidationResult
                {
                    IsValid = false,
                    Message = "服务器内部错误",
                    CertificateId = certificateId,
                    ValidatedAt = DateTime.UtcNow,
                    Errors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<CertificateUsageStatistics> GetCertificateUsageStatisticsAsync(
            string certificateId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书使用统计: {CertificateId}", certificateId);

                // 从内存缓存获取或创建统计数据
                var statistics = _usageStatistics.GetOrAdd(certificateId, id => new CertificateUsageStatistics
                {
                    CertificateId = id,
                    TotalRequests = 0,
                    SuccessfulRequests = 0,
                    FailedRequests = 0,
                    UsedByServices = new List<string>(),
                    UsedByDomains = new List<string>(),
                    RequestCountsByDay = new Dictionary<string, int>(),
                    RequestCountsByHour = new Dictionary<string, int>(),
                    StatisticsGeneratedAt = DateTime.UtcNow
                });

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书使用统计时发生异常: {CertificateId}", certificateId);
                return new CertificateUsageStatistics
                {
                    CertificateId = certificateId,
                    StatisticsGeneratedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<IEnumerable<CertificateOperationHistory>> GetCertificateOperationHistoryAsync(
            string certificateId,
            string? operationType = null,
            int limit = 50,
            int offset = 0,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书操作历史: {CertificateId}, Type={Type}", certificateId, operationType);

                var query = _operationCollection.Query()
                    .Where(x => x.CertificateId == certificateId);

                if (!string.IsNullOrEmpty(operationType))
                {
                    query = query.Where(x => x.OperationType.Equals(operationType, StringComparison.OrdinalIgnoreCase));
                }

                var operations = query
                    .OrderByDescending(x => x.OperatedAt)
                    .Skip(offset)
                    .Take(limit)
                    .ToList();

                var history = operations.Select(op => new CertificateOperationHistory
                {
                    Id = op.Id.ToString(),
                    CertificateId = op.CertificateId,
                    OperationType = op.OperationType,
                    Operation = op.Operation,
                    OperatedAt = op.OperatedAt,
                    Operator = op.Operator,
                    Success = op.Success,
                    Message = op.Message,
                    ErrorMessage = op.ErrorMessage,
                    OperationDetails = op.OperationDetails,
                    Duration = op.Duration
                }).ToList();

                _logger.LogInformation("获取证书操作历史完成: {CertificateId}, 数量={Count}", certificateId, history.Count);
                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书操作历史时发生异常: {CertificateId}", certificateId);
                return new List<CertificateOperationHistory>();
            }
        }

        public async Task<CertificateBatchOperationResult> BatchOperateCertificatesAsync(
            CertificateBatchOperationRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("批量操作证书: 操作={Operation}, 数量={Count}",
                    request.Operation, request.CertificateIds.Count);

                var batchStartedAt = DateTime.UtcNow;
                var operationResults = new List<CertificateOperationResult>();
                var batchErrors = new List<string>();
                var batchWarnings = new List<string>();

                var successfulOperations = 0;
                var failedOperations = 0;
                var skippedOperations = 0;

                foreach (var certificateId in request.CertificateIds)
                {
                    try
                    {
                        var operationStart = DateTime.UtcNow;
                        CertificateOperationResult result;

                        switch (request.Operation.ToLowerInvariant())
                        {
                            case "renew":
                                var renewalResult = await RenewCertificateAsync(certificateId, cancellationToken);
                                result = new CertificateOperationResult
                                {
                                    CertificateId = certificateId,
                                    Success = renewalResult.Success,
                                    Message = renewalResult.Message,
                                    ErrorMessage = renewalResult.Errors.FirstOrDefault(),
                                    OperatedAt = operationStart,
                                    Duration = DateTime.UtcNow - operationStart
                                };
                                break;

                            case "delete":
                                var deletionResult = await DeleteCertificateAsync(certificateId, cancellationToken);
                                result = new CertificateOperationResult
                                {
                                    CertificateId = certificateId,
                                    Success = deletionResult.Success,
                                    Message = deletionResult.Message,
                                    ErrorMessage = deletionResult.Errors.FirstOrDefault(),
                                    OperatedAt = operationStart,
                                    Duration = DateTime.UtcNow - operationStart
                                };
                                break;

                            case "enable-auto-renewal":
                                var config = request.OperationParameters.GetValueOrDefault("configuration") as AutoRenewalConfiguration
                                    ?? new AutoRenewalConfiguration();
                                var enableResult = await EnableAutoRenewalAsync(certificateId, config, cancellationToken);
                                result = new CertificateOperationResult
                                {
                                    CertificateId = certificateId,
                                    Success = enableResult.Success,
                                    Message = enableResult.Message,
                                    ErrorMessage = enableResult.ValidationErrors.FirstOrDefault(),
                                    OperatedAt = operationStart,
                                    Duration = DateTime.UtcNow - operationStart
                                };
                                break;

                            case "disable-auto-renewal":
                                var disableResult = await DisableAutoRenewalAsync(certificateId, cancellationToken);
                                result = new CertificateOperationResult
                                {
                                    CertificateId = certificateId,
                                    Success = disableResult,
                                    Message = disableResult ? "自动续期禁用成功" : "自动续期禁用失败",
                                    OperatedAt = operationStart,
                                    Duration = DateTime.UtcNow - operationStart
                                };
                                break;

                            default:
                                result = new CertificateOperationResult
                                {
                                    CertificateId = certificateId,
                                    Success = false,
                                    Message = "不支持的操作类型",
                                    OperatedAt = operationStart,
                                    Duration = DateTime.UtcNow - operationStart
                                };
                                break;
                        }

                        operationResults.Add(result);

                        if (result.Success)
                            successfulOperations++;
                        else
                            failedOperations++;
                    }
                    catch (Exception ex)
                    {
                        failedOperations++;
                        if (!request.ContinueOnError)
                        {
                            batchErrors.Add($"批量操作中断: {ex.Message}");
                            break;
                        }

                        operationResults.Add(new CertificateOperationResult
                        {
                            CertificateId = certificateId,
                            Success = false,
                            Message = "操作异常",
                            ErrorMessage = ex.Message,
                            OperatedAt = DateTime.UtcNow,
                            Duration = TimeSpan.Zero
                        });
                    }
                }

                var batchCompletedAt = DateTime.UtcNow;
                var totalDuration = batchCompletedAt - batchStartedAt;

                await RecordOperationAsync("batch", request.Operation,
                    $"批量操作完成: 成功={successfulOperations}, 失败={failedOperations}",
                    failedOperations == 0);

                return new CertificateBatchOperationResult
                {
                    Success = failedOperations == 0,
                    Message = failedOperations == 0 ? "批量操作全部成功" : $"批量操作部分失败: {failedOperations} 个失败",
                    Operation = request.Operation,
                    BatchStartedAt = batchStartedAt,
                    BatchCompletedAt = batchCompletedAt,
                    TotalCertificates = request.CertificateIds.Count,
                    SuccessfulOperations = successfulOperations,
                    FailedOperations = failedOperations,
                    SkippedOperations = skippedOperations,
                    OperationResults = operationResults,
                    BatchErrors = batchErrors,
                    BatchWarnings = batchWarnings,
                    TotalDuration = totalDuration,
                    BatchStatistics = new Dictionary<string, object>
                    {
                        ["SuccessRate"] = request.CertificateIds.Count > 0 ? (double)successfulOperations / request.CertificateIds.Count * 100 : 0,
                        ["AverageDuration"] = operationResults.Any() ? operationResults.Average(r => r.Duration.TotalMilliseconds) : 0
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量操作证书时发生异常");
                return new CertificateBatchOperationResult
                {
                    Success = false,
                    Message = "服务器内部错误",
                    Operation = request.Operation,
                    BatchStartedAt = DateTime.UtcNow,
                    BatchCompletedAt = DateTime.UtcNow,
                    TotalCertificates = request.CertificateIds.Count,
                    BatchErrors = new List<string> { ex.Message }
                };
            }
        }

        public async Task<CertificateListStatistics> GetCertificateListStatisticsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("获取证书列表统计信息");

                var allCertificates = _certificateCollection.FindAll().ToList();
                var now = DateTime.UtcNow;

                var statistics = new CertificateListStatistics
                {
                    TotalCertificates = allCertificates.Count,
                    ActiveCertificates = allCertificates.Count(x => x.Status == "active" && x.ExpiresAt > now),
                    ExpiredCertificates = allCertificates.Count(x => x.ExpiresAt <= now),
                    RevokedCertificates = allCertificates.Count(x => x.Status == "revoked"),
                    PendingCertificates = allCertificates.Count(x => x.Status == "pending"),
                    WildcardCertificates = allCertificates.Count(x => x.Type == "wildcard"),
                    SingleDomainCertificates = allCertificates.Count(x => x.Type == "single"),
                    MultiDomainCertificates = allCertificates.Count(x => x.Type == "multi-domain"),
                    CertificatesWithAutoRenewal = allCertificates.Count(x => x.AutoRenewalEnabled),
                    ExpiringNext7Days = allCertificates.Count(x => x.ExpiresAt <= now.AddDays(7) && x.ExpiresAt > now),
                    ExpiringNext30Days = allCertificates.Count(x => x.ExpiresAt <= now.AddDays(30) && x.ExpiresAt > now),
                    ExpiringNext90Days = allCertificates.Count(x => x.ExpiresAt <= now.AddDays(90) && x.ExpiresAt > now),
                    CertificatesByIssuer = allCertificates.GroupBy(x => x.Issuer)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    CertificatesByAccount = allCertificates.GroupBy(x => x.AccountId)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    CertificatesByStatus = allCertificates.GroupBy(x => x.Status)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    StatisticsGeneratedAt = now
                };

                _logger.LogInformation("获取证书列表统计信息完成: 总数={Total}", statistics.TotalCertificates);
                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取证书列表统计信息时发生异常");
                return new CertificateListStatistics
                {
                    StatisticsGeneratedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<CertificateSearchResult> SearchCertificatesAsync(
            string searchTerm,
            IEnumerable<string>? searchFields = null,
            int pageIndex = 0,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("搜索证书: 搜索词={SearchTerm}, 字段={Fields}", searchTerm,
                    searchFields != null ? string.Join(",", searchFields) : "全部");

                var fields = searchFields?.ToList() ?? new List<string> { "name", "domains", "issuer", "status", "type" };

                // 构建搜索条件
                var searchConditions = new List<Func<CertificateRecord, bool>>();

                foreach (var field in fields)
                {
                    switch (field.ToLowerInvariant())
                    {
                        case "name":
                            searchConditions.Add(x => x.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                            break;
                        case "domains":
                            searchConditions.Add(x => x.Domains.Any(d => d.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
                            break;
                        case "issuer":
                            searchConditions.Add(x => x.Issuer.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                            break;
                        case "status":
                            searchConditions.Add(x => x.Status.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                            break;
                        case "type":
                            searchConditions.Add(x => x.Type.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                            break;
                    }
                }

                var matchedCertificates = _certificateCollection.FindAll()
                    .Where(certificate => searchConditions.Count == 0 || searchConditions.Any(condition => condition(certificate)))
                    .ToList();

                var totalCount = matchedCertificates.Count;
                var certificates = matchedCertificates
                    .OrderByDescending(x => x.CreatedAt)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .ToList();

                var results = certificates.Select(ConvertToListItem).ToList();

                // 生成搜索高亮和建议
                var searchHighlights = new Dictionary<string, int>();
                var suggestedSearchTerms = new List<string>();

                foreach (var certificate in certificates)
                {
                    if (certificate.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        searchHighlights["name"] = searchHighlights.GetValueOrDefault("name", 0) + 1;

                    if (certificate.Domains.Any(d => d.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                        searchHighlights["domains"] = searchHighlights.GetValueOrDefault("domains", 0) + 1;
                }

                // 简单的建议词生成
                if (totalCount == 0 && searchTerm.Length > 2)
                {
                    var allCertificates = _certificateCollection.FindAll().ToList();
                    var suggestions = allCertificates
                        .Where(x => x.Name.Contains(searchTerm.Substring(0, 2), StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.Name)
                        .Distinct()
                        .Take(5);
                    suggestedSearchTerms.AddRange(suggestions);
                }

                var searchResult = new CertificateSearchResult
                {
                    Results = results,
                    TotalCount = totalCount,
                    SearchTerm = searchTerm,
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                    SearchHighlights = searchHighlights,
                    SuggestedSearchTerms = suggestedSearchTerms
                };

                _logger.LogInformation("搜索证书完成: 搜索词={SearchTerm}, 结果数={Count}", searchTerm, results.Count);
                return searchResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索证书时发生异常: {SearchTerm}", searchTerm);
                return new CertificateSearchResult
                {
                    Results = new List<CertificateListItem>(),
                    TotalCount = 0,
                    SearchTerm = searchTerm,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                };
            }
        }

        #region Private Helper Methods

        private CertificateListItem ConvertToListItem(CertificateRecord record)
        {
            return new CertificateListItem
            {
                Id = record.Id.ToString(),
                Name = record.Name,
                Type = record.Type,
                Domains = record.Domains,
                Status = record.Status,
                IssuedAt = record.IssuedAt,
                ExpiresAt = record.ExpiresAt,
                AutoRenewalEnabled = record.AutoRenewalEnabled,
                NextRenewalAttempt = record.NextRenewalAttempt,
                Issuer = record.Issuer,
                CertificateId = record.CertificateId,
                Metadata = record.Metadata,
                Tags = record.Tags
            };
        }

        private CertificateDetails ConvertToDetails(CertificateRecord record)
        {
            return new CertificateDetails
            {
                Id = record.Id.ToString(),
                Name = record.Name,
                Type = record.Type,
                Domains = record.Domains,
                Status = record.Status,
                IssuedAt = record.IssuedAt,
                ExpiresAt = record.ExpiresAt,
                RevokedAt = record.RevokedAt,
                RevocationReason = record.RevocationReason,
                Issuer = record.Issuer,
                CertificateData = record.CertificateData,
                PrivateKeyData = record.PrivateKeyData,
                CertificateChain = record.CertificateChain,
                SubjectAlternativeNames = ExtractSubjectAlternativeNames(record),
                KeyAlgorithm = record.KeyAlgorithm,
                KeySize = record.KeySize,
                SignatureAlgorithm = record.SignatureAlgorithm,
                SerialNumber = record.SerialNumber,
                Fingerprint = record.Fingerprint,
                AccountId = record.AccountId,
                OrderId = record.OrderId,
                AutoRenewalEnabled = record.AutoRenewalEnabled,
                AutoRenewalConfiguration = record.AutoRenewalConfiguration,
                NextRenewalAttempt = record.NextRenewalAttempt,
                Metadata = record.Metadata,
                Tags = record.Tags,
                UsageStatistics = new CertificateUsageStatistics(),
                UsedBy = new List<string>()
            };
        }

        private string DetermineCertificateType(List<string> domains)
        {
            if (domains.Count == 1)
            {
                return domains[0].StartsWith("*.") ? "wildcard" : "single";
            }
            return domains.Any(d => d.StartsWith("*.")) ? "wildcard" : "multi-domain";
        }

        private string ExportToPem(CertificateRecord record, bool includePrivateKey)
        {
            var sb = new StringBuilder();
            sb.AppendLine(NormalizeCertificatePem(record.CertificateData));

            if (includePrivateKey && !string.IsNullOrEmpty(record.PrivateKeyData))
            {
                sb.AppendLine();
                sb.AppendLine(NormalizePrivateKeyPem(record.PrivateKeyData));
            }

            if (!string.IsNullOrEmpty(record.CertificateChain)
                && !string.Equals(record.CertificateChain.Trim(), record.CertificateData.Trim(), StringComparison.Ordinal))
            {
                sb.AppendLine();
                sb.AppendLine(NormalizeCertificatePem(record.CertificateChain));
            }

            return sb.ToString();
        }

        private string ExportToDer(CertificateRecord record, bool includePrivateKey)
        {
            using var certificate = LoadCertificateFromStoredData(record.CertificateData);
            return Convert.ToBase64String(certificate.RawData);
        }

        private async Task<string> ExportToPfx(CertificateRecord record, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(record.PrivateKeyData))
                {
                    throw new InvalidOperationException("导出 PFX 需要私钥");
                }

                using var certificate = X509Certificate2.CreateFromPem(
                    NormalizeCertificatePem(record.CertificateData),
                    NormalizePrivateKeyPem(record.PrivateKeyData));
                var pfxData = certificate.Export(X509ContentType.Pkcs12);
                return Convert.ToBase64String(pfxData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"PFX导出失败: {ex.Message}", ex);
            }
        }

        private static X509Certificate2 LoadCertificateFromStoredData(string certificateData)
        {
            if (certificateData.Contains("BEGIN CERTIFICATE", StringComparison.OrdinalIgnoreCase))
            {
                return X509Certificate2.CreateFromPem(certificateData);
            }

            return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(StripPemArmoring(certificateData)));
        }

        private static (bool Passed, string Message, string Details) ValidatePrivateKeyMatchesCertificate(
            string certificateData,
            string privateKeyData)
        {
            try
            {
                using var certificate = X509Certificate2.CreateFromPem(
                    NormalizeCertificatePem(certificateData),
                    NormalizePrivateKeyPem(privateKeyData));

                return certificate.HasPrivateKey
                    ? (true, "私钥与证书匹配", "证书可与当前私钥组成完整密钥对")
                    : (false, "私钥与证书不匹配", "证书未能绑定私钥");
            }
            catch (Exception ex)
            {
                return (false, "私钥与证书不匹配", ex.Message);
            }
        }

        private static List<string> ExtractSubjectAlternativeNames(CertificateRecord record)
        {
            try
            {
                using var certificate = LoadCertificateFromStoredData(record.CertificateData);
                var sanExtension = certificate.Extensions
                    .OfType<X509SubjectAlternativeNameExtension>()
                    .FirstOrDefault();
                var dnsNames = sanExtension?.EnumerateDnsNames().ToList() ?? new List<string>();
                return dnsNames.Count > 0 ? dnsNames : record.Domains;
            }
            catch
            {
                return record.Domains;
            }
        }

        private static string NormalizeCertificatePem(string certificateData)
        {
            if (certificateData.Contains("BEGIN CERTIFICATE", StringComparison.OrdinalIgnoreCase))
            {
                return certificateData.Trim();
            }

            return NormalizePemBlock(certificateData, "CERTIFICATE");
        }

        private static string NormalizePrivateKeyPem(string privateKeyData)
        {
            if (privateKeyData.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
            {
                return privateKeyData.Trim();
            }

            return NormalizePemBlock(privateKeyData, "PRIVATE KEY");
        }

        private static string NormalizePemBlock(string data, string label)
        {
            var base64 = StripPemArmoring(data);
            var builder = new StringBuilder();
            builder.AppendLine($"-----BEGIN {label}-----");

            for (var i = 0; i < base64.Length; i += 64)
            {
                builder.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }

            builder.Append($"-----END {label}-----");
            return builder.ToString();
        }

        private static string StripPemArmoring(string data)
        {
            var lines = data
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !line.StartsWith("-----", StringComparison.Ordinal));

            return string.Concat(lines);
        }

        private int GetKeySize(X509Certificate2 certificate)
        {
            try
            {
                // 在.NET 9.0中，我们需要使用不同的方式获取密钥大小
                var publicKey = certificate.PublicKey;
                var oid = publicKey.Oid;

                // 根据OID推断密钥大小
                if (oid.Value != null)
                {
                    return oid.Value switch
                    {
                        // RSA OID
                        "1.2.840.113549.1.1.1" => 2048, // RSA 默认2048位

                        // ECDSA OID
                        "1.2.840.10045.2.1" => 256,   // ECDSA 通用

                        // 特定椭圆曲线 OID
                        "1.2.840.10045.3.1.7" => 256, // P-256 (prime256v1)
                        "1.3.132.0.34" => 384,        // P-384 (secp384r1)
                        "1.3.132.0.35" => 521,        // P-521 (secp521r1)
                        "1.3.132.0.10" => 256,        // K-256 (secp256k1)

                        // DSA OID
                        "1.2.840.10040.4.1" => 2048,  // DSA 默认2048位

                        // EdDSA OID (现代签名算法)
                        "1.3.101.112" => 255,         // Ed25519
                        "1.3.101.113" => 448,         // Ed448

                        // 其他可能的OID
                        "1.3.14.3.2.29" => 1024,      // SHA1withRSA (较老)
                        "2.5.4.65" => 2048,           // 通用RSA

                        _ => 2048 // 默认值
                    };
                }

                // 如果OID为null，尝试从友好名称推断
                if (!string.IsNullOrEmpty(oid.FriendlyName))
                {
                    return oid.FriendlyName.ToLowerInvariant() switch
                    {
                        var name when name.Contains("rsa") => 2048,
                        var name when name.Contains("ecdsa") => 256,
                        var name when name.Contains("dsa") => 2048,
                        var name when name.Contains("ed25519") => 255,
                        var name when name.Contains("ed448") => 448,
                        _ => 2048
                    };
                }

                return 2048; // 默认值
            }
            catch
            {
                return 2048; // 发生错误时返回默认值
            }
        }

        private async Task RecordOperationAsync(string certificateId, string operationType, string operation, bool success, List<string>? errors = null)
        {
            try
            {
                var operationRecord = new CertificateOperationRecord
                {
                    Id = ObjectId.NewObjectId().ToString(),
                    CertificateId = certificateId,
                    OperationType = operationType,
                    Operation = operation,
                    OperatedAt = DateTime.UtcNow,
                    Operator = "system", // 可以从上下文获取实际用户
                    Success = success,
                    Message = operation,
                    ErrorMessage = errors?.FirstOrDefault(),
                    OperationDetails = new Dictionary<string, object>
                    {
                        ["Errors"] = errors ?? new List<string>()
                    },
                    Duration = TimeSpan.Zero
                };

                _operationCollection.Insert(operationRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "记录证书操作历史失败: {CertificateId}, {Operation}", certificateId, operation);
            }
        }

        #endregion

        #region ID格式处理辅助方法

        /// <summary>
        /// 智能查找证书 - 支持UUID和ObjectId两种格式
        /// </summary>
        /// <param name="certificateId">证书ID（UUID或ObjectId格式）</param>
        /// <returns>证书记录或null</returns>
        private CertificateRecord? FindCertificateById(string certificateId)
        {
            if (string.IsNullOrEmpty(certificateId))
                return null;

            // 直接按 Id 查找
            return _certificateCollection.FindById(certificateId);
        }

        /// <summary>
        /// 删除证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <returns>删除的记录数</returns>
        private int DeleteCertificateById(string certificateId)
        {
            if (string.IsNullOrEmpty(certificateId))
                return 0;

            // 直接按 Id 删除
            return _certificateCollection.Delete(certificateId);
        }

        #endregion
    }

    #region Database Models

    /// <summary>
    /// 证书记录（数据库模型）
    /// </summary>
    [Entity]
    public class CertificateRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevocationReason { get; set; }
        public string Issuer { get; set; } = string.Empty;
        public string CertificateData { get; set; } = string.Empty;
        public string? PrivateKeyData { get; set; }
        public string CertificateChain { get; set; } = string.Empty;
        public string KeyAlgorithm { get; set; } = string.Empty;
        public int KeySize { get; set; }
        public string SignatureAlgorithm { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string? OrderId { get; set; }
        public bool AutoRenewalEnabled { get; set; }
        public AutoRenewalConfiguration? AutoRenewalConfiguration { get; set; }
        public DateTime? NextRenewalAttempt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public List<string>? NotificationEmails { get; set; }
        public string? CertificateId { get; set; } // ACME证书ID
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 证书操作记录（数据库模型）
    /// </summary>
    public class CertificateOperationRecord
    {
        public string Id { get; set; } = string.Empty;
        public string CertificateId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public DateTime OperatedAt { get; set; }
        public string? Operator { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> OperationDetails { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }

    #endregion
}
