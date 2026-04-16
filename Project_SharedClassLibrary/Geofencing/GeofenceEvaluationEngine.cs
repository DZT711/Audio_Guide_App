namespace Project_SharedClassLibrary.Geofencing;

public sealed class GeofenceEvaluationEngine
{
    private readonly GeofenceEngineOptions _options;

    public GeofenceEvaluationEngine(GeofenceEngineOptions options)
    {
        _options = options;
    }

    public GeofenceEvaluationResult Evaluate(
        GeofenceLocationSample location,
        GeofenceSpatialIndex spatialIndex,
        IDictionary<string, GeofencePoiRuntimeState> runtimeStates,
        DateTimeOffset? globalCooldownUntilUtc)
    {
        var acceptedTriggers = new List<GeofenceTriggeredEvent>();
        var skippedTriggers = new List<GeofenceSkippedTrigger>();
        var nowUtc = location.CapturedAtUtc;
        var candidateDefinitions = spatialIndex.GetCandidateDefinitions(
            location.Latitude,
            location.Longitude,
            _options.CandidateLimit);

        var definitionsById = new Dictionary<string, PoiGeofenceDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in candidateDefinitions)
        {
            definitionsById[definition.Id] = definition;
        }

        foreach (var pair in runtimeStates)
        {
            if (!pair.Value.ShouldEvaluate(nowUtc))
            {
                continue;
            }

            if (spatialIndex.TryGetDefinition(pair.Key, out var definition))
            {
                definitionsById[definition.Id] = definition;
            }
        }

        string? nearestPoiId = null;
        double? nearestDistanceMeters = null;

        foreach (var definition in definitionsById.Values)
        {
            var runtimeState = GetOrCreateRuntimeState(runtimeStates, definition.Id);
            var distanceMeters = HaversineDistanceCalculator.CalculateMeters(
                location.Latitude,
                location.Longitude,
                definition.Latitude,
                definition.Longitude);

            runtimeState.LastDistanceMeters = distanceMeters;
            runtimeState.LastEvaluatedAtUtc = nowUtc;

            if (!nearestDistanceMeters.HasValue || distanceMeters < nearestDistanceMeters.Value)
            {
                nearestDistanceMeters = distanceMeters;
                nearestPoiId = definition.Id;
            }

            var insideActivationRadius = distanceMeters <= definition.ActivationRadiusMeters;
            var insideNearRadius = distanceMeters <= definition.NearRadiusMeters;

            if (!runtimeState.HasEstablishedState)
            {
                runtimeState.HasEstablishedState = true;
                runtimeState.State = ResolveState(insideActivationRadius, insideNearRadius, runtimeState.CooldownUntilUtc, nowUtc);
                if (insideActivationRadius)
                {
                    runtimeState.LastEnteredAtUtc = nowUtc;
                }

                if (insideNearRadius)
                {
                    runtimeState.NearWindowStartedAtUtc = nowUtc;
                }

                continue;
            }

            var previousState = runtimeState.State;

            if (!insideNearRadius)
            {
                runtimeState.ResetNearWindow();
            }
            else
            {
                runtimeState.NearWindowStartedAtUtc ??= nowUtc;
            }

            if (!insideActivationRadius &&
                previousState is GeofenceState.Inside or GeofenceState.Near or GeofenceState.Cooldown)
            {
                runtimeState.LastExitedAtUtc = nowUtc;
                runtimeState.ResetNearWindow();
            }

            if (insideActivationRadius && previousState == GeofenceState.Outside)
            {
                runtimeState.LastEnteredAtUtc = nowUtc;
                EvaluateTrigger(
                    acceptedTriggers,
                    skippedTriggers,
                    runtimeState,
                    definition,
                    GeofenceTriggerEvent.EnteredRadius,
                    distanceMeters,
                    globalCooldownUntilUtc,
                    nowUtc);
            }

            if (insideNearRadius &&
                runtimeState.NearWindowStartedAtUtc.HasValue &&
                !runtimeState.NearStayHandled &&
                nowUtc - runtimeState.NearWindowStartedAtUtc.Value >= _options.NearDwellWindow)
            {
                runtimeState.NearStayHandled = true;
                EvaluateTrigger(
                    acceptedTriggers,
                    skippedTriggers,
                    runtimeState,
                    definition,
                    GeofenceTriggerEvent.NearStay,
                    distanceMeters,
                    globalCooldownUntilUtc,
                    nowUtc);
            }

            runtimeState.State = ResolveState(insideActivationRadius, insideNearRadius, runtimeState.CooldownUntilUtc, nowUtc);
        }

