using System.Collections.ObjectModel;
using MauiApp_Mobile.Models;

namespace MauiApp_Mobile.Services;

public class HistoryService
{
    private const int MaxHistoryItems = 40;
    private static HistoryService? _instance;
    public static HistoryService Instance => _instance ??= new HistoryService();

    public ObservableCollection<PlaceItem> HistoryItems { get; private set; } = new();

    private HistoryService() { }

    public void AddToHistory(PlaceItem item)
    {
        var existingItem = HistoryItems.FirstOrDefault(existing =>
            string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase));

        if (existingItem is not null)
        {
            HistoryItems.Remove(existingItem);
        }

        HistoryItems.Insert(0, ClonePlaceItem(item));
        TrimHistory();
    }

    public void RemoveFromHistory(PlaceItem item)
    {
        var existingItem = HistoryItems.FirstOrDefault(existing =>
            string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase));

        if (existingItem is not null)
        {
            HistoryItems.Remove(existingItem);
        }
    }

    public void ClearHistory()
    {
        HistoryItems.Clear();
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
            AudioTracks = Array.Empty<Project_SharedClassLibrary.Contracts.PublicAudioTrackDto>(),
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
}
