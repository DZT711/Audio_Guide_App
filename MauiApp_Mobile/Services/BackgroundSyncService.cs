using Microsoft.Maui.Networking;

namespace MauiApp_Mobile.Services;

public sealed class BackgroundSyncService
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _started;

    public static BackgroundSyncService Instance { get; } = new();

    private BackgroundSyncService()
    {
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _started = false;
    }

    public async Task TriggerCatalogSyncAsync(CancellationToken cancellationToken = default)
    {
        if (!AppDataModeService.Instance.IsApiEnabled || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        if (!await _syncLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            await PlaceCatalogService.Instance.TrySyncCatalogInBackgroundAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        _ = TriggerCatalogSyncAsync();
    }
}
