using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlazorApp_AdminWeb.Models;
using Project_SharedClassLibrary.Constants;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Storage;

namespace BlazorApp_AdminWeb.Services;

public sealed class AdminApiClient(HttpClient httpClient, AdminSessionState sessionState)
{
    private const long MaxAudioUploadBytes = 25L * 1024 * 1024;
    private const long MaxImageUploadBytes = 15L * 1024 * 1024;

    public async Task<AdminLoginResponse> LoginAsync(string userName, string password)
    {
        ClearAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(ApiRoutes.AuthLogin, new AdminLoginRequest
        {
            UserName = userName,
            Password = password
        });

        await EnsureSuccessAsync(response, "Unable to sign in.");
        return await ReadJsonAsync<AdminLoginResponse>(response, "Unable to load the current admin session.");
    }

    public async Task<AdminLoginResponse> GetCurrentSessionAsync()
    {
        ApplyAuthHeader();

        using var response = await httpClient.GetAsync(ApiRoutes.AuthMe);
        await EnsureSuccessAsync(response, "Unable to restore the current session.");
        return await ReadJsonAsync<AdminLoginResponse>(response, "Unable to restore the current session.");
    }

    public async Task LogoutAsync()
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsync(ApiRoutes.AuthLogout, content: null);
        await EnsureSuccessAsync(response, "Unable to sign out.");
    }

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync() =>
        GetListAsync<CategoryDto>(ApiRoutes.Categories, "Unable to load categories.");

    public Task<IReadOnlyList<LocationDto>> GetLocationsAsync() =>
        GetListAsync<LocationDto>(ApiRoutes.Locations, "Unable to load locations.");

    public Task<IReadOnlyList<AudioDto>> GetAudioAsync() =>
        GetListAsync<AudioDto>(ApiRoutes.Audio, "Unable to load audio.");

    public Task<IReadOnlyList<DashboardUserDto>> GetUsersAsync() =>
        GetListAsync<DashboardUserDto>(ApiRoutes.Users, "Unable to load users.");

    public async Task<DashboardOverviewDto> GetDashboardOverviewAsync()
    {
        ApplyAuthHeader();

        using var response = await httpClient.GetAsync(ApiRoutes.DashboardOverview);
        await EnsureSuccessAsync(response, "Unable to load dashboard data.");
        return await ReadJsonAsync<DashboardOverviewDto>(response, "Unable to load dashboard data.");
    }

    public async Task<DashboardSnapshotDto> GetDashboardSnapshotAsync()
    {
        ApplyAuthHeader();

        using var response = await httpClient.GetAsync(ApiRoutes.DashboardSnapshot);
        await EnsureSuccessAsync(response, "Unable to export the dashboard snapshot.");
        return await ReadJsonAsync<DashboardSnapshotDto>(response, "Unable to export the dashboard snapshot.");
    }

    public async Task CreateCategoryAsync(CategoryFormModel model)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(
            ApiRoutes.Categories,
            new CategoryUpsertRequest
            {
                Name = model.Name,
                Description = model.Description,
                Status = model.Status
            });

        await EnsureSuccessAsync(response, "Unable to create category.");
    }

    public async Task UpdateCategoryAsync(int id, CategoryFormModel model)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PutAsJsonAsync(
            $"{ApiRoutes.Categories}/{id}",
            new CategoryUpsertRequest
            {
                Name = model.Name,
                Description = model.Description,
                Status = model.Status
            });

        await EnsureSuccessAsync(response, "Unable to update category.");
    }

    public async Task DeleteCategoryAsync(int id)
    {
        ApplyAuthHeader();

        using var response = await httpClient.DeleteAsync($"{ApiRoutes.Categories}/{id}");
        await EnsureSuccessAsync(response, "Unable to archive category.");
    }

    public async Task CreateLocationAsync(LocationFormModel model)
    {
        ApplyAuthHeader();

        using var content = CreateLocationContent(model);
        using var response = await httpClient.PostAsync(ApiRoutes.Locations, content);

        await EnsureSuccessAsync(response, "Unable to create location.");
    }

    public async Task UpdateLocationAsync(int id, LocationFormModel model)
    {
        ApplyAuthHeader();

        using var content = CreateLocationContent(model);
        using var response = await httpClient.PutAsync($"{ApiRoutes.Locations}/{id}", content);

        await EnsureSuccessAsync(response, "Unable to update location.");
    }

    public async Task DeleteLocationAsync(int id)
    {
        ApplyAuthHeader();

        using var response = await httpClient.DeleteAsync($"{ApiRoutes.Locations}/{id}");
        await EnsureSuccessAsync(response, "Unable to archive location.");
    }

    public async Task CreateAudioAsync(AudioFormModel model)
    {
        ApplyAuthHeader();

        using var content = CreateAudioContent(model);
        using var response = await httpClient.PostAsync(ApiRoutes.Audio, content);

        await EnsureSuccessAsync(response, "Unable to create audio.");
    }

    public async Task UpdateAudioAsync(int id, AudioFormModel model)
    {
        ApplyAuthHeader();

        using var content = CreateAudioContent(model);
        using var response = await httpClient.PutAsync($"{ApiRoutes.Audio}/{id}", content);

        await EnsureSuccessAsync(response, "Unable to update audio.");
    }

    public async Task DeleteAudioAsync(int id)
    {
        ApplyAuthHeader();

        using var response = await httpClient.DeleteAsync($"{ApiRoutes.Audio}/{id}");
        await EnsureSuccessAsync(response, "Unable to archive audio.");
    }

    public async Task CreateUserAsync(UserFormModel model)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(ApiRoutes.Users, CreateUserRequest(model));
        await EnsureSuccessAsync(response, "Unable to create user.");
    }

    public async Task<DashboardUserInviteResultDto> InviteUserAsync(UserFormModel model)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(ApiRoutes.UserInvite, new DashboardUserInviteRequest
        {
            Username = string.IsNullOrWhiteSpace(model.Username) ? null : model.Username,
            FullName = model.FullName,
            Email = model.Email,
            Phone = model.Phone,
            Role = model.Role
        });

        await EnsureSuccessAsync(response, "Unable to invite user.");
        return await ReadJsonAsync<DashboardUserInviteResultDto>(response, "Unable to invite user.");
    }

    public async Task UpdateUserAsync(int id, UserFormModel model)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PutAsJsonAsync($"{ApiRoutes.Users}/{id}", CreateUserRequest(model));
        await EnsureSuccessAsync(response, "Unable to update user.");
    }

    public async Task DeleteUserAsync(int id)
    {
        ApplyAuthHeader();

        using var response = await httpClient.DeleteAsync($"{ApiRoutes.Users}/{id}");
        await EnsureSuccessAsync(response, "Unable to archive user.");
    }

    public async Task<string?> ResolvePlayableAudioUrlAsync(string? audioPath, CancellationToken cancellationToken = default)
    {
        foreach (var candidate in GetContentUrlCandidates(audioPath, SharedStoragePaths.NormalizePublicAudioPath))
        {
            if (await UrlExistsAsync(candidate, cancellationToken))
            {
                return candidate.ToString();
            }
        }

        return null;
    }

    public string? ResolveImageUrl(string? imagePath) =>
        GetContentUrlCandidates(imagePath, SharedStoragePaths.NormalizePublicImagePath)
            .Select(candidate => candidate.ToString())
            .FirstOrDefault();

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string route, string fallbackMessage)
    {
        ApplyAuthHeader();

        using var response = await httpClient.GetAsync(route);
        await EnsureSuccessAsync(response, fallbackMessage);
        return await ReadJsonAsync<List<T>>(response, fallbackMessage) ?? [];
    }

    private static DashboardUserUpsertRequest CreateUserRequest(UserFormModel model) =>
        new()
        {
            Username = model.Username,
            Password = string.IsNullOrWhiteSpace(model.Password) ? null : model.Password,
            FullName = model.FullName,
            Role = model.Role,
            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email,
            Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone,
            Status = model.Status
        };

    private static MultipartFormDataContent CreateLocationContent(LocationFormModel model)
    {
        var content = new MultipartFormDataContent();

        AddString(content, nameof(model.Name), model.Name);
        AddString(content, nameof(model.Description), model.Description);
        AddString(content, nameof(model.CategoryId), model.CategoryId.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.OwnerId), model.OwnerId?.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.Latitude), model.Latitude.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.Longitude), model.Longitude.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.Radius), model.Radius.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.StandbyRadius), model.StandbyRadius.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.Priority), model.Priority.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.DebounceSeconds), model.DebounceSeconds.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.IsGpsTriggerEnabled), model.IsGpsTriggerEnabled.ToString());
        AddString(content, nameof(model.Address), model.Address);
        AddString(content, nameof(model.WebURL), model.WebURL);
        AddString(content, nameof(model.Email), model.Email);
        AddString(content, nameof(model.Phone), model.Phone);
        AddString(content, nameof(model.EstablishedYear), model.EstablishedYear.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.Status), model.Status.ToString(CultureInfo.InvariantCulture));

        foreach (var imageFile in model.ImageFiles)
        {
            var stream = imageFile.OpenReadStream(MaxImageUploadBytes);
            var fileContent = new StreamContent(stream);

            if (!string.IsNullOrWhiteSpace(imageFile.ContentType))
            {
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(imageFile.ContentType);
            }

            content.Add(fileContent, "ImageFiles", Path.GetFileName(imageFile.Name));
        }

        return content;
    }

    private static MultipartFormDataContent CreateAudioContent(AudioFormModel model)
    {
        var content = new MultipartFormDataContent();

        AddString(content, nameof(model.LocationId), model.LocationId.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.Language), model.Language);
        AddString(content, nameof(model.Title), model.Title);
        AddString(content, nameof(model.Description), model.Description);
        AddString(content, nameof(model.SourceType), model.SourceType);
        AddString(content, nameof(model.Script), model.Script);
        AddString(content, "AudioURL", model.AudioPath);
        AddString(content, nameof(model.Duration), model.Duration.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.VoiceName), model.VoiceName);
        AddString(content, nameof(model.VoiceGender), model.VoiceGender);
        AddString(content, nameof(model.Priority), model.Priority.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.PlaybackMode), model.PlaybackMode);
        AddString(content, nameof(model.InterruptPolicy), model.InterruptPolicy);
        AddString(content, nameof(model.IsDownloadable), model.IsDownloadable.ToString());
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

    private IEnumerable<Uri> GetContentUrlCandidates(string? contentPath, Func<string?, string?> normalizePath)
    {
        if (string.IsNullOrWhiteSpace(contentPath) || httpClient.BaseAddress is null)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in BuildCandidates(contentPath, normalizePath))
        {
            if (seen.Add(candidate.AbsoluteUri))
            {
                yield return candidate;
            }
        }
    }

    private IEnumerable<Uri> BuildCandidates(string contentPath, Func<string?, string?> normalizePath)
    {
        var trimmedPath = contentPath.Trim();
        if (Uri.TryCreate(trimmedPath, UriKind.Absolute, out var absoluteUri))
        {
            yield return absoluteUri;
            yield break;
        }

        yield return new Uri(httpClient.BaseAddress!, trimmedPath);

        var normalizedManagedPath = normalizePath(trimmedPath);
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

    private void ApplyAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(sessionState.Token))
        {
            ClearAuthHeader();
            return;
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionState.Token);
    }

    private void ClearAuthHeader() => httpClient.DefaultRequestHeaders.Authorization = null;

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, string fallbackMessage)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>();
        return payload ?? throw new InvalidOperationException(fallbackMessage);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string fallbackMessage)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await ReadApiErrorAsync(response, fallbackMessage);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            sessionState.Clear();
            throw new UnauthorizedAccessException(message);
        }

        throw new InvalidOperationException(message);
    }

    private static async Task<string> ReadApiErrorAsync(HttpResponseMessage response, string fallbackMessage)
    {
        try
        {
            var rawContent = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return fallbackMessage;
            }

            using var document = JsonDocument.Parse(rawContent);
            if (document.RootElement.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(messageElement.GetString()))
            {
                return messageElement.GetString()!;
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
                        if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                        {
                            errors.Add(item.GetString()!);
                        }
                    }
                }

                if (errors.Count > 0)
                {
                    return string.Join(" ", errors.Distinct());
                }
            }

            if (document.RootElement.TryGetProperty("title", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(titleElement.GetString()))
            {
                return titleElement.GetString()!;
            }
        }
        catch (NotSupportedException)
        {
        }
        catch (JsonException)
        {
        }

        return fallbackMessage;
    }
}
