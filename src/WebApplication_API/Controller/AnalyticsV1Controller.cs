using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("api/v1")]
public class AnalyticsV1Controller(
    DBContext context,
    AdminRequestAuthorizationService authService,
    IUsageAnalyticsService usageAnalyticsService,
    ILogger<AnalyticsV1Controller> logger) : ControllerBase
{
    private const int MaxBatchSize = 500;

    [AllowAnonymous]
    [HttpPost("analytics/events")]
    public async Task<IActionResult> IngestEvents(
        [FromBody] List<UsageEvent>? events,
        CancellationToken cancellationToken = default)
    {
        if (events is null || events.Count == 0)
        {
            return BadRequest(new ApiMessageResponse
            {
                Message = "At least one usage event is required."
            });
        }

        if (events.Count > MaxBatchSize)
        {
            return BadRequest(new ApiMessageResponse
            {
                Message = $"Batch size exceeds the limit ({MaxBatchSize})."
            });
        }

        try
        {
            var result = await usageAnalyticsService.IngestEventsAsync(events, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest usage analytics events.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiMessageResponse
            {
                Message = "Failed to ingest usage analytics events."
            });
        }
    }

    [HttpGet("admin/analytics/statistics")]
    public async Task<IActionResult> GetStatistics(CancellationToken cancellationToken = default)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AnalyticsView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        try
        {
            var result = await usageAnalyticsService.GetStatisticsAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get admin analytics statistics.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiMessageResponse
            {
                Message = "Failed to load analytics statistics."
            });
        }
    }

    [HttpGet("admin/analytics/history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AnalyticsView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (pageNumber <= 0 || pageSize <= 0)
        {
            return BadRequest(new ApiMessageResponse
            {
                Message = "PageNumber and PageSize must be greater than 0."
            });
        }

        try
        {
            var result = await usageAnalyticsService.GetHistoryAsync(pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get admin analytics history.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiMessageResponse
            {
                Message = "Failed to load analytics history."
            });
        }
    }
}
