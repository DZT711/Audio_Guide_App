using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
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
    private const string AllLanguageValue = "__all_language__";
    private const string AudioAvailableVoiceValue = "__audio_available__";
    private const string MaleVoiceValue = "Male";
    private const string FemaleVoiceValue = "Female";

    private readonly PlaceCatalogService _catalogService;
    private readonly List<PlaceItem> _allPlaces = [];
    private readonly List<CategoryDto> _categories = [];
    private bool _isSilentRefreshing;
    private int _placesSignature = int.MinValue;
    private int _categoriesSignature = int.MinValue;
    private string _selectedCategoryValue = AllCategoryValue;
    private string _selectedVoiceValue = AllVoiceValue;
    private string _selectedLanguageValue = AllLanguageValue;
    private CancellationTokenSource? _searchDebounceCts;

    public PlacesViewModel(PlaceCatalogService catalogService)
    {
        _catalogService = catalogService;
        ApplyTexts();
    }

    public BatchObservableCollection<PlaceItem> Places { get; } = new();

    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; } = [];

    public ObservableCollection<CategoryFilterOption> VoiceFilters { get; } = [];

    public ObservableCollection<CategoryFilterOption> LanguageFilters { get; } = [];

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

    partial void OnSearchTextChanged(string value) => QueueApplyFilterDebounced();

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
        SyncLanguageFilters();
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
        SyncLanguageFilters();
        ApplyFilter();
    }

    public bool HasCatalogChanged(IReadOnlyList<PlaceItem> places, IReadOnlyList<CategoryDto> categories)
    {
        var nextPlacesSignature = ComputePlacesSignature(places);
        var nextCategoriesSignature = ComputeCategoriesSignature(categories);

        return _placesSignature != nextPlacesSignature
            || _categoriesSignature != nextCategoriesSignature;
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

    public void ApplyLanguage(string languageValue)
    {
        _selectedLanguageValue = string.Equals(_selectedLanguageValue, languageValue, StringComparison.OrdinalIgnoreCase)
            ? AllLanguageValue
            : languageValue;

        UpdateLanguageSelectionState();
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

    public void SyncLanguageFilters()
    {
        var languages = _allPlaces
            .SelectMany(ExtractLanguageCodes)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selectedExists = _selectedLanguageValue == AllLanguageValue ||
            languages.Any(code => string.Equals(code, _selectedLanguageValue, StringComparison.OrdinalIgnoreCase));

        if (!selectedExists)
        {
            _selectedLanguageValue = AllLanguageValue;
        }

        LanguageFilters.Clear();
        LanguageFilters.Add(new CategoryFilterOption
        {
            Value = AllLanguageValue,
            DisplayName = LocalizationService.Instance.T("Filter.All"),
            Icon = "🌐",
            IsAllOption = true
        });

        foreach (var language in languages)
        {
            LanguageFilters.Add(new CategoryFilterOption
            {
                Value = language,
                DisplayName = ResolveLanguageDisplayName(language),
                Icon = ResolveLanguageIcon(language)
            });
        }

        UpdateLanguageSelectionState();
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

    public void UpdateLanguageSelectionState()
    {
        foreach (var option in LanguageFilters)
        {
            option.IsSelected = string.Equals(option.Value, _selectedLanguageValue, StringComparison.OrdinalIgnoreCase);
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

        var availableVoices = place.AvailableVoiceGenders.Any()
            ? place.AvailableVoiceGenders
            : place.AudioTracks.Select(track => track.VoiceGender ?? string.Empty);

        return availableVoices.Any(item =>
            string.Equals(NormalizeVoiceGender(item), _selectedVoiceValue, StringComparison.OrdinalIgnoreCase));
    }

    public bool MatchesLanguageFilter(PlaceItem place)
    {
        if (_selectedLanguageValue == AllLanguageValue)
        {
            return true;
        }

        var selectedLanguage = NormalizeLanguageCode(_selectedLanguageValue);
        return ExtractLanguageCodes(place)
            .Any(code => string.Equals(code, selectedLanguage, StringComparison.OrdinalIgnoreCase));
    }

    public void ApplyFilter()
    {
        var keyword = SearchText.Trim().ToLowerInvariant();

        var filtered = _allPlaces
            .Where(place =>
                (_selectedCategoryValue == AllCategoryValue || string.Equals(place.Category, _selectedCategoryValue, StringComparison.OrdinalIgnoreCase)) &&
                MatchesVoiceFilter(place) &&
                MatchesLanguageFilter(place) &&
                (string.IsNullOrWhiteSpace(keyword) || place.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Places.ReplaceAll(filtered);

        RefreshDisplayState();
    }

    private void QueueApplyFilterDebounced()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        var cancellationToken = _searchDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(140, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(ApplyFilter);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Filter debounce failed: {ex.Message}");
            }
        }, cancellationToken);
    }

    public void RefreshDisplayState()
    {
        CountText = $"{Places.Count} {LocalizationService.Instance.T("Places.CountSuffix")}";
        ShowEmptyState = _allPlaces.Count > 0 && Places.Count == 0;

        var selectedCategory = CategoryFilters.FirstOrDefault(item => item.IsSelected);
        var selectedVoice = VoiceFilters.FirstOrDefault(item => item.IsSelected);
        var selectedLanguage = LanguageFilters.FirstOrDefault(item => item.IsSelected);
        var hasCategoryFilter = selectedCategory is not null && !selectedCategory.IsAllOption;
        var hasVoiceFilter = selectedVoice is not null && !selectedVoice.IsAllOption;
        var hasLanguageFilter = selectedLanguage is not null && !selectedLanguage.IsAllOption;

        if (!hasCategoryFilter && !hasVoiceFilter && !hasLanguageFilter)
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

        if (hasLanguageFilter)
        {
            parts.Add(selectedLanguage!.DisplayName);
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

    private static string NormalizeLanguageCode(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        return language.Trim().ToLowerInvariant() switch
        {
            var value when value.StartsWith("vn") => "vi",
            var value when value.StartsWith("vi") => "vi",
            var value when value.StartsWith("en") => "en",
            var value when value.StartsWith("fr") => "fr",
            var value when value.StartsWith("ja") || value.StartsWith("jp") => "jp",
            var value when value.StartsWith("ko") || value.StartsWith("kr") => "kr",
            var value when value.StartsWith("zh") || value.StartsWith("cn") => "cn",
            _ => language.Trim().ToLowerInvariant()
        };
    }

    private static string ResolveLanguageDisplayName(string languageCode) => languageCode switch
    {
        "vi" => "Tiếng Việt",
        "en" => "English",
        "fr" => "Français",
        "jp" => "日本語",
        "kr" => "한국어",
        "cn" => "中文",
        _ => languageCode.ToUpperInvariant()
    };

    private static string ResolveLanguageIcon(string languageCode) => languageCode switch
    {
        "vi" => "🇻🇳",
        "en" => "🇬🇧",
        "fr" => "🇫🇷",
        "jp" => "🇯🇵",
        "kr" => "🇰🇷",
        "cn" => "🇨🇳",
        _ => "🌐"
    };

    private static IEnumerable<string> ExtractLanguageCodes(PlaceItem place)
    {
        if (place is null)
        {
            return [];
        }

        var fromTracks = place.AudioTracks
            .Select(track => NormalizeLanguageCode(track.Language))
            .Where(code => !string.IsNullOrWhiteSpace(code));

        var normalizedTrackCodes = fromTracks.ToList();
        if (normalizedTrackCodes.Count > 0)
        {
            return normalizedTrackCodes;
        }

        if (string.IsNullOrWhiteSpace(place.LanguageBadgeSummaryText))
        {
            return [];
        }

        return place.LanguageBadgeSummaryText
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => NormalizeLanguageCode(token))
            .Where(IsSupportedLanguageCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSupportedLanguageCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        return code.Equals("vi", StringComparison.OrdinalIgnoreCase) ||
               code.Equals("en", StringComparison.OrdinalIgnoreCase) ||
               code.Equals("fr", StringComparison.OrdinalIgnoreCase) ||
               code.Equals("jp", StringComparison.OrdinalIgnoreCase) ||
               code.Equals("kr", StringComparison.OrdinalIgnoreCase) ||
               code.Equals("cn", StringComparison.OrdinalIgnoreCase);
    }

    private static int ComputePlacesSignature(IEnumerable<PlaceItem> places)
    {
        unchecked
        {
            var hash = 17;

            foreach (var item in places.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
            {
                hash = CombineHash(hash, item.Id);
                hash = CombineHash(hash, item.Name);
                hash = CombineHash(hash, item.Category);
                hash = CombineHash(hash, item.Description);
                hash = CombineHash(hash, item.Image);
                hash = CombineHash(hash, item.AudioCountText);
                hash = CombineHash(hash, item.PriorityText);
                hash = CombineHash(hash, item.StatusText);
                hash = CombineHash(hash, item.Latitude.ToString("F6", CultureInfo.InvariantCulture));
                hash = CombineHash(hash, item.Longitude.ToString("F6", CultureInfo.InvariantCulture));

                foreach (var voice in item.AvailableVoiceGenders.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    hash = CombineHash(hash, NormalizeVoiceGender(voice));
                }
            }

            return hash;
        }
    }

    private static int ComputeCategoriesSignature(IEnumerable<CategoryDto> categories)
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in categories.OrderBy(item => item.Id))
            {
                hash = CombineHash(hash, item.Id);
                hash = CombineHash(hash, item.Name);
                hash = CombineHash(hash, item.Status);
            }

            return hash;
        }
    }

    private static int CombineHash(int current, string? value) =>
        unchecked((current * 31) + (value is null ? 0 : StringComparer.Ordinal.GetHashCode(value)));

    private static int CombineHash(int current, int value) =>
        unchecked((current * 31) + value);

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

/// <summary>
/// ObservableCollection subclass that supports batch replace with a single Reset notification.
/// Drop-in replacement for ObservableCollection.
/// </summary>
public sealed class BatchObservableCollection<T> : System.Collections.ObjectModel.ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> newItems)
    {
        Items.Clear();
        foreach (var item in newItems)
        {
            Items.Add(item);
        }

        OnCollectionChanged(
            new System.Collections.Specialized.NotifyCollectionChangedEventArgs(
                System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
    }
}
