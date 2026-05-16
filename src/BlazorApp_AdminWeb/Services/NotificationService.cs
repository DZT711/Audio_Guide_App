namespace BlazorApp_AdminWeb.Services;

public enum NotificationLevel
{
    Success,
    Info,
    Warning,
    Error
}

public sealed record AppNotification(Guid Id, string Message, NotificationLevel Level, string? Title = null);

public sealed class NotificationService
{
    private readonly List<AppNotification> _items = new();

    public event Action? Changed;

    public IReadOnlyList<AppNotification> Items => _items;

    public void Success(string message, string? title = null, int durationMs = 3600) =>
        Push(message, NotificationLevel.Success, title, durationMs);

    public void Info(string message, string? title = null, int durationMs = 3200) =>
        Push(message, NotificationLevel.Info, title, durationMs);

    public void Warning(string message, string? title = null, int durationMs = 4200) =>
        Push(message, NotificationLevel.Warning, title, durationMs);

    public void Error(string message, string? title = null, int durationMs = 5200) =>
        Push(message, NotificationLevel.Error, title, durationMs);

    public void Dismiss(Guid id)
    {
        if (_items.RemoveAll(item => item.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }

    private void Push(string message, NotificationLevel level, string? title, int durationMs)
    {
        var item = new AppNotification(Guid.NewGuid(), message, level, title);
        _items.Insert(0, item);
        Changed?.Invoke();
        _ = DismissLaterAsync(item.Id, durationMs);
    }

    private async Task DismissLaterAsync(Guid id, int durationMs)
    {
        await Task.Delay(durationMs);
        Dismiss(id);
    }
}
