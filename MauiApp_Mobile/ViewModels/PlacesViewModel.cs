using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp_Mobile.Models;
using MauiApp_Mobile.Services;
using Project_SharedClassLibrary.Contracts;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MauiApp_Mobile.ViewModels;

public partial class PlacesViewModel : ObservableObject
{
    private const string AllCategoryValue = "__all__";
    private const string AllVoiceValue = "__all_voice__";
    private const string AudioAvailableVoiceValue = "__audio_available__";
    private const string MaleVoiceValue = "Male";
    private const string FemaleVoiceValue = "Female";

    private readonly PlaceCatalogService _catalogService;
    private readonly List<PlaceItem> _allPlaces = [];
    private readonly List<CategoryDto> _categories = [];
    private bool _isSilentRefreshing;
    private string _placesSignature = string.Empty;
    private string _categoriesSignature = string.Empty;
    private string _selectedCategoryValue = AllCategoryValue;
    private string _selectedVoiceValue = AllVoiceValue;

    public PlacesViewModel(PlaceCatalogService catalogService)
    {
        _catalogService = catalogService;
        ApplyTexts();
    }

    public ObservableCollection<PlaceItem> Places { get; } = [];

    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; } = [];

    public ObservableCollection<CategoryFilterOption> VoiceFilters { get; } = [];

    public IReadOnlyList<PlaceItem> AllPlaces => _allPlaces;

    public string? PreferredVoiceFilterValue =>
        _selectedVoiceValue == AllVoiceValue || _selectedVoiceValue == AudioAvailableVoiceValue
            ? null
            : _selectedVoiceValue;

    public event Func<PlaceItem, Task>? PlaceRequested;

    public event Func<PlaceItem, Task>? PlayRequested;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string countText = "0 địa điểm";

    [ObservableProperty]
    private string filterHeaderText = "Lọc";

    [ObservableProperty]
    private Color filterHeaderColor = Colors.Black;

    [ObservableProperty]
    private bool showEmptyState;

    [ObservableProperty]
    private DateTimeOffset lastCatalogRefreshAt;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public async Task LoadCatalogAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var catalogSnapshot = await _catalogService.GetCatalogAsync(forceRefresh, cancellationToken);
        ApplyCatalogSnapshot(catalogSnapshot.Places, catalogSnapshot.Categories);
        LastCatalogRefreshAt = DateTimeOffset.UtcNow;
    }

    public async Task<bool> RefreshIfChangedAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (_isSilentRefreshing || IsRefreshing)
        {
            return false;
        }

        try
        {
            _isSilentRefreshing = true;

            var catalogSnapshot = await _catalogService.GetCatalogAsync(forceRefresh, cancellationToken);
            var places = catalogSnapshot.Places;
            var categories = catalogSnapshot.Categories;
            if (!HasCatalogChanged(places, categories))
            {
                return false;
            }

            ApplyCatalogSnapshot(places, categories);
            LastCatalogRefreshAt = DateTimeOffset.UtcNow;
            return true;
        }
        finally
        {
            _isSilentRefreshing = false;
        }
    }

    public void ApplyTexts()
    {
        SyncCategoryFilters(_categories);
        SyncVoiceFilters();
        RefreshDisplayState();
    }

    public void ApplyCatalogSnapshot(IReadOnlyList<PlaceItem> places, IReadOnlyList<CategoryDto> categories)
    {
        _placesSignature = ComputePlacesSignature(places);
        _categoriesSignature = ComputeCategoriesSignature(categories);

        _allPlaces.Clear();
        _allPlaces.AddRange(places);

        _categories.Clear();
        _categories.AddRange(categories);

        SyncCategoryFilters(categories);
        SyncVoiceFilters();
        ApplyFilter();
    }

    public bool HasCatalogChanged(IReadOnlyList<PlaceItem> places, IReadOnlyList<CategoryDto> categories)
    {
        var nextPlacesSignature = ComputePlacesSignature(places);
        var nextCategoriesSignature = ComputeCategoriesSignature(categories);

        return !string.Equals(_placesSignature, nextPlacesSignature, StringComparison.Ordinal)
            || !string.Equals(_categoriesSignature, nextCategoriesSignature, StringComparison.Ordinal);
    }

    public bool NeedsRemoteRefresh(TimeSpan maxAge) =>
        LastCatalogRefreshAt == default || DateTimeOffset.UtcNow - LastCatalogRefreshAt >= maxAge;

    public PlaceItem? FindPlaceById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return _allPlaces.FirstOrDefault(item =>
            string.Equals(item.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void ApplyCategory(string categoryValue)
    {
        _selectedCategoryValue = string.Equals(_selectedCategoryValue, categoryValue, StringComparison.OrdinalIgnoreCase)
            ? AllCategoryValue
            : categoryValue;

        UpdateCategorySelectionState();
        ApplyFilter();
    }

    public void ApplyVoice(string voiceValue)
    {
        _selectedVoiceValue = string.Equals(_selectedVoiceValue, voiceValue, StringComparison.OrdinalIgnoreCase)
            ? AllVoiceValue
            : voiceValue;

        UpdateVoiceSelectionState();
        ApplyFilter();
    }

    public void SyncCategoryFilters(IEnumerable<CategoryDto> categories)
    {
        var categoryNames = categories
            .Where(item => item.Status == 1 && !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => item.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var selectedExists = _selectedCategoryValue == AllCategoryValue ||
            categoryNames.Any(item => string.Equals(item, _selectedCategoryValue, StringComparison.OrdinalIgnoreCase));

        if (!selectedExists)
        {
            _selectedCategoryValue = AllCategoryValue;
        }

        CategoryFilters.Clear();
        CategoryFilters.Add(new CategoryFilterOption
        {
            Value = AllCategoryValue,
            DisplayName = LocalizationService.Instance.T("Filter.All"),
            Icon = "📍",
            IsAllOption = true
        });

        foreach (var categoryName in categoryNames)
        {
            CategoryFilters.Add(new CategoryFilterOption
            {
                Value = categoryName,
                DisplayName = categoryName,
                Icon = ResolveCategoryIcon(categoryName)
            });
        }

        UpdateCategorySelectionState();
    }

    public void SyncVoiceFilters()
    {
        var selectedExists = _selectedVoiceValue == AllVoiceValue ||
            _selectedVoiceValue == AudioAvailableVoiceValue ||
            string.Equals(_selectedVoiceValue, MaleVoiceValue, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_selectedVoiceValue, FemaleVoiceValue, StringComparison.OrdinalIgnoreCase);

        if (!selectedExists)
        {
            _selectedVoiceValue = AllVoiceValue;
        }

        VoiceFilters.Clear();
        VoiceFilters.Add(new CategoryFilterOption
        {
            Value = AllVoiceValue,
            DisplayName = LocalizationService.Instance.T("Filter.All"),
            Icon = "🎙",
            IsAllOption = true
        });
        VoiceFilters.Add(new CategoryFilterOption
        {
            Value = AudioAvailableVoiceValue,
            DisplayName = LocalizationService.Instance.T("Filter.WithAudio"),
            Icon = "🔊"
        });
        VoiceFilters.Add(new CategoryFilterOption
        {
            Value = MaleVoiceValue,
            DisplayName = LocalizationService.Instance.T("Filter.VoiceMale"),
            Icon = "♂"
        });
        VoiceFilters.Add(new CategoryFilterOption
        {
            Value = FemaleVoiceValue,
            DisplayName = LocalizationService.Instance.T("Filter.VoiceFemale"),
            Icon = "♀"
        });

        UpdateVoiceSelectionState();
    }

    public void UpdateCategorySelectionState()
    {
        foreach (var option in CategoryFilters)
        {
            option.IsSelected = string.Equals(option.Value, _selectedCategoryValue, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void UpdateVoiceSelectionState()
    {
        foreach (var option in VoiceFilters)
        {
            option.IsSelected = string.Equals(option.Value, _selectedVoiceValue, StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool MatchesVoiceFilter(PlaceItem place)
    {
        if (_selectedVoiceValue == AllVoiceValue)
        {
            return true;
        }

        if (_selectedVoiceValue == AudioAvailableVoiceValue)
        {
            return HasPlayableAudio(place);
        }

        return place.AvailableVoiceGenders.Any(item =>
            string.Equals(NormalizeVoiceGender(item), _selectedVoiceValue, StringComparison.OrdinalIgnoreCase));
    }

    public void ApplyFilter()
    {
        var keyword = SearchText.Trim().ToLowerInvariant();

        var filtered = _allPlaces
            .Where(place =>
                (_selectedCategoryValue == AllCategoryValue || string.Equals(place.Category, _selectedCategoryValue, StringComparison.OrdinalIgnoreCase)) &&
                MatchesVoiceFilter(place) &&
                (string.IsNullOrWhiteSpace(keyword) || place.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Places.Clear();
        foreach (var item in filtered)
        {
            Places.Add(item);
        }

        RefreshDisplayState();
    }

    public void RefreshDisplayState()
    {
        CountText = $"{Places.Count} {LocalizationService.Instance.T("Places.CountSuffix")}";
        ShowEmptyState = _allPlaces.Count > 0 && Places.Count == 0;

        var selectedCategory = CategoryFilters.FirstOrDefault(item => item.IsSelected);
        var selectedVoice = VoiceFilters.FirstOrDefault(item => item.IsSelected);
        var hasCategoryFilter = selectedCategory is not null && !selectedCategory.IsAllOption;
        var hasVoiceFilter = selectedVoice is not null && !selectedVoice.IsAllOption;

        if (!hasCategoryFilter && !hasVoiceFilter)
        {
            FilterHeaderText = LocalizationService.Instance.T("Places.Filter");
            FilterHeaderColor = ThemeService.Instance.GetColor("BodyText", "#243B5A");
            return;
        }

        var parts = new List<string>();
        if (hasCategoryFilter)
        {
            parts.Add(selectedCategory!.DisplayName);
        }

        if (hasVoiceFilter)
        {
            parts.Add(selectedVoice!.DisplayName);
        }

        FilterHeaderText = $"{LocalizationService.Instance.T("Places.Filter")}: {string.Join(" • ", parts)}";
        FilterHeaderColor = ThemeService.Instance.GetColor("PrimaryGreen", "#18A94B");
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        try
        {
            IsRefreshing = true;
            await LoadCatalogAsync(forceRefresh: true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Places refresh failed: {ex}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task OpenPlaceAsync(PlaceItem? place)
    {
        if (place is null || PlaceRequested is null)
        {
            return;
        }

        await PlaceRequested.Invoke(place);
    }

    [RelayCommand]
    private async Task PlayPlaceAsync(PlaceItem? place)
    {
        if (place is null || PlayRequested is null)
        {
            return;
        }

        await PlayRequested.Invoke(place);
    }

    private static bool HasPlayableAudio(PlaceItem place)
    {
        if (place.AudioTracks.Count > 0)
        {
            return place.AudioTracks.Any(track =>
                !string.IsNullOrWhiteSpace(track.AudioURL) ||
                !string.IsNullOrWhiteSpace(track.Script));
        }

        var numericPrefix = new string(place.AudioCountText
            .TakeWhile(character => char.IsDigit(character))
            .ToArray());

        return int.TryParse(numericPrefix, out var audioCount) && audioCount > 0;
    }

    private static string ResolveCategoryIcon(string category)
    {
        var normalized = category.Trim().ToLowerInvariant();

        if (normalized.Contains("food") || normalized.Contains("ăn"))
            return "🍜";

        if (normalized.Contains("drink") || normalized.Contains("uống") || normalized.Contains("cafe"))
            return "🥤";

        if (normalized.Contains("bus") || normalized.Contains("stop") || normalized.Contains("transit"))
            return "🚌";

        if (normalized.Contains("history") || normalized.Contains("heritage") || normalized.Contains("historical"))
            return "🏛";

        if (normalized.Contains("landmark"))
            return "📍";

        return "🏷";
    }

    private static string ComputePlacesSignature(IEnumerable<PlaceItem> places)
    {
        return string.Join(
            "||",
            places
                .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(item =>
                {
                    var voices = string.Join(",", item.AvailableVoiceGenders.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
                    return string.Join(
                        "|",
                        item.Id,
                        item.Name,
                        item.Category,
                        item.Description,
                        item.Image,
                        item.AudioCountText,
                        voices,
                        item.PriorityText,
                        item.StatusText,
                        item.Latitude.ToString("F6", CultureInfo.InvariantCulture),
                        item.Longitude.ToString("F6", CultureInfo.InvariantCulture));
                }));
    }

    private static string ComputeCategoriesSignature(IEnumerable<CategoryDto> categories)
    {
        return string.Join(
            "||",
            categories
                .OrderBy(item => item.Id)
                .Select(item => $"{item.Id}|{item.Name}|{item.Status}"));
    }

    private static string NormalizeVoiceGender(string? voiceGender)
    {
        if (string.IsNullOrWhiteSpace(voiceGender))
        {
            return string.Empty;
        }

        return voiceGender.Trim().ToUpperInvariant() switch
        {
            "MALE" => MaleVoiceValue,
            "NAM" => MaleVoiceValue,
            "FEMALE" => FemaleVoiceValue,
            "NU" => FemaleVoiceValue,
            "NỮ" => FemaleVoiceValue,
            _ => voiceGender.Trim()
        };
    }
}
