using System.Diagnostics;

namespace MauiApp_Mobile.Services;

/// <summary>
/// Generates animated placeholder text from active POI names for search UI.
/// </summary>
public sealed class SearchPlaceholderService
{
    private static readonly Lazy<SearchPlaceholderService> _instance =
        new(() => new SearchPlaceholderService());

    public static SearchPlaceholderService Instance => _instance.Value;

    private readonly List<string> _placeholderNames = new();
    private int _currentPlaceholderIndex;
    private readonly Random _random = new();

    private SearchPlaceholderService()
    {
        Debug.WriteLine("[SearchPlaceholder] Service initialized");
    }

    public async Task InitializeAsync()
    {
        try
        {
            var catalog = PlaceCatalogService.Instance;
            await catalog.EnsureLoadedAsync(false);

            var activePlaces = catalog.GetPlaces()
                .Where(p => p.Status == 1)
                .Select(p => p.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _placeholderNames.Clear();
            _placeholderNames.AddRange(activePlaces);

            if (_placeholderNames.Count > 0)
            {
                _currentPlaceholderIndex = _random.Next(_placeholderNames.Count);
                Debug.WriteLine($"[SearchPlaceholder] Initialized with {_placeholderNames.Count} placeholders");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SearchPlaceholder] Initialization error: {ex.Message}");
        }
    }

    public string GetRandomPlaceholder()
    {
        if (_placeholderNames.Count == 0)
        {
            return "Search places...";
        }

        _currentPlaceholderIndex = (_currentPlaceholderIndex + 1) % _placeholderNames.Count;
        return _placeholderNames[_currentPlaceholderIndex];
    }
}
