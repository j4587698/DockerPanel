export default {
  title: 'SSL 证书管理',
  subtitle: '管理和申请 SSL/TLS 证书',
  pageTitle: '证书管理',
  pageSubtitle: '管理和监控 ACME SSL/TLS 证书',
  // 证书列表
  certificates: '证书列表',
  addCertificate: '添加证书',
  domain: '域名',
  issuer: '颁发者',
  expiresAt: '过期时间',
  valid: '有效',
  expired: '已过期',
  expiring: '即将过期',
  pending: '待验证',
  cancelled: '已取消',
  // 证书列表页面新增
  totalCertificates: '总证书',
  provider: '服务商',
  searchDomain: '搜索域名...',
  requestCertificate: '申请证书',
  noCertificates: '暂无证书',
  applyFirstCertificate: '申请您的第一个 SSL 证书',
  // 分页
  showingItems: '显示 {start} - {end} 共 {total} 条',
  itemsPerPage: '{count} 条/页',
  // 账户相关
  accountInfo: 'ACME 账户用于申请 SSL 证书。添加账户后，可在申请证书时选择使用，或启用自动申请证书功能。',
  active: '活跃',
  inactive: '未激活',
  noAccounts: '暂无 ACME 账户',
  addAccountHint: '添加账户后可申请 SSL 证书',
  addAccountTitle: '添加 ACME 账户',
  emailPlaceholder: '用于 ACME 注册的邮箱地址',
  selectProvider: '选择 ACME 服务商',
  zerosslEabHint: 'ZeroSSL 需要 EAB 凭据。请在 ZeroSSL 控制台获取 EAB Key ID 和 HMAC Key。',
  eabRequired: '此提供商需要 EAB 凭据',
  eabKidPlaceholder: 'EAB Key ID (kid)',
  eabHmacKeyPlaceholder: 'EAB HMAC Key (hmacKey)',
  formLabelWidth: '100px',
  accountCreated: '账户创建成功',
  deleteAccountConfirm: '确定删除账户 "{email}"?\n\n如果该账户有关联的活跃证书，将无法删除。',
  deleteConfirmMessage: '确定删除证书 "{name}"?\n\n如果证书正在被域名映射使用，将无法删除。',
  autoRenewEnabled: '已启用自动续期',
  autoRenewDisabled: '已禁用自动续期',
  // 表单验证
  validation: {
    emailRequired: '请输入邮箱地址',
    emailInvalid: '请输入有效的邮箱地址',
    providerRequired: '请选择服务商',
    eabKidRequired: '请输入 EAB Key ID',
    eabHmacKeyRequired: '请输入 EAB HMAC Key'
  },
  // 状态标签
  status: '状态',
  failed: '失败',
  expiresInDays: '剩余 {days} 天',
  // 证书描述
  description: '描述',
  // 创建对话框
  createDialog: {
    acmeAccount: 'ACME 账户',
    domainPlaceholder: '输入域名，例如：example.com',
    domainHint: '支持多个域名，每行一个。使用 *.example.com 申请泛域名证书。',
    addDomain: '添加域名',
    selectAcmeAccount: '选择 ACME 账户',
    addNewAccount: '+ 添加新账户',
    http01Hint: 'HTTP-01 验证需要端口 80 可从互联网访问。',
    dns01Hint: 'DNS-01 验证需要配置 DNS 服务商凭据。',
    tlsAlpn01Hint: 'TLS-ALPN-01 验证需要端口 443 可从互联网访问。',
    autoRenewHint: '证书将在过期前自动续期。',
    descriptionPlaceholder: '输入描述（可选）',
    letsEncryptStaging: "Let's Encrypt (测试)",
    selectDnsProvider: '选择 DNS 服务商',
    dnsProviderHint: '选择用于域名验证的 DNS 服务商。',
    submitSuccess: '证书申请已提交',
    validation: {
      accountRequired: '请选择 ACME 账户',
      challengeTypeRequired: '请选择验证方式',
      descriptionMaxLength: '描述不能超过 500 个字符',
      domainRequired: '请至少输入一个域名',
      invalidDomain: '域名格式无效：{domains}',
      dnsProviderRequired: '请选择 DNS 服务商',
      selectValidAccount: '请选择有效的 ACME 账户'
    },
    dnsFields: {
      cloudflare: {
        apiKey: 'API 密钥',
        apiKeyHelp: 'Cloudflare 全局 API 密钥',
        email: '邮箱',
        emailPlaceholder: '账户邮箱地址',
        emailHelp: '与您的 Cloudflare 账户关联的邮箱'
      },
      aliyun: {
        accessKeyId: 'Access Key ID',
        accessKeyIdPlaceholder: '阿里云 Access Key ID',
        accessKeyIdHelp: '阿里云 Access Key ID',
        accessKeySecret: 'Access Key Secret',
        accessKeySecretPlaceholder: '阿里云 Access Key Secret',
        accessKeySecretHelp: '阿里云 Access Key Secret'
      },
      tencent: {
        secretId: 'Secret ID',
        secretIdPlaceholder: '腾讯云 Secret ID',
        secretIdHelp: '腾讯云 Secret ID',
        secretKey: 'Secret Key',
        secretKeyPlaceholder: '腾讯云 Secret Key',
        secretKeyHelp: '腾讯云 Secret Key'
      },
      dnspod: {
        secretId: 'Secret ID',
        secretIdPlaceholder: 'DNSPod Secret ID',
        secretIdHelp: 'DNSPod Secret ID',
        secretKey: 'Secret Key',
        secretKeyPlaceholder: 'DNSPod Secret Key',
        secretKeyHelp: 'DNSPod Secret Key'
      },
      aws: {
        accessKeyId: 'Access Key ID',
        accessKeyIdHelp: 'AWS Access Key ID',
        secretAccessKey: 'Secret Access Key',
        secretAccessKeyHelp: 'AWS Secret Access Key',
        regionHelp: 'AWS 区域，例如 us-east-1'
      },
      azure: {
        clientId: '客户端 ID',
        clientIdHelp: 'Azure AD 应用程序客户端 ID',
        clientSecret: '客户端密钥',
        clientSecretHelp: 'Azure AD 应用程序客户端密钥',
        tenantId: '租户 ID',
        tenantIdHelp: 'Azure AD 租户 ID',
        subscriptionId: '订阅 ID',
        subscriptionIdHelp: 'Azure 订阅 ID',
        resourceGroupPlaceholder: '资源组名称',
        resourceGroupHelp: '包含 DNS 区域的 Azure 资源组'
      },
      godaddy: {
        apiKey: 'API 密钥',
        apiKeyHelp: 'GoDaddy API 密钥',
        apiSecret: 'API 密钥',
        apiSecretHelp: 'GoDaddy API 密钥'
      }
    }
  },
  // 申请证书
  requestTitle: '申请证书',
  requestDomain: '域名',
  wildcardDomain: '泛域名',
  acmeProvider: 'ACME 提供商',
  letsEncrypt: "Let's Encrypt",
  letsEncryptStaging: "Let's Encrypt (测试)",
  zeroSsl: 'ZeroSSL',
  buypass: 'Buypass',
  challengeType: '验证方式',
  httpChallenge: 'HTTP 验证',
  dnsChallenge: 'DNS 验证',
  http01: 'HTTP-01',
  dns01: 'DNS-01',
  tlsAlpn01: 'TLS-ALPN-01',
  dnsProvider: 'DNS 服务商',
  autoRenew: '自动续期',
  // DNS 服务商名称
  aliyunDns: '阿里云 DNS',
  tencentDns: '腾讯云 DNS',
  dnsPod: 'DNSPod',
  awsRoute53: 'AWS Route53',
  azureDns: 'Azure DNS',
  goDaddy: 'GoDaddy',
  region: '区域',
  resourceGroup: '资源组',
  // 证书详情抽屉
  drawerTitle: '证书详情',
  downloadCertificate: '下载证书',
  renewCertificate: '续期证书',
  cancelApplication: '取消申请',
  // 基本信息
  basicInfo: '基本信息',
  certificateName: '证书名称',
  acmeProviderLabel: 'ACME服务商',
  email: '邮箱',
  challengeTypeLabel: '验证方式',
  autoRenewLabel: '自动续期',
  enabled: '已启用',
  disabled: '已禁用',
  createdAt: '创建时间',
  updatedAt: '更新时间',
  // 域名信息
  domainInfo: '域名信息',
  domainCount: '共 {count} 个域名',
  wildcard: '通配符',
  // 证书信息
  certificateInfo: '证书信息',
  issuedAt: '颁发时间',
  serialNumber: '序列号',
  fingerprint: '指纹',
  subject: '主题',
  // ACME 账户
  acmeAccounts: 'ACME 账户',
  addAccount: '添加账户',
  accountEmail: '账户邮箱',
  accountKey: '账户密钥',
  staging: '测试环境',
  production: '生产环境',
  // 自动续期
  autoRenewal: '自动续期',
  renewalDays: '提前续期天数',
  renewalHistory: '续期历史'
}