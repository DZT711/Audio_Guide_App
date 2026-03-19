using System;
using Microsoft.EntityFrameworkCore;
using WebApplication_API.Data;
using WebApplication_API.DTO;
using WebApplication_API.Model;

namespace WebApplication_API.Endpoint;

public static class CategoryEndpoints
{
    const string EndpointName = "GetCategory";

    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/categories");
        group.MapGet("/", async (DBContext context) => await context.Categories.Select(r => new CategoryDTO(r.Id, r.Name, r.Description,  r.Status)).AsNoTracking().ToListAsync());

        group.MapGet("/{id}", async (int id, DBContext dbcontext) =>
        {
            var c = await dbcontext.Categories.FindAsync(id);
            return c is null ? Results.NotFound() : Results.Ok(
                new CategoryDTO(c.Id, c.Name, c.Description,  c.Status)
            );
        }).WithName(EndpointName);


        group.MapPost("/", async (CreateCategoryDTO user, DBContext dbcontext) =>
        {
            Category newCategory = new()
            {
                Name = user.Name,
                Description = user.Description,
                // NumOfLocations = user.NumOfLocations,
                Status = user.Status
            };
            dbcontext.Categories.Add(newCategory);
            await dbcontext.SaveChangesAsync();
            CategoryDTO categoryInfo = new(
                newCategory.Id,
                newCategory.Name,
                newCategory.Description,
                // newCategory.NumOfLocations,
                newCategory.Status
            );
            return Results.CreatedAtRoute(EndpointName, new { id = categoryInfo.Id }, categoryInfo);
        });

        group.MapPut("/{id}", async (int id, UpdateCategoryDTO user, DBContext dbcontext) =>
        {
            var existingUser = await dbcontext.Categories.FindAsync(id);
            if (existingUser is null)
            {
                return Results.NotFound();
            }
            existingUser.Name = user.Name;
            existingUser.Description = user.Description;
            // existingUser.NumOfLocations = user.NumOfLocations;
            existingUser.Status = user.Status;
            await dbcontext.SaveChangesAsync();
            return Results.NoContent();
        });


        group.MapDelete("/{id}", async (int id, DBContext dbcontext) =>
        {
            await dbcontext.Categories.Where(u => u.Id == id).ExecuteDeleteAsync();
            return Results.NoContent();
        });

    }
}
