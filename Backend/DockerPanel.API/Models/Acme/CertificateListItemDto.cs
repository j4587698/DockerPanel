namespace DockerPanel.API.Models.Acme;

/// <summary>
/// 证书列表项 DTO（合并订单与证书记录的统一视图，AOT 兼容强类型）
/// </summary>
public class CertificateListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public List<string> Domains { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string AcmeProvider { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ChallengeType { get; set; } = string.Empty;
    public string? DnsProvider { get; set; }
    public List<string> DnsConfigFields { get; set; } = new();
    public bool AutoRenew { get; set; }
    public bool IsAutoRenewal { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int DaysUntilExpiry { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<object> Logs { get; set; } = new();
    public string? Error { get; set; }
}
