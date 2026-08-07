using System.Security.Claims;
using DockerPanel.API.Models;
using DockerPanel.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DockerPanel.API.Endpoints
{
    /// <summary>
    /// 认证/账户 Minimal API 端点（原 AuthController）。
    /// 认证类端点保持 AllowAnonymous / Authorize 语义；管理员路由单独分组。
    /// </summary>
    public static class AuthEndpoints
    {
        /// <summary>
        /// 映射认证相关路由。
        /// </summary>
        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/auth");

            group.MapGet("/status", GetStatus).AllowAnonymous();
            group.MapPost("/setup", SetupAdmin).AllowAnonymous();
            group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("LoginPolicy");
            group.MapPost("/refresh", Refresh).AllowAnonymous();
            group.MapPost("/logout", Logout);
            group.MapGet("/me", GetMe).RequireAuthorization();
            group.MapPost("/change-password", ChangePassword).RequireAuthorization();

            var adminGroup = app.MapGroup("api/auth/users")
                .RequireAuthorization(new AuthorizeAttribute { Roles = AuthRoles.Admin });
            adminGroup.MapGet("/", ListUsers);
            adminGroup.MapPost("/", CreateUser);
            adminGroup.MapPut("/{id}", UpdateUser);
            adminGroup.MapPost("/{id}/reset-password", ResetUserPassword);
            adminGroup.MapDelete("/{id}", DeleteUser);

            return app;
        }

        private static async Task<IResult> GetStatus(IAuthService authService)
        {
            return TypedResults.Ok(await authService.GetStatusAsync());
        }

        private static async Task<IResult> SetupAdmin(HttpContext httpContext, SetupAdminRequest request, IAuthService authService)
        {
            var result = await authService.SetupAdminAsync(request, httpContext.Connection.RemoteIpAddress?.ToString());
            if (!result.Success || result.Data == null)
            {
                return Fail(result);
            }

            SetAuthCookies(httpContext.Response, result.Data);
            return TypedResults.Ok(result.Data);
        }

        private static async Task<IResult> Login(HttpContext httpContext, LoginRequest request, IAuthService authService)
        {
            var result = await authService.LoginAsync(
                request,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString());

            if (!result.Success || result.Data == null)
            {
                return Fail(result);
            }

            SetAuthCookies(httpContext.Response, result.Data);
            return TypedResults.Ok(result.Data);
        }

        private static async Task<IResult> Refresh(HttpContext httpContext, IAuthService authService, ILocalizationService localization)
        {
            if (!httpContext.Request.Cookies.TryGetValue("refresh_token", out var refreshToken))
            {
                return TypedResults.Json(new ApiErrorResponse { Code = "REFRESH_INVALID", Message = "未找到刷新凭证。" }, statusCode: 401);
            }

            var result = await authService.RefreshTokenAsync(
                refreshToken,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString());

            if (!result.Success || result.Data == null)
            {
                return Fail(result);
            }

            SetAuthCookies(httpContext.Response, result.Data);
            return TypedResults.Ok(result.Data);
        }

        private static async Task<IResult> Logout(HttpContext httpContext, IAuthService authService)
        {
            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                await authService.InvalidateRefreshTokenAsync(httpContext.User);
            }

            httpContext.Response.Cookies.Delete("jwt_token");
            httpContext.Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/api/auth/refresh" });
            return TypedResults.Ok(new MessageResponse { Message = "登出成功" });
        }

        private static async Task<IResult> GetMe(ClaimsPrincipal user, IAuthService authService)
        {
            var currentUser = await authService.GetCurrentUserAsync(user);
            return currentUser == null ? TypedResults.Unauthorized() : TypedResults.Ok(currentUser);
        }

        private static async Task<IResult> ChangePassword(ClaimsPrincipal user, ChangePasswordRequest request, IAuthService authService)
        {
            var result = await authService.ChangePasswordAsync(user, request);
            if (!result.Success || result.Data == null)
            {
                return Fail(result);
            }

            return TypedResults.Ok(result.Data);
        }

        private static async Task<IResult> ListUsers(IAuthService authService)
        {
            return TypedResults.Ok(await authService.ListUsersAsync());
        }

        private static async Task<IResult> CreateUser(CreateUserRequest request, IAuthService authService)
        {
            var result = await authService.CreateUserAsync(request);
            if (!result.Success || result.Data == null)
            {
                return Fail(result);
            }

            return TypedResults.Created($"/api/auth/users/{result.Data.Id}", result.Data);
        }

        private static async Task<IResult> UpdateUser(string id, UpdateUserRequest request, ClaimsPrincipal user, IAuthService authService)
        {
            var result = await authService.UpdateUserAsync(id, request, user);
            if (!result.Success || result.Data == null)
            {
                return Fail(result);
            }

            return TypedResults.Ok(result.Data);
        }

        private static async Task<IResult> ResetUserPassword(string id, ResetUserPasswordRequest request, IAuthService authService)
        {
            var result = await authService.ResetUserPasswordAsync(id, request);
            if (!result.Success || result.Data == null)
            {
                return Fail(result);
            }

            return TypedResults.Ok(result.Data);
        }

        private static async Task<IResult> DeleteUser(string id, ClaimsPrincipal user, IAuthService authService)
        {
            var result = await authService.DeleteUserAsync(id, user);
            if (!result.Success)
            {
                return Fail(result);
            }

            return TypedResults.NoContent();
        }

        /// <summary>
        /// 将 AuthServiceResult 转成带 code/message 的统一错误响应。
        /// </summary>
        private static IResult Fail<T>(AuthServiceResult<T> result)
        {
            return TypedResults.Json(new ApiErrorResponse { Code = result.Code, Message = result.Message }, statusCode: result.StatusCode);
        }

        private static void SetAuthCookies(HttpResponse response, LoginResponse data)
        {
            var secure = response.HttpContext.Request.IsHttps
                || response.HttpContext.Request.Headers["X-Forwarded-Proto"].ToString().Equals("https", StringComparison.OrdinalIgnoreCase);

            var jwtOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                Expires = data.ExpiresAt
            };
            response.Cookies.Append("jwt_token", data.AccessToken, jwtOptions);

            var refreshOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                Expires = data.RefreshTokenExpiry,
                Path = "/api/auth/refresh"
            };
            response.Cookies.Append("refresh_token", data.RefreshToken, refreshOptions);
        }
    }
}