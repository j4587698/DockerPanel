using DockerPanel.API.Models.Acme;

namespace DockerPanel.API.Services.Acme
{
    /// <summary>
    /// ACME证书管理服务接口
    /// </summary>
    public interface IAcmeService
    {
        /// <summary>
        /// 获取ACME提供商列表
        /// </summary>
        /// <returns>ACME提供商列表</returns>
        Task<IEnumerable<AcmeProvider>> GetProvidersAsync();

        /// <summary>
        /// 测试ACME提供商连接
        /// </summary>
        /// <param name="provider">ACME提供商名称</param>
        /// <returns>连接测试结果</returns>
        Task<AcmeConnectionTestResult> TestProviderConnectionAsync(string provider);

        /// <summary>
        /// 获取ACME账户列表
        /// </summary>
        /// <returns>ACME账户列表</returns>
        Task<IEnumerable<AcmeAccount>> GetAccountsAsync();

        /// <summary>
        /// 获取指定域名的pending订单列表
        /// </summary>
        /// <param name="domains">域名列表</param>
        /// <returns>pending订单列表</returns>
        Task<IEnumerable<AcmeCertificateOrder>> GetPendingOrdersForDomainsAsync(IEnumerable<string> domains);

        /// <summary>
        /// 根据ID获取ACME账户
        /// </summary>
        /// <param name="accountId">账户ID</param>
        /// <returns>ACME账户</returns>
        Task<AcmeAccount?> GetAccountAsync(string accountId);

        /// <summary>
        /// 创建ACME账户
        /// </summary>
        /// <param name="request">创建账户请求</param>
        /// <returns>创建的ACME账户</returns>
        Task<AcmeAccount> CreateAccountAsync(CreateAcmeAccountRequest request);

        /// <summary>
        /// 删除ACME账户
        /// </summary>
        /// <param name="accountId">账户ID</param>
        /// <returns>删除结果</returns>
        Task<bool> DeleteAccountAsync(string accountId);

        /// <summary>
        /// 申请证书
        /// </summary>
        /// <param name="request">证书申请请求</param>
        /// <returns>证书订单</returns>
        Task<AcmeCertificateOrder?> OrderCertificateAsync(AcmeCertificateRequest request);

        /// <summary>
        /// 获取证书订单列表
        /// </summary>
        /// <param name="accountId">可选账户ID过滤</param>
        /// <returns>证书订单列表</returns>
        Task<IEnumerable<AcmeCertificateOrder>> GetCertificateOrdersAsync(string? accountId = null);

        /// <summary>
        /// 根据ID获取证书订单
        /// </summary>
        /// <param name="orderId">订单ID</param>
        /// <returns>证书订单</returns>
        Task<AcmeCertificateOrder?> GetCertificateOrderAsync(string orderId);

        /// <summary>
        /// 完成域名验证挑战
        /// </summary>
        /// <param name="orderId">订单ID</param>
        /// <param name="authorizationId">授权ID</param>
        /// <param name="request">完成挑战请求</param>
        /// <returns>挑战结果</returns>
        Task<AcmeChallengeResult> CompleteChallengeAsync(string orderId, string authorizationId, CompleteChallengeRequest request);

        /// <summary>
        /// 查询挑战验证状态
        /// </summary>
        /// <param name="orderId">订单ID</param>
        /// <param name="authorizationId">授权ID</param>
        /// <param name="challengeType">挑战类型</param>
        /// <returns>挑战状态结果</returns>
        Task<AcmeChallengeResult> CheckChallengeStatusAsync(string orderId, string authorizationId, string challengeType = "http-01");

        /// <summary>
        /// 下载证书
        /// </summary>
        /// <param name="orderId">订单ID</param>
        /// <returns>证书数据</returns>
        Task<AcmeCertificateData> DownloadCertificateAsync(string orderId);

