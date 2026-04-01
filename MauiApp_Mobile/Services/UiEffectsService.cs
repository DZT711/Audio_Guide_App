namespace MauiApp_Mobile.Services;

public static class UiEffectsService
{
    public static async Task RunSkeletonPulseAsync(CancellationToken cancellationToken, params VisualElement[] elements)
    {
        var activeElements = elements.Where(element => element is not null).ToArray();
        if (activeElements.Length == 0)
            return;

        foreach (var element in activeElements)
        {
            element.Opacity = 0.95;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.WhenAll(activeElements.Select(element => element.FadeToAsync(0.42, 650, Easing.CubicInOut)));
                await Task.WhenAll(activeElements.Select(element => element.FadeToAsync(0.95, 650, Easing.CubicInOut)));
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            foreach (var element in activeElements)
            {
                element.Opacity = 1;
            }
        }
    }

    public static async Task AnimateEntranceAsync(params VisualElement[] elements)
    {
        var activeElements = elements.Where(element => element is not null).ToArray();
        if (activeElements.Length == 0)
            return;

        foreach (var element in activeElements)
        {
            element.Opacity = 0;
            element.TranslationY = 18;
        }

        for (var index = 0; index < activeElements.Length; index++)
        {
            var element = activeElements[index];
            await Task.Delay(index == 0 ? 0 : 55);
            _ = element.TranslateToAsync(0, 0, 280, Easing.CubicOut);
            _ = element.FadeToAsync(1, 220, Easing.CubicOut);
        }
    }

    public static async Task TogglePopupAsync(VisualElement popup, bool show)
    {
        if (show)
        {
            popup.Opacity = 0;
            popup.Scale = 0.96;
            popup.IsVisible = true;
            await Task.WhenAll(
                popup.FadeToAsync(1, 180, Easing.CubicOut),
                popup.ScaleToAsync(1, 180, Easing.CubicOut));
            return;
        }

        if (!popup.IsVisible)
            return;

        await Task.WhenAll(
            popup.FadeToAsync(0, 120, Easing.CubicIn),
            popup.ScaleToAsync(0.96, 120, Easing.CubicIn));

        popup.IsVisible = false;
        popup.Opacity = 1;
        popup.Scale = 1;
    }
}
