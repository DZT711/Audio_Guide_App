using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MauiApp_Mobile.Services;
using Microsoft.Maui.Devices;
using Project_SharedClassLibrary.Geofencing;

namespace MauiApp_Mobile.Services.Geofencing;

public sealed partial class GeofenceOrchestratorService : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly SemaphoreSlim _lifecycleSemaphore = new(1, 1);
    private readonly SemaphoreSlim _catalogRefreshSemaphore = new(1, 1);
    private readonly SemaphoreSlim _playbackSingleFlight = new(1, 1);
    private readonly object _stateGate = new();
    private readonly Dictionary<string, GeofencePoiRuntimeState> _runtimeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _cooldownSkipNotifiedAtUtc = new(StringComparer.OrdinalIgnoreCase);
    private const double PoiSwitchThresholdScore = 5d;
    private static readonly TimeSpan CooldownSkipNotificationThrottle = TimeSpan.FromSeconds(5);

    private CancellationTokenSource? _engineCts;
    private Channel<GeofenceLocationSample>? _locationChannel;
    private Task? _processingLoopTask;
    private Task? _watchdogLoopTask;
    private List<PoiGeofenceDefinition> _definitions = [];
    private GeofenceSpatialIndex _spatialIndex = GeofenceSpatialIndex.Empty;
    private GeofenceEngineOptions _engineOptions = GeofenceEngineOptions.Create(GeofencePerformanceTier.Normal);
    private GeofenceEvaluationEngine _evaluationEngine = new(GeofenceEngineOptions.Create(GeofencePerformanceTier.Normal));
    private GeofenceLocationSample? _lastProcessedSample;
    private DateTimeOffset? _lastProcessedAtUtc;
    private DateTimeOffset _lastProcessorHeartbeatUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastEnqueuedAtUtc;
    private DateTimeOffset? _nativeCircuitBrokenUntilUtc;
    private int _queueDepth;
    private int _nativeFailureCount;
    private long _droppedLocationEvents;
    private string? _activePriorityPoiId;
    private bool _subscriptionsAttached;
    private bool _nativeMonitorAttached;
    private bool _isDisposed;

    public static GeofenceOrchestratorService Instance { get; } = new();

    private GeofenceOrchestratorService()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<GeofenceTriggeredEvent>? TriggerAccepted;

    public GeofenceRunState RunState { get; private set; } = GeofenceRunState.Stopped;
    public string StatusMessage { get; private set; } = "Geofence engine is idle.";
    public GeofencePerformanceTier PerformanceTier { get; private set; } = GeofencePerformanceTier.Normal;
    public GeofenceDebugSnapshot DebugSnapshot { get; private set; } = GeofenceDebugSnapshot.Empty;

    public async Task WarmStartAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return;
        }

        await _lifecycleSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_isDisposed)
            {
                return;
            }

            SetRunState(GeofenceRunState.Starting, "Starting automatic audio guidance...");
            RefreshPerformanceTier();
            AttachSubscriptions();
            EnsureNativeMonitorAttached();
            EnsureBackgroundLoopsLocked(cancellationToken);
            _ = RefreshCatalogAndRegistrationsAsync("warm-start", _engineCts?.Token ?? CancellationToken.None);
        }
        finally
        {
            _lifecycleSemaphore.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleSemaphore.WaitAsync(cancellationToken);
        try
        {
            _engineCts?.Cancel();
            _locationChannel?.Writer.TryComplete();

            if (_processingLoopTask is not null)
            {
                await AwaitSilentlyAsync(_processingLoopTask);
            }

            if (_watchdogLoopTask is not null)
            {
                await AwaitSilentlyAsync(_watchdogLoopTask);
            }

            await SafeUnregisterNativeAsync(cancellationToken);

            _processingLoopTask = null;
            _watchdogLoopTask = null;
            _locationChannel = null;
            _engineCts?.Dispose();
            _engineCts = null;
            _queueDepth = 0;
            _activePriorityPoiId = null;

            DetachSubscriptions();
            DetachNativeMonitor();
            SetRunState(GeofenceRunState.Stopped, "Automatic audio guidance stopped.");
        }
        finally
        {
            _lifecycleSemaphore.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _isDisposed = true;
        return new ValueTask(StopAsync());
    }

    private void AttachSubscriptions()
    {
        if (_subscriptionsAttached)
        {
            return;
        }

        LocationTrackingService.Instance.LocationUpdated += OnTrackedLocationUpdated;
        UserLocationService.Instance.LocationUpdated += OnUserLocationUpdated;
        PlaceCatalogService.Instance.CatalogChanged += OnCatalogChanged;
        AppSettingsService.Instance.PropertyChanged += OnAppSettingsChanged;
        _subscriptionsAttached = true;
    }

    private void DetachSubscriptions()
    {
        if (!_subscriptionsAttached)
        {
            return;
        }

        LocationTrackingService.Instance.LocationUpdated -= OnTrackedLocationUpdated;
        UserLocationService.Instance.LocationUpdated -= OnUserLocationUpdated;
        PlaceCatalogService.Instance.CatalogChanged -= OnCatalogChanged;
        AppSettingsService.Instance.PropertyChanged -= OnAppSettingsChanged;
        _subscriptionsAttached = false;
    }

    private void EnsureNativeMonitorAttached()
    {
        if (_nativeMonitorAttached)
        {
            return;
        }

        GeofencePlatformMonitor.Instance.TransitionReceived += OnNativeTransitionReceived;
        _nativeMonitorAttached = true;
    }

    private void DetachNativeMonitor()
    {
        if (!_nativeMonitorAttached)
        {
            return;
        }

        GeofencePlatformMonitor.Instance.TransitionReceived -= OnNativeTransitionReceived;
        _nativeMonitorAttached = false;
    }

    private void EnsureBackgroundLoopsLocked(CancellationToken cancellationToken)
    {
        if (_processingLoopTask is { IsCompleted: false } && _watchdogLoopTask is { IsCompleted: false })
        {
            return;
        }

        _engineCts?.Cancel();
        _engineCts?.Dispose();
        _engineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _locationChannel = Channel.CreateBounded<GeofenceLocationSample>(new BoundedChannelOptions(8)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        _processingLoopTask = Task.Run(() => ProcessingLoopAsync(_engineCts.Token), _engineCts.Token);
        _watchdogLoopTask = Task.Run(() => WatchdogLoopAsync(_engineCts.Token), _engineCts.Token);
    }

    private async Task RestartBackgroundLoopsAsync(CancellationToken cancellationToken)
    {
        await _lifecycleSemaphore.WaitAsync(cancellationToken);
        try
        {
            EnsureBackgroundLoopsLocked(cancellationToken);
        }
        finally
        {
            _lifecycleSemaphore.Release();
        }
    }

    private void RefreshPerformanceTier()
    {
        PerformanceTier = AppSettingsService.Instance.BatterySaverEnabled
            ? GeofencePerformanceTier.BatterySaver
            : GeofencePerformanceTier.Normal;
        RaisePropertyChanged(nameof(PerformanceTier));
    }

    private void SetRunState(GeofenceRunState state, string message)
    {
        RunState = state;
        StatusMessage = message;
        RaisePropertyChanged(nameof(RunState));
        RaisePropertyChanged(nameof(StatusMessage));
    }

    private void UpdateDebugSnapshot(
        string? nearestPoiId,
        double? nearestDistanceMeters,
        int candidateCount,
        TimeSpan lastEvaluationDuration,
        DateTimeOffset? lastProcessedAtUtc,
        string lastTriggerSummary)
    {
        DebugSnapshot = new GeofenceDebugSnapshot(
            nearestPoiId,
            nearestDistanceMeters,
            candidateCount,
            Interlocked.Read(ref _droppedLocationEvents),
            Volatile.Read(ref _queueDepth),
            lastEvaluationDuration,
            lastProcessedAtUtc,
            string.IsNullOrWhiteSpace(lastTriggerSummary) ? DebugSnapshot.LastTriggerSummary : lastTriggerSummary);

        RaisePropertyChanged(nameof(DebugSnapshot));
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void RaiseTriggerAccepted(GeofenceTriggeredEvent trigger)
    {
        var handlers = TriggerAccepted;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<GeofenceTriggeredEvent> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, trigger);
            }
            catch (Exception ex)
            {
                Log(
                    "trigger-accepted-subscriber-failed",
                    ("poiId", trigger.Definition?.Id ?? string.Empty),
                    ("eventType", trigger.EventType.ToString()),
                    ("error", ex.Message));
            }
        }
    }
}
