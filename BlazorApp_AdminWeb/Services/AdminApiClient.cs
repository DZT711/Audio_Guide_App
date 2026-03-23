using System.Net.Http.Json;
using BlazorApp_AdminWeb.Models;

namespace BlazorApp_AdminWeb.Services;

public sealed class AdminApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync() =>
        await httpClient.GetFromJsonAsync<List<CategoryDto>>("Category") ?? [];

    public async Task<IReadOnlyList<LocationDto>> GetLocationsAsync() =>
        await httpClient.GetFromJsonAsync<List<LocationDto>>("Location") ?? [];

    public async Task<IReadOnlyList<AudioDto>> GetAudioAsync() =>
        await httpClient.GetFromJsonAsync<List<AudioDto>>("Audio") ?? [];

    public async Task CreateCategoryAsync(CategoryFormModel model)
    {
        using var response = await httpClient.PostAsJsonAsync("Category", new
        {
            model.Name,
            model.Description,
            model.Status
        });

        await EnsureSuccessAsync(response, "Unable to create category.");
    }

    public async Task UpdateCategoryAsync(int id, CategoryFormModel model)
    {
        using var response = await httpClient.PutAsJsonAsync($"Category/{id}", new
        {
            model.Name,
            model.Description,
            model.Status
        });

        await EnsureSuccessAsync(response, "Unable to update category.");
    }

    public async Task DeleteCategoryAsync(int id)
    {
        using var response = await httpClient.DeleteAsync($"Category/{id}");
        await EnsureSuccessAsync(response, "Unable to archive category.");
    }

    public async Task CreateLocationAsync(LocationFormModel model)
    {
        using var response = await httpClient.PostAsJsonAsync("Location", new
        {
            model.Name,
            model.Address,
            model.CategoryId,
            model.EstablishedYear,
            model.Description,
            model.Latitude,
            model.Longitude,
            model.OwnerName,
            model.WebURL,
            model.Phone,
            model.Email,
            model.Status
        });

        await EnsureSuccessAsync(response, "Unable to create location.");
    }

    public async Task UpdateLocationAsync(int id, LocationFormModel model)
    {
        using var response = await httpClient.PutAsJsonAsync($"Location/{id}", new
        {
            model.Name,
            model.Address,
            model.CategoryId,
            model.EstablishedYear,
            model.Description,
            model.Latitude,
            model.Longitude,
            model.OwnerName,
            model.WebURL,
            model.Phone,
            model.Email,
            model.Status
        });

        await EnsureSuccessAsync(response, "Unable to update location.");
    }

    public async Task DeleteLocationAsync(int id)
    {
        using var response = await httpClient.DeleteAsync($"Location/{id}");
        await EnsureSuccessAsync(response, "Unable to archive location.");
    }

    public async Task CreateAudioAsync(AudioFormModel model)
    {
        using var response = await httpClient.PostAsJsonAsync("Audio", new
        {
            model.Title,
            model.LocationName,
            model.Description,
            model.AudioURL,
            model.Language,
            model.VoiceGender,
            model.Script,
            model.Duration,
            model.Status
        });

        await EnsureSuccessAsync(response, "Unable to create audio.");
    }

    public async Task UpdateAudioAsync(int id, AudioFormModel model)
    {
        using var response = await httpClient.PutAsJsonAsync($"Audio/{id}", new
        {
            model.Title,
            model.LocationName,
            model.Description,
            model.AudioURL,
            model.Language,
            model.VoiceGender,
            model.Script,
            model.Duration,
            model.Status
        });

        await EnsureSuccessAsync(response, "Unable to update audio.");
    }

    public async Task DeleteAudioAsync(int id)
    {
        using var response = await httpClient.DeleteAsync($"Audio/{id}");
        await EnsureSuccessAsync(response, "Unable to archive audio.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string fallbackMessage)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        try
        {
            var apiMessage = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (!string.IsNullOrWhiteSpace(apiMessage?.Message))
            {
                throw new InvalidOperationException(apiMessage.Message);
            }
        }
        catch (NotSupportedException)
        {
        }
        catch (System.Text.Json.JsonException)
        {
        }

        throw new InvalidOperationException(fallbackMessage);
    }
}
