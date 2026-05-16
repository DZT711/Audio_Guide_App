using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Model;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class AuthController(
    DBContext context,
    AdminSessionTokenService sessionTokenService,
    AdminRequestAuthorizationService authService) : ControllerBase
{
    private static readonly PasswordHasher<DashboardUser> PasswordHasher = new();
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await context.DashboardUsers.FirstOrDefaultAsync(item =>
            item.Username.ToLower() == request.UserName.Trim().ToLower());

        if (user is null)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        if (user.Status != 1)
        {
            return StatusCode(403, new { message = "This account is inactive and cannot sign in." });
        }

        var verificationResult = PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var ticket = sessionTokenService.Create(user.UserId, SessionLifetime);
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(ToLoginResponse(user, ticket));
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.DashboardView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        return Ok(ToLoginResponse(access.User!, access.Ticket!));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        authService.Logout(HttpContext);
        return Ok(new ApiMessageResponse { Message = "Signed out successfully." });
    }

    private static AdminLoginResponse ToLoginResponse(DashboardUser user, AdminSessionTicket ticket) =>
        new()
        {
            Token = ticket.Token,
            ExpiresAt = ticket.ExpiresAt,
            User = user.ToSessionDto()
        };
}
