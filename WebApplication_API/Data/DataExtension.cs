using System;
using Microsoft.EntityFrameworkCore;
using WebApplication_API.Model;
namespace WebApplication_API.Data;

public static class DataExtension
{
    public static void MigrateDb(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DBContext>();
        context.Database.Migrate();
    }
    public static void AddDataToDatabase(this WebApplicationBuilder builder)
    {
        var conn = builder.Configuration.GetConnectionString("ConnectionKey");
        builder.Services.AddSqlite<DBContext>(
            conn,
            optionsAction: options => options.UseSeeding((context, _) =>
                        {
                            if (!context.Set<Category>().Any())
                            {
                                context.Set<Category>().AddRange(
                                    new Category { Name = "Restaurant", Description = "Food Restaurants", Status = 1 },
                                    new Category { Name = "Cafe", Description = "Coffee & Beverage Shops", Status = 1 },
                                    new Category { Name = "Street Food", Description = "Street Food Vendors", Status = 1 },
                                    new Category { Name = "Bakery", Description = "Bread & Bakery Shops", Status = 1 },
                                    new Category { Name = "Fast Food", Description = "Fast Food Chains", Status = 1 }
                                );
                                context.SaveChanges();
                            }
                        }
                    )
                );
    }
}
