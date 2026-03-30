using Microsoft.AspNetCore.Mvc;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class InboxController(
    DBContext context,
    AdminRequestAuthorizationService authService,
    ChangeRequestWorkflowService workflowService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetInbox([FromQuery] InboxQueryDto query, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.InboxView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var result = await workflowService.GetInboxAsync(access.User!, query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.InboxView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        try
        {
            await workflowService.MarkInboxReadAsync(id, access.User!, cancellationToken);
            return Ok(new ApiMessageResponse { Message = "Message marked as read." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
