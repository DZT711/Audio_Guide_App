namespace WebApplication_API.Model;

public class TourLocation
{
    public int TourId { get; set; }

    public Tour? Tour { get; set; }

    public int LocationId { get; set; }

    public Location? Location { get; set; }

    public int SequenceOrder { get; set; }

    public double SegmentDistanceKm { get; set; }
}
