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
public class LanguageController(
    DBContext context,
    AdminRequestAuthorizationService authService,
    ActivityLogService activityLogService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllLanguages()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LanguageRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var languages = await context.Languages
            .OrderByDescending(item => item.Status)
            .ThenByDescending(item => item.IsDefault)
            .ThenBy(item => item.LangName)
            .ToListAsync();

        return Ok(languages.Select(item => item.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetLanguageById(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LanguageRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var language = await context.Languages.FirstOrDefaultAsync(item => item.LanguageId == id);
        if (language is null)
        {
            return NotFound(new { message = "Language not found." });
        }

        return Ok(language.ToDto());
    }

    [HttpPost]
    public async Task<IActionResult> CreateLanguage([FromBody] LanguageUpsertRequest request)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LanguageManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedCode = request.Code.Trim();
        var duplicateExists = await context.Languages.AnyAsync(item => item.LangCode == normalizedCode);
        if (duplicateExists)
        {
            return Conflict(new { message = "A language with the same code already exists." });
        }

        if (request.IsDefault)
        {
            await ClearDefaultLanguageAsync();
        }

        var language = new Language
        {
            LangCode = normalizedCode,
            LangName = request.Name.Trim(),
            NativeName = Normalize(request.NativeName),
            PreferNativeVoice = request.PreferNativeVoice,
            IsDefault = request.IsDefault,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow
        };

        context.Languages.Add(language);
        await context.SaveChangesAsync();
        await EnsureDefaultLanguageAsync(language.LanguageId);
        await activityLogService.LogAsync(
            access.User!,
            "Create",
            "Language",
            language.LanguageId,
            language.LangName,
            $"Created language '{language.LangName}' ({language.LangCode}).");

        return CreatedAtAction(nameof(GetLanguageById), new { id = language.LanguageId }, language.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateLanguage(int id, [FromBody] LanguageUpsertRequest request)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LanguageManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var language = await context.Languages.FirstOrDefaultAsync(item => item.LanguageId == id);
        if (language is null)
        {
            return NotFound(new { message = "Language not found." });
        }

        var normalizedCode = request.Code.Trim();
        var duplicateExists = await context.Languages.AnyAsync(item =>
            item.LanguageId != id && item.LangCode == normalizedCode);
        if (duplicateExists)
        {
            return Conflict(new { message = "A language with the same code already exists." });
        }

        if (request.IsDefault)
        {
            await ClearDefaultLanguageAsync(id);
        }

        language.LangCode = normalizedCode;
        language.LangName = request.Name.Trim();
        language.NativeName = Normalize(request.NativeName);
        language.PreferNativeVoice = request.PreferNativeVoice;
        language.IsDefault = request.IsDefault;
        language.Status = request.Status;

        await context.SaveChangesAsync();
        await EnsureDefaultLanguageAsync(id);
        await activityLogService.LogAsync(
            access.User!,
            "Edit",
            "Language",
            language.LanguageId,
            language.LangName,
            $"Updated language '{language.LangName}' ({language.LangCode}).");

        return Ok(new ApiMessageResponse { Message = "Language updated successfully." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLanguage(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LanguageManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var language = await context.Languages.FirstOrDefaultAsync(item => item.LanguageId == id);
        if (language is null)
        {
            return NotFound(new { message = "Language not found." });
        }

        language.Status = 0;
        language.IsDefault = false;
        await context.SaveChangesAsync();
        await EnsureDefaultLanguageAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Delete",
            "Language",
            language.LanguageId,
            language.LangName,
            $"Archived language '{language.LangName}' ({language.LangCode}).");

        return Ok(new ApiMessageResponse { Message = "Language archived successfully." });
    }

    private async Task ClearDefaultLanguageAsync(int? exceptLanguageId = null)
    {
        var excludedLanguageId = exceptLanguageId;
        var currentDefaults = await context.Languages
            .Where(item => item.IsDefault && (!excludedLanguageId.HasValue || item.LanguageId != excludedLanguageId.Value))
            .ToListAsync();

        if (currentDefaults.Count == 0)
        {
            return;
        }

        foreach (var currentDefault in currentDefaults)
        {
            currentDefault.IsDefault = false;
        }

        await context.SaveChangesAsync();
    }

    private async Task EnsureDefaultLanguageAsync(int? preferredLanguageId = null)
    {
        var hasDefault = await context.Languages.AnyAsync(item => item.IsDefault && item.Status == 1);
        if (hasDefault)
        {
            return;
        }

        Language? fallbackLanguage = null;
        if (preferredLanguageId is not null)
        {
            fallbackLanguage = await context.Languages.FirstOrDefaultAsync(item =>
                item.LanguageId == preferredLanguageId.Value && item.Status == 1);
        }

        fallbackLanguage ??= await context.Languages
            .Where(item => item.Status == 1)
            .OrderBy(item => item.LanguageId)
            .FirstOrDefaultAsync();

        if (fallbackLanguage is null)
        {
            return;
        }

        fallbackLanguage.IsDefault = true;
        await context.SaveChangesAsync();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
