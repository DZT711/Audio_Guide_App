using System;
using WebApplication_API.Data;

namespace WebApplication_API.Endpoint;

public static class CategoryEndpoints
{
    const string EndpointName = "GetCategory";

    public static void MapCategoryEndpoints(this WebApplication app)
    {
        // var group = app.MapGroup("/categories");
        // group.MapGet("/", async (DBContext dbcontext) =>
        //     await dbcontext.Categories.Include(u => u.category).Select(u =>
        //         new UserDTO(u.Id, u.Name, u.Email, u.category!.Name, u.AccountBalance, u.CreatedDate)).ToListAsync()
        //     );


        // group.MapGet("/{id}", async (int id, DBContext dbcontext) =>
        // {
        //     var user = await dbcontext.Categories.FindAsync(id);
        //     return user is null ? Results.NotFound() : Results.Ok(
        //         new UserInformationDTO(user.Id, user.Name, user.Email, user.RoleId, user.AccountBalance, user.CreatedDate)
        //     );
        // }).WithName(EndpointName);


        // group.MapPost("/", async (CreateUserDTO user, DBContext dbcontext) =>
        // {
        //     User newusr = new()
        //     {
        //         Name = user.Name,
        //         Email = user.Email,
        //         RoleId = user.RoleId,
        //         AccountBalance = user.AccountBalance,
        //         CreatedDate = user.CreatedDate
        //     };
        //     dbcontext.Categories.Add(newusr);
        //     await dbcontext.SaveChangesAsync();
        //     UserInformationDTO usrInfo = new(
        //         newusr.Id,
        //         newusr.Name,
        //         newusr.Email,
        //         newusr.RoleId,
        //         newusr.AccountBalance,
        //         newusr.CreatedDate
        //     );
        //     return Results.CreatedAtRoute(EndpointName, new { id = usrInfo.Id }, usrInfo);
        // });


        // group.MapPut("/{id}", async (int id, UpdateUserDTO user, DBContext dbcontext) =>
        // {
        //     var existingUser = await dbcontext.Categories.FindAsync(id);
        //     if (existingUser is null)
        //     {
        //         return Results.NotFound();
        //     }
        //     existingUser.Name = user.Name;
        //     existingUser.Email = user.Email;
        //     existingUser.RoleId = user.RoleId;
        //     existingUser.AccountBalance = user.AccountBalance;
        //     existingUser.CreatedDate = user.CreatedDate;
        //     await dbcontext.SaveChangesAsync();
        //     return Results.NoContent();
        // });


        // group.MapDelete("/{id}", async (int id, DBContext dbcontext) =>
        // {
        //     await dbcontext.Categories.Where(u => u.Id == id).ExecuteDeleteAsync();
        //     return Results.NoContent();
        // });

    }
}
