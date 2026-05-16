namespace BlazorApp_AdminWeb.Models;

public static class CategoryVisuals
{
    public static string GetIconClass(string? categoryName)
    {
        var normalized = Normalize(categoryName);

        if (ContainsAny(normalized, "art", "culture", "museum", "gallery", "heritage"))
        {
            return "bi bi-palette-fill";
        }

        if (ContainsAny(normalized, "food", "cuisine", "market", "restaurant", "street food"))
        {
            return "bi bi-cup-hot-fill";
        }

        if (ContainsAny(normalized, "nature", "park", "garden", "eco", "forest"))
        {
            return "bi bi-tree-fill";
        }

        if (ContainsAny(normalized, "history", "histor", "memorial"))
        {
            return "bi bi-hourglass-split";
        }

        if (ContainsAny(normalized, "relig", "temple", "church", "pagoda", "spiritual"))
        {
            return "bi bi-bank2";
        }

        if (ContainsAny(normalized, "shop", "mall", "retail"))
        {
            return "bi bi-bag-fill";
        }

        if (ContainsAny(normalized, "river", "water", "canal", "harbor"))
        {
            return "bi bi-water";
        }

        if (ContainsAny(normalized, "architecture", "building", "urban"))
        {
            return "bi bi-building";
        }

        if (ContainsAny(normalized, "night", "music", "entertainment"))
        {
            return "bi bi-music-note-beamed";
        }

        return "bi bi-stars";
    }

    public static string GetToneClass(string? categoryName)
    {
        var normalized = Normalize(categoryName);

        if (ContainsAny(normalized, "art", "culture", "museum", "history", "heritage"))
        {
            return "category-icon--rose";
        }

        if (ContainsAny(normalized, "food", "market", "cuisine"))
        {
            return "category-icon--amber";
        }

        if (ContainsAny(normalized, "nature", "park", "garden", "water"))
        {
            return "category-icon--emerald";
        }

        return "category-icon--sky";
    }

    private static string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant();

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.Ordinal));
}
