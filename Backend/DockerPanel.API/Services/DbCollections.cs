namespace DockerPanel.API.Services;

public static class DbCollections
{
    public const string Settings = "settings";
    public const string NodeResources = "node_resources";
    public const string SshKeyPairs = "ssh_keypairs";
    public const string Users = "users";
    public const string Groups = "groups";
    
    public const string AcmeAccounts = "acme_accounts";
    public const string AcmeOrders = "acme_orders";
    public const string AcmeChallenges = "acme_challenges";
    public const string AcmeOperationLogs = "acme_operation_logs";
    public const string AcmeJobs = "acme_jobs";
    
    // 证书相关
    public const string Certificates = "certificates";
    public const string CertificateOperations = "certificate_operations";
    public const string WildcardCerts = "wildcard_certs";
    public const string SniCertificates = "sni_certificates";
    
    // 审计与监控相关
    public const string OperationAudits = "operation_audit_logs";
    public const string ResourceAlertRules = "resource_alert_rules";
}
