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
        if (!HistoryItems.Contains(item))
        {
            // Add to top
            HistoryItems.Insert(0, item);
        }
    }

    public void RemoveFromHistory(PlaceItem item)
    {
        if (HistoryItems.Contains(item))
        {
            HistoryItems.Remove(item);
            // Optional: Reset state if removed from history?
            // item.IsPlayed = false; 
        }
    }
}