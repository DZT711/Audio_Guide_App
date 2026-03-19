using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication_API.Data;
using WebApplication_API.DTO;
using WebApplication_API.Model;

namespace WebApplication_API.Controller;

[ApiController]
[Route("[controller]")]
public class CategoryController : ControllerBase
{
    private readonly DBContext _context;

    public CategoryController(DBContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDTO>>> GetAllCategories()
    {
        try
        {
            var categories = await _context.Categories.ToListAsync();
            var categoryDTOs = categories.Select(c => new CategoryDTO(
                c.Id,
                c.Name,
                c.Description,
                // c.NumOfLocations,
                c.Status
            )).ToList();

            return Ok(categoryDTOs);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving categories", error = ex.Message });
        }
    }

    /// <summary>
    /// Get category by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CategoryDTO>> GetCategoryById(int id)
    {
        try
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound(new { message = "Category not found" });

            var categoryDTO = new CategoryDTO(
                category.Id,
                category.Name,
                category.Description,
                // category.NumOfLocations,
                category.Status
            );

            return Ok(categoryDTO);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving category", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new category
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CategoryDTO>> CreateCategory([FromBody] CreateCategoryDTO createCategoryDTO)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = new Category
            {
                Name = createCategoryDTO.Name,
                Description = createCategoryDTO.Description,
                // // NumOfLocations = createCategoryDTO.NumOfLocations,
                Status = createCategoryDTO.Status
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            var categoryDTO = new CategoryDTO(
                category.Id,
                category.Name,
                category.Description,
                // category.NumOfLocations,
                category.Status
            );

            return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, categoryDTO);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error creating category", error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing category
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryDTO updateCategoryDTO)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound(new { message = "Category not found" });

            category.Name = updateCategoryDTO.Name;
            category.Description = updateCategoryDTO.Description;
            // // category.NumOfLocations = updateCategoryDTO.NumOfLocations;
            category.Status = updateCategoryDTO.Status;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Category updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating category", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a category by ID
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        try
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound(new { message = "Category not found" });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Category deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting category", error = ex.Message });
        }
    }
}