        /// <summary>
        /// 获取待处理的验证挑战
        /// </summary>
        /// <param name="orderId">订单ID</param>
        /// <returns>待处理的挑战列表</returns>
        Task<IEnumerable<AcmeChallenge>> GetPendingChallengesAsync(string orderId);

        /// <summary>
        /// 续期证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <returns>新证书订单</returns>
        Task<AcmeCertificateOrder> RenewCertificateAsync(string certificateId);

        /// <summary>
        /// 重试失败的证书申请
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <returns>新证书订单</returns>
        Task<AcmeCertificateOrder> RetryCertificateOrderAsync(string certificateId);

        /// <summary>
        /// 撤销证书
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <param name="request">撤销请求</param>
        /// <returns>撤销结果</returns>
        Task<bool> RevokeCertificateAsync(string certificateId, RevokeCertificateRequest request);

        /// <summary>
        /// 获取ACME操作日志
        /// </summary>
        /// <param name="accountId">可选账户ID过滤</param>
        /// <param name="limit">限制数量</param>
        /// <param name="offset">偏移量</param>
        /// <returns>操作日志列表</returns>
        Task<IEnumerable<AcmeOperationLog>> GetOperationLogsAsync(string? accountId = null, int limit = 100, int offset = 0);

        /// <summary>
        /// 检查证书到期时间
        /// </summary>
        /// <param name="certificateId">证书ID</param>
        /// <returns>到期天数</returns>
        Task<int> CheckCertificateExpiryAsync(string certificateId);

        /// <summary>
        /// <summary>
        /// 处理由队列投递的 ACME 后台任务
        /// </summary>
        Task ProcessJobAsync(AcmeJobRecord job);

        /// <summary>
        /// 自动续期即将到期的证书
        /// </summary>
        /// <param name="daysBeforeExpiry">到期前多少天续期</param>
        /// <returns>续期的证书数量</returns>
        Task<int> AutoRenewCertificatesAsync(int daysBeforeExpiry = 15);

        /// <summary>
        /// 验证域名所有权
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="challengeType">挑战类型</param>
        /// <returns>验证结果</returns>
        Task<AcmeChallengeResult> VerifyDomainOwnershipAsync(string domain, string challengeType);

        /// <summary>
        /// 生成CSR（证书签名请求）
        /// </summary>
        /// <param name="domains">域名列表</param>
        /// <param name="keyType">密钥类型</param>
        /// <returns>CSR字符串</returns>
        Task<string> GenerateCsrAsync(IEnumerable<string> domains, string keyType = "rsa2048");

        /// <summary>
        /// 验证证书有效性
        /// </summary>
        /// <param name="certificateData">证书数据</param>
        /// <returns>验证结果</returns>
        Task<AcmeCertificateValidationResult> ValidateCertificateAsync(string certificateData);

        /// <summary>
        /// 获取账户密钥信息
        /// </summary>
        /// <param name="accountId">账户ID</param>
        /// <returns>密钥信息</returns>
        Task<AcmeKeyInfo> GetAccountKeyInfoAsync(string accountId);

        /// <summary>
        /// 生成新的账户密钥对
        /// </summary>
        /// <param name="keyType">密钥类型</param>
        /// <returns>密钥对信息</returns>
        Task<AcmeKeyPair> GenerateKeyPairAsync(string keyType = "rsa2048");

        /// <summary>
        /// 导出账户密钥
        /// </summary>
        /// <param name="accountId">账户ID</param>
        /// <param name="format">导出格式</param>
        /// <returns>导出的密钥数据</returns>
        Task<string> ExportAccountKeyAsync(string accountId, string format = "pem");

        /// <summary>
        /// 导入账户密钥
        /// </summary>
        /// <param name="keyData">密钥数据</param>
        /// <param name="format">密钥格式</param>
        /// <returns>导入的密钥信息</returns>
        Task<AcmeKeyInfo> ImportAccountKeyAsync(string keyData, string format = "pem");
    }
}