using TinyDb;
using TinyDb.Attributes;
using TinyDb.Collections;
using TinyDb.Core;

namespace DockerPanel.API.Data;

/// <summary>
/// TinyDb数据库上下文
/// </summary>
public class TinyDbContext : IDisposable
{
    private readonly TinyDbEngine _database;
    private static readonly object CollectionAccessLock = new();
    private ITransaction? _currentTransaction;
    private bool _disposed = false;

    public TinyDbContext(TinyDbEngine database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <summary>
    /// 获取集合
    /// </summary>
    public ITinyCollection<T> GetCollection<T>(string? collectionName = null) where T : class, new()
    {
        // TinyDb 0.4.x 在首次创建集合元数据/索引时并发访问可能触发系统目录重复写入。
        // 统一串行化集合解析，避免后台服务同时启动时损坏 __sys_catalog 或索引页。
        lock (CollectionAccessLock)
        {
            return _database.GetCollection<T>(collectionName ?? typeof(T).Name);
        }
    }

    /// <summary>
    /// 创建索引
    /// </summary>
    public void CreateIndexes()
    {
        // TinyDb 使用 [Index] 属性自动创建索引
        // 这里不需要手动创建，索引在实体类中标记
    }

    // 添加DbSet属性以支持Entity Framework风格的访问
    public ITinyCollection<Models.SystemSettings> Settings => GetCollection<Models.SystemSettings>();
    public ITinyCollection<Models.ComposeFile> ComposeFiles => GetCollection<Models.ComposeFile>();
    public ITinyCollection<Models.ImageRegistry> ImageRegistries => GetCollection<Models.ImageRegistry>();
    public ITinyCollection<Models.ContainerInfo> ContainerInfos => GetCollection<Models.ContainerInfo>();
    public ITinyCollection<Models.ImageInfo> ImageInfos => GetCollection<Models.ImageInfo>();
    public ITinyCollection<Models.NetworkInfo> NetworkInfos => GetCollection<Models.NetworkInfo>();
    public ITinyCollection<Models.VolumeInfo> VolumeInfos => GetCollection<Models.VolumeInfo>();
    public ITinyCollection<Models.NodeInfo> NodeInfos => GetCollection<Models.NodeInfo>();
    public ITinyCollection<Models.ContainerTemplate> ContainerTemplates => GetCollection<Models.ContainerTemplate>();
    public ITinyCollection<Models.ContainerAutoUpdateConfig> AutoUpdateConfigs => GetCollection<Models.ContainerAutoUpdateConfig>();
    public ITinyCollection<Models.GlobalAutoUpdateSettings> GlobalAutoUpdateSettings => GetCollection<Models.GlobalAutoUpdateSettings>();
    public ITinyCollection<Models.UserAccount> UserAccounts => GetCollection<Models.UserAccount>("auth_users");
    
    // ACME Challenge 集合（合并到主数据库）
    public ITinyCollection<Services.Acme.HttpChallengeEntity> HttpChallenges => GetCollection<Services.Acme.HttpChallengeEntity>();
    public ITinyCollection<Services.Acme.DnsChallengeEntity> DnsChallenges => GetCollection<Services.Acme.DnsChallengeEntity>();

    /// <summary>
    /// 保存更改（兼容Entity Framework风格）
    /// </summary>
    public async Task<int> SaveChangesAsync()
    {
        // TinyDb是即时保存的，这里返回0表示没有受影响的行数
        // 实际的保存操作在Insert/Update/Delete时已经完成
        return await Task.FromResult(0);
    }

    /// <summary>
    /// 开始事务
    /// </summary>
    public void BeginTrans()
    {
        _currentTransaction = _database.BeginTransaction();
    }

    /// <summary>
    /// 提交事务
    /// </summary>
    public void Commit()
    {
        _currentTransaction?.Commit();
        _currentTransaction?.Dispose();
        _currentTransaction = null;
    }

    /// <summary>
    /// 回滚事务
    /// </summary>
    public void Rollback()
    {
        _currentTransaction?.Rollback();
        _currentTransaction?.Dispose();
        _currentTransaction = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
            // 注意：不释放 _database，因为它是 Singleton，由 DI 容器管理生命周期
            _disposed = true;
        }
    }
}
