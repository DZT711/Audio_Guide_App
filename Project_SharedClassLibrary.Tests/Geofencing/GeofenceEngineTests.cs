using Project_SharedClassLibrary.Geofencing;
using Xunit;

namespace Project_SharedClassLibrary.Tests.Geofencing;

public sealed class GeofenceEngineTests
{
    [Fact]
    public void HaversineDistance_ShouldBeCloseToKnownLatitudeDistance()
    {
        var distanceMeters = HaversineDistanceCalculator.CalculateMeters(0d, 0d, 1d, 0d);

        Assert.InRange(distanceMeters, 111_000d, 111_300d);
    }

    [Fact]
    public void Evaluate_ShouldTriggerEnteredRadius_WhenCrossingFromOutsideToInside()
    {
        var options = GeofenceEngineOptions.Create(GeofencePerformanceTier.Normal);
        var definition = new PoiGeofenceDefinition("1", 0d, 0d, 100d, 25d, 3, 10, true);
        var spatialIndex = GeofenceSpatialIndex.Build([definition], options);
        var engine = new GeofenceEvaluationEngine(options);
        var states = new Dictionary<string, GeofencePoiRuntimeState>(StringComparer.OrdinalIgnoreCase);

        _ = engine.Evaluate(CreateSample(0.002d, 0d, 0), spatialIndex, states, null);
        var result = engine.Evaluate(CreateSample(0.0005d, 0d, 5), spatialIndex, states, null);

        var trigger = Assert.Single(result.AcceptedTriggers);
        Assert.Equal(GeofenceTriggerEvent.EnteredRadius, trigger.EventType);
        Assert.Equal("1", trigger.Definition.Id);
    }

    [Fact]
    public void Evaluate_ShouldTriggerNearStay_AfterDwellWindow()
    {
        var options = new GeofenceEngineOptions(
            DefaultPoiCooldown: TimeSpan.FromSeconds(1),
            GlobalCooldown: TimeSpan.Zero,
            NearDwellWindow: TimeSpan.FromSeconds(5),
            MinimumEvaluationInterval: TimeSpan.Zero,
            MinimumMovementMeters: 0d,
            SpatialPaddingMeters: 20d,
            CandidateLimit: 10,
            NativeCircuitBreakerDuration: TimeSpan.FromMinutes(5),
            NativeFailureThreshold: 3,
            WatchdogThreshold: TimeSpan.FromMinutes(1));

        var definition = new PoiGeofenceDefinition("1", 0d, 0d, 100d, 20d, 2, 0, true);
        var spatialIndex = GeofenceSpatialIndex.Build([definition], options);
        var engine = new GeofenceEvaluationEngine(options);
        var states = new Dictionary<string, GeofencePoiRuntimeState>(StringComparer.OrdinalIgnoreCase);

        _ = engine.Evaluate(CreateSample(0.002d, 0d, 0), spatialIndex, states, null);
        _ = engine.Evaluate(CreateSample(0.0004d, 0d, 1), spatialIndex, states, null);
        _ = engine.Evaluate(CreateSample(0.0001d, 0d, 2), spatialIndex, states, null);
        var result = engine.Evaluate(CreateSample(0.0001d, 0d, 8), spatialIndex, states, null);

        var trigger = Assert.Single(result.AcceptedTriggers);
        Assert.Equal(GeofenceTriggerEvent.NearStay, trigger.EventType);
    }

    [Fact]
    public void Evaluate_ShouldSkipReentry_DuringPoiCooldown()
    {
        var options = new GeofenceEngineOptions(
            DefaultPoiCooldown: TimeSpan.FromSeconds(20),
            GlobalCooldown: TimeSpan.Zero,
            NearDwellWindow: TimeSpan.FromSeconds(5),
            MinimumEvaluationInterval: TimeSpan.Zero,
            MinimumMovementMeters: 0d,
            SpatialPaddingMeters: 20d,
            CandidateLimit: 10,
            NativeCircuitBreakerDuration: TimeSpan.FromMinutes(5),
            NativeFailureThreshold: 3,
            WatchdogThreshold: TimeSpan.FromMinutes(1));

        var definition = new PoiGeofenceDefinition("1", 0d, 0d, 100d, 20d, 1, 0, true);
        var spatialIndex = GeofenceSpatialIndex.Build([definition], options);
        var engine = new GeofenceEvaluationEngine(options);
        var states = new Dictionary<string, GeofencePoiRuntimeState>(StringComparer.OrdinalIgnoreCase);

        _ = engine.Evaluate(CreateSample(0.002d, 0d, 0), spatialIndex, states, null);
        _ = engine.Evaluate(CreateSample(0.0005d, 0d, 2), spatialIndex, states, null);
        _ = engine.Evaluate(CreateSample(0.002d, 0d, 4), spatialIndex, states, null);
        var result = engine.Evaluate(CreateSample(0.0005d, 0d, 8), spatialIndex, states, null);

        Assert.Empty(result.AcceptedTriggers);
        var skipped = Assert.Single(result.SkippedTriggers);
        Assert.Equal("poi-cooldown", skipped.Reason);
    }

    [Fact]
    public void TriggerSelector_ShouldPickHighestPriority_ThenNearest()
    {
        var lowPriority = new GeofenceTriggeredEvent(
            new PoiGeofenceDefinition("low", 0d, 0d, 100d, 20d, 1, 0, true),
            GeofenceTriggerEvent.EnteredRadius,
            10d,
            TimeSpan.FromSeconds(10));

        var samePriorityFarther = new GeofenceTriggeredEvent(
            new PoiGeofenceDefinition("far", 0d, 0d, 100d, 20d, 5, 0, true),
            GeofenceTriggerEvent.EnteredRadius,
            30d,
            TimeSpan.FromSeconds(10));

        var samePriorityNearer = new GeofenceTriggeredEvent(
            new PoiGeofenceDefinition("near", 0d, 0d, 100d, 20d, 5, 0, true),
            GeofenceTriggerEvent.NearStay,
            12d,
            TimeSpan.FromSeconds(10));

        var selected = GeofenceTriggerSelector.SelectBest([lowPriority, samePriorityFarther, samePriorityNearer]);

        Assert.NotNull(selected);
        Assert.Equal("near", selected!.Definition.Id);
    }

    private static GeofenceLocationSample CreateSample(double latitude, double longitude, int secondsOffset) =>
        new(
            latitude,
            longitude,
            AccuracyMeters: 5d,
            SpeedMetersPerSecond: 0d,
            CapturedAtUtc: DateTimeOffset.UtcNow.AddSeconds(secondsOffset),
            IsForeground: true);
}
