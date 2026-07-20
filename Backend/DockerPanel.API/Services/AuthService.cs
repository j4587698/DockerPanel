using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DockerPanel.API.Data;
using DockerPanel.API.Models;
using Microsoft.IdentityModel.Tokens;

namespace DockerPanel.API.Services;

public interface IAuthService
{
    Task<AuthStatusResponse> GetStatusAsync();
    Task<AuthServiceResult<LoginResponse>> SetupAdminAsync(SetupAdminRequest request, string? ipAddress);
    Task<AuthServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent);
    Task<AuthServiceResult<LoginResponse>> RefreshTokenAsync(string oldToken, string? ipAddress, string? userAgent);
    Task InvalidateRefreshTokenAsync(ClaimsPrincipal principal);
    Task<AuthUserDto?> GetCurrentUserAsync(ClaimsPrincipal principal);
    Task<AuthServiceResult<AuthUserDto>> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request);
    Task<IReadOnlyList<UserAccountDto>> ListUsersAsync();
    Task<AuthServiceResult<UserAccountDto>> CreateUserAsync(CreateUserRequest request);
    Task<AuthServiceResult<UserAccountDto>> UpdateUserAsync(string id, UpdateUserRequest request, ClaimsPrincipal principal);
    Task<AuthServiceResult<UserAccountDto>> ResetUserPasswordAsync(string id, ResetUserPasswordRequest request);
    Task<AuthServiceResult<bool>> DeleteUserAsync(string id, ClaimsPrincipal principal);
}

/// <summary>
/// JWT 密钥提供器。优先读取配置/环境变量，未配置时生成并保存到 Data 目录，确保重启后 token 仍可验证。
/// </summary>
public sealed class JwtSecretProvider
{
    private const int MinimumSecretLength = 32;

    public JwtSecretProvider(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Issuer = configuration["Auth:Issuer"] ?? "DockerPanel";
        Audience = configuration["Auth:Audience"] ?? "DockerPanel.Web";
        Secret = ResolveOrCreateSecret(configuration, environment);
    }

    public string Issuer { get; }
    public string Audience { get; }
    public string Secret { get; }

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Secret));

    private static string ResolveOrCreateSecret(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredSecret = configuration["Auth:JwtSecret"]
            ?? configuration["Jwt:Secret"]
            ?? Environment.GetEnvironmentVariable("DOCKERPANEL_JWT_SECRET");

        if (!string.IsNullOrWhiteSpace(configuredSecret))
        {
            var secret = configuredSecret.Trim();
            if (secret.Length < MinimumSecretLength)
            {
                throw new InvalidOperationException($"JWT 密钥长度不能小于 {MinimumSecretLength} 个字符，请设置 Auth:JwtSecret 或 DOCKERPANEL_JWT_SECRET。");
            }

            return secret;
        }

        var configuredFile = configuration["Auth:JwtSecretFile"];
        string secretFile;
        
        if (string.IsNullOrWhiteSpace(configuredFile) || configuredFile == "Data/jwt-secret.key")
        {
            secretFile = DockerPanel.API.Utils.AppPathResolver.GetJwtSecretPath();
        }
        else
        {
            secretFile = Path.IsPathRooted(configuredFile)
                ? configuredFile
                : Path.Combine(environment.ContentRootPath, configuredFile);
        }

        var secretDirectory = Path.GetDirectoryName(secretFile);
        if (!string.IsNullOrWhiteSpace(secretDirectory))
        {
            Directory.CreateDirectory(secretDirectory);
        }

        if (File.Exists(secretFile))
        {
            var storedSecret = File.ReadAllText(secretFile).Trim();
            if (storedSecret.Length >= MinimumSecretLength)
            {
                return storedSecret;
            }
        }

        var generatedSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        File.WriteAllText(secretFile, generatedSecret);
        return generatedSecret;
    }
}

/// <summary>
/// 面板认证服务
/// </summary>
public class AuthService : IAuthService
{
    private static readonly Regex UsernameRegex = new("^[a-zA-Z0-9_.@-]{3,64}$", RegexOptions.Compiled);
    private static readonly SemaphoreSlim SetupLock = new(1, 1);
    private const string UsersCollectionName = "auth_users";

