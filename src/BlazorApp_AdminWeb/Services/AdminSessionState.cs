using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;

namespace BlazorApp_AdminWeb.Services;

public sealed class AdminSessionState
{
    public event Action? Changed;

    public string? Token { get; private set; }

    public DateTime? ExpiresAt { get; private set; }

    public AdminSessionUserDto? CurrentUser { get; private set; }

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(Token)
        && CurrentUser is not null
        && ExpiresAt is not null
        && ExpiresAt > DateTime.UtcNow;

    public string DisplayName => CurrentUser?.FullName ?? CurrentUser?.Username ?? "Guest";

    public string Role => CurrentUser?.Role ?? "";

    public string StatusLabel => CurrentUser?.Status == 1 ? "Active" : "Inactive";

    public void SetSession(AdminLoginResponse response)
    {
        Token = response.Token;
        ExpiresAt = response.ExpiresAt;
        CurrentUser = response.User;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Token = null;
        ExpiresAt = null;
        CurrentUser = null;
        Changed?.Invoke();
    }

    public bool HasPermission(string permission) =>
        AdminRolePolicies.HasPermission(CurrentUser?.Role, permission);
}
