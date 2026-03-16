using System;

namespace WebApplication_API.Data;

public class Location
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category? Category{ get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int EstablishedYear { get; set; }
    public required double Latitude { get; set; }// Vi do
    public required double Longitude { get; set; }// Kinh do
    public string? Address { get; set; }
    public string? ImgURL{ get; set; }
    public string? OwnerName{ get; set; }
    public string? WebURL{ get; set; }
    public string? Phone{ get; set; }
    public string? Email { get; set; }
    public int NumOfAudio { get; set; }
    public int NumOfImg { get; set; }
    public int NumOfPeopleVisited { get; set; }
    public required int Status { get; set; }
}
