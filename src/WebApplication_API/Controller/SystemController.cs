using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using WebApplication_API.Data;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class SystemController(
    DBContext context,
    AdminRequestAuthorizationService authService,
    AdminSessionTokenService sessionTokenService,
    ServerRuntimeInfoService runtimeInfoService,
    IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("public/info")]
    public IActionResult GetPublicInfo()
    {
        Response.Headers.CacheControl = "public,max-age=15";

        var serverInfo = BuildServerInfo();
        return Ok(new PublicServerInfoDto
        {
            Status = serverInfo.Status,
            ApiVersion = serverInfo.ApiVersion,
            FrameworkDescription = serverInfo.FrameworkDescription,
            EnvironmentName = serverInfo.EnvironmentName,
            TimeZoneDisplayName = serverInfo.TimeZoneDisplayName,
            ServerTimeUtc = serverInfo.ServerTimeUtc,
            ServerTimeLocal = serverInfo.ServerTimeLocal,
            StartedAtUtc = serverInfo.StartedAtUtc,
            UptimeSeconds = serverInfo.UptimeSeconds
        });
    }

    [HttpGet("info")]
    public async Task<IActionResult> GetInfo()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.DashboardView);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        return Ok(BuildServerInfo(sessionTokenService.GetActiveUserCount()));
    }

    private ServerInfoDto BuildServerInfo(int activeAdminUserCount = 0)
    {
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        var version = informationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "1.0.0";

        var nowUtc = DateTime.UtcNow;
        var nowLocal = DateTime.Now;

        return new ServerInfoDto
        {
            Status = "Online",
            ApiVersion = version,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            EnvironmentName = environment.EnvironmentName,
            TimeZoneDisplayName = TimeZoneInfo.Local.DisplayName,
            ActiveAdminUserCount = activeAdminUserCount,
            ServerTimeUtc = nowUtc,
            ServerTimeLocal = nowLocal,
            StartedAtUtc = runtimeInfoService.StartedAtUtc,
            UptimeSeconds = Math.Max(0, (long)(nowUtc - runtimeInfoService.StartedAtUtc).TotalSeconds)
        };
    }
}
