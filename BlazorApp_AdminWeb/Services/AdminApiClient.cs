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
    private const string NgrokBypassHeaderName = "ngrok-skip-browser-warning";
    private const string NgrokBypassHeaderValue = "true";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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

    public Task<IReadOnlyList<LanguageDto>> GetLanguagesAsync() =>
        GetListAsync<LanguageDto>(ApiRoutes.Languages, "Unable to load languages.");

    public Task<IReadOnlyList<LocationDto>> GetLocationsAsync() =>
        GetListAsync<LocationDto>(ApiRoutes.Locations, "Unable to load locations.");

    public Task<IReadOnlyList<TourDto>> GetToursAsync() =>
        GetListAsync<TourDto>(ApiRoutes.Tours, "Unable to load tours.");

    public Task<IReadOnlyList<AudioDto>> GetAudioAsync() =>
        GetListAsync<AudioDto>(ApiRoutes.Audio, "Unable to load audio.");

    public Task<IReadOnlyList<AudioDto>> GetAudioByLocationAsync(int locationId) =>
        GetListAsync<AudioDto>($"{ApiRoutes.Audio}/location/{locationId}", "Unable to load location audio.");

    public Task<IReadOnlyList<DashboardUserDto>> GetUsersAsync() =>
        GetListAsync<DashboardUserDto>(ApiRoutes.Users, "Unable to load users.");

    public async Task<LocationQrStatusDto> GetLocationQrStatusAsync(
        int locationId,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.GetAsync(ApiRoutes.GetLocationQrStatus(locationId), cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load the location QR status.");
        return await ReadJsonAsync<LocationQrStatusDto>(response, "Unable to load the location QR status.");
    }

    public async Task<DownloadedAdminFile> GenerateLocationQrAsync(
        int locationId,
        LocationQrGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(
            ApiRoutes.GetLocationQrGenerate(locationId),
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, "Unable to generate the location QR.");
        return await ReadFileAsync(response, cancellationToken);
    }

    public async Task<DownloadedAdminFile> GenerateBulkLocationQrAsync(
        LocationQrBulkRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(
            ApiRoutes.GetLocationQrBulkGenerate(),
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, "Unable to export bulk location QR files.");
        return await ReadFileAsync(response, cancellationToken);
    }

    public async Task<ChangeRequestListDto> GetChangeRequestsAsync(
        ChangeRequestQueryDto query,
        bool ownerOnly = false,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var route = BuildChangeRequestRoute(query, ownerOnly);
        using var response = await httpClient.GetAsync(route, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load change requests.");
        return await ReadJsonAsync<ChangeRequestListDto>(response, "Unable to load change requests.");
    }

    public async Task<InboxOverviewDto> GetInboxAsync(InboxQueryDto query, CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var route = BuildInboxRoute(query);
        using var response = await httpClient.GetAsync(route, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load inbox messages.");
        return await ReadJsonAsync<InboxOverviewDto>(response, "Unable to load inbox messages.");
    }

    public async Task<UsageHistoryOverviewDto> GetUsageHistoryAsync(bool includeSynthetic = false)
    {
        if (includeSynthetic)
        {
            return await GetLegacyUsageHistoryAsync(includeSynthetic);
        }

        try
        {
            var statisticsTask = GetUsageAnalyticsStatisticsV1Async();
            var historyTask = GetUsageAnalyticsHistorySnapshotV1Async();
            await Task.WhenAll(statisticsTask, historyTask);

            return MapUsageHistoryOverviewFromV1(
                historyTask.Result.Items,
                historyTask.Result.TotalCount,
                statisticsTask.Result);
        }
        catch
        {
            return await GetLegacyUsageHistoryAsync(includeSynthetic);
        }
    }

    public async Task<StatisticsOverviewDto> GetStatisticsAsync(StatisticsQueryDto query, CancellationToken cancellationToken = default)
    {
        // Statistics map requires coordinate-rich payloads (Locations + HeatmapPoints + RouteHistory).
        // The V1 analytics aggregation currently does not return those map layers.
        // Keep the legacy statistics endpoint as the source of truth for the Statistics page.
        return await GetLegacyStatisticsAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<StatisticsPoiReportRowDto>> GetTopPoisByPlayCountAsync(
        StatisticsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var route = BuildStatisticsRoute(query, ApiRoutes.StatisticsTopPois);
        using var response = await httpClient.GetAsync(route, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load top POIs report.");
        return await ReadJsonAsync<List<StatisticsPoiReportRowDto>>(response, "Unable to load top POIs report.");
    }

    public async Task<IReadOnlyList<StatisticsPoiReportRowDto>> GetAverageListeningByPoiAsync(
        StatisticsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var route = BuildStatisticsRoute(query, ApiRoutes.StatisticsAverageListening);
        using var response = await httpClient.GetAsync(route, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load average listening report.");
        return await ReadJsonAsync<List<StatisticsPoiReportRowDto>>(response, "Unable to load average listening report.");
    }

    public async Task<IReadOnlyList<StatisticsHeatPointDto>> GetStatisticsHeatmapAsync(
        StatisticsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var route = BuildStatisticsRoute(query, ApiRoutes.StatisticsHeatmap);
        using var response = await httpClient.GetAsync(route, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load heatmap data.");
        return await ReadJsonAsync<List<StatisticsHeatPointDto>>(response, "Unable to load heatmap data.");
    }

    public async Task<ActivityLogListDto> GetActivityLogsAsync(ActivityLogQueryDto query, CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var route = BuildActivityLogRoute(query);
        using var response = await httpClient.GetAsync(route, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load activity history.");
        return await ReadJsonAsync<ActivityLogListDto>(response, "Unable to load activity history.");
    }

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

    public async Task<ServerInfoDto> GetServerInfoAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.GetAsync(ApiRoutes.ServerInfo, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load server info.");
        return await ReadJsonAsync<ServerInfoDto>(response, "Unable to load server info.");
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

    public async Task CreateLanguageAsync(LanguageFormModel model)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(
            ApiRoutes.Languages,
            new LanguageUpsertRequest
            {
                Code = model.Code,
                Name = model.Name,
                NativeName = model.NativeName,
                PreferNativeVoice = model.PreferNativeVoice,
                IsDefault = model.IsDefault,
                Status = model.Status
            });

        await EnsureSuccessAsync(response, "Unable to create language.");
    }

    public async Task UpdateLanguageAsync(int id, LanguageFormModel model)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PutAsJsonAsync(
            $"{ApiRoutes.Languages}/{id}",
            new LanguageUpsertRequest
            {
                Code = model.Code,
                Name = model.Name,
                NativeName = model.NativeName,
                PreferNativeVoice = model.PreferNativeVoice,
                IsDefault = model.IsDefault,
                Status = model.Status
            });

        await EnsureSuccessAsync(response, "Unable to update language.");
    }

    public async Task DeleteLanguageAsync(int id)
    {
        ApplyAuthHeader();

        using var response = await httpClient.DeleteAsync($"{ApiRoutes.Languages}/{id}");
        await EnsureSuccessAsync(response, "Unable to archive language.");
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

    public async Task<ChangeRequestDto> SubmitLocationChangeRequestAsync(
        LocationFormModel model,
        string requestType,
        int? targetId = null,
        string? reason = null)
    {
        ApplyAuthHeader();

        using var content = CreateLocationContent(model, requestType, targetId, reason);
        using var response = await httpClient.PostAsync($"{ApiRoutes.ChangeRequests}/location", content);
        await EnsureSuccessAsync(response, "Unable to submit the POI request.");
        return await ReadJsonAsync<ChangeRequestDto>(response, "Unable to read the submitted POI request.");
    }

    public async Task<TourDto> CreateTourAsync(TourFormModel model, TourRoutePreviewDto? routePreview = null)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(
            ApiRoutes.Tours,
            CreateTourRequest(model, routePreview));

        await EnsureSuccessAsync(response, "Unable to create tour.");
        return await ReadJsonAsync<TourDto>(response, "Unable to read the created tour.");
    }

    public async Task UpdateTourAsync(int id, TourFormModel model, TourRoutePreviewDto? routePreview = null)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PutAsJsonAsync(
            $"{ApiRoutes.Tours}/{id}",
            CreateTourRequest(model, routePreview));

        await EnsureSuccessAsync(response, "Unable to update tour.");
    }

    public async Task DeleteTourAsync(int id)
    {
        ApplyAuthHeader();

        using var response = await httpClient.DeleteAsync($"{ApiRoutes.Tours}/{id}");
        await EnsureSuccessAsync(response, "Unable to archive tour.");
    }

    public async Task<TourRoutePreviewDto> PreviewTourRouteAsync(
        TourRoutePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(ApiRoutes.ToursPreview, request, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to preview the selected route.");
        return await ReadJsonAsync<TourRoutePreviewDto>(response, "Unable to preview the selected route.");
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

    public async Task<ChangeRequestDto> SubmitAudioChangeRequestAsync(
        AudioFormModel model,
        string requestType,
        int? targetId = null,
        string? reason = null)
    {
        ApplyAuthHeader();

        using var content = CreateAudioContent(model, requestType, targetId, reason);
        using var response = await httpClient.PostAsync($"{ApiRoutes.ChangeRequests}/audio", content);
        await EnsureSuccessAsync(response, "Unable to submit the audio request.");
        return await ReadJsonAsync<ChangeRequestDto>(response, "Unable to read the submitted audio request.");
    }

    public async Task<ChangeRequestDto> ApproveChangeRequestAsync(int id, string? adminNote, CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(
            $"{ApiRoutes.ChangeRequests}/{id}/approve",
            new ReviewChangeRequestRequest { AdminNote = adminNote },
            cancellationToken);
        await EnsureSuccessAsync(response, "Unable to approve the request.");
        return await ReadJsonAsync<ChangeRequestDto>(response, "Unable to read the approved request.");
    }

    public async Task<ChangeRequestDto> RejectChangeRequestAsync(int id, string adminNote, CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(
            $"{ApiRoutes.ChangeRequests}/{id}/reject",
            new ReviewChangeRequestRequest { AdminNote = adminNote },
            cancellationToken);
        await EnsureSuccessAsync(response, "Unable to reject the request.");
        return await ReadJsonAsync<ChangeRequestDto>(response, "Unable to read the rejected request.");
    }

    public async Task<AudioTtsPreviewResult> GenerateAudioTtsPreviewAsync(
        AudioTtsPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync(ApiRoutes.AudioTtsPreview, request, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to generate the TTS preview.");

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "audio/mpeg";
        var provider = response.Headers.TryGetValues("X-SmartTour-Tts-Provider", out var providerValues)
            ? providerValues.FirstOrDefault() ?? ""
            : "";
        var voiceName = response.Headers.TryGetValues("X-SmartTour-Tts-Voice", out var voiceValues)
            ? voiceValues.FirstOrDefault() ?? ""
            : "";
        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new AudioTtsPreviewResult(
            new MemoryStream(payload, writable: false),
            contentType,
            provider,
            voiceName);
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

    public async Task MarkInboxMessageReadAsync(int id, CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsync($"{ApiRoutes.Inbox}/{id}/read", content: null, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to mark the message as read.");
    }

    public async Task CreateInboxAnnouncementAsync(InboxAnnouncementRequest request, CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.PostAsJsonAsync($"{ApiRoutes.Inbox}/announcement", request, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to send the announcement.");
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

    private static TourUpsertRequest CreateTourRequest(TourFormModel model, TourRoutePreviewDto? routePreview) =>
        new()
        {
            Name = model.Name,
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            WalkingSpeedKph = model.WalkingSpeedKph,
            StartTime = string.IsNullOrWhiteSpace(model.StartTime) ? null : model.StartTime,
            Status = model.Status,
            RoutePreview = routePreview,
            Stops = model.StopLocationIds
                .Select((locationId, index) => new TourStopUpsertRequest
                {
                    LocationId = locationId,
                    SequenceOrder = index + 1
                })
                .ToList()
        };

    private static MultipartFormDataContent CreateLocationContent(
        LocationFormModel model,
        string? requestType = null,
        int? targetId = null,
        string? reason = null)
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
        AddString(content, "RetainedPreferenceImageUrl", model.ExistingPreferenceImageUrl);
        foreach (var retainedImageUrl in model.ExistingImageUrls.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            AddString(content, "RetainedImageUrls", retainedImageUrl);
        }
        AddString(content, nameof(model.EstablishedYear), model.EstablishedYear.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.Status), model.Status.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.QrSize), model.QrSize.ToString(CultureInfo.InvariantCulture));
        AddString(content, nameof(model.QrFormat), model.QrFormat);
        AddString(content, nameof(model.QrAutoplay), model.QrAutoplay.ToString());
        AddString(content, nameof(model.QrAudioTrackId), model.QrAudioTrackId?.ToString(CultureInfo.InvariantCulture));
        AddString(content, "RequestType", requestType);
        AddString(content, "TargetId", targetId?.ToString(CultureInfo.InvariantCulture));
        AddString(content, "Reason", reason);

        if (model.PreferenceImageFile is not null)
        {
            var fileContent = new ByteArrayContent(model.PreferenceImageFile.Content);

            if (!string.IsNullOrWhiteSpace(model.PreferenceImageFile.ContentType))
            {
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(model.PreferenceImageFile.ContentType);
            }

            content.Add(fileContent, "PreferenceImageFile", Path.GetFileName(model.PreferenceImageFile.Name));
        }

        foreach (var imageFile in model.ImageFiles)
        {
            var fileContent = new ByteArrayContent(imageFile.Content);

            if (!string.IsNullOrWhiteSpace(imageFile.ContentType))
            {
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(imageFile.ContentType);
            }

            content.Add(fileContent, "ImageFiles", Path.GetFileName(imageFile.Name));
        }

        return content;
    }

    private static MultipartFormDataContent CreateAudioContent(
        AudioFormModel model,
        string? requestType = null,
        int? targetId = null,
        string? reason = null)
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
        AddString(content, "RequestType", requestType);
        AddString(content, "TargetId", targetId?.ToString(CultureInfo.InvariantCulture));
        AddString(content, "Reason", reason);

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
        EnsureNgrokBypassHeader();

        if (string.IsNullOrWhiteSpace(sessionState.Token))
        {
            ClearAuthHeader();
            return;
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionState.Token);
    }

    private void ClearAuthHeader()
    {
        EnsureNgrokBypassHeader();
        httpClient.DefaultRequestHeaders.Authorization = null;
    }

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, string fallbackMessage)
    {
        var rawContent = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new InvalidOperationException(fallbackMessage);
        }

        try
        {
            using var document = JsonDocument.Parse(rawContent);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object
                && (TryReadWrappedPayload(root, "data", out T? wrappedPayload)
                    || TryReadWrappedPayload(root, "result", out wrappedPayload)))
            {
                return wrappedPayload!;
            }

            if (TryDeserializeElement(root, out T? payload))
            {
                return payload!;
            }
        }
        catch (JsonException)
        {
            if (LooksLikeHtml(rawContent))
            {
                throw new InvalidOperationException(
                    "API returned HTML instead of JSON. Check AdminApi base URL or ngrok tunnel configuration.");
            }

            throw new InvalidOperationException(fallbackMessage);
        }

        throw new InvalidOperationException(fallbackMessage);
    }

    private void EnsureNgrokBypassHeader()
    {
        if (httpClient.BaseAddress is null
            || !httpClient.BaseAddress.Host.Contains("ngrok", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (httpClient.DefaultRequestHeaders.Contains(NgrokBypassHeaderName))
        {
            return;
        }

        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(NgrokBypassHeaderName, NgrokBypassHeaderValue);
    }

    private static bool TryReadWrappedPayload<T>(JsonElement root, string propertyName, out T? payload)
    {
        payload = default;
        if (!root.TryGetProperty(propertyName, out var wrappedElement)
            || wrappedElement.ValueKind == JsonValueKind.Null
            || wrappedElement.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        return TryDeserializeElement(wrappedElement, out payload);
    }

    private static bool TryDeserializeElement<T>(JsonElement element, out T? payload)
    {
        payload = default;
        try
        {
            payload = element.Deserialize<T>(JsonOptions);
            return payload is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool LooksLikeHtml(string rawContent)
    {
        var trimmed = rawContent.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<DownloadedAdminFile> ReadFileAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var headers = response.Headers.ToDictionary(
            item => item.Key,
            item => string.Join(", ", item.Value),
            StringComparer.OrdinalIgnoreCase);
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "download.bin";

        return new DownloadedAdminFile(
            fileName.Trim('"'),
            response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            payload,
            headers);
    }

    private static string BuildStatisticsRoute(StatisticsQueryDto query)
        => BuildStatisticsRoute(query, ApiRoutes.Statistics);

    private static string BuildStatisticsRoute(StatisticsQueryDto query, string baseRoute)
    {
        var segments = new List<string>();

        AddQuerySegment("from", query.From?.ToString("O", CultureInfo.InvariantCulture));
        AddQuerySegment("to", query.To?.ToString("O", CultureInfo.InvariantCulture));
        AddQuerySegment("timezone", string.IsNullOrWhiteSpace(query.Timezone) ? null : query.Timezone.Trim());
        AddQuerySegment("tourId", query.TourId is > 0 ? query.TourId.Value.ToString(CultureInfo.InvariantCulture) : null);
        AddQuerySegment("ward", string.IsNullOrWhiteSpace(query.Ward) ? null : query.Ward);
        AddQuerySegment("search", string.IsNullOrWhiteSpace(query.Search) ? null : query.Search);
        AddQuerySegment("includeSynthetic", query.IncludeSynthetic ? "true" : null);

        return segments.Count == 0
            ? baseRoute
            : $"{baseRoute}?{string.Join("&", segments)}";

        void AddQuerySegment(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            segments.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }
    }

    private static string BuildChangeRequestRoute(ChangeRequestQueryDto query, bool ownerOnly)
    {
        var segments = new List<string>();
        AddQuerySegment("page", query.Page.ToString(CultureInfo.InvariantCulture));
        AddQuerySegment("pageSize", query.PageSize.ToString(CultureInfo.InvariantCulture));
        AddQuerySegment("type", query.Type);
        AddQuerySegment("action", query.Action);
        AddQuerySegment("status", query.Status);
        AddQuerySegment("search", query.Search);

        var baseRoute = ownerOnly ? $"{ApiRoutes.ChangeRequests}/mine" : ApiRoutes.ChangeRequests;
        return segments.Count == 0 ? baseRoute : $"{baseRoute}?{string.Join("&", segments)}";

        void AddQuerySegment(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            segments.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }
    }

    private static string BuildInboxRoute(InboxQueryDto query)
    {
        var segments = new List<string>
        {
            $"page={query.Page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={query.PageSize.ToString(CultureInfo.InvariantCulture)}"
        };

        if (query.UnreadOnly)
        {
            segments.Add("unreadOnly=true");
        }

        return $"{ApiRoutes.Inbox}?{string.Join("&", segments)}";
    }

    private static string BuildActivityLogRoute(ActivityLogQueryDto query)
    {
        var segments = new List<string>
        {
            $"page={Math.Max(1, query.Page).ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={Math.Clamp(query.PageSize, 1, 50).ToString(CultureInfo.InvariantCulture)}"
        };

        AddQuerySegment("search", query.Search);
        AddQuerySegment("action", query.Action);
        AddQuerySegment("entity", query.Entity);

        return $"{ApiRoutes.ActivityLogs}?{string.Join("&", segments)}";

        void AddQuerySegment(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            segments.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }
    }

    public async Task<UsageStatisticsDto> GetUsageAnalyticsStatisticsV1Async(CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        using var response = await httpClient.GetAsync(ApiRoutes.AdminAnalyticsStatisticsV1, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load usage analytics statistics.");
        return await ReadJsonAsync<UsageStatisticsDto>(response, "Unable to load usage analytics statistics.");
    }

    public async Task<UsageEventHistoryPageDto> GetUsageAnalyticsHistoryPageV1Async(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var safePageNumber = Math.Max(1, pageNumber);
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var route = $"{ApiRoutes.AdminAnalyticsHistoryV1}?pageNumber={safePageNumber.ToString(CultureInfo.InvariantCulture)}&pageSize={safePageSize.ToString(CultureInfo.InvariantCulture)}";

        using var response = await httpClient.GetAsync(route, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load usage analytics history.");
        return await ReadJsonAsync<UsageEventHistoryPageDto>(response, "Unable to load usage analytics history.");
    }

    private async Task<(IReadOnlyList<UsageEvent> Items, int TotalCount)> GetUsageAnalyticsHistorySnapshotV1Async(
        CancellationToken cancellationToken = default)
    {
        const int maxItems = 800;
        const int pageSize = 100;

        var collected = new List<UsageEvent>(maxItems);
        var pageNumber = 1;
        var totalCount = 0;

        while (collected.Count < maxItems)
        {
            var page = await GetUsageAnalyticsHistoryPageV1Async(pageNumber, pageSize, cancellationToken);
            totalCount = page.TotalCount;
            if (page.Items.Count == 0)
            {
                break;
            }

            collected.AddRange(page.Items);

            if (collected.Count >= totalCount || page.Items.Count < pageSize)
            {
                break;
            }

            pageNumber++;
        }

        if (collected.Count > maxItems)
        {
            collected = collected.Take(maxItems).ToList();
        }

        return (collected, totalCount);
    }

    private async Task<UsageHistoryOverviewDto> GetLegacyUsageHistoryAsync(bool includeSynthetic, CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var route = includeSynthetic
            ? $"{ApiRoutes.UsageHistory}?includeSynthetic=true"
            : ApiRoutes.UsageHistory;
        using var response = await httpClient.GetAsync(route, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load usage history.");
        return await ReadJsonAsync<UsageHistoryOverviewDto>(response, "Unable to load usage history.");
    }

    private async Task<StatisticsOverviewDto> GetLegacyStatisticsAsync(
        StatisticsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();

        var route = BuildStatisticsRoute(query);
        using var response = await httpClient.GetAsync(route, cancellationToken);
        await EnsureSuccessAsync(response, "Unable to load statistics.");
        return await ReadJsonAsync<StatisticsOverviewDto>(response, "Unable to load statistics.");
    }
//
    private static UsageHistoryOverviewDto MapUsageHistoryOverviewFromV1(
        IReadOnlyList<UsageEvent> items,
        int totalCount,
        UsageStatisticsDto summary)
    {
        var orderedItems = items
            .OrderByDescending(item => NormalizeUtc(item.Timestamp))
            .ToList();

        var mappedItems = orderedItems
            .Select((item, index) =>
            {
                var eventAt = NormalizeUtc(item.Timestamp);
                var locationLabel = BuildReferenceLabel(item.ReferenceId);
                var locationId = TryParsePositiveInt(item.ReferenceId);

                return new UsageHistoryItemDto
                {
                    Id = index + 1,
                    LocationId = locationId,
                    LocationName = locationLabel,
                    AudioTitle = item.EventType == UsageEventType.PlayAudio
                        ? $"Audio event ({locationLabel})"
                        : null,
                    TriggerSource = "MobileApp",
                    EventType = item.EventType.ToString(),
                    EventAt = eventAt,
                    TimeAgo = BuildRelativeTime(eventAt),
                    DeviceId = item.DeviceId,
                    SessionId = null,
                    ListeningSeconds = item.EventType == UsageEventType.PlayAudio && item.DurationSeconds > 0
                        ? item.DurationSeconds
                        : null,
                    QueuePosition = null,
                    BatteryPercent = null,
                    NetworkType = null,
                    TourNames = []
                };
            })
            .ToList();

        var listeningSamples = mappedItems
            .Where(item => item.ListeningSeconds.HasValue)
            .Select(item => item.ListeningSeconds!.Value)
            .ToList();

        var fallbackUniqueGuests = mappedItems
            .Select(item => item.DeviceId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var distinctLocations = mappedItems
            .Select(item => item.LocationName)
            .Where(item => !string.IsNullOrWhiteSpace(item) &&
                           !string.Equals(item, "N/A", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new UsageHistoryOverviewDto
        {
            TotalEvents = totalCount > 0 ? totalCount : mappedItems.Count,
            UniqueGuests = summary.UniqueUsers > 0 ? summary.UniqueUsers : fallbackUniqueGuests,
            OnlineGuests = summary.OnlineUsers,
            DistinctLocations = distinctLocations,
            AverageListeningSeconds = listeningSamples.Count == 0
                ? 0d
                : Math.Round(listeningSamples.Average(), 1, MidpointRounding.AwayFromZero),
            Items = mappedItems
        };
    }

    private static StatisticsOverviewDto MapStatisticsOverviewFromV1(
        UsageStatisticsDto summary,
        IReadOnlyList<UsageEvent> allEvents,
        StatisticsQueryDto query)
    {
        var filteredEvents = ApplyUsageEventFilter(allEvents, query);
        var topPoiRows = BuildTopPoiRows(summary, query.Search);
        var averageListeningRows = BuildAverageListeningRows(filteredEvents);
        var playbackTimeline = BuildPlaybackTimeline(filteredEvents, query);

        var listeningSamples = filteredEvents
            .Where(item => item.EventType == UsageEventType.PlayAudio && item.DurationSeconds > 0)
            .Select(item => item.DurationSeconds)
            .ToList();

        var uniqueGuests = filteredEvents
            .Select(item => item.DeviceId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new StatisticsOverviewDto
        {
            AppliedFilters = query,
            IsOwnerScoped = false,
            ScopeLabel = "Showing analytics captured from mobile usage events.",
            Summary = new StatisticsSummaryDto
            {
                TotalPlaybackEvents = filteredEvents.Count(item => item.EventType == UsageEventType.PlayAudio),
                TotalTrackingPoints = filteredEvents.Count(item => item.EventType == UsageEventType.ViewMap),
                RouteSessions = 0,
                UniqueGuests = uniqueGuests > 0 ? uniqueGuests : summary.UniqueUsers,
                OnlineGuests = summary.OnlineUsers,
                VisiblePois = topPoiRows.Count,
                AverageListeningSeconds = listeningSamples.Count == 0
                    ? 0d
                    : Math.Round(listeningSamples.Average(), 1, MidpointRounding.AwayFromZero)
            },
            TourOptions = [],
            WardOptions = [],
            PlaybackTimeline = playbackTimeline,
            PlaysByWard = [],
            PlaysByTour = [],
            Locations = [],
            HeatmapPoints = [],
            RouteHistory = [],
            TopPoisByPlayCount = topPoiRows,
            AverageListeningByPoi = averageListeningRows
        };
    }

    private static IReadOnlyList<UsageEvent> ApplyUsageEventFilter(
        IReadOnlyList<UsageEvent> items,
        StatisticsQueryDto query)
    {
        var fromUtc = query.From.HasValue
            ? NormalizeUtc(query.From.Value)
            : (DateTime?)null;
        var toUtc = query.To.HasValue
            ? NormalizeUtc(query.To.Value)
            : (DateTime?)null;

        var filtered = items.AsEnumerable();

        if (fromUtc.HasValue)
        {
            filtered = filtered.Where(item => NormalizeUtc(item.Timestamp) >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            filtered = filtered.Where(item => NormalizeUtc(item.Timestamp) <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(item =>
                Contains(item.ReferenceId, search) ||
                Contains(item.Details, search) ||
                Contains(item.EventType.ToString(), search));
        }

        return filtered.ToList();
    }

    private static IReadOnlyList<StatisticsPoiReportRowDto> BuildTopPoiRows(
        UsageStatisticsDto summary,
        string? search)
    {
        IEnumerable<TopPoiInteractionDto> source = summary.TopPoiInteractions;
        if (!string.IsNullOrWhiteSpace(search))
        {
            source = source.Where(item => Contains(item.ReferenceId, search));
        }

        return source
            .Select(item =>
            {
                var locationId = TryParsePositiveInt(item.ReferenceId);
                var label = BuildReferenceLabel(item.ReferenceId);
                return new StatisticsPoiReportRowDto
                {
                    LocationId = locationId ?? 0,
                    LocationName = label,
                    Ward = "N/A",
                    PlayCount = item.PlayAudioCount,
                    AverageListeningSeconds = 0,
                    ListeningSamples = 0,
                    TopAudioTitle = null,
                    UniqueGuests = 0,
                    TourNames = []
                };
            })
            .OrderByDescending(item => item.PlayCount)
            .ThenBy(item => item.LocationName, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<StatisticsPoiReportRowDto> BuildAverageListeningRows(IReadOnlyList<UsageEvent> items) =>
        items
            .Where(item => item.EventType == UsageEventType.PlayAudio
                           && item.DurationSeconds > 0
                           && !string.IsNullOrWhiteSpace(item.ReferenceId))
            .GroupBy(item => item.ReferenceId!, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var locationId = TryParsePositiveInt(group.Key);
                return new StatisticsPoiReportRowDto
                {
                    LocationId = locationId ?? 0,
                    LocationName = BuildReferenceLabel(group.Key),
                    Ward = "N/A",
                    PlayCount = group.Count(),
                    AverageListeningSeconds = Math.Round(group.Average(item => item.DurationSeconds), 1, MidpointRounding.AwayFromZero),
                    ListeningSamples = group.Count(),
                    TopAudioTitle = null,
                    UniqueGuests = group
                        .Select(item => item.DeviceId)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    TourNames = []
                };
            })
            .OrderByDescending(item => item.AverageListeningSeconds)
            .ThenBy(item => item.LocationName, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

    private static IReadOnlyList<StatisticsChartPointDto> BuildPlaybackTimeline(
        IReadOnlyList<UsageEvent> items,
        StatisticsQueryDto query)
    {
        var startDate = (query.From.HasValue ? NormalizeUtc(query.From.Value) : DateTime.UtcNow.AddDays(-6)).Date;
        var endDate = (query.To.HasValue ? NormalizeUtc(query.To.Value) : DateTime.UtcNow).Date;

        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var playCountByDate = items
            .Where(item => item.EventType == UsageEventType.PlayAudio)
            .GroupBy(item => NormalizeUtc(item.Timestamp).Date)
            .ToDictionary(group => group.Key, group => group.Count());

        var result = new List<StatisticsChartPointDto>();
        for (var cursor = startDate; cursor <= endDate; cursor = cursor.AddDays(1))
        {
            playCountByDate.TryGetValue(cursor, out var count);
            result.Add(new StatisticsChartPointDto
            {
                Label = cursor.ToString("dd MMM", CultureInfo.InvariantCulture),
                Value = count,
                Hint = $"{count} plays on {cursor:dd MMM yyyy} (UTC)"
            });
        }

        return result;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }

    private static int? TryParsePositiveInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;

    private static string BuildReferenceLabel(string? referenceId) =>
        string.IsNullOrWhiteSpace(referenceId)
            ? "N/A"
            : referenceId.Trim();

    private static bool Contains(string? value, string? keyword) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.IsNullOrWhiteSpace(keyword) &&
        value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string BuildRelativeTime(DateTime eventAtUtc)
    {
        var elapsed = DateTime.UtcNow - eventAtUtc;
        if (elapsed <= TimeSpan.Zero)
        {
            return "just now";
        }

        if (elapsed.TotalSeconds < 60)
        {
            return $"{(int)elapsed.TotalSeconds} sec ago";
        }

        if (elapsed.TotalMinutes < 60)
        {
            return $"{(int)elapsed.TotalMinutes} min ago";
        }

        if (elapsed.TotalHours < 24)
        {
            return $"{(int)elapsed.TotalHours}h ago";
        }

        return $"{(int)elapsed.TotalDays}d ago";
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

public sealed record AudioTtsPreviewResult(
    MemoryStream Stream,
    string ContentType,
    string Provider,
    string VoiceName);