    private readonly TinyDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly JwtSecretProvider _jwtSecretProvider;
    private readonly ILogger<AuthService> _logger;
    private readonly ILocalizationService _localization;

    public AuthService(
        TinyDbContext dbContext,
        IConfiguration configuration,
        JwtSecretProvider jwtSecretProvider,
        ILogger<AuthService> logger,
        ILocalizationService localization)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _jwtSecretProvider = jwtSecretProvider;
        _logger = logger;
        _localization = localization;
    }

    public async Task<AuthStatusResponse> GetStatusAsync()
    {
        await EnsureConfiguredAdminAsync();

        var users = GetUsers().FindAll().AsEnumerable().ToList();
        return new AuthStatusResponse
        {
            Enabled = true,
            HasUsers = users.Count > 0,
            RequiresSetup = users.Count == 0,
            DefaultAdminUsername = GetConfiguredAdminUsername(),
            CanBootstrapFromEnvironment = !string.IsNullOrWhiteSpace(GetConfiguredAdminPassword())
        };
    }

    public async Task<AuthServiceResult<LoginResponse>> SetupAdminAsync(SetupAdminRequest request, string? ipAddress)
    {
        await EnsureConfiguredAdminAsync();

        await SetupLock.WaitAsync();
        try
        {
            var users = GetUsers();
            if (users.FindAll().AsEnumerable().Any())
            {
                return AuthServiceResult<LoginResponse>.Fail("管理员已初始化，请直接登录。", StatusCodes.Status409Conflict);
            }

            var validationError = await ValidateUserInputAsync(request.Username, request.Password);
            if (validationError != null)
            {
                return AuthServiceResult<LoginResponse>.Fail(validationError);
            }

            var now = DateTime.UtcNow;
            var user = new UserAccount
            {
                Id = Guid.NewGuid().ToString("N"),
                Username = request.Username.Trim(),
                NormalizedUsername = NormalizeUsername(request.Username),
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username.Trim() : request.DisplayName.Trim(),
                PasswordHash = HashPassword(request.Password),
                Role = AuthRoles.Admin,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                LastLoginAt = now,
                LastLoginIp = ipAddress
            };

            users.Insert(user);
            _logger.LogInformation("已初始化管理员账户: {Username}", user.Username);

            return AuthServiceResult<LoginResponse>.Ok(CreateLoginResponse(user));
        }
        finally
        {
            SetupLock.Release();
        }
    }

    public async Task<AuthServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent)
    {
        await EnsureConfiguredAdminAsync();

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthServiceResult<LoginResponse>.Fail("用户名或密码不能为空。", StatusCodes.Status400BadRequest);
        }

        var users = GetUsers();
        var user = FindByUsername(request.Username);
        if (user == null)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            return AuthServiceResult<LoginResponse>.Fail(_localization.GetMessage("error.invalidCredentials", "用户名或密码错误。"), StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS");
        }

        NormalizeStoredRole(users, user);

        if (!user.IsActive)
        {
            return AuthServiceResult<LoginResponse>.Fail(_localization.GetMessage("error.accountDisabled", "账户已禁用。"), StatusCodes.Status403Forbidden, "ACCOUNT_DISABLED");
        }

        var now = DateTime.UtcNow;
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > now)
        {
            return AuthServiceResult<LoginResponse>.Fail($"账户已锁定，请在 {user.LockedUntil.Value:yyyy-MM-dd HH:mm:ss} UTC 后重试。", StatusCodes.Status423Locked);
        }

        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            var policy = GetLoginPolicy();
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= policy.MaxLoginAttempts)
            {
                user.LockedUntil = now.AddMinutes(policy.LockoutDurationMinutes);
                _logger.LogWarning("用户 {Username} 登录失败次数过多，锁定到 {LockedUntil}，IP: {IpAddress}, UA: {UserAgent}",
                    user.Username, user.LockedUntil, ipAddress, userAgent);
            }
            user.UpdatedAt = now;
            users.Update(user);

            return AuthServiceResult<LoginResponse>.Fail(_localization.GetMessage("error.invalidCredentials", "用户名或密码错误。"), StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS");
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        user.LastLoginIp = ipAddress;
        user.UpdatedAt = now;
        users.Update(user);

        _logger.LogInformation("用户 {Username} 登录成功，IP: {IpAddress}", user.Username, ipAddress);
        return AuthServiceResult<LoginResponse>.Ok(CreateLoginResponse(user));
    }

    public Task<AuthServiceResult<LoginResponse>> RefreshTokenAsync(string oldToken, string? ipAddress, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(oldToken))
        {
            return Task.FromResult(AuthServiceResult<LoginResponse>.Fail(_localization.GetMessage("error.refreshInvalid", "无效的 Refresh Token。"), StatusCodes.Status401Unauthorized, "REFRESH_INVALID"));
        }

        var users = GetUsers();
        var user = users.FindAll().AsEnumerable().FirstOrDefault(u => u.RefreshToken == oldToken);

        if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
        {
            return Task.FromResult(AuthServiceResult<LoginResponse>.Fail(_localization.GetMessage("error.refreshExpired", "Refresh Token 已失效或过期，请重新登录。"), StatusCodes.Status401Unauthorized, "REFRESH_EXPIRED"));
        }

        if (!user.IsActive || (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow))
        {
            return Task.FromResult(AuthServiceResult<LoginResponse>.Fail(_localization.GetMessage("error.accountDisabled", "账户已被禁用或锁定。"), StatusCodes.Status401Unauthorized, "ACCOUNT_DISABLED"));
        }

        user.LastLoginAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            user.LastLoginIp = ipAddress;
        }
        
        _logger.LogInformation("用户 {Username} 通过 Refresh Token 刷新了登录状态，IP: {IpAddress}", user.Username, ipAddress);
        return Task.FromResult(AuthServiceResult<LoginResponse>.Ok(CreateLoginResponse(user)));
    }

    public Task InvalidateRefreshTokenAsync(ClaimsPrincipal principal)
    {
        var userId = GetUserId(principal);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var users = GetUsers();
            var user = users.FindById(userId);
            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
                users.Update(user);
            }
        }
        return Task.CompletedTask;
    }

    public Task<AuthUserDto?> GetCurrentUserAsync(ClaimsPrincipal principal)
    {
        var userId = GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<AuthUserDto?>(null);
        }

        var users = GetUsers();
        var user = users.FindById(userId);
        if (user != null)
        {
            NormalizeStoredRole(users, user);
        }

        return Task.FromResult(user is { IsActive: true } ? ToDto(user) : null);
    }

    public async Task<AuthServiceResult<AuthUserDto>> ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest request)
    {
        var userId = GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return AuthServiceResult<AuthUserDto>.Fail("未登录。", StatusCodes.Status401Unauthorized);
        }

        var users = GetUsers();
        var user = users.FindById(userId);
        if (user is not { IsActive: true })
        {
            return AuthServiceResult<AuthUserDto>.Fail("账户不存在或已禁用。", StatusCodes.Status404NotFound);
        }

        NormalizeStoredRole(users, user);

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return AuthServiceResult<AuthUserDto>.Fail("当前密码错误。", StatusCodes.Status400BadRequest);
        }

        var validationError = await ValidateUserInputAsync(user.Username, request.NewPassword, validateUsername: false);
        if (validationError != null)
        {
            return AuthServiceResult<AuthUserDto>.Fail(validationError);
        }

        user.PasswordHash = HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        users.Update(user);

        _logger.LogInformation("用户 {Username} 已修改密码", user.Username);
        return AuthServiceResult<AuthUserDto>.Ok(ToDto(user));
    }

    public Task<IReadOnlyList<UserAccountDto>> ListUsersAsync()
    {
        var users = GetUsers()
            .FindAll()
            .AsEnumerable()
            .OrderByDescending(u => string.Equals(AuthRoles.Normalize(u.Role), AuthRoles.Admin, StringComparison.OrdinalIgnoreCase))
            .ThenBy(u => string.Equals(AuthRoles.Normalize(u.Role), AuthRoles.Operator, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .Select(ToAccountDto)
            .ToList();

        return Task.FromResult<IReadOnlyList<UserAccountDto>>(users);
    }

    public async Task<AuthServiceResult<UserAccountDto>> CreateUserAsync(CreateUserRequest request)
    {
        await EnsureConfiguredAdminAsync();

        var validationError = await ValidateUserInputAsync(request.Username, request.Password);
        if (validationError != null)
        {
            return AuthServiceResult<UserAccountDto>.Fail(validationError);
        }

        if (!AuthRoles.IsValid(request.Role))
        {
            return AuthServiceResult<UserAccountDto>.Fail("用户角色无效。");
        }

        if (FindByUsername(request.Username) != null)
        {
            return AuthServiceResult<UserAccountDto>.Fail("用户名已存在。", StatusCodes.Status409Conflict);
        }

        var now = DateTime.UtcNow;
        var user = new UserAccount
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = request.Username.Trim(),
            NormalizedUsername = NormalizeUsername(request.Username),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username.Trim() : request.DisplayName.Trim(),
            PasswordHash = HashPassword(request.Password),
            Role = AuthRoles.Normalize(request.Role),
            IsActive = request.IsActive,
            MustChangePassword = request.MustChangePassword,
            CreatedAt = now,
            UpdatedAt = now
        };

        GetUsers().Insert(user);
        _logger.LogInformation("管理员创建用户: {Username}, Role={Role}, IsActive={IsActive}", user.Username, user.Role, user.IsActive);

        return AuthServiceResult<UserAccountDto>.Ok(ToAccountDto(user));
    }

    public Task<AuthServiceResult<UserAccountDto>> UpdateUserAsync(string id, UpdateUserRequest request, ClaimsPrincipal principal)
    {
        var users = GetUsers();
        var user = users.FindById(id);
        if (user == null)
        {
            return Task.FromResult(AuthServiceResult<UserAccountDto>.Fail("用户不存在。", StatusCodes.Status404NotFound));
        }
        NormalizeStoredRole(users, user);

        var currentUserId = GetUserId(principal);
        var nextRole = request.Role == null ? AuthRoles.Normalize(user.Role) : AuthRoles.Normalize(request.Role);
        var nextIsActive = request.IsActive ?? user.IsActive;

        if (request.Role != null && !AuthRoles.IsValid(request.Role))
        {
            return Task.FromResult(AuthServiceResult<UserAccountDto>.Fail("用户角色无效。"));
        }

        if (string.Equals(currentUserId, user.Id, StringComparison.Ordinal) && !nextIsActive)
        {
            return Task.FromResult(AuthServiceResult<UserAccountDto>.Fail("不能禁用当前登录账户。"));
        }

        if (string.Equals(currentUserId, user.Id, StringComparison.Ordinal) && !string.Equals(nextRole, user.Role, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthServiceResult<UserAccountDto>.Fail("不能修改当前登录账户的角色。"));
        }

        if (WouldRemoveLastActiveAdmin(user, nextRole, nextIsActive))
        {
            return Task.FromResult(AuthServiceResult<UserAccountDto>.Fail("至少需要保留一个启用的管理员账户。"));
        }

        if (request.DisplayName != null)
        {
            user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? user.Username : request.DisplayName.Trim();
        }

        if (request.Role != null)
        {
            user.Role = nextRole;
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = nextIsActive;
        }

        if (request.MustChangePassword.HasValue)
        {
            user.MustChangePassword = request.MustChangePassword.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;
        users.Update(user);

        _logger.LogInformation("管理员更新用户: {Username}, Role={Role}, IsActive={IsActive}", user.Username, user.Role, user.IsActive);
        return Task.FromResult(AuthServiceResult<UserAccountDto>.Ok(ToAccountDto(user)));
    }

    public async Task<AuthServiceResult<UserAccountDto>> ResetUserPasswordAsync(string id, ResetUserPasswordRequest request)
    {
        var users = GetUsers();
        var user = users.FindById(id);
        if (user == null)
        {
            return AuthServiceResult<UserAccountDto>.Fail("用户不存在。", StatusCodes.Status404NotFound);
        }
        NormalizeStoredRole(users, user);

        var validationError = await ValidateUserInputAsync(user.Username, request.NewPassword, validateUsername: false);
        if (validationError != null)
        {
            return AuthServiceResult<UserAccountDto>.Fail(validationError);
        }

        user.PasswordHash = HashPassword(request.NewPassword);
        user.MustChangePassword = request.MustChangePassword;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        users.Update(user);

        _logger.LogInformation("管理员重置用户密码: {Username}", user.Username);
        return AuthServiceResult<UserAccountDto>.Ok(ToAccountDto(user));
    }

    public Task<AuthServiceResult<bool>> DeleteUserAsync(string id, ClaimsPrincipal principal)
    {
        var currentUserId = GetUserId(principal);
        if (string.Equals(currentUserId, id, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthServiceResult<bool>.Fail("不能删除当前登录账户。"));
        }

        var users = GetUsers();
        var user = users.FindById(id);
        if (user == null)
        {
            return Task.FromResult(AuthServiceResult<bool>.Fail("用户不存在。", StatusCodes.Status404NotFound));
        }
        NormalizeStoredRole(users, user);

        if (WouldRemoveLastActiveAdmin(user, user.Role, isActive: false))
        {
            return Task.FromResult(AuthServiceResult<bool>.Fail("至少需要保留一个启用的管理员账户。"));
        }

        users.Delete(id);
        _logger.LogInformation("管理员删除用户: {Username}", user.Username);
        return Task.FromResult(AuthServiceResult<bool>.Ok(true));
    }

    private TinyDb.Collections.ITinyCollection<UserAccount> GetUsers() => _dbContext.GetCollection<UserAccount>(UsersCollectionName);

    private static void NormalizeStoredRole(TinyDb.Collections.ITinyCollection<UserAccount> users, UserAccount user)
    {
        var normalizedRole = AuthRoles.Normalize(user.Role);
        if (string.Equals(user.Role, normalizedRole, StringComparison.Ordinal))
        {
            return;
        }

        user.Role = normalizedRole;
        user.UpdatedAt = DateTime.UtcNow;
        users.Update(user);
    }

    private UserAccount? FindByUsername(string username)
    {
        var normalizedUsername = NormalizeUsername(username);
        return GetUsers()
            .FindAll()
            .AsEnumerable()
            .FirstOrDefault(u => string.Equals(u.NormalizedUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureConfiguredAdminAsync()
    {
        await SetupLock.WaitAsync();
        try
        {
            await EnsureConfiguredAdminCoreAsync();
        }
        finally
        {
            SetupLock.Release();
        }
    }

    private async Task EnsureConfiguredAdminCoreAsync()
    {
        var users = GetUsers();
        if (users.FindAll().AsEnumerable().Any())
        {
            return;
        }

        var configuredPassword = GetConfiguredAdminPassword();
        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            return;
        }

        var configuredUsername = GetConfiguredAdminUsername();
        var validationError = await ValidateUserInputAsync(configuredUsername, configuredPassword);
        if (validationError != null)
        {
            _logger.LogWarning("环境变量/配置中的管理员账户无效，跳过自动初始化: {Message}", validationError);
            return;
        }

        var now = DateTime.UtcNow;
        users.Insert(new UserAccount
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = configuredUsername,
            NormalizedUsername = NormalizeUsername(configuredUsername),
            DisplayName = _configuration["Auth:AdminDisplayName"] ?? configuredUsername,
            PasswordHash = HashPassword(configuredPassword),
            Role = AuthRoles.Admin,
            IsActive = true,
            MustChangePassword = _configuration.GetValue("Auth:AdminMustChangePassword", false),
            CreatedAt = now,
            UpdatedAt = now
        });

        _logger.LogInformation("已通过环境变量/配置自动初始化管理员账户: {Username}", configuredUsername);
    }

    private string GetConfiguredAdminUsername()
    {
        return (_configuration["Auth:AdminUsername"]
            ?? Environment.GetEnvironmentVariable("DOCKERPANEL_ADMIN_USERNAME")
            ?? "admin").Trim();
    }

    private string? GetConfiguredAdminPassword()
    {
        return _configuration["Auth:AdminPassword"]
            ?? Environment.GetEnvironmentVariable("DOCKERPANEL_ADMIN_PASSWORD");
    }

    private LoginResponse CreateLoginResponse(UserAccount user)
    {
        var expirationMinutes = GetTokenExpirationMinutes();
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = refreshTokenExpiry;
        GetUsers().Update(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
            new("displayName", user.DisplayName),
            new("mustChangePassword", user.MustChangePassword.ToString().ToLowerInvariant())
        };

        var credentials = new SigningCredentials(_jwtSecretProvider.CreateSigningKey(), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtSecretProvider.Issuer,
            audience: _jwtSecretProvider.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            User = ToDto(user),
            RefreshToken = refreshToken,
            RefreshTokenExpiry = refreshTokenExpiry
        };
    }

    private int GetTokenExpirationMinutes()
    {
        var configured = _configuration.GetValue<int?>("Auth:AccessTokenExpirationMinutes")
            ?? _configuration.GetValue<int?>("Auth:JwtExpirationMinutes");
        if (configured.HasValue)
        {
            return Math.Clamp(configured.Value, 5, 1440);
        }

        var settings = _dbContext.Settings.FindById("default");
        return Math.Clamp(settings?.Security.JwtExpirationMinutes ?? 60, 5, 1440);
    }

    private (int MaxLoginAttempts, int LockoutDurationMinutes) GetLoginPolicy()
    {
        var settings = _dbContext.Settings.FindById("default");
        var maxLoginAttempts = _configuration.GetValue<int?>("Auth:MaxLoginAttempts") ?? settings?.MaxLoginAttempts ?? 5;
        var lockoutDurationMinutes = _configuration.GetValue<int?>("Auth:LockoutDurationMinutes") ?? settings?.LockoutDurationMinutes ?? 15;

        return (Math.Clamp(maxLoginAttempts, 1, 20), Math.Clamp(lockoutDurationMinutes, 1, 1440));
    }

    private async Task<string?> ValidateUserInputAsync(string username, string password, bool validateUsername = true)
    {
        await Task.CompletedTask;

        if (validateUsername && !UsernameRegex.IsMatch(username.Trim()))
        {
            return "用户名长度需为 3-64 位，只能包含字母、数字、下划线、点、@ 和短横线。";
        }

        var security = _dbContext.Settings.FindById("default")?.Security ?? new SecuritySettings();
        var passwordMinLength = Math.Clamp(security.PasswordMinLength <= 0 ? 8 : security.PasswordMinLength, 6, 32);
        if (string.IsNullOrWhiteSpace(password) || password.Length < passwordMinLength)
        {
            return $"密码长度不能小于 {passwordMinLength} 位。";
        }

        if (security.RequireUppercase && !password.Any(char.IsUpper))
        {
            return "密码必须包含大写字母。";
        }
        if (security.RequireLowercase && !password.Any(char.IsLower))
        {
            return "密码必须包含小写字母。";
        }
        if (security.RequireNumbers && !password.Any(char.IsDigit))
        {
            return "密码必须包含数字。";
        }
        if (security.RequireSpecialChars && !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return "密码必须包含特殊字符。";
        }

        return null;
    }

    private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    private static string HashPassword(string password)
    {
        const int iterations = 100_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2-SHA256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || !string.Equals(parts[0], "PBKDF2-SHA256", StringComparison.Ordinal))
            {
                return false;
            }

            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static string? GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    }

    private static AuthUserDto ToDto(UserAccount user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName,
        Role = AuthRoles.Normalize(user.Role),
        MustChangePassword = user.MustChangePassword,
        LastLoginAt = user.LastLoginAt
    };

    private static UserAccountDto ToAccountDto(UserAccount user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName,
        Role = AuthRoles.Normalize(user.Role),
        IsActive = user.IsActive,
        MustChangePassword = user.MustChangePassword,
        FailedLoginAttempts = user.FailedLoginAttempts,
        LockedUntil = user.LockedUntil,
        LastLoginAt = user.LastLoginAt,
        LastLoginIp = user.LastLoginIp,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };

    private bool WouldRemoveLastActiveAdmin(UserAccount targetUser, string nextRole, bool isActive)
    {
        var activeAdminCount = GetUsers()
            .FindAll()
            .AsEnumerable()
            .Count(u => u.IsActive && string.Equals(AuthRoles.Normalize(u.Role), AuthRoles.Admin, StringComparison.OrdinalIgnoreCase));

        var wasActiveAdmin = targetUser.IsActive && string.Equals(AuthRoles.Normalize(targetUser.Role), AuthRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var willBeActiveAdmin = isActive && string.Equals(AuthRoles.Normalize(nextRole), AuthRoles.Admin, StringComparison.OrdinalIgnoreCase);

        return wasActiveAdmin && !willBeActiveAdmin && activeAdminCount <= 1;
    }
}