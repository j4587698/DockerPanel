using DockerPanel.API.Data;
using DockerPanel.API.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Docker.DotNet;
using Docker.DotNet.Models;
using Renci.SshNet;

namespace DockerPanel.API.Services;

/// <summary>
/// 节点服务接口
/// </summary>
public interface INodeService
{
    Task<IEnumerable<NodeInfo>> GetNodesAsync();
    Task<NodeInfo?> GetNodeAsync(string id);
    Task<NodeInfo?> GetNodeByIdAsync(string id);
    Task<string> AddNodeAsync(AddNodeRequest request);
    Task UpdateNodeAsync(string id, UpdateNodeRequest request);
    Task RemoveNodeAsync(string id);
    Task<bool> TestNodeConnectionAsync(string id);
    Task<TestNodeConnectionResult> TestConnectionAsync(TestNodeConnectionRequest request);
    Task<NodeStats?> GetNodeStatsAsync(string id);
    Task<NodeInfo?> GetNodeInfoAsync(string id);
    Task<NodeHealthStatus> GetNodeHealthStatusAsync(string id);
    Task<NodeInfo?> GetDefaultNodeAsync();
    Task SetDefaultNodeAsync(string id);
    Task<IEnumerable<DockerPanel.API.Models.NodeGroup>> GetGroupsAsync();
    Task<DockerPanel.API.Models.NodeGroup?> GetGroupAsync(string id);
    Task<DockerPanel.API.Models.NodeGroup> CreateGroupAsync(DockerPanel.API.Models.CreateNodeGroupRequest request);
    Task UpdateGroupAsync(string id, DockerPanel.API.Models.UpdateNodeGroupRequest request);
    Task DeleteGroupAsync(string id);
    Task<DockerClient> GetDockerClientAsync(string? nodeId = null);
    Task<Uri?> GetNodeEndpointAsync(string? nodeId = null);
}

/// <summary>
/// 节点服务实现 - 支持数据库持久化和多节点管理
/// </summary>
public class NodeService : INodeService
{
    private readonly ILogger<NodeService> _logger;
    private readonly TinyDbContext _dbContext;
    private readonly ISshService _sshService;
    private readonly IServiceProvider _serviceProvider;

    private const string LocalNodeId = "local";
    private static readonly TimeSpan LocalNodeStatusRefreshInterval = TimeSpan.FromSeconds(30);

    // Docker 客户端缓存池
    private static readonly ConcurrentDictionary<string, DockerClient> _dockerClients = new();
    private static readonly ConcurrentDictionary<string, SshClient> _sshTunnels = new();
    private static readonly SemaphoreSlim _clientLock = new(1, 1);

    public NodeService(
        ILogger<NodeService> logger,
        TinyDbContext dbContext,
        ISshService sshService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _sshService = sshService;
        _serviceProvider = serviceProvider;
    }

    private NodeInfo EnsureLocalNode()
    {
        var nodes = _dbContext.NodeInfos.Query().ToList();
        var localNode = nodes.FirstOrDefault(IsLocalNode);
        var hasDefaultNode = nodes.Any(n => n.IsDefault);

        if (localNode == null)
        {
            localNode = CreateLocalNode(hasDefaultNode);
            _dbContext.NodeInfos.Insert(localNode);
            _logger.LogInformation("已初始化内置本地节点: {Id}", localNode.Id);
            return localNode;
        }

        var changed = false;

        if (localNode.ConnectionType != DockerConnectionType.Local)
        {
            localNode.ConnectionType = DockerConnectionType.Local;
            changed = true;
        }

        var localEndpoint = GetLocalDockerEndpoint();
        if (string.IsNullOrWhiteSpace(localNode.DockerEndpoint))
        {
            localNode.DockerEndpoint = localEndpoint;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(localNode.Name))
        {
            localNode.Name = "本地 Docker";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(localNode.Host))
        {
            localNode.Host = "localhost";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(localNode.EngineType))
        {
            localNode.EngineType = "docker";
            changed = true;
        }

        if (!hasDefaultNode)
        {
            localNode.IsDefault = true;
            changed = true;
        }

        if (changed)
        {
            localNode.UpdatedAt = DateTime.UtcNow;
            _dbContext.NodeInfos.Update(localNode);
        }

        return localNode;
    }

