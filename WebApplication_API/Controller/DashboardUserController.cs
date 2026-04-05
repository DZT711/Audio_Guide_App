using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Model;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class DashboardUserController(
    DBContext context,
    AdminRequestAuthorizationService authService,
    ActivityLogService activityLogService) : ControllerBase
{
    private static readonly PasswordHasher<DashboardUser> PasswordHasher = new();

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.UserRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var users = await context.DashboardUsers
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.Username)
            .Select(item => new
            {
                User = item,
                OwnedLocationCount = item.OwnedLocations.Count,
                OwnedAudioCount = item.OwnedLocations.SelectMany(location => location.AudioContents).Count()
            })
            .ToListAsync();

        return Ok(users.Select(item => item.User.ToDto(item.OwnedLocationCount, item.OwnedAudioCount)).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.UserRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var user = await context.DashboardUsers
            .Where(item => item.UserId == id)
            .Select(item => new
            {
                User = item,
                OwnedLocationCount = item.OwnedLocations.Count,
                OwnedAudioCount = item.OwnedLocations.SelectMany(location => location.AudioContents).Count()
            })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(user.User.ToDto(user.OwnedLocationCount, user.OwnedAudioCount));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] DashboardUserUpsertRequest request)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.UserManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Password is required when creating a user." });
        }

        var validationMessage = await ValidateUserRequestAsync(access.User!, request, null);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        validationMessage = ValidateInactiveStatusRequest(access.User!, null, request.Role, request.Status);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        var user = new DashboardUser
        {
            Username = request.Username.Trim(),
            PasswordHash = string.Empty,
            FullName = Normalize(request.FullName),
            Role = request.Role.Trim(),
            Email = Normalize(request.Email),
            Phone = Normalize(request.Phone),
            Status = request.Status,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = PasswordHasher.HashPassword(user, request.Password.Trim());
        context.DashboardUsers.Add(user);
        await context.SaveChangesAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Create",
            "User",
            user.UserId,
            user.Username,
            $"Created user '{user.Username}' with role {user.Role}.");

        return CreatedAtAction(nameof(GetUserById), new { id = user.UserId }, user.ToDto(0, 0));
    }

    [HttpPost("invite")]
    public async Task<IActionResult> InviteUser([FromBody] DashboardUserInviteRequest request)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.UserManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var generatedUsername = string.IsNullOrWhiteSpace(request.Username)
            ? request.Email.Split('@')[0]
            : request.Username.Trim();

        var upsertRequest = new DashboardUserUpsertRequest
        {
            Username = generatedUsername,
            Password = Guid.NewGuid().ToString("N")[..12],
            FullName = request.FullName,
            Role = request.Role,
            Email = request.Email,
            Phone = request.Phone,
            Status = 0
        };

        var validationMessage = await ValidateUserRequestAsync(access.User!, upsertRequest, null);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        validationMessage = ValidateInactiveStatusRequest(access.User!, null, upsertRequest.Role, upsertRequest.Status);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        var user = new DashboardUser
        {
            Username = upsertRequest.Username.Trim(),
            PasswordHash = string.Empty,
            FullName = Normalize(upsertRequest.FullName),
            Role = upsertRequest.Role.Trim(),
            Email = Normalize(upsertRequest.Email),
            Phone = Normalize(upsertRequest.Phone),
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = PasswordHasher.HashPassword(user, upsertRequest.Password);
        context.DashboardUsers.Add(user);
        await context.SaveChangesAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Create",
            "User Invite",
            user.UserId,
            user.Username,
            $"Invited user '{user.Username}' with role {user.Role}.");

        return Ok(new DashboardUserInviteResultDto
        {
            User = user.ToDto(0, 0),
            TemporaryPassword = upsertRequest.Password
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] DashboardUserUpsertRequest request)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.UserManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = await context.DashboardUsers.FirstOrDefaultAsync(item => item.UserId == id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var validationMessage = await ValidateUserRequestAsync(access.User!, request, user.UserId);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        validationMessage = ValidateInactiveStatusRequest(access.User!, user, request.Role, request.Status);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        user.Username = request.Username.Trim();
        user.FullName = Normalize(request.FullName);
        user.Role = request.Role.Trim();
        user.Email = Normalize(request.Email);
        user.Phone = Normalize(request.Phone);
        user.Status = request.Status;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = PasswordHasher.HashPassword(user, request.Password.Trim());
        }

        await context.SaveChangesAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Edit",
            "User",
            user.UserId,
            user.Username,
            $"Updated user '{user.Username}' with role {user.Role}.");
        return Ok(new ApiMessageResponse { Message = "User updated successfully." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> ArchiveUser(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.UserManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var user = await context.DashboardUsers.FirstOrDefaultAsync(item => item.UserId == id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var validationMessage = ValidateInactiveStatusRequest(access.User!, user, user.Role, 0);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        user.Status = 0;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Delete",
            "User",
            user.UserId,
            user.Username,
            $"Archived user '{user.Username}'.");

        return Ok(new ApiMessageResponse { Message = "User archived successfully." });
    }

    private async Task<string?> ValidateUserRequestAsync(
        DashboardUser actor,
        DashboardUserUpsertRequest request,
        int? existingUserId)
    {
        if (!AdminRoles.All.Contains(request.Role.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return "The selected role is invalid.";
        }

        if (!AdminRolePolicies.CanManageUserRole(actor.Role, request.Role))
        {
            return "You do not have permission to assign the selected role.";
        }

        var normalizedUsername = request.Username.Trim();
        var usernameExists = await context.DashboardUsers.AnyAsync(item =>
            item.UserId != existingUserId && item.Username == normalizedUsername);
        if (usernameExists)
        {
            return "Username is already in use.";
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailExists = await context.DashboardUsers.AnyAsync(item =>
                item.UserId != existingUserId && item.Email == request.Email.Trim());
            if (emailExists)
            {
                return "Email is already in use.";
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phoneExists = await context.DashboardUsers.AnyAsync(item =>
                item.UserId != existingUserId && item.Phone == request.Phone.Trim());
            if (phoneExists)
            {
                return "Phone number is already in use.";
            }
        }

        return null;
    }

    private static string? ValidateInactiveStatusRequest(
        DashboardUser actor,
        DashboardUser? targetUser,
        string? targetRole,
        int nextStatus)
    {
        if (nextStatus != 0 || targetUser?.Status == 0)
        {
            return null;
        }

        if (targetUser is not null && actor.UserId == targetUser.UserId)
        {
            return "You cannot block your own account.";
        }

        if (AdminRolePolicies.CanSetUserStatus(actor.Role, actor.UserId, targetUser?.UserId, targetRole, nextStatus))
        {
            return null;
        }

        return string.Equals(actor.Role, AdminRoles.Admin, StringComparison.OrdinalIgnoreCase)
            && string.Equals(targetRole, AdminRoles.Developer, StringComparison.OrdinalIgnoreCase)
                ? "Admin accounts cannot block Developer accounts."
                : "You do not have permission to block this account.";
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
