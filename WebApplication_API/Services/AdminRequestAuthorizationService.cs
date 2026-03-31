using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Model;

namespace WebApplication_API.Services;

public sealed class AdminRequestAuthorizationService(AdminSessionTokenService tokenService)
{
    private const string BearerPrefix = "Bearer ";

    public async Task<AdminAuthorizationResult> AuthorizeAsync(
        HttpContext httpContext,
        DBContext dbContext,
        string requiredPermission)
    {
        var token = ReadToken(httpContext);
        if (string.IsNullOrWhiteSpace(token))
        {
            return AdminAuthorizationResult.Failed(401, "Authentication is required.");
        }

        if (!tokenService.TryGet(token, out var ticket) || ticket.ExpiresAt <= DateTime.UtcNow)
        {
            tokenService.Remove(token);
            return AdminAuthorizationResult.Failed(401, "The current session has expired.");
        }

        var user = await dbContext.DashboardUsers.FirstOrDefaultAsync(item => item.UserId == ticket.UserId);
        if (user is null)
        {
            tokenService.Remove(token);
            return AdminAuthorizationResult.Failed(401, "The current session is no longer valid.");
        }

        if (user.Status != 1)
        {
            tokenService.Remove(token);
            return AdminAuthorizationResult.Failed(403, "Your account is inactive.");
        }

        if (!AdminRolePolicies.HasPermission(user.Role, requiredPermission))
        {
            return AdminAuthorizationResult.Failed(403, "You do not have permission for this action.");
        }

        return AdminAuthorizationResult.Success(user, ticket);
    }

    public void Logout(HttpContext httpContext)
    {
        var token = ReadToken(httpContext);
        tokenService.Remove(token);
    }

    public string? ReadToken(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers.Authorization.ToString().Trim();
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        if (header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return header[BearerPrefix.Length..].Trim();
        }

        return null;
    }
}

public sealed class AdminAuthorizationResult
{
    private AdminAuthorizationResult()
    {
    }

    public DashboardUser? User { get; private init; }
    public AdminSessionTicket? Ticket { get; private init; }
    public int? StatusCode { get; private init; }
    public string? Message { get; private init; }

    public bool Succeeded => User is not null && Ticket is not null;

    public IActionResult ToFailureResult() =>
        new ObjectResult(new { message = Message ?? "Authorization failed." })
        {
            StatusCode = StatusCode ?? 403
        };

    public static AdminAuthorizationResult Success(DashboardUser user, AdminSessionTicket ticket) =>
        new()
        {
            User = user,
            Ticket = ticket
        };

    public static AdminAuthorizationResult Failed(int statusCode, string message) =>
        new()
        {
            StatusCode = statusCode,
            Message = message
        };
}
