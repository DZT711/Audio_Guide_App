using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using WebApplication_API.Data;
using WebApplication_API.Model;
using WebApplication_API.Services;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class AudioController : ControllerBase
{
    private readonly DBContext _context;
    private readonly SharedAudioFileStorageService _audioStorage;

    public AudioController(DBContext context, SharedAudioFileStorageService audioStorage)
    {
        _context = context;
        _audioStorage = audioStorage;
    }

    /// <summary>
    /// Get all audio content
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AudioDto>>> GetAllAudio()
    {
        try
        {
            var audioList = await _context.AudioContents.ToListAsync();

            var audioDTOs = new List<AudioDto>();
            foreach (var audio in audioList)
            {
                var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == audio.LocationId);
                var audioDTOItem = new AudioDto(
                    audio.Id,
                    audio.Title,
                    location?.Name ?? "Unknown",
                    audio.Description ?? "",
                    audio.FilePath,
                    audio.Language ?? "",
                    audio.VoiceGender ?? "",
                    audio.Script ?? "",
                    audio.Duration ?? 0,
                    // audio.NumOfPeoplePlayed,
                    audio.Status
                );
                audioDTOs.Add(audioDTOItem);
            }

            return Ok(audioDTOs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving audio content", error = ex.Message });
        }
    }

    /// <summary>
    /// Get audio content by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AudioDto>> GetAudioById(int id)
    {
        try
        {
            var audio = await _context.AudioContents.FirstOrDefaultAsync(a => a.Id == id);

            if (audio == null)
                return NotFound(new { message = "Audio not found" });

            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == audio.LocationId);
            var audioDTO = new AudioDto(
                audio.Id,
                audio.Title,
                location?.Name ?? "Unknown",
                audio.Description ?? "",
                audio.FilePath,
                audio.Language ?? "",
                audio.VoiceGender ?? "",
                audio.Script ?? "",
                audio.Duration ?? 0,
                audio.Status
            );

            return Ok(audioDTO);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving audio", error = ex.Message });
        }
    }

    /// <summary>
    /// Get audio content by location ID
    /// </summary>
    [HttpGet("location/{locationId}")]
    public async Task<ActionResult<IEnumerable<AudioDto>>> GetAudioByLocation(int locationId)
    {
        try
        {
            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null)
                return NotFound(new { message = "Location not found" });

            var audioList = await _context.AudioContents
                .Where(a => a.LocationId == locationId)
                .ToListAsync();

            var audioDTOs = audioList.Select(a => new AudioDto(
                a.Id,
                a.Title,
                location.Name,
                a.Description ?? "",
                a.FilePath,
                a.Language ?? "",
                a.VoiceGender ?? "",
                a.Script ?? "",
                a.Duration ?? 0,
                a.Status
            )).ToList();

            return Ok(audioDTOs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving audio by location", error = ex.Message });
        }
    }

    /// <summary>
    /// Create new audio content
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AudioDto>> CreateAudio(
        [FromForm] AudioUpsertRequest request,
        [FromForm(Name = "AudioFile")] IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (audioFile is null && string.IsNullOrWhiteSpace(request.AudioURL))
            {
                return BadRequest(new { message = "Choose an audio file to upload." });
            }

            var location = await _context.Locations
                .FirstOrDefaultAsync(l => l.Name == request.LocationName, cancellationToken);

            if (location == null)
                return NotFound(new { message = "Location not found" });

            var audioPath = request.AudioURL;
            if (audioFile is not null)
            {
                audioPath = await _audioStorage.SaveAudioAsync(audioFile, request.LocationName, request.Title, cancellationToken);
            }

            var audio = new Audio
            {
                LocationId = location.Id,
                Title = request.Title,
                FilePath = audioPath ?? string.Empty,
                Language = request.Language,
                Duration = request.Duration,
                Script = request.Script,
                Description = request.Description,
                VoiceGender = request.VoiceGender,
                Status = request.Status
            };

            _context.AudioContents.Add(audio);
            // location.NumOfAudio += 1;
            _context.Locations.Update(location);
            await _context.SaveChangesAsync();

            var audioDTO = new AudioDto(
                audio.Id,
                audio.Title,
                location.Name,
                audio.Description ?? "",
                audio.FilePath,
                audio.Language ?? "",
                audio.VoiceGender ?? "",
                audio.Script ?? "",
                audio.Duration ?? 0,
                audio.Status
            );

            return CreatedAtAction(nameof(GetAudioById), new { id = audio.Id }, audioDTO);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error creating audio", error = ex.Message });
        }
    }

    /// <summary>
    /// Update existing audio content
    /// </summary>
    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateAudio(
        int id,
        [FromForm] AudioUpsertRequest request,
        [FromForm(Name = "AudioFile")] IFormFile? audioFile,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var audio = await _context.AudioContents.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

            if (audio == null)
                return NotFound(new { message = "Audio not found" });

            var location = await _context.Locations
                .FirstOrDefaultAsync(l => l.Name == request.LocationName, cancellationToken);

            if (location == null)
                return NotFound(new { message = "Location not found" });

            var nextAudioPath = string.IsNullOrWhiteSpace(request.AudioURL) ? audio.FilePath : request.AudioURL;
            if (audioFile is not null)
            {
                nextAudioPath = await _audioStorage.SaveAudioAsync(audioFile, request.LocationName, request.Title, cancellationToken);
                _audioStorage.DeleteIfManaged(audio.FilePath);
            }

            if (string.IsNullOrWhiteSpace(nextAudioPath))
            {
                return BadRequest(new { message = "Choose an audio file to upload." });
            }

            audio.Title = request.Title;
            audio.FilePath = nextAudioPath;
            audio.Language = request.Language;
            audio.Duration = request.Duration;
            audio.Description = request.Description;
            audio.VoiceGender = request.VoiceGender;
            audio.Script = request.Script;
            audio.Status = request.Status;
            audio.LocationId = location.Id;

            _context.AudioContents.Update(audio);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Audio updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating audio", error = ex.Message });
        }
    }

    /// <summary>
    /// Archive audio content by ID
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAudio(int id)
    {
        try
        {
            var audio = await _context.AudioContents.FirstOrDefaultAsync(a => a.Id == id);

            if (audio == null)
                return NotFound(new { message = "Audio not found" });

            if (audio.Status == 0)
                return Ok(new { message = "Audio is already inactive" });

            audio.Status = 0;
            _context.AudioContents.Update(audio);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Audio archived successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error archiving audio", error = ex.Message });
        }
    }
}
