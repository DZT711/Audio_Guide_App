using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class ActivityLogController(
    DBContext context,
    AdminRequestAuthorizationService authService,
    ActivityLogService activityLogService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetActivityLogs([FromQuery] ActivityLogQueryDto query, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.ActivityLogView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var normalizedSearch = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var normalizedAction = string.IsNullOrWhiteSpace(query.Action) ? null : query.Action.Trim();
        var normalizedEntity = string.IsNullOrWhiteSpace(query.Entity) ? null : query.Entity.Trim();

        var items = activityLogService.BuildQuery();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            items = items.Where(item =>
                item.UserName.Contains(normalizedSearch)
                || (item.FullName != null && item.FullName.Contains(normalizedSearch))
                || item.Summary.Contains(normalizedSearch)
                || (item.EntityName != null && item.EntityName.Contains(normalizedSearch))
                || item.EntityType.Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(normalizedAction))
        {
            items = items.Where(item => item.ActionType == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(normalizedEntity))
        {
            items = items.Where(item => item.EntityType == normalizedEntity);
        }

        var totalCount = await items.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = Math.Min(page, totalPages);

        var results = await items
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.ActivityLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new ActivityLogListDto
        {
            Items = results.Select(item => item.ToDto()).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        });
    }
}
