using System;

namespace WebApplication_API.Data;

using Microsoft.EntityFrameworkCore;
using WebApplication_API.Model;


public class DBContext(DbContextOptions<DBContext> options) : DbContext(options)
{
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Audio> AudioContents => Set<Audio>();
    public DbSet<Category> Categories => Set<Category>();
}

    