    private static NodeInfo CreateLocalNode(bool hasDefaultNode)
    {
        var now = DateTime.UtcNow;
        return new NodeInfo
        {
            Id = LocalNodeId,
            Name = "本地 Docker",
            Host = "localhost",
            Port = 0,
            EngineType = "docker",
            ConnectionType = DockerConnectionType.Local,
            DockerEndpoint = GetLocalDockerEndpoint(),
            IsDefault = !hasDefaultNode,
            SortOrder = -1000,
            Description = "当前服务器上的 Docker 引擎",
            Status = NodeResourceStatus.Unknown,
            IsOnline = false,
            CreatedAt = now,
            UpdatedAt = now,
            Labels = new Dictionary<string, string>
            {
                ["builtin"] = "true",
                ["scope"] = "local"
            }
        };
    }

    private static bool IsLocalNode(NodeInfo node)
    {
        return string.Equals(node.Id, LocalNodeId, StringComparison.OrdinalIgnoreCase) ||
               node.ConnectionType == DockerConnectionType.Local;
    }

    private static string GetLocalDockerEndpoint()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";
    }

    private async Task RefreshLocalNodeStatusAsync(NodeInfo localNode)
    {
        if (!IsLocalNode(localNode))
        {
            return;
        }

        if (localNode.LastHealthCheck.HasValue &&
            DateTime.UtcNow - localNode.LastHealthCheck.Value < LocalNodeStatusRefreshInterval)
        {
            return;
        }

        var now = DateTime.UtcNow;

        try
        {
            var client = await GetDockerClientAsync(localNode.Id);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var version = await client.System.GetVersionAsync(cts.Token);
            await client.System.PingAsync(cts.Token);

            localNode.EngineType = "docker";
            localNode.Version = version.Version ?? string.Empty;
            localNode.ApiVersion = version.APIVersion ?? string.Empty;
            localNode.Os = version.Os ?? string.Empty;
            localNode.Architecture = version.Arch ?? string.Empty;
            localNode.Status = NodeResourceStatus.Online;
            localNode.IsOnline = true;
            localNode.LastConnected = now;
            localNode.LastSeen = now;
            localNode.HealthCheckMessage = null;
        }
        catch (Exception ex)
        {
            localNode.Status = NodeResourceStatus.Offline;
            localNode.IsOnline = false;
            localNode.HealthCheckMessage = ex.Message;
            _logger.LogDebug(ex, "本地节点状态刷新失败");
        }

        localNode.LastHealthCheck = now;
        localNode.UpdatedAt = now;
        _dbContext.NodeInfos.Update(localNode);
    }

    #region 节点CRUD

    /// <summary>
    /// 获取所有节点
    /// </summary>
    public async Task<IEnumerable<NodeInfo>> GetNodesAsync()
    {
        try
        {
            var localNode = EnsureLocalNode();
            await RefreshLocalNodeStatusAsync(localNode);

            var nodes = _dbContext.NodeInfos.Query().OrderBy(n => n.SortOrder).ThenBy(n => n.Name).ToList();
            _logger.LogInformation("获取节点列表: {Count} 个节点", nodes.Count);
            return await Task.FromResult(nodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点列表失败");
            return new List<NodeInfo>();
        }
    }

    /// <summary>
    /// 获取节点
    /// </summary>
    public async Task<NodeInfo?> GetNodeAsync(string id)
    {
        var node = _dbContext.NodeInfos.FindById(id);
        if (node == null && string.Equals(id, LocalNodeId, StringComparison.OrdinalIgnoreCase))
        {
            node = EnsureLocalNode();
        }

        return await Task.FromResult(node);
    }

    /// <summary>
    /// 获取节点详情
    /// </summary>
    public async Task<NodeInfo?> GetNodeByIdAsync(string id)
    {
        return await GetNodeAsync(id);
    }

    /// <summary>
    /// 添加节点
    /// </summary>
    public async Task<string> AddNodeAsync(AddNodeRequest request)
    {
        _logger.LogInformation("添加节点: {Name}", request.Name);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("节点名称不能为空", nameof(request.Name));

        var node = new NodeInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Host = request.Host,
            Port = request.Port,
            EngineType = request.EngineType ?? "docker",
            ConnectionType = request.ConnectionType,
            DockerEndpoint = request.DockerEndpoint,
            GroupId = request.GroupId,
            Tags = request.Tags ?? new List<string>(),
            Labels = request.Labels ?? new Dictionary<string, string>(),
            IsDefault = request.IsDefault,
            Description = request.Description,
            ConnectionTimeout = request.ConnectionTimeout,
            EnableHealthCheck = request.EnableHealthCheck,
            HealthCheckInterval = request.HealthCheckInterval,
            Username = request.Username,
            Password = request.Password,
            TlsConfig = request.TlsConfig,
            UseSsh = request.UseSsh,
            SshPort = request.SshPort,
            SshUsername = request.SshUsername,
            SshPrivateKeyPath = request.SshPrivateKeyPath,
            Status = NodeResourceStatus.Unknown,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // SSH 隧道配置
        if (request.UseSsh)
        {
            node.SshTunnelConfig = new NodeSshTunnelConfig
            {
                SshHost = request.Host,
                SshPort = request.SshPort,
                SshUsername = request.SshUsername ?? string.Empty,
                SshPassword = request.SshPassword,
                SshPrivateKeyPath = request.SshPrivateKeyPath,
                SshPrivateKeyPassphrase = request.SshPrivateKeyPassphrase,
                RemoteDockerSocket = request.RemoteDockerSocket,
                SshConnectionId = request.SshConnectionId
            };
        }

        // 如果设为默认节点，清除其他节点的默认标记
        if (node.IsDefault)
        {
            var existingNodes = _dbContext.NodeInfos.Query().Where(n => n.IsDefault).ToList();
            foreach (var n in existingNodes)
            {
                n.IsDefault = false;
                _dbContext.NodeInfos.Update(n);
            }
        }

        _dbContext.NodeInfos.Insert(node);
        _logger.LogInformation("节点添加成功: {Id}", node.Id);

        return await Task.FromResult(node.Id);
    }

    /// <summary>
    /// 更新节点
    /// </summary>
    public async Task UpdateNodeAsync(string id, UpdateNodeRequest request)
    {
        _logger.LogInformation("更新节点: {Id}", id);

        var node = _dbContext.NodeInfos.FindById(id);
        if (node == null)
            throw new InvalidOperationException($"节点 '{id}' 不存在");

        if (request.Name != null)
            node.Name = request.Name;
        if (request.Host != null)
            node.Host = request.Host;
        if (request.Port.HasValue)
            node.Port = request.Port.Value;
        if (request.EngineType != null)
            node.EngineType = request.EngineType;
        if (request.ConnectionType.HasValue)
            node.ConnectionType = request.ConnectionType.Value;
        if (request.DockerEndpoint != null)
            node.DockerEndpoint = request.DockerEndpoint;
        if (request.GroupId != null)
            node.GroupId = request.GroupId;
        if (request.Tags != null)
            node.Tags = request.Tags;
        if (request.Labels != null)
            node.Labels = request.Labels;
        if (request.Description != null)
            node.Description = request.Description;
        if (request.ConnectionTimeout.HasValue)
            node.ConnectionTimeout = request.ConnectionTimeout.Value;
        if (request.EnableHealthCheck.HasValue)
            node.EnableHealthCheck = request.EnableHealthCheck.Value;
        if (request.HealthCheckInterval.HasValue)
            node.HealthCheckInterval = request.HealthCheckInterval.Value;
        if (request.Username != null)
            node.Username = request.Username;
        if (request.Password != null)
            node.Password = request.Password;
        if (request.TlsConfig != null)
            node.TlsConfig = request.TlsConfig;

        // SSH 配置
        if (request.UseSsh.HasValue)
            node.UseSsh = request.UseSsh.Value;
        if (request.SshPort.HasValue)
            node.SshPort = request.SshPort.Value;
        if (request.SshUsername != null)
            node.SshUsername = request.SshUsername;
        if (request.SshPassword != null)
            node.Password = request.SshPassword;
        if (request.SshPrivateKeyPath != null)
            node.SshPrivateKeyPath = request.SshPrivateKeyPath;

        // 如果设为默认节点
        if (request.IsDefault.HasValue && request.IsDefault.Value)
        {
            var existingNodes = _dbContext.NodeInfos.Query().Where(n => n.IsDefault && n.Id != id).ToList();
            foreach (var n in existingNodes)
            {
                n.IsDefault = false;
                _dbContext.NodeInfos.Update(n);
            }
            node.IsDefault = true;
        }

        node.UpdatedAt = DateTime.UtcNow;

        // 清除缓存的客户端，下次使用时重新创建
        RemoveCachedClient(id);

        _dbContext.NodeInfos.Update(node);
        _logger.LogInformation("节点更新成功: {Id}", id);

        await Task.CompletedTask;
    }

    /// <summary>
    /// 删除节点
    /// </summary>
    public async Task RemoveNodeAsync(string id)
    {
        _logger.LogInformation("删除节点: {Id}", id);

        if (string.Equals(id, LocalNodeId, StringComparison.OrdinalIgnoreCase))
        {
            EnsureLocalNode();
            throw new InvalidOperationException("内置本地节点不能删除");
        }

        // 清除缓存的客户端
        RemoveCachedClient(id);

        var removed = _dbContext.NodeInfos.Delete(id);
        if (removed > 0)
        {
            _logger.LogInformation("节点删除成功: {Id}", id);
        }

        await Task.CompletedTask;
    }

    #endregion

    #region 连接测试

    /// <summary>
    /// 测试节点连接
    /// </summary>
    public async Task<bool> TestNodeConnectionAsync(string id)
    {
        var node = await GetNodeAsync(id);
        if (node == null)
        {
            _logger.LogWarning("节点不存在: {Id}", id);
            return false;
        }

        try
        {
            var client = await GetDockerClientAsync(id);
            await client.System.PingAsync();

            // 更新节点状态
            node.Status = NodeResourceStatus.Online;
            node.IsOnline = true;
            node.LastConnected = DateTime.UtcNow;
            node.LastSeen = DateTime.UtcNow;
            node.LastHealthCheck = DateTime.UtcNow;
            node.HealthCheckMessage = null;
            _dbContext.NodeInfos.Update(node);

            _logger.LogInformation("节点 {Id} 连接测试成功", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "节点 {Id} 连接测试失败", id);

            node.Status = NodeResourceStatus.Offline;
            node.IsOnline = false;
            node.LastHealthCheck = DateTime.UtcNow;
            node.HealthCheckMessage = ex.Message;
            _dbContext.NodeInfos.Update(node);

            return false;
        }
    }

    /// <summary>
    /// 测试连接参数
    /// </summary>
    public async Task<TestNodeConnectionResult> TestConnectionAsync(TestNodeConnectionRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TestNodeConnectionResult();

        try
        {
            DockerClient? client = null;

            switch (request.ConnectionType ?? DockerConnectionType.Local)
            {
                case DockerConnectionType.Local:
                    client = CreateLocalDockerClient();
                    break;

                case DockerConnectionType.Tcp:
                    client = CreateTcpDockerClient(request.Host!, request.Port ?? 2375, null, request.ConnectionTimeout ?? 30);
                    break;

                case DockerConnectionType.Tls:
                    client = CreateTlsDockerClient(request.Host!, request.Port ?? 2376, request.TlsConfig!, request.ConnectionTimeout ?? 30);
                    break;

                case DockerConnectionType.SshTunnel:
                    var (sshClient, localPort) = await CreateSshTunnelAsync(
                        request.Host!,
                        request.SshPort ?? 22,
                        request.SshUsername!,
                        request.SshPassword,
                        request.SshPrivateKeyPath,
                        request.RemoteDockerSocket ?? "/var/run/docker.sock"
                    );
                    client = CreateTcpDockerClient("localhost", localPort, null, request.ConnectionTimeout ?? 30);
                    break;
            }

            if (client != null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(request.ConnectionTimeout ?? 30));
                var version = await client.System.GetVersionAsync(cts.Token);
                await client.System.PingAsync(cts.Token);

                stopwatch.Stop();

                result.Success = true;
                result.Message = "连接成功";
                result.DockerVersion = version.Version;
                result.ApiVersion = version.APIVersion;
                result.Os = version.Os;
                result.Architecture = version.Arch;
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

                client.Dispose();
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            _logger.LogError(ex, "连接测试失败");
        }

        return result;
    }

    #endregion

    #region Docker 客户端管理

    /// <summary>
    /// 获取 Docker 客户端
    /// </summary>
    public async Task<DockerClient> GetDockerClientAsync(string? nodeId = null)
    {
        // 如果没有指定节点ID，获取默认节点或本地连接
        if (string.IsNullOrEmpty(nodeId))
        {
            nodeId = (await GetDefaultNodeAsync())?.Id ?? LocalNodeId;
        }

        // 检查缓存
        if (_dockerClients.TryGetValue(nodeId, out var cachedClient))
        {
            try
            {
                // 快速检查连接是否仍然有效
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await cachedClient.System.PingAsync(cts.Token);
                return cachedClient;
            }
            catch
            {
                // 连接失效，移除缓存
                RemoveCachedClient(nodeId);
            }
        }

        await _clientLock.WaitAsync();
        try
        {
            // 双重检查
            if (_dockerClients.TryGetValue(nodeId, out cachedClient))
            {
                return cachedClient;
            }

            var client = await CreateDockerClientForNodeAsync(nodeId);
            _dockerClients[nodeId] = client;
            return client;
        }
        finally
        {
            _clientLock.Release();
        }
    }

    /// <summary>
    /// 获取节点端点
    /// </summary>
    public async Task<Uri?> GetNodeEndpointAsync(string? nodeId = null)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            nodeId = (await GetDefaultNodeAsync())?.Id ?? LocalNodeId;
        }

        if (string.Equals(nodeId, LocalNodeId, StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new Uri("npipe://./pipe/docker_engine")
                : new Uri("unix:///var/run/docker.sock");
        }

        var node = await GetNodeAsync(nodeId);
        if (node == null) return null;

        // 如果已经有自定义端点，直接返回
        if (!string.IsNullOrEmpty(node.DockerEndpoint))
        {
            return new Uri(node.DockerEndpoint);
        }

        // 根据连接类型构建端点
        return node.ConnectionType switch
        {
            DockerConnectionType.Local => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new Uri("npipe://./pipe/docker_engine")
                : new Uri("unix:///var/run/docker.sock"),
            DockerConnectionType.Tcp => new Uri($"tcp://{node.Host}:{node.Port}"),
            DockerConnectionType.Tls => new Uri($"https://{node.Host}:{node.Port}"),
            DockerConnectionType.SshTunnel => new Uri($"tcp://localhost:{await GetOrCreateSshTunnelPortAsync(node)}"),
            _ => null
        };
    }

    /// <summary>
    /// 获取默认节点
    /// </summary>
    public async Task<NodeInfo?> GetDefaultNodeAsync()
    {
        var localNode = EnsureLocalNode();

        var defaultNode = _dbContext.NodeInfos.Query().FirstOrDefault(n => n.IsDefault);
        if (defaultNode != null)
        {
            return defaultNode;
        }

        // 如果没有默认节点，返回第一个在线节点
        var firstNode = _dbContext.NodeInfos.Query().FirstOrDefault();
        return await Task.FromResult(firstNode ?? localNode);
    }

    /// <summary>
    /// 设置默认节点
    /// </summary>
    public async Task SetDefaultNodeAsync(string id)
    {
        EnsureLocalNode();

        var targetNode = _dbContext.NodeInfos.FindById(id);
        if (targetNode == null)
        {
            throw new InvalidOperationException($"节点 '{id}' 不存在");
        }

        var nodes = _dbContext.NodeInfos.Query().ToList();
        foreach (var node in nodes)
        {
            node.IsDefault = node.Id == id;
            _dbContext.NodeInfos.Update(node);
        }

        _logger.LogInformation("设置默认节点: {Id}", id);
        await Task.CompletedTask;
    }

    private async Task<DockerClient> CreateDockerClientForNodeAsync(string nodeId)
    {
        if (string.Equals(nodeId, LocalNodeId, StringComparison.OrdinalIgnoreCase))
        {
            return CreateLocalDockerClient();
        }

        var node = await GetNodeAsync(nodeId);
        if (node == null)
        {
            throw new InvalidOperationException($"节点 '{nodeId}' 不存在");
        }

        return node.ConnectionType switch
        {
            DockerConnectionType.Local => CreateLocalDockerClient(),
            DockerConnectionType.Tcp => CreateTcpDockerClient(node.Host, node.Port, null, node.ConnectionTimeout),
            DockerConnectionType.Tls => CreateTcpDockerClient(node.Host, node.Port, node.TlsConfig, node.ConnectionTimeout),
            DockerConnectionType.SshTunnel => await CreateSshTunnelDockerClientAsync(node),
            _ => throw new NotSupportedException($"不支持的连接类型: {node.ConnectionType}")
        };
    }

    private DockerClient CreateLocalDockerClient()
    {
        var endpoint = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");

        return new DockerClientBuilder()
            .WithEndpoint(endpoint)
            .WithTimeout(Timeout.InfiniteTimeSpan)
            .Build();
    }

    private DockerClient CreateTcpDockerClient(string host, int port, NodeTlsConfig? tlsConfig, int timeout)
    {
        var scheme = tlsConfig?.Enabled == true ? "https" : "tcp";
        var endpoint = new Uri($"{scheme}://{host}:{port}");

        var builder = new DockerClientBuilder()
            .WithEndpoint(endpoint)
            .WithTimeout(TimeSpan.FromSeconds(timeout));

        // TLS 配置（如果需要，Docker.DotNet 支持通过自定义 HttpMessageHandler）
        // 注：复杂的 TLS 配置可能需要自定义 HttpMessageHandler

        return builder.Build();
    }

    private DockerClient CreateTlsDockerClient(string host, int port, NodeTlsConfig tlsConfig, int timeout)
    {
        tlsConfig.Enabled = true;
        return CreateTcpDockerClient(host, port, tlsConfig, timeout);
    }

    private async Task<(SshClient sshClient, int localPort)> CreateSshTunnelAsync(
        string host,
        int sshPort,
        string username,
        string? password,
        string? privateKeyPath,
        string remoteDockerSocket)
    {
        var node = new NodeInfo
        {
            Id = $"test-{Guid.NewGuid():N}",
            SshTunnelConfig = new NodeSshTunnelConfig
            {
                SshHost = host,
                SshPort = sshPort,
                SshUsername = username,
                SshPassword = password,
                SshPrivateKeyPath = privateKeyPath,
                RemoteDockerSocket = remoteDockerSocket,
                LocalForwardPort = GetAvailablePort()
            }
        };

        var localPort = await GetOrCreateSshTunnelPortAsync(node);
        if (!_sshTunnels.TryGetValue(node.Id, out var sshClient))
        {
            throw new InvalidOperationException("SSH 隧道创建失败");
        }

        return (sshClient, localPort);
    }

    private async Task<DockerClient> CreateSshTunnelDockerClientAsync(NodeInfo node)
    {
        var localPort = await GetOrCreateSshTunnelPortAsync(node);
        return CreateTcpDockerClient("localhost", localPort, null, node.ConnectionTimeout);
    }

    private async Task<int> GetOrCreateSshTunnelPortAsync(NodeInfo node)
    {
        var config = node.SshTunnelConfig;
        if (config == null)
        {
            throw new InvalidOperationException("SSH 隧道配置不存在");
        }

        // 检查是否已有活跃的 SSH 隧道
        if (_sshTunnels.TryGetValue(node.Id, out var existingClient) && existingClient.IsConnected)
        {
            return config.LocalForwardPort > 0 ? config.LocalForwardPort : 2375;
        }

        // 创建新的 SSH 隧道
        var connectionInfo = CreateSshConnectionInfo(config);
        var sshClient = new SshClient(connectionInfo);
        sshClient.Connect();

        // 创建端口转发
        var localPort = config.LocalForwardPort > 0 ? config.LocalForwardPort : GetAvailablePort();
        var forwardPort = new ForwardedPortLocal("localhost", (uint)localPort, "localhost", (uint)2375);
        sshClient.AddForwardedPort(forwardPort);
        forwardPort.Start();

        // 缓存 SSH 客户端
        _sshTunnels[node.Id] = sshClient;

        _logger.LogInformation("SSH 隧道已建立: {Host}:{RemotePort} -> localhost:{LocalPort}",
            config.SshHost, config.RemoteDockerSocket, localPort);

        return localPort;
    }

    private Renci.SshNet.ConnectionInfo CreateSshConnectionInfo(NodeSshTunnelConfig config)
    {
        var authMethods = new List<AuthenticationMethod>();

        // 私钥认证
        if (!string.IsNullOrEmpty(config.SshPrivateKeyPath) && File.Exists(config.SshPrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(config.SshPrivateKeyPassphrase)
                ? new PrivateKeyFile(config.SshPrivateKeyPath)
                : new PrivateKeyFile(config.SshPrivateKeyPath, config.SshPrivateKeyPassphrase);
            authMethods.Add(new PrivateKeyAuthenticationMethod(config.SshUsername, keyFile));
        }

        // 密码认证
        if (!string.IsNullOrEmpty(config.SshPassword))
        {
            authMethods.Add(new PasswordAuthenticationMethod(config.SshUsername, config.SshPassword));
        }

        if (authMethods.Count == 0)
        {
            throw new ArgumentException("SSH 认证信息不完整");
        }

        return new Renci.SshNet.ConnectionInfo(config.SshHost, config.SshPort, config.SshUsername, authMethods.ToArray());
    }

    private static int GetAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private void RemoveCachedClient(string nodeId)
    {
        if (_dockerClients.TryRemove(nodeId, out var client))
        {
            client.Dispose();
        }

        if (_sshTunnels.TryRemove(nodeId, out var sshClient))
        {
            sshClient.Disconnect();
            sshClient.Dispose();
        }
    }

    #endregion

    #region 节点统计

    /// <summary>
    /// 获取节点统计信息
    /// </summary>
    public async Task<NodeStats?> GetNodeStatsAsync(string id)
    {
        var node = await GetNodeAsync(id);
        if (node == null) return null;

        try
        {
            var client = await GetDockerClientAsync(id);

            // 获取容器列表
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true });

            // 获取镜像数量
            var images = await client.Images.ListImagesAsync(new ImagesListParameters { All = true });

            // 获取网络数量
            var networks = await client.Networks.ListNetworksAsync();

            // 获取存储卷数量
            var volumes = await client.Volumes.ListAsync(null, CancellationToken.None);

            return new NodeStats
            {
                NodeId = node.Id,
                NodeName = node.Name,
                ContainerCount = containers.Count,
                RunningContainerCount = containers.Count(c => c.State == "running"),
                StoppedContainerCount = containers.Count(c => c.State != "running"),
                ImageCount = images.Count,
                NetworkCount = networks.Count,
                VolumeCount = volumes.Volumes?.Count ?? 0,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取节点 {Id} 统计信息失败", id);
            return new NodeStats
            {
                NodeId = node.Id,
                NodeName = node.Name,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// 获取节点信息
    /// </summary>
    public async Task<NodeInfo?> GetNodeInfoAsync(string id)
    {
        return await GetNodeAsync(id);
    }

    /// <summary>
    /// 获取节点健康状态
    /// </summary>
    public async Task<NodeHealthStatus> GetNodeHealthStatusAsync(string id)
    {
        var node = await GetNodeAsync(id);
        if (node == null)
        {
            return new NodeHealthStatus
            {
                NodeId = id,
                NodeName = "Unknown",
                Status = "unknown",
                Message = "节点不存在",
                IsHealthy = false
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var isHealthy = await TestNodeConnectionAsync(id);
        stopwatch.Stop();

        return new NodeHealthStatus
        {
            NodeId = node.Id,
            NodeName = node.Name,
            Status = isHealthy ? "healthy" : "unhealthy",
            Message = isHealthy ? "节点运行正常" : node.HealthCheckMessage ?? "节点离线",
            LastCheck = DateTime.UtcNow,
            ResponseTime = stopwatch.Elapsed,
            IsHealthy = isHealthy,
            Checks = new Dictionary<string, bool>
            {
                ["Connectivity"] = isHealthy,
                ["Docker Engine"] = isHealthy
            }
        };
    }

    #endregion

    #region 分组管理

    /// <summary>
    /// 获取所有分组
    /// </summary>
    public async Task<IEnumerable<DockerPanel.API.Models.NodeGroup>> GetGroupsAsync()
    {
        // 从节点数据中提取分组信息
        var nodes = _dbContext.NodeInfos.Query().ToList();
        var groups = nodes
            .Where(n => !string.IsNullOrEmpty(n.GroupId))
            .GroupBy(n => n.GroupId)
            .Select(g => new DockerPanel.API.Models.NodeGroup
            {
                Id = g.Key!,
                Name = g.First().GroupName ?? g.Key!,
                NodeIds = g.Select(n => n.Id).ToList(),
                NodeCount = g.Count(),
                OnlineNodeCount = g.Count(n => n.Status == NodeResourceStatus.Online),
                CreatedAt = g.Min(n => n.CreatedAt),
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        return await Task.FromResult(groups);
    }

    /// <summary>
    /// 获取分组
    /// </summary>
    public async Task<DockerPanel.API.Models.NodeGroup?> GetGroupAsync(string id)
    {
        var groups = await GetGroupsAsync();
        return groups.FirstOrDefault(g => g.Id == id);
    }

    /// <summary>
    /// 创建分组
    /// </summary>
    public async Task<DockerPanel.API.Models.NodeGroup> CreateGroupAsync(DockerPanel.API.Models.CreateNodeGroupRequest request)
    {
        var groupId = Guid.NewGuid().ToString();
        var group = new DockerPanel.API.Models.NodeGroup
        {
            Id = groupId,
            Name = request.Name,
            Description = request.Description,
            NodeIds = request.NodeIds,
            Labels = request.Labels,
            Settings = request.Settings ?? new GroupSettings(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 更新节点的分组信息
        foreach (var nodeId in request.NodeIds)
        {
            var node = _dbContext.NodeInfos.FindById(nodeId);
            if (node != null)
            {
                node.GroupId = groupId;
                node.GroupName = request.Name;
                _dbContext.NodeInfos.Update(node);
            }
        }

        _logger.LogInformation("创建节点分组: {Name}", request.Name);
        return await Task.FromResult(group);
    }

    /// <summary>
    /// 更新分组
    /// </summary>
    public async Task UpdateGroupAsync(string id, DockerPanel.API.Models.UpdateNodeGroupRequest request)
    {
        var group = await GetGroupAsync(id);
        if (group == null) return;

        // 更新节点分组
        if (request.NodeIds != null)
        {
            // 移除旧节点的分组
            var oldNodes = _dbContext.NodeInfos.Query().Where(n => n.GroupId == id).ToList();
            foreach (var node in oldNodes)
            {
                if (!request.NodeIds.Contains(node.Id))
                {
                    node.GroupId = null;
                    node.GroupName = null;
                    _dbContext.NodeInfos.Update(node);
                }
            }

            // 添加新节点的分组
            foreach (var nodeId in request.NodeIds)
            {
                var node = _dbContext.NodeInfos.FindById(nodeId);
                if (node != null)
                {
                    node.GroupId = id;
                    node.GroupName = request.Name ?? group.Name;
                    _dbContext.NodeInfos.Update(node);
                }
            }
        }

        _logger.LogInformation("更新节点分组: {Id}", id);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 删除分组
    /// </summary>
    public async Task DeleteGroupAsync(string id)
    {
        // 清除节点的分组信息
        var nodes = _dbContext.NodeInfos.Query().Where(n => n.GroupId == id).ToList();
        foreach (var node in nodes)
        {
            node.GroupId = null;
            node.GroupName = null;
            _dbContext.NodeInfos.Update(node);
        }

        _logger.LogInformation("删除节点分组: {Id}", id);
        await Task.CompletedTask;
    }

    #endregion
}
