namespace WebApplication_API.Model;

public class LocationImage
{
    public int ImageId { get; set; }
    public int LocationId { get; set; }
    public Location? Location { get; set; }
    public required string ImageUrl { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
