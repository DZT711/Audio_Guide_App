using Microsoft.AspNetCore.Mvc;
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
    AdminRequestAuthorizationService authService) : ControllerBase
{
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

        return Ok(audioItems.Select(item => item.ToDto()).ToList());
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

        return Ok(audio.ToDto());
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

        return Ok(audioItems.Select(item => item.ToDto()).ToList());
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

        if (IsOwnerScoped(access.User!) && location.OwnerId != access.User!.UserId)
        {
            return StatusCode(403, new { message = "You can only create audio for your own locations." });
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
            LanguageCode = request.Language.Trim(),
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

        var savedAudio = await BuildAudioQuery(access.User!)
            .FirstAsync(item => item.AudioId == audio.AudioId, cancellationToken);

        return CreatedAtAction(nameof(GetAudioById), new { id = audio.AudioId }, savedAudio.ToDto());
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

        if (IsOwnerScoped(access.User!) && audio.Location?.OwnerId != access.User!.UserId)
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

        if (IsOwnerScoped(access.User!) && location.OwnerId != access.User!.UserId)
        {
            return StatusCode(403, new { message = "You can only assign audio to your own locations." });
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
        audio.LanguageCode = request.Language.Trim();
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

        if (IsOwnerScoped(access.User!) && audio.Location?.OwnerId != access.User!.UserId)
        {
            return StatusCode(403, new { message = "You can only archive audio for your own locations." });
        }

        audio.Status = 0;
        audio.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new ApiMessageResponse { Message = "Audio archived successfully." });
    }

    private IQueryable<Audio> BuildAudioQuery(DashboardUser currentUser)
    {
        var query = context.AudioContents
            .Include(item => item.Location)
            .ThenInclude(item => item!.Owner)
            .AsQueryable();

        return IsOwnerScoped(currentUser)
            ? query.Where(item => item.Location!.OwnerId == currentUser.UserId)
            : query;
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

    private static bool IsOwnerScoped(DashboardUser user) =>
        string.Equals(user.Role, AdminRoles.User, StringComparison.OrdinalIgnoreCase);
}
