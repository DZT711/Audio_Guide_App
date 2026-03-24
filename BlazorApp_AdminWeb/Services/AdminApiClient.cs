using System.Net.Http.Json;
using BlazorApp_AdminWeb.Models;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;
using System.Globalization;
using System.Net.Http.Headers;

namespace BlazorApp_AdminWeb.Services;

public sealed class AdminApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync() =>
        await httpClient.GetFromJsonAsync<List<CategoryDto>>(ApiRoutes.Categories) ?? [];

    public async Task<IReadOnlyList<LocationDto>> GetLocationsAsync() =>
        await httpClient.GetFromJsonAsync<List<LocationDto>>(ApiRoutes.Locations) ?? [];

    public async Task<IReadOnlyList<AudioDto>> GetAudioAsync() =>
        await httpClient.GetFromJsonAsync<List<AudioDto>>(ApiRoutes.Audio) ?? [];

    public async Task CreateCategoryAsync(CategoryFormModel model)
    {
        using var response = await httpClient.PostAsJsonAsync(
            ApiRoutes.Categories,
            new CategoryUpsertRequest(model.Name, model.Description, model.Status));

        await EnsureSuccessAsync(response, "Unable to create category.");
    }

    public async Task UpdateCategoryAsync(int id, CategoryFormModel model)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"{ApiRoutes.Categories}/{id}",
            new CategoryUpsertRequest(model.Name, model.Description, model.Status));

        await EnsureSuccessAsync(response, "Unable to update category.");
    }

    public async Task DeleteCategoryAsync(int id)
    {
        using var response = await httpClient.DeleteAsync($"{ApiRoutes.Categories}/{id}");
        await EnsureSuccessAsync(response, "Unable to archive category.");
    }

    public async Task CreateLocationAsync(LocationFormModel model)
    {
        using var response = await httpClient.PostAsJsonAsync(
            ApiRoutes.Locations,
            new LocationUpsertRequest(
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
                model.Status));

        await EnsureSuccessAsync(response, "Unable to create location.");
    }

    public async Task UpdateLocationAsync(int id, LocationFormModel model)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"{ApiRoutes.Locations}/{id}",
            new LocationUpsertRequest(
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
                model.Status));

        await EnsureSuccessAsync(response, "Unable to update location.");
    }

    public async Task DeleteLocationAsync(int id)
    {
        using var response = await httpClient.DeleteAsync($"{ApiRoutes.Locations}/{id}");
        await EnsureSuccessAsync(response, "Unable to archive location.");
    }

    public async Task CreateAudioAsync(AudioFormModel model)
    {
        using var content = CreateAudioContent(model);
        using var response = await httpClient.PostAsync(ApiRoutes.Audio, content);

        await EnsureSuccessAsync(response, "Unable to create audio.");
    }

    public async Task UpdateAudioAsync(int id, AudioFormModel model)
    {
        using var content = CreateAudioContent(model);
        using var response = await httpClient.PutAsync($"{ApiRoutes.Audio}/{id}", content);

        await EnsureSuccessAsync(response, "Unable to update audio.");
    }

    public async Task DeleteAudioAsync(int id)
    {
        using var response = await httpClient.DeleteAsync($"{ApiRoutes.Audio}/{id}");
        await EnsureSuccessAsync(response, "Unable to archive audio.");
    }

    private static MultipartFormDataContent CreateAudioContent(AudioFormModel model)
    {
        var content = new MultipartFormDataContent();

        AddString(content, nameof(model.Title), model.Title);
        AddString(content, nameof(model.LocationName), model.LocationName);
        AddString(content, nameof(model.Description), model.Description);
        AddString(content, "AudioURL", model.AudioPath);
        AddString(content, nameof(model.Language), model.Language);
        AddString(content, nameof(model.VoiceGender), model.VoiceGender);
        AddString(content, nameof(model.Script), model.Script);
        AddString(content, nameof(model.Duration), model.Duration.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.Status), model.Status.ToString(CultureInfo.InvariantCulture));

        if (model.AudioFile is not null)
        {
            var stream = model.AudioFile.OpenReadStream(25 * 1024 * 1024);
            var fileContent = new StreamContent(stream);

            if (!string.IsNullOrWhiteSpace(model.AudioFile.ContentType))
            {
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(model.AudioFile.ContentType);
            }

            content.Add(fileContent, "AudioFile", model.AudioFile.Name);
        }

        return content;
    }

    private static void AddString(MultipartFormDataContent content, string name, string? value)
    {
        content.Add(new StringContent(value ?? string.Empty), name);
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
