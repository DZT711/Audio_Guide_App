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
public class CategoryController(
    DBContext context,
    AdminRequestAuthorizationService authService,
    ActivityLogService activityLogService) : ControllerBase
{
    [HttpGet("public")]
    public async Task<IActionResult> GetPublicCategories(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "public,max-age=120";

        var categories = await context.Categories
            .AsNoTracking()
            .Where(item => item.Status == 1)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return Ok(categories.Select(item => item.ToDto()).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCategories()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.CategoryRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var categories = await context.Categories
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.Name)
            .ToListAsync();

        return Ok(categories.Select(item => item.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCategoryById(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.CategoryRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var category = await context.Categories.FirstOrDefaultAsync(item => item.CategoryId == id);
        if (category is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        return Ok(category.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryUpsertRequest request)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.CategoryManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var duplicateExists = await context.Categories.AnyAsync(item => item.Name == request.Name.Trim());
        if (duplicateExists)
        {
            return Conflict(new { message = "A category with the same name already exists." });
        }

        var normalizedThemeName = NormalizeThemeName(request.ThemeName);
        if (!string.IsNullOrWhiteSpace(normalizedThemeName)
            && !IsSupportedThemeName(normalizedThemeName))
        {
            return BadRequest(new { message = "ThemeName must match one of the supported QR landing themes." });
        }

        var duplicateThemeExists = !string.IsNullOrWhiteSpace(normalizedThemeName)
            && await context.Categories.AnyAsync(item => item.ThemeName == normalizedThemeName);
        if (duplicateThemeExists)
        {
            return Conflict(new { message = "Another category is already using the same theme name." });
        }

        var category = new WebApplication_API.Model.Category
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ThemeName = normalizedThemeName,
            IconEmoji = NormalizeOptional(request.IconEmoji),
            PrimaryColor = NormalizeHexColor(request.PrimaryColor),
            SecondaryColor = NormalizeHexColor(request.SecondaryColor),
            Status = request.Status,
            CreatedAt = DateTime.UtcNow
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Create",
            "Category",
            category.CategoryId,
            category.Name,
            $"Created category '{category.Name}'.");

        return CreatedAtAction(nameof(GetCategoryById), new { id = category.CategoryId }, category.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryUpsertRequest request)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.CategoryManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var category = await context.Categories.FirstOrDefaultAsync(item => item.CategoryId == id);
        if (category is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var duplicateExists = await context.Categories.AnyAsync(item =>
            item.CategoryId != id && item.Name == request.Name.Trim());
        if (duplicateExists)
        {
            return Conflict(new { message = "A category with the same name already exists." });
        }

        var normalizedThemeName = NormalizeThemeName(request.ThemeName);
        if (!string.IsNullOrWhiteSpace(normalizedThemeName)
            && !IsSupportedThemeName(normalizedThemeName))
        {
            return BadRequest(new { message = "ThemeName must match one of the supported QR landing themes." });
        }

        var duplicateThemeExists = !string.IsNullOrWhiteSpace(normalizedThemeName)
            && await context.Categories.AnyAsync(item =>
                item.CategoryId != id
                && item.ThemeName == normalizedThemeName);
        if (duplicateThemeExists)
        {
            return Conflict(new { message = "Another category is already using the same theme name." });
        }

        category.Name = request.Name.Trim();
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.ThemeName = normalizedThemeName;
        category.IconEmoji = NormalizeOptional(request.IconEmoji);
        category.PrimaryColor = NormalizeHexColor(request.PrimaryColor);
        category.SecondaryColor = NormalizeHexColor(request.SecondaryColor);
        category.Status = request.Status;
        category.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Edit",
            "Category",
            category.CategoryId,
            category.Name,
            $"Updated category '{category.Name}'.");
        return Ok(new ApiMessageResponse { Message = "Category updated successfully." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.CategoryManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var category = await context.Categories.FirstOrDefaultAsync(item => item.CategoryId == id);
        if (category is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        category.Status = 0;
        category.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Delete",
            "Category",
            category.CategoryId,
            category.Name,
            $"Archived category '{category.Name}'.");

        return Ok(new ApiMessageResponse { Message = "Category archived successfully." });
    }

    private static bool IsSupportedThemeName(string themeName) =>
        LocationQrThemeCatalog.Themes.Any(item =>
            string.Equals(item.ThemeName, themeName, StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeThemeName(string? themeName)
    {
        var normalized = NormalizeOptional(themeName);
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : LocationQrThemeCatalog.NormalizeThemeName(normalized);
    }

    private static string? NormalizeHexColor(string? value)
    {
        var normalized = NormalizeOptional(value);
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