        return new GeofenceEvaluationResult(
            acceptedTriggers,
            skippedTriggers,
            nearestPoiId,
            nearestDistanceMeters,
            definitionsById.Count);
    }

    private void EvaluateTrigger(
        ICollection<GeofenceTriggeredEvent> acceptedTriggers,
        ICollection<GeofenceSkippedTrigger> skippedTriggers,
        GeofencePoiRuntimeState runtimeState,
        PoiGeofenceDefinition definition,
        GeofenceTriggerEvent eventType,
        double distanceMeters,
        DateTimeOffset? globalCooldownUntilUtc,
        DateTimeOffset nowUtc)
    {
        var cooldownWindow = TimeSpan.FromSeconds(Math.Max(
            Math.Max(0, definition.DebounceSeconds),
            Math.Max(1, (int)Math.Round(_options.DefaultPoiCooldown.TotalSeconds))));

        if (globalCooldownUntilUtc.HasValue && globalCooldownUntilUtc.Value > nowUtc)
        {
            skippedTriggers.Add(new GeofenceSkippedTrigger(
                definition,
                eventType,
                distanceMeters,
                "global-cooldown"));
            return;
        }

        if (runtimeState.CooldownUntilUtc.HasValue && runtimeState.CooldownUntilUtc.Value > nowUtc)
        {
            skippedTriggers.Add(new GeofenceSkippedTrigger(
                definition,
                eventType,
                distanceMeters,
                "poi-cooldown"));
            return;
        }

        if (runtimeState.TryGetLastTriggerTime(eventType, out var lastTriggeredAtUtc) &&
            nowUtc - lastTriggeredAtUtc < cooldownWindow)
        {
            skippedTriggers.Add(new GeofenceSkippedTrigger(
                definition,
                eventType,
                distanceMeters,
                "duplicate-event"));
            return;
        }

        runtimeState.LastTriggeredAtUtc = nowUtc;
        runtimeState.CooldownUntilUtc = nowUtc.Add(cooldownWindow);
        runtimeState.SetTriggerTime(eventType, nowUtc);

        acceptedTriggers.Add(new GeofenceTriggeredEvent(
            definition,
            eventType,
            distanceMeters,
            cooldownWindow));
    }

    private static GeofencePoiRuntimeState GetOrCreateRuntimeState(
        IDictionary<string, GeofencePoiRuntimeState> runtimeStates,
        string poiId)
    {
        if (runtimeStates.TryGetValue(poiId, out var runtimeState))
        {
            return runtimeState;
        }

        runtimeState = new GeofencePoiRuntimeState();
        runtimeStates[poiId] = runtimeState;
        return runtimeState;
    }

    private static GeofenceState ResolveState(
        bool insideActivationRadius,
        bool insideNearRadius,
        DateTimeOffset? cooldownUntilUtc,
        DateTimeOffset nowUtc)
    {
        if (insideNearRadius)
        {
            return GeofenceState.Near;
        }

        if (insideActivationRadius)
        {
            return GeofenceState.Inside;
        }

        return cooldownUntilUtc.HasValue && cooldownUntilUtc.Value > nowUtc
            ? GeofenceState.Cooldown
            : GeofenceState.Outside;
    }
}
