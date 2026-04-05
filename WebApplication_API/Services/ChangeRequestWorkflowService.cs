using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Storage;
using WebApplication_API.Data;
using WebApplication_API.Model;

namespace WebApplication_API.Services;

public sealed partial class ChangeRequestWorkflowService(
    DBContext context,
    SharedImageFileStorageService imageStorage,
    SharedAudioFileStorageService audioStorage,
    ActivityLogService activityLogService)
{
    private const string LocationTarget = "Location";
    private const string AudioTarget = "Audio";
    private const string PendingStatus = "Pending";
    private const string ApprovedStatus = "Approved";
    private const string RejectedStatus = "Rejected";
    private const string CreateAction = "CREATE";
    private const string UpdateAction = "UPDATE";
    private const string DeleteAction = "DELETE";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ChangeRequestListDto> GetRequestsAsync(
        ChangeRequestQueryDto query,
        DashboardUser currentUser,
        bool ownerOnly,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var normalizedType = NormalizeTypeFilter(query.Type);
        var normalizedAction = NormalizeActionFilter(query.Action);
        var normalizedStatus = NormalizeStatus(query.Status);
        var normalizedSearch = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var items = await context.ChangeRequests
            .AsNoTracking()
            .Include(item => item.Owner)
            .Where(item => !ownerOnly || item.OwnerId == currentUser.UserId)
            .ToListAsync(cancellationToken);

        var liveLocationIds = items
            .Where(item => string.Equals(item.TargetTable, LocationTarget, StringComparison.OrdinalIgnoreCase)
                           && item.TargetId is > 0)
            .Select(item => item.TargetId!.Value)
            .Distinct()
            .ToList();

        var liveAudioIds = items
            .Where(item => string.Equals(item.TargetTable, AudioTarget, StringComparison.OrdinalIgnoreCase)
                           && item.TargetId is > 0)
            .Select(item => item.TargetId!.Value)
            .Distinct()
            .ToList();

        var liveLocationsById = liveLocationIds.Count == 0
            ? new Dictionary<int, Location>()
            : await context.Locations
                .AsNoTracking()
                .Include(item => item.Category)
                .Include(item => item.Owner)
                .Include(item => item.Images)
                .Include(item => item.AudioContents)
                .AsSplitQuery()
                .Where(item => liveLocationIds.Contains(item.LocationId))
                .ToDictionaryAsync(item => item.LocationId, cancellationToken);

        var liveAudiosById = liveAudioIds.Count == 0
            ? new Dictionary<int, Audio>()
            : await context.AudioContents
                .AsNoTracking()
                .Include(item => item.Location)
                .ThenInclude(item => item!.Owner)
                .Where(item => liveAudioIds.Contains(item.AudioId))
                .ToDictionaryAsync(item => item.AudioId, cancellationToken);

        var languagesByCode = await context.Languages
            .AsNoTracking()
            .ToDictionaryAsync(item => item.LangCode, cancellationToken);

        var mapped = items
            .Select(item => MapToDto(item, liveLocationsById, liveAudiosById, languagesByCode))
            .Where(item => normalizedType is null || string.Equals(item.Type, normalizedType, StringComparison.OrdinalIgnoreCase))
            .Where(item => normalizedAction is null || string.Equals(item.ActionType, normalizedAction, StringComparison.OrdinalIgnoreCase))
            .Where(item => normalizedStatus is null || string.Equals(item.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
            .Where(item =>
                normalizedSearch is null
                || item.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.OwnerName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.Type.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || (item.Location?.Category?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Audio?.LocationName?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(item => item.UpdatedAt ?? item.SubmittedAt)
            .ToList();

        var totalCount = mapped.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Min(page, totalPages);

        return new ChangeRequestListDto
        {
            Items = mapped
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            Page = currentPage,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<ChangeRequestDto> SubmitLocationAsync(
        DashboardUser owner,
        LocationChangeRequestSubmission request,
        IFormFile? preferenceImageFile,
        IEnumerable<IFormFile>? imageFiles,
        CancellationToken cancellationToken = default)
    {
        var actionType = NormalizeRequestType(request.RequestType);
        var targetId = actionType == CreateAction ? null : request.TargetId;
        Location? liveLocation = null;

        if (actionType != CreateAction)
        {
            if (targetId is null or <= 0)
            {
                throw new InvalidOperationException("A live POI target is required for update or delete requests.");
            }

            liveLocation = await context.Locations
                .Include(item => item.Category)
                .Include(item => item.Owner)
                .Include(item => item.Images)
                .Include(item => item.AudioContents)
                .AsSplitQuery()
                .FirstOrDefaultAsync(item => item.LocationId == targetId.Value, cancellationToken);

            if (liveLocation is null)
            {
                throw new InvalidOperationException("The requested POI no longer exists.");
            }

            if (liveLocation.OwnerId != owner.UserId)
            {
                throw new InvalidOperationException("You can only submit requests for your own POIs.");
            }
        }

        PendingLocationChangeData payload;
        if (actionType == DeleteAction)
        {
            payload = BuildLocationPayload(liveLocation!);
        }
        else
        {
            var category = await context.Categories
                .FirstOrDefaultAsync(item => item.CategoryId == request.CategoryId, cancellationToken);
            if (category is null)
            {
                throw new InvalidOperationException("Category not found.");
            }

            if (category.Status != 1)
            {
                throw new InvalidOperationException("Inactive categories cannot be assigned to POIs.");
            }

            var currentPreferenceImageUrl = liveLocation is null
                ? null
                : ResolvePreferenceImagePath(liveLocation);
            var currentImageSet = liveLocation?.Images
                .Select(item => NormalizeImagePath(item.ImageUrl))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

            var retainedPreferenceImageUrl = actionType == UpdateAction
                ? NormalizeImagePath(request.RetainedPreferenceImageUrl)
                : null;

            var retainedImages = actionType == UpdateAction
                ? request.RetainedImageUrls
                    .Select(NormalizeImagePath)
                    .Where(item => !string.IsNullOrWhiteSpace(item) && currentImageSet.Contains(item))
                    .Cast<string>()
                    .ToList()
                : [];

            if (!string.IsNullOrWhiteSpace(currentPreferenceImageUrl)
                && string.IsNullOrWhiteSpace(retainedPreferenceImageUrl)
                && retainedImages.RemoveAll(item => string.Equals(item, currentPreferenceImageUrl, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                retainedPreferenceImageUrl = currentPreferenceImageUrl;
            }

            if (!string.IsNullOrWhiteSpace(retainedPreferenceImageUrl)
                && !string.Equals(retainedPreferenceImageUrl, currentPreferenceImageUrl, StringComparison.OrdinalIgnoreCase))
            {
                retainedPreferenceImageUrl = null;
            }

            var uploadedPreferenceImageUrl = await SavePendingPreferenceImageAsync(
                preferenceImageFile,
                request.Name,
                cancellationToken);
            var preferenceImageUrl = uploadedPreferenceImageUrl ?? retainedPreferenceImageUrl;
            if (string.IsNullOrWhiteSpace(preferenceImageUrl))
            {
                throw new InvalidOperationException("Upload a preference image before submitting this POI request.");
            }

            var uploadedImageUrls = await SavePendingImagesAsync(
                imageFiles,
                request.Name,
                retainedImages.Count + 2,
                cancellationToken);

            payload = new PendingLocationChangeData
            {
                CategoryId = category.CategoryId,
                CategoryName = category.Name,
                Name = request.Name.Trim(),
                Description = Normalize(request.Description),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Radius = request.Radius,
                StandbyRadius = request.StandbyRadius,
                Priority = request.Priority,
                DebounceSeconds = request.DebounceSeconds,
                IsGpsTriggerEnabled = request.IsGpsTriggerEnabled,
                Address = Normalize(request.Address),
                WebURL = Normalize(request.WebURL),
                Email = Normalize(request.Email),
                Phone = Normalize(request.Phone),
                EstablishedYear = request.EstablishedYear,
                Status = request.Status,
                OwnerId = owner.UserId,
                OwnerName = owner.FullName ?? owner.Username,
                PreferenceImageUrl = preferenceImageUrl,
                ImageUrls = BuildDesiredImageUrls(preferenceImageUrl, retainedImages, uploadedImageUrls).ToList(),
                AudioCount = liveLocation?.AudioContents.Count(item => item.Status == 1) ?? 0,
                AvailableVoiceGenders = liveLocation?.AudioContents
                    .Where(item => item.Status == 1 && !string.IsNullOrWhiteSpace(item.VoiceGender))
                    .Select(item => item.VoiceGender!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? []
            };
        }

        var changeRequest = await UpsertPendingRequestAsync(
            owner,
            LocationTarget,
            targetId,
            actionType,
            request.Reason,
            JsonSerializer.Serialize(payload, JsonOptions),
            cancellationToken);
        await activityLogService.LogAsync(
            owner,
            ToActionLabel(actionType),
            "POI Request",
            changeRequest.RequestId,
            payload.Name,
            $"Submitted {ToActionLabel(actionType).ToLowerInvariant()} request for POI '{payload.Name}'.",
            cancellationToken);

        return MapToDto(changeRequest);
    }

    public async Task<ChangeRequestDto> SubmitAudioAsync(
        DashboardUser owner,
        AudioChangeRequestSubmission request,
        IFormFile? audioFile,
        CancellationToken cancellationToken = default)
    {
        var actionType = NormalizeRequestType(request.RequestType);
        var targetId = actionType == CreateAction ? null : request.TargetId;
        Audio? liveAudio = null;

        if (actionType != CreateAction)
        {
            if (targetId is null or <= 0)
            {
                throw new InvalidOperationException("A live audio target is required for update or delete requests.");
            }

            liveAudio = await context.AudioContents
                .Include(item => item.Location)
                .ThenInclude(item => item!.Owner)
                .FirstOrDefaultAsync(item => item.AudioId == targetId.Value, cancellationToken);

            if (liveAudio is null)
            {
                throw new InvalidOperationException("The requested audio item no longer exists.");
            }

            if (liveAudio.Location?.OwnerId != owner.UserId)
            {
                throw new InvalidOperationException("You can only submit requests for audio linked to your own POIs.");
            }
        }

        PendingAudioChangeData payload;
        if (actionType == DeleteAction)
        {
            payload = await BuildAudioPayloadAsync(liveAudio!, cancellationToken);
        }
        else
        {
            var location = await context.Locations
                .Include(item => item.Owner)
                .FirstOrDefaultAsync(item => item.LocationId == request.LocationId, cancellationToken);
            if (location is null)
            {
                throw new InvalidOperationException("Location not found.");
            }

            if (location.Status != 1)
            {
                throw new InvalidOperationException("Inactive POIs cannot be assigned to audio.");
            }

            if (location.OwnerId != owner.UserId)
            {
                throw new InvalidOperationException("You can only submit audio requests for your own POIs.");
            }

            var language = await context.Languages
                .FirstOrDefaultAsync(item => item.LangCode == request.Language.Trim(), cancellationToken);
            if (language is null)
            {
                throw new InvalidOperationException("Language not found.");
            }

            if (language.Status != 1)
            {
                throw new InvalidOperationException("Inactive languages cannot be assigned to audio.");
            }

            var nextAudioPath = NormalizeAudioPath(request.AudioURL);
            if (audioFile is not null)
            {
                nextAudioPath = await audioStorage.SaveAudioAsync(audioFile, location.Name, request.Title, cancellationToken);
            }
            else if (liveAudio is not null && string.Equals(nextAudioPath, NormalizeAudioPath(liveAudio.FilePath), StringComparison.OrdinalIgnoreCase))
            {
                nextAudioPath = NormalizeAudioPath(liveAudio.FilePath);
            }

            var validationMessage = ValidateAudioPayload(request.SourceType, request.Script, nextAudioPath);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            payload = new PendingAudioChangeData
            {
                LocationId = location.LocationId,
                LocationName = location.Name,
                OwnerId = owner.UserId,
                OwnerName = owner.FullName ?? owner.Username,
                Language = language.LangCode,
                LanguageName = language.LangName,
                NativeLanguageName = language.NativeName,
                PreferNativeVoice = language.PreferNativeVoice,
                Title = request.Title.Trim(),
                Description = Normalize(request.Description),
                SourceType = request.SourceType.Trim(),
                Script = Normalize(request.Script),
                AudioURL = nextAudioPath,
                FileSizeBytes = audioFile is null ? request.FileSizeBytes : (int?)audioFile.Length,
                Duration = request.Duration,
                VoiceName = Normalize(request.VoiceName),
                VoiceGender = Normalize(request.VoiceGender),
                Priority = request.Priority,
                PlaybackMode = request.PlaybackMode.Trim(),
                InterruptPolicy = request.InterruptPolicy.Trim(),
                IsDownloadable = request.IsDownloadable,
                Status = request.Status
            };
        }

        var changeRequest = await UpsertPendingRequestAsync(
            owner,
            AudioTarget,
            targetId,
            actionType,
            request.Reason,
            JsonSerializer.Serialize(payload, JsonOptions),
            cancellationToken);
        await activityLogService.LogAsync(
            owner,
            ToActionLabel(actionType),
            "Audio Request",
            changeRequest.RequestId,
            payload.Title,
            $"Submitted {ToActionLabel(actionType).ToLowerInvariant()} request for audio '{payload.Title}'.",
            cancellationToken);

        return MapToDto(changeRequest);
    }

    public async Task<ChangeRequestDto> ApproveAsync(
        int requestId,
        DashboardUser reviewer,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        var changeRequest = await context.ChangeRequests
            .Include(item => item.Owner)
            .FirstOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);

        if (changeRequest is null)
        {
            throw new InvalidOperationException("Change request not found.");
        }

        EnsurePending(changeRequest);

        var orphanedPaths = changeRequest.TargetTable switch
        {
            LocationTarget => await ApplyLocationApprovalAsync(changeRequest, cancellationToken),
            AudioTarget => await ApplyAudioApprovalAsync(changeRequest, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported request target.")
        };

        changeRequest.Status = ApprovedStatus;
        changeRequest.AdminNote = Normalize(adminNote);
        changeRequest.UpdatedAt = DateTime.UtcNow;

        context.InboxMessages.Add(CreateInboxMessage(
            changeRequest.OwnerId,
            changeRequest.RequestId,
            $"{GetFriendlyType(changeRequest.TargetTable)} request approved",
            BuildApprovalMessage(changeRequest, reviewer, adminNote),
            "Approval"));
        context.InboxMessages.Add(CreateInboxMessage(
            reviewer.UserId,
            changeRequest.RequestId,
            $"You approved {GetFriendlyType(changeRequest.TargetTable)} request #{changeRequest.RequestId}",
            BuildReviewerApprovalMessage(changeRequest, adminNote),
            "Approval"));

        await context.SaveChangesAsync(cancellationToken);
        DeleteManagedFiles(orphanedPaths);
        var approvedRequestName = MapToDto(changeRequest).Name;
        await activityLogService.LogAsync(
            reviewer,
            "Approve",
            $"{GetFriendlyType(changeRequest.TargetTable)} Request",
            changeRequest.RequestId,
            approvedRequestName,
            $"Approved {GetFriendlyType(changeRequest.TargetTable).ToLowerInvariant()} request #{changeRequest.RequestId}.",
            cancellationToken);
        return MapToDto(changeRequest);
    }

    public async Task<ChangeRequestDto> RejectAsync(
        int requestId,
        DashboardUser reviewer,
        string adminNote,
        CancellationToken cancellationToken = default)
    {
        var changeRequest = await context.ChangeRequests
            .Include(item => item.Owner)
            .FirstOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);

        if (changeRequest is null)
        {
            throw new InvalidOperationException("Change request not found.");
        }

        EnsurePending(changeRequest);

        var orphanedPaths = await GetOrphanedPathsRelativeToLiveDataAsync(changeRequest, cancellationToken);

        changeRequest.Status = RejectedStatus;
        changeRequest.AdminNote = adminNote.Trim();
        changeRequest.UpdatedAt = DateTime.UtcNow;

        context.InboxMessages.Add(CreateInboxMessage(
            changeRequest.OwnerId,
            changeRequest.RequestId,
            $"{GetFriendlyType(changeRequest.TargetTable)} request rejected",
            BuildRejectionMessage(changeRequest, reviewer, adminNote),
            "Rejection"));
        context.InboxMessages.Add(CreateInboxMessage(
            reviewer.UserId,
            changeRequest.RequestId,
            $"You rejected {GetFriendlyType(changeRequest.TargetTable)} request #{changeRequest.RequestId}",
            BuildReviewerRejectionMessage(changeRequest, adminNote),
            "Rejection"));

        await context.SaveChangesAsync(cancellationToken);
        DeleteManagedFiles(orphanedPaths);
        var rejectedRequestName = MapToDto(changeRequest).Name;
        await activityLogService.LogAsync(
            reviewer,
            "Reject",
            $"{GetFriendlyType(changeRequest.TargetTable)} Request",
            changeRequest.RequestId,
            rejectedRequestName,
            $"Rejected {GetFriendlyType(changeRequest.TargetTable).ToLowerInvariant()} request #{changeRequest.RequestId}.",
            cancellationToken);
        return MapToDto(changeRequest);
    }

    public async Task<InboxOverviewDto> GetInboxAsync(
        DashboardUser user,
        InboxQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var itemsQuery = context.InboxMessages
            .AsNoTracking()
            .Where(item => item.UserId == user.UserId);

        if (query.UnreadOnly)
        {
            itemsQuery = itemsQuery.Where(item => !item.IsRead);
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Min(page, totalPages);
        var unreadCount = await context.InboxMessages
            .AsNoTracking()
            .CountAsync(item => item.UserId == user.UserId && !item.IsRead, cancellationToken);

        var items = await itemsQuery
            .OrderBy(item => item.IsRead)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new InboxMessageDto
            {
                Id = item.MessageId,
                Title = item.Title,
                Body = item.Body,
                MessageType = item.MessageType,
                RelatedRequestId = item.RelatedRequestId,
                IsRead = item.IsRead,
                CreatedAt = item.CreatedAt,
                ReadAt = item.ReadAt
            })
            .ToListAsync(cancellationToken);

        return new InboxOverviewDto
        {
            Items = items,
            Page = currentPage,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            UnreadCount = unreadCount
        };
    }

    public async Task MarkInboxReadAsync(int messageId, DashboardUser user, CancellationToken cancellationToken = default)
    {
        var message = await context.InboxMessages
            .FirstOrDefaultAsync(item => item.MessageId == messageId && item.UserId == user.UserId, cancellationToken);

        if (message is null)
        {
            throw new InvalidOperationException("Inbox message not found.");
        }

        if (message.IsRead)
        {
            return;
        }

        message.IsRead = true;
        message.ReadAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task BroadcastAnnouncementAsync(
        DashboardUser sender,
        InboxAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        var body = request.Body.Trim();
        var senderName = sender.FullName ?? sender.Username;
        var timestamp = DateTime.UtcNow;

        var userIds = await context.DashboardUsers
            .AsNoTracking()
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
        {
            return;
        }

        foreach (var userId in userIds)
        {
            context.InboxMessages.Add(CreateInboxMessage(
                userId,
                relatedRequestId: null,
                title,
                $"{body}\n\nSent by {senderName}.",
                "Announcement",
                timestamp));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<ChangeRequest> UpsertPendingRequestAsync(
        DashboardUser owner,
        string targetTable,
        int? targetId,
        string requestType,
        string? reason,
        string newDataJson,
        CancellationToken cancellationToken)
    {
        var normalizedReason = Normalize(reason);
        ChangeRequest? existingPendingRequest = null;

        if (targetId is > 0)
        {
            existingPendingRequest = await context.ChangeRequests
                .FirstOrDefaultAsync(item =>
                    item.OwnerId == owner.UserId
                    && item.TargetTable == targetTable
                    && item.TargetId == targetId
                    && item.Status == PendingStatus,
                    cancellationToken);
        }

        var orphanedPaths = existingPendingRequest is null
            ? []
            : await GetReplacementOrphanedPathsAsync(existingPendingRequest, newDataJson, cancellationToken);

        if (existingPendingRequest is null)
        {
            existingPendingRequest = new ChangeRequest
            {
                OwnerId = owner.UserId,
                TargetTable = targetTable,
                TargetId = targetId,
                RequestType = requestType,
                NewDataJson = newDataJson,
                Reason = normalizedReason,
                Status = PendingStatus,
                CreatedAt = DateTime.UtcNow
            };

            context.ChangeRequests.Add(existingPendingRequest);
        }
        else
        {
            existingPendingRequest.RequestType = requestType;
            existingPendingRequest.NewDataJson = newDataJson;
            existingPendingRequest.Reason = normalizedReason;
            existingPendingRequest.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        DeleteManagedFiles(orphanedPaths);

        existingPendingRequest.Owner = owner;
        return existingPendingRequest;
    }

    private async Task<List<string>> ApplyLocationApprovalAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        var payload = DeserializeLocationPayload(request.NewDataJson);
        var category = await context.Categories.FirstOrDefaultAsync(item => item.CategoryId == payload.CategoryId, cancellationToken);
        if (category is null || category.Status != 1)
        {
            throw new InvalidOperationException("The requested category is no longer available.");
        }

        if (request.RequestType == CreateAction)
        {
            var location = new Location
            {
                CategoryId = payload.CategoryId,
                OwnerId = request.OwnerId,
                Name = payload.Name,
                Description = payload.Description,
                Latitude = payload.Latitude,
                Longitude = payload.Longitude,
                Radius = payload.Radius,
                StandbyRadius = payload.StandbyRadius,
                Priority = payload.Priority,
                DebounceSeconds = payload.DebounceSeconds,
                IsGpsTriggerEnabled = payload.IsGpsTriggerEnabled,
                Address = payload.Address,
                WebURL = payload.WebURL,
                Email = payload.Email,
                PhoneContact = payload.Phone,
                EstablishedYear = payload.EstablishedYear,
                Status = payload.Status,
                CreatedAt = DateTime.UtcNow
            };

            context.Locations.Add(location);
            await context.SaveChangesAsync(cancellationToken);
            await SyncLocationImagesAsync(location, payload.PreferenceImageUrl, payload.ImageUrls, cancellationToken);
            request.TargetId = location.LocationId;
            return [];
        }

        var liveLocation = await context.Locations
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.LocationId == request.TargetId, cancellationToken);

        if (liveLocation is null)
        {
            throw new InvalidOperationException("The target POI no longer exists.");
        }

        if (liveLocation.OwnerId != request.OwnerId)
        {
            throw new InvalidOperationException("The POI is no longer owned by the submitting account.");
        }

        if (request.RequestType == DeleteAction)
        {
            liveLocation.Status = 0;
            liveLocation.UpdatedAt = DateTime.UtcNow;
            return [];
        }

        liveLocation.CategoryId = payload.CategoryId;
        liveLocation.Name = payload.Name;
        liveLocation.Description = payload.Description;
        liveLocation.Latitude = payload.Latitude;
        liveLocation.Longitude = payload.Longitude;
        liveLocation.Radius = payload.Radius;
        liveLocation.StandbyRadius = payload.StandbyRadius;
        liveLocation.Priority = payload.Priority;
        liveLocation.DebounceSeconds = payload.DebounceSeconds;
        liveLocation.IsGpsTriggerEnabled = payload.IsGpsTriggerEnabled;
        liveLocation.Address = payload.Address;
        liveLocation.WebURL = payload.WebURL;
        liveLocation.Email = payload.Email;
        liveLocation.PhoneContact = payload.Phone;
        liveLocation.EstablishedYear = payload.EstablishedYear;
        liveLocation.Status = payload.Status;
        liveLocation.UpdatedAt = DateTime.UtcNow;

        return await SyncLocationImagesAsync(liveLocation, payload.PreferenceImageUrl, payload.ImageUrls, cancellationToken);
    }

    private async Task<List<string>> ApplyAudioApprovalAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        var payload = DeserializeAudioPayload(request.NewDataJson);
        var location = await context.Locations
            .Include(item => item.Owner)
            .FirstOrDefaultAsync(item => item.LocationId == payload.LocationId, cancellationToken);

        if (location is null || location.Status != 1)
        {
            throw new InvalidOperationException("The requested POI is no longer available.");
        }

        if (location.OwnerId != request.OwnerId)
        {
            throw new InvalidOperationException("The POI is no longer owned by the submitting account.");
        }

        var language = await context.Languages
            .FirstOrDefaultAsync(item => item.LangCode == payload.Language, cancellationToken);
        if (language is null || language.Status != 1)
        {
            throw new InvalidOperationException("The requested language is no longer available.");
        }

        if (request.RequestType == CreateAction)
        {
            var audio = new Audio
            {
                LocationId = payload.LocationId,
                LanguageCode = payload.Language,
                Title = payload.Title,
                Description = payload.Description,
                SourceType = payload.SourceType,
                Script = payload.Script,
                FilePath = payload.AudioURL,
                FileSizeBytes = payload.FileSizeBytes,
                DurationSeconds = payload.Duration,
                VoiceName = payload.VoiceName,
                VoiceGender = payload.VoiceGender,
                Priority = payload.Priority,
                PlaybackMode = payload.PlaybackMode,
                InterruptPolicy = payload.InterruptPolicy,
                IsDownloadable = payload.IsDownloadable,
                Status = payload.Status,
                CreatedAt = DateTime.UtcNow
            };

            context.AudioContents.Add(audio);
            await context.SaveChangesAsync(cancellationToken);
            request.TargetId = audio.AudioId;
            return [];
        }

        var liveAudio = await context.AudioContents
            .Include(item => item.Location)
            .FirstOrDefaultAsync(item => item.AudioId == request.TargetId, cancellationToken);

        if (liveAudio is null)
        {
            throw new InvalidOperationException("The target audio item no longer exists.");
        }

        if (liveAudio.Location?.OwnerId != request.OwnerId)
        {
            throw new InvalidOperationException("The audio item is no longer owned by the submitting account.");
        }

        if (request.RequestType == DeleteAction)
        {
            liveAudio.Status = 0;
            liveAudio.UpdatedAt = DateTime.UtcNow;
            return [];
        }

        var previousAudioPath = NormalizeAudioPath(liveAudio.FilePath);

        liveAudio.LocationId = payload.LocationId;
        liveAudio.LanguageCode = payload.Language;
        liveAudio.Title = payload.Title;
        liveAudio.Description = payload.Description;
        liveAudio.SourceType = payload.SourceType;
        liveAudio.Script = payload.Script;
        liveAudio.FilePath = payload.AudioURL;
        liveAudio.FileSizeBytes = payload.FileSizeBytes;
        liveAudio.DurationSeconds = payload.Duration;
        liveAudio.VoiceName = payload.VoiceName;
        liveAudio.VoiceGender = payload.VoiceGender;
        liveAudio.Priority = payload.Priority;
        liveAudio.PlaybackMode = payload.PlaybackMode;
        liveAudio.InterruptPolicy = payload.InterruptPolicy;
        liveAudio.IsDownloadable = payload.IsDownloadable;
        liveAudio.Status = payload.Status;
        liveAudio.UpdatedAt = DateTime.UtcNow;

        var nextAudioPath = NormalizeAudioPath(payload.AudioURL);
        return previousAudioPath is not null
               && !string.Equals(previousAudioPath, nextAudioPath, StringComparison.OrdinalIgnoreCase)
               && SharedStoragePaths.TryGetManagedAudioFileName(previousAudioPath) is not null
            ? [previousAudioPath]
            : [];
    }

    private async Task<List<string>> SyncLocationImagesAsync(
        Location location,
        string? preferenceImageUrl,
        IReadOnlyList<string> desiredImageUrls,
        CancellationToken cancellationToken)
    {
        var normalizedPreferenceImageUrl = NormalizeImagePath(preferenceImageUrl)
            ?? throw new InvalidOperationException("A valid preference image is required.");

        var normalizedDesired = desiredImageUrls
            .Select(NormalizeImagePath)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item => !string.Equals(item, normalizedPreferenceImageUrl, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        normalizedDesired.Insert(0, normalizedPreferenceImageUrl);

        await context.Entry(location)
            .Collection(item => item.Images)
            .LoadAsync(cancellationToken);

        var existingImages = location.Images.ToList();
        var existingLookup = existingImages.ToDictionary(
            item => NormalizeImagePath(item.ImageUrl) ?? item.ImageUrl,
            item => item,
            StringComparer.OrdinalIgnoreCase);

        var removedImages = existingImages
            .Where(item => !normalizedDesired.Contains(NormalizeImagePath(item.ImageUrl) ?? item.ImageUrl, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var removedImage in removedImages)
        {
            context.LocationImages.Remove(removedImage);
        }

        location.PreferenceImageUrl = normalizedPreferenceImageUrl;

        for (var index = 0; index < normalizedDesired.Count; index++)
        {
            var imageUrl = normalizedDesired[index];
            if (existingLookup.TryGetValue(imageUrl, out var existingImage))
            {
                existingImage.SortOrder = index + 1;
                continue;
            }

            context.LocationImages.Add(new LocationImage
            {
                LocationId = location.LocationId,
                ImageUrl = imageUrl,
                SortOrder = index + 1,
                CreatedAt = DateTime.UtcNow
            });
        }

        return removedImages
            .Select(item => NormalizeImagePath(item.ImageUrl))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
    }

    private ChangeRequestDto MapToDto(
        ChangeRequest item,
        IReadOnlyDictionary<int, Location>? liveLocationsById = null,
        IReadOnlyDictionary<int, Audio>? liveAudiosById = null,
        IReadOnlyDictionary<string, Language>? languagesByCode = null)
    {
        var submittedAt = item.UpdatedAt ?? item.CreatedAt;
        Location? liveLocation = null;
        if (item.TargetId is > 0 && liveLocationsById is not null)
        {
            liveLocationsById.TryGetValue(item.TargetId.Value, out liveLocation);
        }

        Audio? liveAudio = null;
        if (item.TargetId is > 0 && liveAudiosById is not null)
        {
            liveAudiosById.TryGetValue(item.TargetId.Value, out liveAudio);
        }

        return item.TargetTable switch
        {
            LocationTarget => MapLocationRequest(item, submittedAt, liveLocation),
            AudioTarget => MapAudioRequest(item, submittedAt, liveAudio, languagesByCode),
            _ => new ChangeRequestDto
            {
                Id = item.RequestId,
                Type = item.TargetTable,
                ActionType = ToActionLabel(item.RequestType),
                TargetId = item.TargetId,
                Name = $"{item.TargetTable} request",
                OwnerId = item.OwnerId,
                OwnerName = item.Owner?.FullName ?? item.Owner?.Username ?? "Unknown owner",
                Status = item.Status,
                Reason = item.Reason,
                AdminNote = item.AdminNote,
                SubmittedAt = submittedAt,
                UpdatedAt = item.UpdatedAt
            }
        };
    }

    private ChangeRequestDto MapLocationRequest(ChangeRequest item, DateTime submittedAt, Location? liveLocation)
    {
        var payload = DeserializeLocationPayload(item.NewDataJson);
        return new ChangeRequestDto
        {
            Id = item.RequestId,
            Type = "POI",
            ActionType = ToActionLabel(item.RequestType),
            TargetId = item.TargetId,
            Name = payload.Name,
            OwnerId = item.OwnerId,
            OwnerName = item.Owner?.FullName ?? item.Owner?.Username ?? payload.OwnerName ?? "Unknown owner",
            Status = item.Status,
            Reason = item.Reason,
            AdminNote = item.AdminNote,
            SubmittedAt = submittedAt,
            UpdatedAt = item.UpdatedAt,
            LiveLocation = liveLocation?.ToDto(),
            Location = new LocationDto
            {
                Id = item.TargetId ?? 0,
                CategoryId = payload.CategoryId,
                Category = payload.CategoryName,
                OwnerId = payload.OwnerId,
                OwnerName = payload.OwnerName,
                Name = payload.Name,
                Description = payload.Description,
                Latitude = payload.Latitude,
                Longitude = payload.Longitude,
                Radius = payload.Radius,
                StandbyRadius = payload.StandbyRadius,
                Priority = payload.Priority,
                DebounceSeconds = payload.DebounceSeconds,
                IsGpsTriggerEnabled = payload.IsGpsTriggerEnabled,
                Address = payload.Address,
                PreferenceImageUrl = payload.PreferenceImageUrl,
                CoverImageUrl = payload.PreferenceImageUrl ?? payload.ImageUrls.FirstOrDefault(),
                ImageUrls = payload.ImageUrls,
                WebURL = payload.WebURL,
                Email = payload.Email,
                Phone = payload.Phone,
                EstablishedYear = payload.EstablishedYear,
                AudioCount = payload.AudioCount,
                AvailableVoiceGenders = payload.AvailableVoiceGenders,
                Status = payload.Status,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            }
        };
    }

    private ChangeRequestDto MapAudioRequest(
        ChangeRequest item,
        DateTime submittedAt,
        Audio? liveAudio,
        IReadOnlyDictionary<string, Language>? languagesByCode)
    {
        var payload = DeserializeAudioPayload(item.NewDataJson);
        Language? liveAudioLanguage = null;
        if (liveAudio is not null
            && languagesByCode is not null
            && !string.IsNullOrWhiteSpace(liveAudio.LanguageCode))
        {
            languagesByCode.TryGetValue(liveAudio.LanguageCode, out liveAudioLanguage);
        }

        return new ChangeRequestDto
        {
            Id = item.RequestId,
            Type = "Audio",
            ActionType = ToActionLabel(item.RequestType),
            TargetId = item.TargetId,
            Name = payload.Title,
            OwnerId = item.OwnerId,
            OwnerName = item.Owner?.FullName ?? item.Owner?.Username ?? payload.OwnerName ?? "Unknown owner",
            Status = item.Status,
            Reason = item.Reason,
            AdminNote = item.AdminNote,
            SubmittedAt = submittedAt,
            UpdatedAt = item.UpdatedAt,
            LiveAudio = liveAudio?.ToDto(liveAudioLanguage),
            Audio = new AudioDto
            {
                Id = item.TargetId ?? 0,
                LocationId = payload.LocationId,
                LocationName = payload.LocationName,
                OwnerId = payload.OwnerId,
                OwnerName = payload.OwnerName,
                Language = payload.Language,
                LanguageName = payload.LanguageName,
                NativeLanguageName = payload.NativeLanguageName,
                PreferNativeVoice = payload.PreferNativeVoice,
                Title = payload.Title,
                Description = payload.Description,
                SourceType = payload.SourceType,
                Script = payload.Script,
                AudioURL = payload.AudioURL,
                FileSizeBytes = payload.FileSizeBytes,
                Duration = payload.Duration,
                VoiceName = payload.VoiceName,
                VoiceGender = payload.VoiceGender,
                Priority = payload.Priority,
                PlaybackMode = payload.PlaybackMode,
                InterruptPolicy = payload.InterruptPolicy,
                IsDownloadable = payload.IsDownloadable,
                Status = payload.Status,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            }
        };
    }

    private async Task<string?> SavePendingPreferenceImageAsync(
        IFormFile? preferenceImageFile,
        string locationName,
        CancellationToken cancellationToken)
    {
        if (preferenceImageFile is null || preferenceImageFile.Length <= 0)
        {
            return null;
        }

        if (!IsSupportedImageFile(preferenceImageFile))
        {
            throw new InvalidOperationException($"'{preferenceImageFile.FileName}' is not a supported image file.");
        }

        var savedPath = await imageStorage.SaveImageAsync(preferenceImageFile, locationName, 1, cancellationToken);
        return NormalizeImagePath(savedPath) ?? savedPath;
    }

    private async Task<List<string>> SavePendingImagesAsync(
        IEnumerable<IFormFile>? imageFiles,
        string locationName,
        int startSortOrder,
        CancellationToken cancellationToken)
    {
        if (imageFiles is null)
        {
            return [];
        }

        var files = imageFiles
            .Where(item => item is not null && item.Length > 0)
            .ToList();

        if (files.Count == 0)
        {
            return [];
        }

        var invalidImage = files.FirstOrDefault(item => !IsSupportedImageFile(item));
        if (invalidImage is not null)
        {
            throw new InvalidOperationException($"'{invalidImage.FileName}' is not a supported image file.");
        }

        var savedPaths = new List<string>(files.Count);
        for (var index = 0; index < files.Count; index++)
        {
            var savedPath = await imageStorage.SaveImageAsync(files[index], locationName, startSortOrder + index, cancellationToken);
            savedPaths.Add(NormalizeImagePath(savedPath) ?? savedPath);
        }

        return savedPaths;
    }

    private async Task<PendingAudioChangeData> BuildAudioPayloadAsync(Audio liveAudio, CancellationToken cancellationToken)
    {
        var language = await context.Languages
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.LangCode == liveAudio.LanguageCode, cancellationToken);

        return new PendingAudioChangeData
        {
            LocationId = liveAudio.LocationId,
            LocationName = liveAudio.Location?.Name ?? "Unknown",
            OwnerId = liveAudio.Location?.OwnerId,
            OwnerName = liveAudio.Location?.Owner?.FullName ?? liveAudio.Location?.Owner?.Username,
            Language = liveAudio.LanguageCode,
            LanguageName = language?.LangName,
            NativeLanguageName = language?.NativeName,
            PreferNativeVoice = language?.PreferNativeVoice ?? true,
            Title = liveAudio.Title,
            Description = liveAudio.Description,
            SourceType = liveAudio.SourceType,
            Script = liveAudio.Script,
            AudioURL = NormalizeAudioPath(liveAudio.FilePath),
            FileSizeBytes = liveAudio.FileSizeBytes,
            Duration = liveAudio.DurationSeconds ?? 0,
            VoiceName = liveAudio.VoiceName,
            VoiceGender = liveAudio.VoiceGender,
            Priority = liveAudio.Priority,
            PlaybackMode = liveAudio.PlaybackMode,
            InterruptPolicy = liveAudio.InterruptPolicy,
            IsDownloadable = liveAudio.IsDownloadable,
            Status = liveAudio.Status
        };
    }

    private static PendingLocationChangeData BuildLocationPayload(Location liveLocation) =>
        new()
        {
            CategoryId = liveLocation.CategoryId ?? 0,
            CategoryName = liveLocation.Category?.Name ?? "Unassigned",
            OwnerId = liveLocation.OwnerId,
            OwnerName = liveLocation.Owner?.FullName ?? liveLocation.Owner?.Username,
            Name = liveLocation.Name,
            Description = liveLocation.Description,
            Latitude = liveLocation.Latitude,
            Longitude = liveLocation.Longitude,
            Radius = liveLocation.Radius,
            StandbyRadius = liveLocation.StandbyRadius,
            Priority = liveLocation.Priority,
            DebounceSeconds = liveLocation.DebounceSeconds,
            IsGpsTriggerEnabled = liveLocation.IsGpsTriggerEnabled,
            Address = liveLocation.Address,
            PreferenceImageUrl = ResolvePreferenceImagePath(liveLocation),
            WebURL = liveLocation.WebURL,
            Email = liveLocation.Email,
            Phone = liveLocation.PhoneContact,
            EstablishedYear = liveLocation.EstablishedYear ?? DateTime.UtcNow.Year,
            Status = liveLocation.Status,
            ImageUrls = liveLocation.Images
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.ImageId)
                .Select(item => NormalizeImagePath(item.ImageUrl) ?? item.ImageUrl)
                .ToList(),
            AudioCount = liveLocation.AudioContents.Count(item => item.Status == 1),
            AvailableVoiceGenders = liveLocation.AudioContents
                .Where(item => item.Status == 1 && !string.IsNullOrWhiteSpace(item.VoiceGender))
                .Select(item => item.VoiceGender!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

    private async Task<List<string>> GetReplacementOrphanedPathsAsync(
        ChangeRequest existingPendingRequest,
        string nextDataJson,
        CancellationToken cancellationToken)
    {
        var currentPendingPaths = GetManagedFilePaths(existingPendingRequest.TargetTable, existingPendingRequest.NewDataJson);
        var nextPendingPaths = GetManagedFilePaths(existingPendingRequest.TargetTable, nextDataJson);
        var livePaths = await GetLiveReferencedFilePathsAsync(existingPendingRequest.TargetTable, existingPendingRequest.TargetId, cancellationToken);

        return currentPendingPaths
            .Except(nextPendingPaths, StringComparer.OrdinalIgnoreCase)
            .Except(livePaths, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<string>> GetOrphanedPathsRelativeToLiveDataAsync(ChangeRequest request, CancellationToken cancellationToken)
    {
        var pendingPaths = GetManagedFilePaths(request.TargetTable, request.NewDataJson);
        var livePaths = await GetLiveReferencedFilePathsAsync(request.TargetTable, request.TargetId, cancellationToken);

        return pendingPaths
            .Except(livePaths, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<HashSet<string>> GetLiveReferencedFilePathsAsync(
        string targetTable,
        int? targetId,
        CancellationToken cancellationToken)
    {
        if (targetId is null or <= 0)
        {
            return [];
        }

        return targetTable switch
        {
            LocationTarget => await GetLiveLocationReferencedFilePathsAsync(targetId.Value, cancellationToken),
            AudioTarget => await context.AudioContents
                .AsNoTracking()
                .Where(item => item.AudioId == targetId.Value && item.FilePath != null)
                .Select(item => NormalizeAudioPath(item.FilePath) ?? item.FilePath!)
                .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken),
            _ => []
        };
    }

    private static HashSet<string> GetManagedFilePaths(string targetTable, string payloadJson)
    {
        if (string.Equals(targetTable, LocationTarget, StringComparison.OrdinalIgnoreCase))
        {
            var payload = DeserializeLocationPayload(payloadJson);
            return payload.ImageUrls
                .Append(payload.PreferenceImageUrl)
                .Select(NormalizeImagePath)
                .Where(item => SharedStoragePaths.TryGetManagedImageFileName(item) is not null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (string.Equals(targetTable, AudioTarget, StringComparison.OrdinalIgnoreCase))
        {
            return new[] { NormalizeAudioPath(DeserializeAudioPayload(payloadJson).AudioURL) }
                .Where(item => SharedStoragePaths.TryGetManagedAudioFileName(item) is not null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return [];
    }

    private async Task<HashSet<string>> GetLiveLocationReferencedFilePathsAsync(int targetId, CancellationToken cancellationToken)
    {
        var location = await context.Locations
            .AsNoTracking()
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.LocationId == targetId, cancellationToken);

        if (location is null)
        {
            return [];
        }

        return location.Images
            .Select(item => NormalizeImagePath(item.ImageUrl))
            .Append(NormalizeImagePath(location.PreferenceImageUrl))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static PendingLocationChangeData DeserializeLocationPayload(string json) =>
        JsonSerializer.Deserialize<PendingLocationChangeData>(json, JsonOptions)
        ?? throw new InvalidOperationException("The POI request payload is invalid.");

    private static PendingAudioChangeData DeserializeAudioPayload(string json) =>
        JsonSerializer.Deserialize<PendingAudioChangeData>(json, JsonOptions)
        ?? throw new InvalidOperationException("The audio request payload is invalid.");

    private static string NormalizeRequestType(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized switch
        {
            CreateAction or UpdateAction or DeleteAction => normalized,
            _ => throw new InvalidOperationException("Unsupported request type.")
        };
    }

    private static string? NormalizeTypeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "POI" or "LOCATION" => "POI",
            "AUDIO" => "Audio",
            _ => value.Trim()
        };
    }

    private static string? NormalizeActionFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "CREATE" or "ADD" => "Add",
            "UPDATE" or "EDIT" => "Edit",
            "DELETE" => "Delete",
            _ => value.Trim()
        };
    }

    private static string? NormalizeStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Equals("pending", StringComparison.OrdinalIgnoreCase)
            ? PendingStatus
            : normalized.Equals("approved", StringComparison.OrdinalIgnoreCase)
                ? ApprovedStatus
                : normalized.Equals("rejected", StringComparison.OrdinalIgnoreCase)
                    ? RejectedStatus
                    : normalized;
    }

    private static string ToActionLabel(string requestType) =>
        requestType.ToUpperInvariant() switch
        {
            CreateAction => "Add",
            UpdateAction => "Edit",
            DeleteAction => "Delete",
            _ => requestType
        };

    private static string GetFriendlyType(string targetTable) =>
        targetTable == LocationTarget ? "POI" : targetTable;

    private static string BuildApprovalMessage(ChangeRequest request, DashboardUser reviewer, string? adminNote)
    {
        var reviewerName = reviewer.FullName ?? reviewer.Username;
        return string.IsNullOrWhiteSpace(adminNote)
            ? $"{GetFriendlyType(request.TargetTable)} request #{request.RequestId} was approved by {reviewerName}."
            : $"{GetFriendlyType(request.TargetTable)} request #{request.RequestId} was approved by {reviewerName}. Note: {adminNote.Trim()}";
    }

    private static string BuildReviewerApprovalMessage(ChangeRequest request, string? adminNote) =>
        string.IsNullOrWhiteSpace(adminNote)
            ? $"You approved {GetFriendlyType(request.TargetTable)} request #{request.RequestId} and the owner inbox was updated."
            : $"You approved {GetFriendlyType(request.TargetTable)} request #{request.RequestId}. Shared note: {adminNote.Trim()}";

    private static string BuildRejectionMessage(ChangeRequest request, DashboardUser reviewer, string adminNote)
    {
        var reviewerName = reviewer.FullName ?? reviewer.Username;
        return $"{GetFriendlyType(request.TargetTable)} request #{request.RequestId} was rejected by {reviewerName}. Reason: {adminNote.Trim()}";
    }

    private static string BuildReviewerRejectionMessage(ChangeRequest request, string adminNote) =>
        $"You rejected {GetFriendlyType(request.TargetTable)} request #{request.RequestId}. Shared reason: {adminNote.Trim()}";

    private static InboxMessage CreateInboxMessage(
        int userId,
        int? relatedRequestId,
        string title,
        string body,
        string messageType,
        DateTime? createdAt = null) =>
        new()
        {
            UserId = userId,
            RelatedRequestId = relatedRequestId,
            Title = TrimToLength(title, 200),
            Body = TrimToLength(body, 4000),
            MessageType = TrimToLength(messageType, 32),
            IsRead = false,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

    private static string TrimToLength(string value, int maxLength)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static void EnsurePending(ChangeRequest request)
    {
        if (!string.Equals(request.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only pending requests can be reviewed.");
        }
    }

    private void DeleteManagedFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (SharedStoragePaths.TryGetManagedImageFileName(path) is not null)
            {
                imageStorage.DeleteIfManaged(path);
            }
            else if (SharedStoragePaths.TryGetManagedAudioFileName(path) is not null)
            {
                audioStorage.DeleteIfManaged(path);
            }
        }
    }

    private static string? ValidateAudioPayload(string sourceType, string? script, string? audioPath)
    {
        var normalizedSourceType = sourceType.Trim();
        var hasScript = !string.IsNullOrWhiteSpace(script);
        var hasFile = !string.IsNullOrWhiteSpace(audioPath);

        return normalizedSourceType switch
        {
            "TTS" when !hasScript => "TTS audio requires a script.",
            "Recorded" when !hasFile => "Recorded audio requires an uploaded file or stored path.",
            "Hybrid" when !hasScript || !hasFile => "Hybrid audio requires both a script and an uploaded file.",
            _ => null
        };
    }

    private static bool IsSupportedImageFile(IFormFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.ContentType)
            && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(file.FileName);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeAudioPath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : SharedStoragePaths.NormalizePublicAudioPath(value);

    private static string? NormalizeImagePath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : SharedStoragePaths.NormalizePublicImagePath(value);

    private static IReadOnlyList<string> BuildDesiredImageUrls(
        string preferenceImageUrl,
        IEnumerable<string>? retainedGalleryImageUrls,
        IEnumerable<string>? uploadedGalleryImageUrls)
    {
        var normalizedPreferenceImageUrl = NormalizeImagePath(preferenceImageUrl)
            ?? throw new InvalidOperationException("A valid preference image is required.");

        return new[] { normalizedPreferenceImageUrl }
            .Concat(retainedGalleryImageUrls ?? [])
            .Concat(uploadedGalleryImageUrls ?? [])
            .Select(NormalizeImagePath)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Where(item => !string.Equals(item, normalizedPreferenceImageUrl, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Prepend(normalizedPreferenceImageUrl)
            .ToList();
    }

    private static string? ResolvePreferenceImagePath(Location location) =>
        NormalizeImagePath(location.PreferenceImageUrl)
        ?? location.Images
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.ImageId)
            .Select(item => NormalizeImagePath(item.ImageUrl))
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));

    private sealed class PendingLocationChangeData
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public int? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; }
        public double StandbyRadius { get; set; }
        public int Priority { get; set; }
        public int DebounceSeconds { get; set; }
        public bool IsGpsTriggerEnabled { get; set; } = true;
        public string? Address { get; set; }
        public string? PreferenceImageUrl { get; set; }
        public string? WebURL { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public int EstablishedYear { get; set; }
        public int Status { get; set; } = 1;
        public List<string> ImageUrls { get; set; } = [];
        public int AudioCount { get; set; }
        public List<string> AvailableVoiceGenders { get; set; } = [];
    }

    private sealed class PendingAudioChangeData
    {
        public int LocationId { get; set; }
        public string LocationName { get; set; } = "";
        public int? OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public string Language { get; set; } = "vi-VN";
        public string? LanguageName { get; set; }
        public string? NativeLanguageName { get; set; }
        public bool PreferNativeVoice { get; set; } = true;
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string SourceType { get; set; } = "TTS";
        public string? Script { get; set; }
        public string? AudioURL { get; set; }
        public int? FileSizeBytes { get; set; }
        public int Duration { get; set; }
        public string? VoiceName { get; set; }
        public string? VoiceGender { get; set; }
        public int Priority { get; set; }
        public string PlaybackMode { get; set; } = "Auto";
        public string InterruptPolicy { get; set; } = "NotificationFirst";
        public bool IsDownloadable { get; set; } = true;
        public int Status { get; set; } = 1;
    }
}
