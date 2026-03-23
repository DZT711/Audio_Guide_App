using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication_API.Data;
using WebApplication_API.DTO;
using WebApplication_API.Model;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class AudioController : ControllerBase
{
    private readonly DBContext _context;

    public AudioController(DBContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all audio content
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AudioDTO>>> GetAllAudio()
    {
        try
        {
            var audioList = await _context.AudioContents.ToListAsync();

            var audioDTOs = new List<AudioDTO>();
            foreach (var audio in audioList)
            {
                var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == audio.LocationId);
                var audioDTOItem = new AudioDTO(
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
    public async Task<ActionResult<AudioDTO>> GetAudioById(int id)
    {
        try
        {
            var audio = await _context.AudioContents.FirstOrDefaultAsync(a => a.Id == id);

            if (audio == null)
                return NotFound(new { message = "Audio not found" });

            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == audio.LocationId);
            var audioDTO = new AudioDTO(
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
    public async Task<ActionResult<IEnumerable<AudioDTO>>> GetAudioByLocation(int locationId)
    {
        try
        {
            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null)
                return NotFound(new { message = "Location not found" });

            var audioList = await _context.AudioContents
                .Where(a => a.LocationId == locationId)
                .ToListAsync();

            var audioDTOs = audioList.Select(a => new AudioDTO(
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
    public async Task<ActionResult<AudioDTO>> CreateAudio([FromBody] CreateAudioDTO createAudioDTO)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var location = await _context.Locations
                .FirstOrDefaultAsync(l => l.Name == createAudioDTO.LocationName);

            if (location == null)
                return NotFound(new { message = "Location not found" });

            var audio = new Audio
            {
                LocationId = location.Id,
                Title = createAudioDTO.Title,
                FilePath = createAudioDTO.AudioURL,
                Language = createAudioDTO.Language,
                Duration = createAudioDTO.Duration,
                Script = createAudioDTO.Script,
                Description = createAudioDTO.Description,
                VoiceGender = createAudioDTO.VoiceGender,
                Status = createAudioDTO.Status
            };

            _context.AudioContents.Add(audio);
            // location.NumOfAudio += 1;
            _context.Locations.Update(location);
            await _context.SaveChangesAsync();

            var audioDTO = new AudioDTO(
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
    public async Task<IActionResult> UpdateAudio(int id, [FromBody] UpdateAudioDTO updateAudioDTO)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var audio = await _context.AudioContents.FirstOrDefaultAsync(a => a.Id == id);

            if (audio == null)
                return NotFound(new { message = "Audio not found" });

            var location = await _context.Locations
                .FirstOrDefaultAsync(l => l.Name == updateAudioDTO.LocationName);

            if (location == null)
                return NotFound(new { message = "Location not found" });

            audio.Title = updateAudioDTO.Title;
            audio.FilePath = updateAudioDTO.AudioURL;
            audio.Language = updateAudioDTO.Language;
            audio.Duration = updateAudioDTO.Duration;
            audio.Description = updateAudioDTO.Description;
            audio.VoiceGender = updateAudioDTO.VoiceGender;
            audio.Script = updateAudioDTO.Script;
            audio.Status = updateAudioDTO.Status;
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
    /// Delete audio content by ID
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAudio(int id)
    {
        try
        {
            var audio = await _context.AudioContents.FirstOrDefaultAsync(a => a.Id == id);

            if (audio == null)
                return NotFound(new { message = "Audio not found" });

            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == audio.LocationId);
            if (location != null)
            {
                // location.NumOfAudio -= 1;
                _context.Locations.Update(location);
            }

            _context.AudioContents.Remove(audio);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Audio deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting audio", error = ex.Message });
        }
    }
}
