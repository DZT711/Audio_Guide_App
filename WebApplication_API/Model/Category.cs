using System.ComponentModel.DataAnnotations;

namespace WebApplication_API.Model;

public class Category
{
    public int CategoryId { get; set; }

    [MaxLength(100)]
    public required string Name { get; set; }

    public string? Description { get; set; }

    public int Status { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Location> Locations { get; set; } = [];
}
