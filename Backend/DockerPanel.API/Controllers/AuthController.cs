using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DockerPanel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// 获取认证状态
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthStatusResponse>> GetStatus()
    {
        return Ok(await _authService.GetStatusAsync());
    }

    /// <summary>
    /// 初始化第一个管理员账户
    /// </summary>
    [HttpPost("setup")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> SetupAdmin([FromBody] SetupAdminRequest request)
    {
        var result = await _authService.SetupAdminAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (!result.Success || result.Data == null)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// 登录并签发 JWT
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());

        if (!result.Success || result.Data == null)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        // 将 Token 写入 HttpOnly Cookie
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            // 动态判断：如果是 HTTPS 请求才标记 Secure，否则 HTTP 访问（如 IP 直接访问）会导致浏览器拒绝发送 Cookie 从而秒退
            Secure = Request.IsHttps || Request.Headers["X-Forwarded-Proto"].ToString().Equals("https", StringComparison.OrdinalIgnoreCase), 
            SameSite = SameSiteMode.Lax,
            Expires = result.Data.ExpiresAt
        };
        Response.Cookies.Append("jwt_token", result.Data.AccessToken, cookieOptions);

        // 返回时为了安全可以不再在 Body 里返回 AccessToken，或者继续返回以兼容旧版本但前端不再存 localStorage。
        // 为了渐进式升级，这里仍返回，由前端决定如何处理。
        return Ok(result.Data);
    }

    /// <summary>
    /// 退出登录
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("jwt_token");
        return Ok(new { message = "登出成功" });
    }

    /// <summary>
    /// 获取当前登录用户
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthUserDto>> GetMe()
    {
        var user = await _authService.GetCurrentUserAsync(User);
        return user == null ? Unauthorized(new { message = "未登录或账户不可用。" }) : Ok(user);
    }


    /// <summary>
    /// 修改当前用户密码
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<AuthUserDto>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await _authService.ChangePasswordAsync(User, request);
        if (!result.Success || result.Data == null)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// 获取用户列表（管理员）
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult<IReadOnlyList<UserAccountDto>>> GetUsers()
    {
        return Ok(await _authService.ListUsersAsync());
    }

    /// <summary>
    /// 创建用户（管理员）
    /// </summary>
    [HttpPost("users")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult<UserAccountDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await _authService.CreateUserAsync(request);
        if (!result.Success || result.Data == null)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return CreatedAtAction(nameof(GetUsers), new { id = result.Data.Id }, result.Data);
    }

    /// <summary>
    /// 更新用户（管理员）
    /// </summary>
    [HttpPut("users/{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult<UserAccountDto>> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var result = await _authService.UpdateUserAsync(id, request, User);
        if (!result.Success || result.Data == null)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// 重置用户密码（管理员）
    /// </summary>
    [HttpPost("users/{id}/reset-password")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult<UserAccountDto>> ResetUserPassword(string id, [FromBody] ResetUserPasswordRequest request)
    {
        var result = await _authService.ResetUserPasswordAsync(id, request);
        if (!result.Success || result.Data == null)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// 删除用户（管理员）
    /// </summary>
    [HttpDelete("users/{id}")]
    [Authorize(Roles = AuthRoles.Admin)]
    public async Task<ActionResult> DeleteUser(string id)
    {
        var result = await _authService.DeleteUserAsync(id, User);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return NoContent();
    }
}