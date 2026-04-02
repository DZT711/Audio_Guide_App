using System.Collections.ObjectModel;
using MauiApp_Mobile.Models;

namespace MauiApp_Mobile.Services;

public class HistoryService
{
    private static HistoryService? _instance;
    public static HistoryService Instance => _instance ??= new HistoryService();

    public ObservableCollection<PlaceItem> HistoryItems { get; private set; } = new();

    private HistoryService() { }

    public void AddToHistory(PlaceItem item)
    {
        if (HistoryItems.Any(existing => string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        HistoryItems.Insert(0, ClonePlaceItem(item));
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
            GalleryImages = item.GalleryImages.ToList(),
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
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            CategoryColor = item.CategoryColor,
            CategoryTextColor = item.CategoryTextColor,
            IsPlayed = false
        };
    }
}
