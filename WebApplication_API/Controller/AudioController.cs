using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Security;
using Project_SharedClassLibrary.Storage;
using WebApplication_API.Data;
using WebApplication_API.Model;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class AudioController(
    DBContext context,
    SharedAudioFileStorageService audioStorage,
    TtsPreviewService ttsPreviewService,
    GeminiSpeechService geminiSpeechService,
    AdminRequestAuthorizationService authService,
    ActivityLogService activityLogService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("public/location/{locationId:int}")]
    public async Task<IActionResult> GetPublicAudioByLocation(int locationId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "public,max-age=60";

        var location = await context.Locations
            .AsNoTracking()
            .Where(item => item.LocationId == locationId && item.Status == 1)
            .Select(item => new
            {
                item.LocationId,
                item.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }

        var audioItems = (await context.AudioContents
            .AsNoTracking()
            .Where(item => item.LocationId == locationId && item.Status == 1)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => GetSourceTypeOrder(item.SourceType))
            .ThenBy(item => item.AudioId)
            .ToList();

        var languageLookup = await LoadLanguageLookupAsync(audioItems.Select(item => item.LanguageCode));
        var defaultAudioId = audioItems.FirstOrDefault()?.AudioId;

        return Ok(audioItems.Select(item => ToPublicAudioTrackDto(
            item,
            location.Name,
            GetLanguage(languageLookup, item.LanguageCode),
            item.AudioId == defaultAudioId)).ToList());
    }

    [AllowAnonymous]
    [HttpGet("public/location/{locationId:int}/default")]
    public async Task<IActionResult> GetPublicDefaultAudioByLocation(int locationId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "public,max-age=60";

        var location = await context.Locations
            .AsNoTracking()
            .Where(item => item.LocationId == locationId && item.Status == 1)
            .Select(item => new
            {
                item.LocationId,
                item.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }

        var audio = (await context.AudioContents
            .AsNoTracking()
            .Where(item => item.LocationId == locationId && item.Status == 1)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => GetSourceTypeOrder(item.SourceType))
            .ThenBy(item => item.AudioId)
            .FirstOrDefault();

        if (audio is null)
        {
            return NotFound(new { message = "No active audio found for this location." });
        }

        var languageLookup = await LoadLanguageLookupAsync([audio.LanguageCode]);
        return Ok(ToPublicAudioTrackDto(
            audio,
            location.Name,
            GetLanguage(languageLookup, audio.LanguageCode),
            true));
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAudio()
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AudioRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var audioItems = await BuildAudioQuery(access.User!)
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.Title)
            .ToListAsync();

        var languageLookup = await LoadLanguageLookupAsync(audioItems.Select(item => item.LanguageCode));
        return Ok(audioItems.Select(item => item.ToDto(GetLanguage(languageLookup, item.LanguageCode))).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAudioById(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AudioRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var audio = await BuildAudioQuery(access.User!)
            .FirstOrDefaultAsync(item => item.AudioId == id);

        if (audio is null)
        {
            return NotFound(new { message = "Audio item not found." });
        }

        var languageLookup = await LoadLanguageLookupAsync([audio.LanguageCode]);
        return Ok(audio.ToDto(GetLanguage(languageLookup, audio.LanguageCode)));
    }

    [HttpGet("location/{locationId:int}")]
    public async Task<IActionResult> GetAudioByLocation(int locationId)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AudioRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var audioItems = await BuildAudioQuery(access.User!)
            .Where(item => item.LocationId == locationId)
            .OrderByDescending(item => item.Status)
            .ThenBy(item => item.Title)
            .ToListAsync();

        var languageLookup = await LoadLanguageLookupAsync(audioItems.Select(item => item.LanguageCode));
        return Ok(audioItems.Select(item => item.ToDto(GetLanguage(languageLookup, item.LanguageCode))).ToList());
    }

    [HttpPost("preview-tts")]
    public async Task<IActionResult> PreviewTts([FromBody] AudioTtsPreviewRequest request, CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AudioRead);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            if (geminiSpeechService.IsEnabled)
            {
                var geminiResult = await geminiSpeechService.TranslateAndGenerateSpeechAsync(
                    new PublicAudioTranslateTtsRequest
                    {
                        Text = request.Text,
                        SourceLanguage = request.Language,
                        TargetLanguage = request.Language,
                        VoiceGender = request.VoiceGender
                    },
                    cancellationToken);

                Response.Headers["X-SmartTour-Tts-Provider"] = "Gemini";
                Response.Headers["X-SmartTour-Tts-Voice"] = geminiResult.VoiceName;
                return File(geminiResult.AudioContent, geminiResult.ContentType);
            }

            var preview = await ttsPreviewService.GeneratePreviewAsync(request, cancellationToken);
            Response.Headers["X-SmartTour-Tts-Provider"] = preview.Provider;
            Response.Headers["X-SmartTour-Tts-Voice"] = preview.VoiceName;
            return File(preview.Content, preview.ContentType);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { message = "The TTS preview provider could not be reached." });
        }
    }

    [AllowAnonymous]
    [HttpPost("public/translate-tts")]
    public async Task<IActionResult> TranslateTts(
        [FromBody] PublicAudioTranslateTtsRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await geminiSpeechService.TranslateAndGenerateSpeechAsync(request, cancellationToken);
            Response.Headers["X-SmartTour-Translated-Language"] = result.TargetLanguage;
            Response.Headers["X-SmartTour-Translated-Voice"] = result.VoiceName;
            Response.Headers["X-SmartTour-Translated-Text"] = Uri.EscapeDataString(result.TranslatedText);
            return File(result.AudioContent, result.ContentType);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { message = "The cloud speech provider could not be reached." });
        }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateAudio(
        [FromForm] AudioUpsertRequest request,
        [FromForm(Name = "AudioFile")] IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AudioManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var location = await context.Locations
            .Include(item => item.Owner)
            .FirstOrDefaultAsync(item => item.LocationId == request.LocationId, cancellationToken);

        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }

        if (location.Status != 1)
        {
            return BadRequest(new { message = "Inactive locations cannot be assigned to audio." });
        }

        if (!AdminOwnershipScope.CanAccessLocation(access.User!, location))
        {
            return StatusCode(403, new { message = "You can only create audio for your own locations." });
        }

        var language = await context.Languages
            .FirstOrDefaultAsync(item => item.LangCode == request.Language.Trim(), cancellationToken);
        if (language is null)
        {
            return NotFound(new { message = "Language not found." });
        }

        if (language.Status != 1)
        {
            return BadRequest(new { message = "Inactive languages cannot be assigned to audio." });
        }

        var audioPath = NormalizeAudioPath(request.AudioURL);
        if (audioFile is not null)
        {
            audioPath = await audioStorage.SaveAudioAsync(audioFile, location.Name, request.Title, cancellationToken);
        }

        var validationMessage = ValidateAudioPayload(request, audioPath);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        var audio = new Audio
        {
            LocationId = location.LocationId,
            LanguageCode = language.LangCode,
            Title = request.Title.Trim(),
            Description = Normalize(request.Description),
            SourceType = request.SourceType.Trim(),
            Script = Normalize(request.Script),
            FilePath = audioPath,
            FileSizeBytes = audioFile is null ? request.FileSizeBytes : (int?)audioFile.Length,
            DurationSeconds = request.Duration,
            VoiceName = Normalize(request.VoiceName),
            VoiceGender = Normalize(request.VoiceGender),
            Priority = request.Priority,
            PlaybackMode = request.PlaybackMode.Trim(),
            InterruptPolicy = request.InterruptPolicy.Trim(),
            IsDownloadable = request.IsDownloadable,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow
        };

        context.AudioContents.Add(audio);
        await context.SaveChangesAsync(cancellationToken);
        await activityLogService.LogAsync(
            access.User!,
            "Create",
            "Audio",
            audio.AudioId,
            audio.Title,
            $"Created audio '{audio.Title}' for '{location.Name}'.",
            cancellationToken);

        var savedAudio = await BuildAudioQuery(access.User!)
            .FirstAsync(item => item.AudioId == audio.AudioId, cancellationToken);

        return CreatedAtAction(nameof(GetAudioById), new { id = audio.AudioId }, savedAudio.ToDto(language));
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateAudio(
        int id,
        [FromForm] AudioUpsertRequest request,
        [FromForm(Name = "AudioFile")] IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AudioManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var audio = await context.AudioContents
            .Include(item => item.Location)
            .ThenInclude(item => item!.Owner)
            .FirstOrDefaultAsync(item => item.AudioId == id, cancellationToken);

        if (audio is null)
        {
            return NotFound(new { message = "Audio item not found." });
        }

        if (!AdminOwnershipScope.CanAccessLocation(access.User!, audio.Location))
        {
            return StatusCode(403, new { message = "You can only update audio for your own locations." });
        }

        var location = await context.Locations
            .Include(item => item.Owner)
            .FirstOrDefaultAsync(item => item.LocationId == request.LocationId, cancellationToken);

        if (location is null)
        {
            return NotFound(new { message = "Location not found." });
        }

        if (location.Status != 1)
        {
            return BadRequest(new { message = "Inactive locations cannot be assigned to audio." });
        }

        if (!AdminOwnershipScope.CanAccessLocation(access.User!, location))
        {
            return StatusCode(403, new { message = "You can only assign audio to your own locations." });
        }

        var language = await context.Languages
            .FirstOrDefaultAsync(item => item.LangCode == request.Language.Trim(), cancellationToken);
        if (language is null)
        {
            return NotFound(new { message = "Language not found." });
        }

        if (language.Status != 1)
        {
            return BadRequest(new { message = "Inactive languages cannot be assigned to audio." });
        }

        var nextAudioPath = string.IsNullOrWhiteSpace(request.AudioURL)
            ? NormalizeAudioPath(audio.FilePath)
            : NormalizeAudioPath(request.AudioURL);

        if (audioFile is not null)
        {
            nextAudioPath = await audioStorage.SaveAudioAsync(audioFile, location.Name, request.Title, cancellationToken);
            audioStorage.DeleteIfManaged(audio.FilePath);
        }

        var validationMessage = ValidateAudioPayload(request, nextAudioPath);
        if (!string.IsNullOrWhiteSpace(validationMessage))
        {
            return BadRequest(new { message = validationMessage });
        }

        audio.LocationId = location.LocationId;
        audio.LanguageCode = language.LangCode;
        audio.Title = request.Title.Trim();
        audio.Description = Normalize(request.Description);
        audio.SourceType = request.SourceType.Trim();
        audio.Script = Normalize(request.Script);
        audio.FilePath = nextAudioPath;
        audio.FileSizeBytes = audioFile is null ? request.FileSizeBytes : (int?)audioFile.Length;
        audio.DurationSeconds = request.Duration;
        audio.VoiceName = Normalize(request.VoiceName);
        audio.VoiceGender = Normalize(request.VoiceGender);
        audio.Priority = request.Priority;
        audio.PlaybackMode = request.PlaybackMode.Trim();
        audio.InterruptPolicy = request.InterruptPolicy.Trim();
        audio.IsDownloadable = request.IsDownloadable;
        audio.Status = request.Status;
        audio.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await activityLogService.LogAsync(
            access.User!,
            "Edit",
            "Audio",
            audio.AudioId,
            audio.Title,
            $"Updated audio '{audio.Title}' for '{location.Name}'.",
            cancellationToken);

        return Ok(new ApiMessageResponse { Message = "Audio updated successfully." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAudio(int id)
    {
        var access = await authService.AuthorizeAsync(HttpContext, context, AdminPermissions.AudioManage);
        if (!access.Succeeded)
        {
            return access.ToFailureResult();
        }

        var audio = await context.AudioContents
            .Include(item => item.Location)
            .FirstOrDefaultAsync(item => item.AudioId == id);

        if (audio is null)
        {
            return NotFound(new { message = "Audio item not found." });
        }

        if (!AdminOwnershipScope.CanAccessLocation(access.User!, audio.Location))
        {
            return StatusCode(403, new { message = "You can only archive audio for your own locations." });
        }

        audio.Status = 0;
        audio.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await activityLogService.LogAsync(
            access.User!,
            "Delete",
            "Audio",
            audio.AudioId,
            audio.Title,
            $"Archived audio '{audio.Title}'.");

        return Ok(new ApiMessageResponse { Message = "Audio archived successfully." });
    }

    private IQueryable<Audio> BuildAudioQuery(DashboardUser currentUser)
    {
        var query = context.AudioContents
            .Include(item => item.Location)
            .ThenInclude(item => item!.Owner)
            .AsQueryable();

        return AdminOwnershipScope.ApplyAudioScope(query, currentUser);
    }

    private static string? ValidateAudioPayload(AudioUpsertRequest request, string? audioPath)
    {
        var sourceType = request.SourceType.Trim();
        var hasScript = !string.IsNullOrWhiteSpace(request.Script);
        var hasFile = !string.IsNullOrWhiteSpace(audioPath);

        return sourceType switch
        {
            "TTS" when !hasScript => "TTS audio requires a script.",
            "Recorded" when !hasFile => "Recorded audio requires an uploaded file or stored path.",
            "Hybrid" when !hasScript || !hasFile => "Hybrid audio requires both a script and an uploaded file.",
            _ => null
        };
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeAudioPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : SharedStoragePaths.NormalizePublicAudioPath(path);

    private static PublicAudioTrackDto ToPublicAudioTrackDto(
        Audio audio,
        string locationName,
        Language? language,
        bool isDefault) =>
        new()
        {
            Id = audio.AudioId,
            LocationId = audio.LocationId,
            LocationName = locationName,
            Language = audio.LanguageCode,
            LanguageName = language?.LangName,
            Title = audio.Title,
            Description = audio.Description,
            SourceType = audio.SourceType,
            Script = audio.Script,
            AudioURL = NormalizeAudioPath(audio.FilePath),
            Duration = audio.DurationSeconds ?? 0,
            VoiceName = audio.VoiceName,
            VoiceGender = audio.VoiceGender,
            Priority = audio.Priority,
            IsDefault = isDefault
        };

    private static int GetSourceTypeOrder(string? sourceType) =>
        sourceType?.Trim().ToUpperInvariant() switch
        {
            "RECORDED" => 0,
            "HYBRID" => 1,
            _ => 2
        };

    private async Task<Dictionary<string, Language>> LoadLanguageLookupAsync(IEnumerable<string> languageCodes)
    {
        var normalizedCodes = languageCodes
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedCodes.Count == 0)
        {
            return new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase);
        }

        var languages = await context.Languages
            .Where(item => normalizedCodes.Contains(item.LangCode))
            .ToListAsync();

        return languages.ToDictionary(item => item.LangCode, StringComparer.OrdinalIgnoreCase);
    }

    private static Language? GetLanguage(IReadOnlyDictionary<string, Language> languageLookup, string? languageCode) =>
        string.IsNullOrWhiteSpace(languageCode)
            ? null
            : languageLookup.TryGetValue(languageCode, out var language)
                ? language
                : null;

}
