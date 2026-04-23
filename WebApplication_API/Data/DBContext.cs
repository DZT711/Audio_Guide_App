namespace WebApplication_API.Data;

using Microsoft.EntityFrameworkCore;
using Project_SharedClassLibrary.Contracts;
using Project_SharedClassLibrary.Constants;
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
    public DbSet<AudioListeningSession> AudioListeningSessions => Set<AudioListeningSession>();
    public DbSet<HeatmapEvent> HeatmapEvents => Set<HeatmapEvent>();
    public DbSet<UsageEventEntity> UsageEvents => Set<UsageEventEntity>();
    public DbSet<QrLandingVisit> QrLandingVisits => Set<QrLandingVisit>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

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
            entity.Property(item => item.QrFormat).HasMaxLength(8).HasDefaultValue("png");
            entity.Property(item => item.QrSize).HasDefaultValue(512);
            entity.Property(item => item.QrAutoplay).HasDefaultValue(true);

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
            entity.Property(item => item.WalkingSpeedKph).HasDefaultValue(TourDefaults.DefaultWalkingSpeedKph);
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
            entity.Property(item => item.SegmentDistanceKm).HasDefaultValue(0d);

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
            entity.HasIndex(item => item.EventAt);
            entity.HasIndex(item => new { item.PoiId, item.TourId, item.EventAt });

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
            entity.HasIndex(item => item.CapturedAt);
            entity.HasIndex(item => new { item.PoiId, item.TourId, item.CapturedAt });
        });

        modelBuilder.Entity<AudioListeningSession>(entity =>
        {
            entity.ToTable("AudioListeningSessions");
            entity.HasKey(item => item.AudioListeningSessionId);
            entity.HasIndex(item => item.StartedAt);
            entity.HasIndex(item => new { item.PoiId, item.TourId, item.StartedAt });
            entity.HasIndex(item => new { item.LocationId, item.StartedAt });

            entity.HasOne(item => item.Location)
                .WithMany(item => item.AudioListeningSessions)
                .HasForeignKey(item => item.LocationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(item => item.Audio)
                .WithMany(item => item.AudioListeningSessions)
                .HasForeignKey(item => item.AudioId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<HeatmapEvent>(entity =>
        {
            entity.ToTable("HeatmapEvents");
            entity.HasKey(item => item.HeatmapEventId);
            entity.HasIndex(item => item.CapturedAt);
            entity.HasIndex(item => new { item.LocationId, item.CapturedAt });
            entity.HasIndex(item => new { item.LocationId, item.TourId, item.CapturedAt });
            entity.HasIndex(item => new { item.EventType, item.CapturedAt });

            entity.HasOne(item => item.Location)
                .WithMany(item => item.HeatmapEvents)
                .HasForeignKey(item => item.LocationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QrLandingVisit>(entity =>
        {
            entity.ToTable("QrLandingVisits");
            entity.HasKey(item => item.QrLandingVisitId);
            entity.HasIndex(item => item.OpenedAt);
            entity.HasIndex(item => new { item.LocationId, item.OpenedAt });

            entity.Property(item => item.Source).HasMaxLength(64);
            entity.Property(item => item.UserAgent).HasMaxLength(512);
            entity.Property(item => item.Referrer).HasMaxLength(500);

            entity.HasOne(item => item.Location)
                .WithMany()
                .HasForeignKey(item => item.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UsageEventEntity>(entity =>
        {
            entity.ToTable("UsageEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.DeviceId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.EventType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(item => item.ReferenceId).HasMaxLength(128);
            entity.Property(item => item.Details).HasMaxLength(4000);
            entity.Property(item => item.DurationSeconds).HasDefaultValue(0);

            entity.HasIndex(item => item.EventType);
            entity.HasIndex(item => item.Timestamp);
            entity.HasIndex(item => new { item.DeviceId, item.Timestamp });
            entity.HasIndex(item => new { item.ReferenceId, item.Timestamp });
        });

        modelBuilder.Entity<ChangeRequest>(entity =>
        {
            entity.ToTable("ChangeRequests");
            entity.HasKey(item => item.RequestId);
            entity.HasIndex(item => new { item.TargetTable, item.TargetId });
            entity.HasIndex(item => new { item.OwnerId, item.Status });
            entity.Property(item => item.Status).HasDefaultValue("Pending");
            entity.Property(item => item.RequestType).HasDefaultValue("CREATE");

            entity.HasOne(item => item.Owner)
                .WithMany(item => item.ChangeRequests)
                .HasForeignKey(item => item.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(item => item.MessageId);
            entity.HasIndex(item => new { item.UserId, item.IsRead, item.CreatedAt });
            entity.Property(item => item.MessageType).HasDefaultValue("Info");

            entity.HasOne(item => item.User)
                .WithMany(item => item.InboxMessages)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.RelatedRequest)
                .WithMany(item => item.InboxMessages)
                .HasForeignKey(item => item.RelatedRequestId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("ActivityLogs");
            entity.HasKey(item => item.ActivityLogId);
            entity.HasIndex(item => item.CreatedAt);
            entity.HasIndex(item => new { item.ActionType, item.CreatedAt });
            entity.HasIndex(item => new { item.EntityType, item.CreatedAt });
            entity.HasIndex(item => new { item.UserId, item.CreatedAt });

            entity.HasOne(item => item.User)
                .WithMany(item => item.ActivityLogs)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
