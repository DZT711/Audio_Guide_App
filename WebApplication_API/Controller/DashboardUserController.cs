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
    AdminRequestAuthorizationService authService) : ControllerBase
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

        var validationMessage = await ValidateUserRequestAsync(request, null);
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

        var validationMessage = await ValidateUserRequestAsync(upsertRequest, null);
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

        var validationMessage = await ValidateUserRequestAsync(request, user.UserId);
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

        user.Status = 0;
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new ApiMessageResponse { Message = "User archived successfully." });
    }

    private async Task<string?> ValidateUserRequestAsync(DashboardUserUpsertRequest request, int? existingUserId)
    {
        if (!AdminRoles.All.Contains(request.Role.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return "The selected role is invalid.";
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

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
