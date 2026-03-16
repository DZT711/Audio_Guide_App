using System;

namespace WebApplication_API.Data;

public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int NumOfLocations { get; set; }
    public required int Status { get; set; }
}
