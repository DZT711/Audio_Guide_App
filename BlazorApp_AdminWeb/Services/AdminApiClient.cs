using System.Net.Http.Json;
using BlazorApp_AdminWeb.Models;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Storage;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BlazorApp_AdminWeb.Services;

public sealed class AdminApiClient(HttpClient httpClient)
{
    private const long MaxAudioUploadBytes = 25L * 1024 * 1024;

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

    public async Task<string?> ResolvePlayableAudioUrlAsync(string? audioPath, CancellationToken cancellationToken = default)
    {
        foreach (var candidate in GetAudioUrlCandidates(audioPath))
        {
            if (await UrlExistsAsync(candidate, cancellationToken))
            {
                return candidate.ToString();
            }
        }

        return null;
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
            var stream = model.AudioFile.OpenReadStream(MaxAudioUploadBytes);
            var fileContent = new StreamContent(stream);

            if (!string.IsNullOrWhiteSpace(model.AudioFile.ContentType))
            {
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(model.AudioFile.ContentType);
            }

            content.Add(fileContent, "AudioFile", Path.GetFileName(model.AudioFile.Name));
        }

        return content;
    }

    private static void AddString(MultipartFormDataContent content, string name, string? value)
    {
        content.Add(new StringContent(value ?? string.Empty), name);
    }

    private IEnumerable<Uri> GetAudioUrlCandidates(string? audioPath)
    {
        if (string.IsNullOrWhiteSpace(audioPath) || httpClient.BaseAddress is null)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in BuildCandidates(audioPath))
        {
            if (seen.Add(candidate.AbsoluteUri))
            {
                yield return candidate;
            }
        }
    }

    private IEnumerable<Uri> BuildCandidates(string audioPath)
    {
        var trimmedPath = audioPath.Trim();
        if (Uri.TryCreate(trimmedPath, UriKind.Absolute, out var absoluteUri))
        {
            yield return absoluteUri;
            yield break;
        }

        yield return new Uri(httpClient.BaseAddress!, trimmedPath);

        var normalizedManagedPath = SharedStoragePaths.NormalizePublicAudioPath(trimmedPath);
        if (!string.Equals(normalizedManagedPath, trimmedPath, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(normalizedManagedPath))
        {
            yield return new Uri(httpClient.BaseAddress!, normalizedManagedPath);
        }
    }

    private async Task<bool> UrlExistsAsync(Uri audioUri, CancellationToken cancellationToken)
    {
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, audioUri);
        using var headResponse = await httpClient.SendAsync(
            headRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (headResponse.IsSuccessStatusCode)
        {
            return true;
        }

        if (headResponse.StatusCode != HttpStatusCode.MethodNotAllowed)
        {
            return false;
        }

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, audioUri);
        using var getResponse = await httpClient.SendAsync(
            getRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        return getResponse.IsSuccessStatusCode;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string fallbackMessage)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        try
        {
            var rawContent = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                throw new InvalidOperationException(fallbackMessage);
            }

            using var document = JsonDocument.Parse(rawContent);
            if (document.RootElement.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(messageElement.GetString()))
            {
                throw new InvalidOperationException(messageElement.GetString());
            }

            if (document.RootElement.TryGetProperty("errors", out var errorsElement)
                && errorsElement.ValueKind == JsonValueKind.Object)
            {
                var errors = new List<string>();
                foreach (var property in errorsElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var item in property.Value.EnumerateArray())
                    {
                        var message = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            errors.Add(message);
                        }
                    }
                }

                if (errors.Count > 0)
                {
                    throw new InvalidOperationException(string.Join(" ", errors.Distinct()));
                }
            }

            if (document.RootElement.TryGetProperty("title", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(titleElement.GetString()))
            {
                throw new InvalidOperationException(titleElement.GetString());
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
