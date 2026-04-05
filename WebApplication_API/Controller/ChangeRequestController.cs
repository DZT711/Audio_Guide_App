using Microsoft.AspNetCore.Mvc;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class ChangeRequestController(
    DBContext context,
    AdminRequestAuthorizationService authService,
    ChangeRequestWorkflowService workflowService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRequests([FromQuery] ChangeRequestQueryDto query, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.ModerationView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var result = await workflowService.GetRequestsAsync(query, access.User!, ownerOnly: false, cancellationToken);
        return Ok(result);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMyRequests([FromQuery] ChangeRequestQueryDto query, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.DashboardView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var result = await workflowService.GetRequestsAsync(query, access.User!, ownerOnly: true, cancellationToken);
        return Ok(result);
    }

    [HttpPost("location")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitLocationRequest(
        [FromForm] LocationChangeRequestSubmission request,
        [FromForm(Name = "PreferenceImageFile")] IFormFile? preferenceImageFile,
        [FromForm(Name = "ImageFiles")] List<IFormFile>? imageFiles,
        CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.LocationRequest);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await workflowService.SubmitLocationAsync(
                access.User!,
                request,
                preferenceImageFile,
                imageFiles,
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return ToErrorResult(ex);
        }
    }

    [HttpPost("audio")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitAudioRequest(
        [FromForm] AudioChangeRequestSubmission request,
        [FromForm(Name = "AudioFile")] IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AudioRequest);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await workflowService.SubmitAudioAsync(access.User!, request, audioFile, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return ToErrorResult(ex);
        }
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ReviewChangeRequestRequest request, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.ModerationManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!AdminRolePolicies.IsPrivileged(access.User!.Role))
        {
            return StatusCode(403, new { message = "Only Admin and Developer accounts can approve requests." });
        }

        try
        {
            var result = await workflowService.ApproveAsync(id, access.User!, request.AdminNote, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return ToErrorResult(ex);
        }
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ReviewChangeRequestRequest request, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.ModerationManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!AdminRolePolicies.IsPrivileged(access.User!.Role))
        {
            return StatusCode(403, new { message = "Only Admin and Developer accounts can reject requests." });
        }

        if (string.IsNullOrWhiteSpace(request.AdminNote))
        {
            return BadRequest(new { message = "Enter a rejection reason before rejecting this request." });
        }

        try
        {
            var result = await workflowService.RejectAsync(id, access.User!, request.AdminNote, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return ToErrorResult(ex);
        }
    }

    private IActionResult ToErrorResult(InvalidOperationException exception)
    {
        var message = exception.Message;
        return message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("no longer exists", StringComparison.OrdinalIgnoreCase)
            ? NotFound(new { message })
            : BadRequest(new { message });
    }
}
