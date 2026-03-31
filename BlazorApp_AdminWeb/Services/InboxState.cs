using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;

namespace BlazorApp_AdminWeb.Services;

public sealed class InboxState(AdminApiClient apiClient, AdminSessionState sessionState)
{
    public event Action? Changed;

    public int UnreadCount { get; private set; }

    public void Reset()
    {
        if (UnreadCount == 0)
        {
            return;
        }

        UnreadCount = 0;
        Changed?.Invoke();
    }

    public void UpdateUnreadCount(int unreadCount)
    {
        var normalizedCount = Math.Max(0, unreadCount);
        if (UnreadCount == normalizedCount)
        {
            return;
        }

        UnreadCount = normalizedCount;
        Changed?.Invoke();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!sessionState.IsAuthenticated || !sessionState.HasPermission(AdminPermissions.InboxView))
        {
            Reset();
            return;
        }

        try
        {
            var overview = await apiClient.GetInboxAsync(new InboxQueryDto
            {
                Page = 1,
                PageSize = 1,
                UnreadOnly = false
            }, cancellationToken);

            UpdateUnreadCount(overview.UnreadCount);
        }
        catch
        {
        }
    }
}
