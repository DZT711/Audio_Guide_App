using Microsoft.EntityFrameworkCore;
using WebApplication_API.Data;
using WebApplication_API.Model;

namespace WebApplication_API.Services;

public sealed class ActivityLogService(DBContext context)
{
    public async Task LogAsync(
        DashboardUser actor,
        string actionType,
        string entityType,
        int? entityId,
        string? entityName,
        string summary,
        CancellationToken cancellationToken = default)
    {
        if (actor is null || string.IsNullOrWhiteSpace(actionType) || string.IsNullOrWhiteSpace(entityType))
        {
            return;
        }

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = actor.UserId,
            UserName = actor.Username,
            FullName = actor.FullName,
            Role = actor.Role,
            ActionType = actionType.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            EntityName = string.IsNullOrWhiteSpace(entityName) ? null : entityName.Trim(),
            Summary = string.IsNullOrWhiteSpace(summary) ? $"{actionType.Trim()} {entityType.Trim()}" : summary.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public IQueryable<ActivityLog> BuildQuery() => context.ActivityLogs
        .AsNoTracking()
        .Include(item => item.User)
        .AsQueryable();
}
