using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;

namespace BlazorApp_AdminWeb.Services;

public sealed class ModerationState(AdminApiClient apiClient, AdminSessionState sessionState)
{
    public event Action? Changed;

    public int PendingCount { get; private set; }

    public void Reset()
    {
        if (PendingCount == 0)
        {
            return;
        }

        PendingCount = 0;
        Changed?.Invoke();
    }

    public void UpdatePendingCount(int pendingCount)
    {
        var normalizedCount = Math.Max(0, pendingCount);
        if (PendingCount == normalizedCount)
        {
            return;
        }

        PendingCount = normalizedCount;
        Changed?.Invoke();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!sessionState.IsAuthenticated || !sessionState.HasPermission(AdminPermissions.ModerationView))
        {
            Reset();
            return;
        }

        try
        {
            var overview = await apiClient.GetChangeRequestsAsync(new ChangeRequestQueryDto
            {
                Page = 1,
                PageSize = 1,
                Status = "Pending"
            }, cancellationToken: cancellationToken);

            UpdatePendingCount(overview.TotalCount);
        }
        catch
        {
        }
    }
}
