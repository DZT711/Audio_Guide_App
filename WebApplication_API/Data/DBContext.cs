namespace WebApplication_API.Data;

using Microsoft.EntityFrameworkCore;
using WebApplication_API.Model;

public class DBContext(DbContextOptions<DBContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<DashboardUser> DashboardUsers => Set<DashboardUser>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourLocation> TourLocations => Set<TourLocation>();
    public DbSet<Audio> AudioContents => Set<Audio>();
    public DbSet<LocationImage> LocationImages => Set<LocationImage>();
    public DbSet<PlaybackEvent> PlaybackEvents => Set<PlaybackEvent>();
    public DbSet<LocationTrackingEvent> LocationTrackingEvents => Set<LocationTrackingEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(item => item.CategoryId);
            entity.HasIndex(item => item.Name).IsUnique();
            entity.Property(item => item.Status).HasDefaultValue(1);
        });

        modelBuilder.Entity<Language>(entity =>
        {
            entity.ToTable("Languages");
            entity.HasKey(item => item.LanguageId);
            entity.HasIndex(item => item.LangCode).IsUnique();
            entity.Property(item => item.PreferNativeVoice).HasDefaultValue(true);
            entity.Property(item => item.Status).HasDefaultValue(1);
        });

        modelBuilder.Entity<DashboardUser>(entity =>
        {
            entity.ToTable("DashboardUsers");
            entity.HasKey(item => item.UserId);
            entity.HasIndex(item => item.Username).IsUnique();
            entity.HasIndex(item => item.Email).IsUnique();
            entity.HasIndex(item => item.Phone).IsUnique();
            entity.Property(item => item.Role).HasDefaultValue("User");
            entity.Property(item => item.Status).HasDefaultValue(1);
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("Locations");
            entity.HasKey(item => item.LocationId);
            entity.HasIndex(item => item.CategoryId);
            entity.HasIndex(item => item.OwnerId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.Latitude, item.Longitude });
            entity.Property(item => item.Radius).HasDefaultValue(30d);
            entity.Property(item => item.StandbyRadius).HasDefaultValue(12d);
            entity.Property(item => item.DebounceSeconds).HasDefaultValue(300);
            entity.Property(item => item.IsGpsTriggerEnabled).HasDefaultValue(true);
            entity.Property(item => item.Status).HasDefaultValue(1);

            entity.HasOne(item => item.Category)
                .WithMany(item => item.Locations)
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(item => item.Owner)
                .WithMany(item => item.OwnedLocations)
                .HasForeignKey(item => item.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Tour>(entity =>
        {
            entity.ToTable("Tours");
            entity.HasKey(item => item.TourId);
            entity.HasIndex(item => item.OwnerId);
            entity.HasIndex(item => item.Status);
            entity.Property(item => item.WalkingSpeedKph).HasDefaultValue(4.5d);
            entity.Property(item => item.Status).HasDefaultValue(1);

            entity.HasOne(item => item.Owner)
                .WithMany(item => item.OwnedTours)
                .HasForeignKey(item => item.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TourLocation>(entity =>
        {
            entity.ToTable("TourLocations");
            entity.HasKey(item => new { item.TourId, item.LocationId });
            entity.HasIndex(item => item.LocationId);
            entity.HasIndex(item => new { item.TourId, item.SequenceOrder }).IsUnique();

            entity.HasOne(item => item.Tour)
                .WithMany(item => item.Stops)
                .HasForeignKey(item => item.TourId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Location)
                .WithMany(item => item.TourStops)
                .HasForeignKey(item => item.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Audio>(entity =>
        {
            entity.ToTable("AudioContents");
            entity.HasKey(item => item.AudioId);
            entity.HasIndex(item => item.LocationId);
            entity.Property(item => item.LanguageCode).HasDefaultValue("vi-VN");
            entity.Property(item => item.SourceType).HasDefaultValue("TTS");
            entity.Property(item => item.PlaybackMode).HasDefaultValue("Auto");
            entity.Property(item => item.InterruptPolicy).HasDefaultValue("NotificationFirst");
            entity.Property(item => item.IsDownloadable).HasDefaultValue(true);
            entity.Property(item => item.Status).HasDefaultValue(1);

            entity.HasOne(item => item.Location)
                .WithMany(item => item.AudioContents)
                .HasForeignKey(item => item.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LocationImage>(entity =>
        {
            entity.ToTable("LocationImages");
            entity.HasKey(item => item.ImageId);
            entity.HasOne(item => item.Location)
                .WithMany(item => item.Images)
                .HasForeignKey(item => item.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlaybackEvent>(entity =>
        {
            entity.ToTable("PlaybackEvents");
            entity.HasKey(item => item.PlaybackEventId);
            entity.HasIndex(item => new { item.LocationId, item.EventAt });

            entity.HasOne(item => item.Location)
                .WithMany(item => item.PlaybackEvents)
                .HasForeignKey(item => item.LocationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(item => item.Audio)
                .WithMany(item => item.PlaybackEvents)
                .HasForeignKey(item => item.AudioId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LocationTrackingEvent>(entity =>
        {
            entity.ToTable("LocationTrackingEvents");
            entity.HasKey(item => item.TrackingEventId);
            entity.HasIndex(item => new { item.DeviceId, item.CapturedAt });
        });
    }
}
