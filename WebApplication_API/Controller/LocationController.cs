using System;
using Microsoft.AspNetCore.Mvc;
using WebApplication_API.Data;
using Microsoft.EntityFrameworkCore;
using WebApplication_API.Model;
using WebApplication_API.DTO;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class LocationController : ControllerBase
{
    private readonly DBContext _context;

    public LocationController(DBContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all locations with categories
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllLocations()
    {
        try
        {
            var locations = await _context.Locations
                .Include(l => l.Category)
                .ToListAsync();

            var locationDTOs = new List<LocationDTO>();
            foreach (var location in locations)
            {
                var locationDTO = new LocationDTO(
                    location.Id,
                    location.Name,
                    location.Address,
                    location.Category?.Name ?? "Unknown",
                    location.EstablishedYear,
                    location.Description,
                    location.Latitude,
                    location.Longitude,
                    location.OwnerName,
                    location.WebURL,
                    location.Phone,
                    location.Email,
                    // location.NumOfAudio,
                    // location.NumOfImg,
                    // location.NumOfPeopleVisited,
                    location.Status
                );
                locationDTOs.Add(locationDTO);
            }

            return Ok(locationDTOs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving locations", error = ex.Message });
        }
    }

    /// <summary>
    /// Get location by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<LocationDTO>> GetLocationById(int id)
    {
        try
        {
            var location = await _context.Locations
                .Include(l => l.Category)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (location == null)
                return NotFound(new { message = "Location not found" });

            var locationDTO = new LocationDTO(
                location.Id,
                location.Name,
                location.Address,
                location.Category?.Name ?? "Unknown",
                location.EstablishedYear,
                location.Description,
                location.Latitude,
                location.Longitude,
                location.OwnerName,
                location.WebURL,
                location.Phone,
                location.Email,
                // location.NumOfAudio,
                // location.NumOfImg,
                // location.NumOfPeopleVisited,
                location.Status
            );

            return Ok(locationDTO);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving location", error = ex.Message });
        }
    }

    /// <summary>
    /// Get locations by category ID
    /// </summary>
    [HttpGet("category/{categoryId}")]
    public async Task<ActionResult<IEnumerable<LocationDTO>>> GetLocationsByCategory(int categoryId)
    {
        try
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);
            if (category == null)
                return NotFound(new { message = "Category not found" });

            var locations = await _context.Locations
                .Where(l => l.CategoryId == categoryId)
                .ToListAsync();

            var locationDTOs = locations.Select(l => new LocationDTO(
                l.Id,
                l.Name,
                l.Address,
                category.Name,
                l.EstablishedYear,
                l.Description,
                l.Latitude,
                l.Longitude,
                l.OwnerName,
                l.WebURL,
                l.Phone,
                l.Email,
                // l.NumOfAudio,
                // l.NumOfImg,
                // l.NumOfPeopleVisited,
                l.Status
            )).ToList();

            return Ok(locationDTOs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving locations by category", error = ex.Message });
        }
    }

    /// <summary>
    /// Create new location
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LocationDTO>> CreateLocation([FromBody] CreateLocationDTO createLocationDTO)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == createLocationDTO.CategoryId);
            if (category == null)
                return NotFound(new { message = "Category not found" });

            var location = new Location
            {
                CategoryId = createLocationDTO.CategoryId,
                Name = createLocationDTO.Name,
                Description = createLocationDTO.Description,
                EstablishedYear = createLocationDTO.EstablishedYear,
                Latitude = createLocationDTO.Latitude,
                Longitude = createLocationDTO.Longitude,
                Address = createLocationDTO.Address,
                ImgURL = null,
                OwnerName = createLocationDTO.OwnerName,
                WebURL = createLocationDTO.WebURL,
                Phone = createLocationDTO.Phone,
                Email = createLocationDTO.Email,
                // // NumOfAudio = createLocationDTO.NumOfAudio,
                // // NumOfImg = createLocationDTO.NumOfImg,
                // // NumOfPeopleVisited = createLocationDTO.NumOfPeopleVisited,
                Status = createLocationDTO.Status
            };

            _context.Locations.Add(location);
            // category.NumOfLocations += 1;
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();

            var locationDTO = new LocationDTO(
                location.Id,
                location.Name,
                location.Address,
                category.Name,
                location.EstablishedYear,
                location.Description,
                location.Latitude,
                location.Longitude,
                location.OwnerName,
                location.WebURL,
                location.Phone,
                location.Email,
                // location.NumOfAudio,
                // location.NumOfImg,
                // location.NumOfPeopleVisited,
                location.Status
            );

            return CreatedAtAction(nameof(GetLocationById), new { id = location.Id }, locationDTO);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error creating location", error = ex.Message });
        }
    }

    /// <summary>
    /// Update existing location
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLocation(int id, [FromBody] CreateLocationDTO updateLocationDTO)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
            if (location == null)
                return NotFound(new { message = "Location not found" });

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == updateLocationDTO.CategoryId);
            if (category == null)
                return NotFound(new { message = "Category not found" });

            location.Name = updateLocationDTO.Name;
            location.Description = updateLocationDTO.Description;
            location.EstablishedYear = updateLocationDTO.EstablishedYear;
            location.Latitude = updateLocationDTO.Latitude;
            location.Longitude = updateLocationDTO.Longitude;
            location.Address = updateLocationDTO.Address;
            location.OwnerName = updateLocationDTO.OwnerName;
            location.WebURL = updateLocationDTO.WebURL;
            location.Phone = updateLocationDTO.Phone;
            location.Email = updateLocationDTO.Email;
            // location.NumOfAudio = updateLocationDTO.NumOfAudio;
            // location.NumOfImg = updateLocationDTO.NumOfImg;
            // location.NumOfPeopleVisited = updateLocationDTO.NumOfPeopleVisited;
            location.Status = updateLocationDTO.Status;
            location.CategoryId = updateLocationDTO.CategoryId;

            _context.Locations.Update(location);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Location updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating location", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete location by ID
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLocation(int id)
    {
        try
        {
            var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
            if (location == null)
                return NotFound(new { message = "Location not found" });

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == location.CategoryId);
            if (category != null)
            {
                // category.NumOfLocations -= 1;
                _context.Categories.Update(category);
            }

            _context.Locations.Remove(location);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Location deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting location", error = ex.Message });
        }
    }
}