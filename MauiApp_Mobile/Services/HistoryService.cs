using System.Collections.ObjectModel;
using System.Text.Json;
using MauiApp_Mobile.Models;

namespace MauiApp_Mobile.Services;

public class HistoryService
{
    private const int MaxHistoryItems = 40;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static HistoryService? _instance;
    public static HistoryService Instance => _instance ??= new HistoryService();

    public ObservableCollection<PlaceItem> HistoryItems { get; private set; } = new();
    private bool _isInitialized;

    private HistoryService() { }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await MobileDatabaseService.Instance.InitializeAsync(cancellationToken);
        var rows = await MobileDatabaseService.Instance.LoadPlaybackHistoryAsync(cancellationToken);
        HistoryItems.Clear();

        foreach (var row in rows)
        {
            try
            {
                var place = JsonSerializer.Deserialize<PlaceItem>(row.PlacePayloadJson, JsonOptions);
                if (place is null)
                {
                    continue;
                }

                place.HistoryAddedAt = row.PlayedAtUtc.ToLocalTime();
                HistoryItems.Add(place);
            }
            catch
            {
            }
        }

        _isInitialized = true;
    }

    public void AddToHistory(PlaceItem item)
    {
        var existingItem = HistoryItems.FirstOrDefault(existing =>
            string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase));

        if (existingItem is not null)
        {
            HistoryItems.Remove(existingItem);
        }

        var clonedItem = ClonePlaceItem(item);
        HistoryItems.Insert(0, clonedItem);
        TrimHistory();
        _ = PersistAsync(clonedItem);
    }

    public void RemoveFromHistory(PlaceItem item)
    {
        var existingItem = HistoryItems.FirstOrDefault(existing =>
            string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase));

        if (existingItem is not null)
        {
            HistoryItems.Remove(existingItem);
        }

        _ = MobileDatabaseService.Instance.DeletePlaybackHistoryAsync(item.Id);
    }

    public void ClearHistory()
    {
        HistoryItems.Clear();
        _ = MobileDatabaseService.Instance.ClearPlaybackHistoryAsync();
    }

    private static PlaceItem ClonePlaceItem(PlaceItem item)
    {
        return new PlaceItem
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            AudioDescription = item.AudioDescription,
            Category = item.Category,
            Rating = item.Rating,
            Image = item.Image,
            PreferenceImage = item.PreferenceImage,
            GalleryImages = Array.Empty<string>(),
            Address = item.Address,
            Phone = item.Phone,
            Email = item.Email,
            Website = item.Website,
            EstablishedYear = item.EstablishedYear,
            RadiusText = item.RadiusText,
            StandbyRadiusText = item.StandbyRadiusText,
            GpsText = item.GpsText,
            PriorityText = item.PriorityText,
            DebounceText = item.DebounceText,
            OwnerName = item.OwnerName,
            StatusText = item.StatusText,
            GpsTriggerText = item.GpsTriggerText,
            AudioCountText = item.AudioCountText,
            AudioTracks = item.AudioTracks.ToList(),
            AvailableVoiceGenders = item.AvailableVoiceGenders.Take(3).ToList(),
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            CategoryColor = item.CategoryColor,
            CategoryTextColor = item.CategoryTextColor,
            HistoryAddedAt = DateTimeOffset.Now,
            IsPlayed = false
        };
    }

    private void TrimHistory()
    {
        while (HistoryItems.Count > MaxHistoryItems)
        {
            HistoryItems.RemoveAt(HistoryItems.Count - 1);
        }
    }

    private static async Task PersistAsync(PlaceItem item)
    {
        try
        {
            var payload = JsonSerializer.Serialize(item, JsonOptions);
            await MobileDatabaseService.Instance.SavePlaybackHistoryAsync(
                item.Id,
                payload,
                item.HistoryAddedAt ?? DateTimeOffset.Now);
        }
        catch
        {
        }
    }
}
