using System.ComponentModel.DataAnnotations;
using TinyDb.Attributes;

namespace DockerPanel.API.Models;

public static class AuthRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Viewer = "Viewer";
    public const string LegacyUser = "User";

    public const string AdminOrOperator = Admin + "," + Operator;

    public static readonly string[] All = [Admin, Operator, Viewer];

    public static bool IsValid(string? role) =>
        All.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)) ||
        string.Equals(role, LegacyUser, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? role)
    {
        if (string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase)) return Admin;
        if (string.Equals(role, Viewer, StringComparison.OrdinalIgnoreCase)) return Viewer;
        return Operator;
    }
}

/// <summary>
/// 面板用户账户
/// </summary>
[Entity]
public class UserAccount
{
    [Id]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Index]
    [Required]
    public string Username { get; set; } = string.Empty;

    [Index]
    public string NormalizedUsername { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = AuthRoles.Admin;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AuthUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UserAccountDto : AuthUserDto
{
    public bool IsActive { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AuthStatusResponse
{
    public bool Enabled { get; set; } = true;
    public bool HasUsers { get; set; }
    public bool RequiresSetup { get; set; }
    public string DefaultAdminUsername { get; set; } = "admin";
    public bool CanBootstrapFromEnvironment { get; set; }
}

public class SetupAdminRequest
{
    [Required]
    [StringLength(64, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [StringLength(64)]
    public string? DisplayName { get; set; }
}

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public AuthUserDto User { get; set; } = new();
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class CreateUserRequest
{
    [Required]
    [StringLength(64, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [StringLength(64)]
    public string? DisplayName { get; set; }

    [StringLength(16)]
    public string Role { get; set; } = AuthRoles.Operator;

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
}

public class UpdateUserRequest
{
    [StringLength(64)]
    public string? DisplayName { get; set; }

    [StringLength(16)]
    public string? Role { get; set; }

    public bool? IsActive { get; set; }
    public bool? MustChangePassword { get; set; }
}

public class ResetUserPasswordRequest
{
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; } = true;
}

public class AuthServiceResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; } = StatusCodes.Status400BadRequest;

    public static AuthServiceResult<T> Ok(T data) => new()
    {
        Success = true,
        Data = data,
        StatusCode = StatusCodes.Status200OK
    };

    public static AuthServiceResult<T> Fail(string message, int statusCode = StatusCodes.Status400BadRequest) => new()
    {
        Success = false,
        Message = message,
        StatusCode = statusCode
    };
}