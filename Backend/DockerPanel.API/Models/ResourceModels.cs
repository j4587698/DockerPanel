using System.ComponentModel.DataAnnotations;
using TinyDb.Attributes;

namespace DockerPanel.API.Models;

/// <summary>
/// 创建资源警报规则请求
/// </summary>
public class CreateResourceAlertRuleRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty; // cpu, memory, disk, network
    public string MetricType { get; set; } = string.Empty; // usage, threshold, percent
    public double ThresholdValue { get; set; }
    public string ComparisonOperator { get; set; } = ">"; // >, <, >=, <=, ==
    public TimeSpan EvaluationPeriod { get; set; } = TimeSpan.FromMinutes(5);
    public int ConsecutiveEvaluations { get; set; } = 1;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public bool IsEnabled { get; set; } = true;
    public List<string> NotificationEmails { get; set; } = new();
    public List<string> NotificationWebhooks { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 更新资源警报规则请求
/// </summary>
public class UpdateResourceAlertRuleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ResourceType { get; set; }
    public string? MetricType { get; set; }
    public double? ThresholdValue { get; set; }
    public string? ComparisonOperator { get; set; }
    public TimeSpan? EvaluationPeriod { get; set; }
    public int? ConsecutiveEvaluations { get; set; }
    public AlertSeverity? Severity { get; set; }
    public bool? IsEnabled { get; set; }
    public List<string>? NotificationEmails { get; set; }
    public List<string>? NotificationWebhooks { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 资源警报规则
/// </summary>
[Entity]
public class ResourceAlertRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public double ThresholdValue { get; set; }
    public string ComparisonOperator { get; set; } = string.Empty;
    public TimeSpan EvaluationPeriod { get; set; }
    public int ConsecutiveEvaluations { get; set; }
    public AlertSeverity Severity { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> NotificationEmails { get; set; } = new();
    public List<string> NotificationWebhooks { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public AlertRuleStatus Status { get; set; } = new();
}

/// <summary>
/// 警报规则状态
/// </summary>
public class AlertRuleStatus
{
    public string State { get; set; } = string.Empty; // normal, warning, critical, unknown
    public string? Message { get; set; }
    public DateTime? LastEvaluation { get; set; }
    public DateTime? LastAlert { get; set; }
    public int CurrentConsecutive { get; set; }
    public double CurrentValue { get; set; }
    public bool IsTriggered { get; set; }
}

/// <summary>
/// 资源警报
/// </summary>
public class ResourceAlert
{
    public string Id { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double ThresholdValue { get; set; }
    public AlertSeverity Severity { get; set; }
    public string State { get; set; } = string.Empty; // fired, resolved, acknowledged
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public List<AlertNotification> Notifications { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool IsActive => State == "fired"; // 添加IsActive属性，基于State计算
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 添加CreatedAt属性
    public double Threshold { get; set; } // 添加Threshold属性
}

/// <summary>
/// 警报通知
/// </summary>
public class AlertNotification
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // email, webhook, slack
    public string Target { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // pending, sent, failed
    public DateTime SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastRetry { get; set; }
}

/// <summary>
/// 告警类型
/// </summary>
public enum AlertType
{
    Threshold,
    Anomaly,
    ConnectionLost,
    ServiceDown,
    Capacity,
    Performance
}

/// <summary>
/// 警报严重程度
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// 资源使用报告
/// </summary>
public class ResourceUsageReport
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Interval { get; set; }
    public List<ResourceUsageDataPoint> CpuUsage { get; set; } = new();
    public List<ResourceUsageDataPoint> MemoryUsage { get; set; } = new();
    public List<ResourceUsageDataPoint> DiskUsage { get; set; } = new();
    public List<ResourceUsageDataPoint> NetworkIn { get; set; } = new();
    public List<ResourceUsageDataPoint> NetworkOut { get; set; } = new();
    public ResourceUsageSummary Summary { get; set; } = new();
}

/// <summary>
/// 资源使用数据点
/// </summary>
public class ResourceUsageDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// 资源使用摘要
/// </summary>
public class ResourceUsageSummary
{
    public ResourceMetricSummary Cpu { get; set; } = new();
    public ResourceMetricSummary Memory { get; set; } = new();
    public ResourceMetricSummary Disk { get; set; } = new();
    public ResourceMetricSummary NetworkIn { get; set; } = new();
    public ResourceMetricSummary NetworkOut { get; set; } = new();
}

/// <summary>
/// 资源指标摘要
/// </summary>
public class ResourceMetricSummary
{
    public double Average { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double P95 { get; set; } // 95th percentile
    public double P99 { get; set; } // 99th percentile
    public double StandardDeviation { get; set; }
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// 资源配额
/// </summary>
public class ResourceQuota
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty; // node, nodegroup
    public ResourceLimit CpuLimit { get; set; } = new();
    public ResourceLimit MemoryLimit { get; set; } = new();
    public ResourceLimit DiskLimit { get; set; } = new();
    public ResourceLimit NetworkLimit { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 资源限制
/// </summary>
public class ResourceLimit
{
    public double HardLimit { get; set; }
    public double SoftLimit { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsEnforced { get; set; } = true;
}

/// <summary>
/// 资源预测
/// </summary>
public class ResourceForecast
{
    public string NodeId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public DateTime ForecastDate { get; set; }
    public double PredictedValue { get; set; }
    public double ConfidenceInterval { get; set; }
    public string Unit { get; set; } = string.Empty;
    public List<ForecastDataPoint> HistoricalData { get; set; } = new();
    public List<ForecastDataPoint> PredictedData { get; set; } = new();
    public ForecastAccuracy Accuracy { get; set; } = new();
}

/// <summary>
/// 预测数据点
/// </summary>
public class ForecastDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public bool IsPredicted { get; set; }
    public double? ConfidenceLower { get; set; }
    public double? ConfidenceUpper { get; set; }
}

/// <summary>
/// 预测准确度
/// </summary>
public class ForecastAccuracy
{
    public double MeanAbsoluteError { get; set; }
    public double MeanSquaredError { get; set; }
    public double R2Score { get; set; }
    public DateTime LastTrained { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
}