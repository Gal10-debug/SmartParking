using Microsoft.EntityFrameworkCore;
using SmartParking.Api.Models;

namespace SmartParking.Api.Data;

public sealed class ParkingDbContext(DbContextOptions<ParkingDbContext> options) : DbContext(options)
{
    public static readonly Guid DemoLotId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public DbSet<ParkingLot> ParkingLots => Set<ParkingLot>();
    public DbSet<ParkingSpace> ParkingSpaces => Set<ParkingSpace>();
    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();
    public DbSet<OccupancyResult> OccupancyResults => Set<OccupancyResult>();

    public DbSet<VehicleDetection> VehicleDetections => Set<VehicleDetection>();
    public DbSet<AnalysisImage> AnalysisImages => Set<AnalysisImage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ParkingLot>().Property(x => x.Name).HasMaxLength(120);
        model.Entity<ParkingLot>().HasMany(x => x.Spaces).WithOne().HasForeignKey(x => x.ParkingLotId);
        model.Entity<ParkingSpace>().Property(x => x.Label).HasMaxLength(20);
        model.Entity<ParkingSpace>().HasIndex(x => new { x.ParkingLotId, x.Label }).IsUnique();
        model.Entity<AnalysisRun>().HasOne<ParkingLot>().WithMany().HasForeignKey(x => x.ParkingLotId);
        model.Entity<AnalysisRun>().HasIndex(x => new { x.ParkingLotId, x.CreatedAt });
        model.Entity<AnalysisRun>().Property(x => x.ImageName).HasMaxLength(255);
        model.Entity<AnalysisRun>().Property(x => x.Analyzer).HasMaxLength(80);
        model.Entity<AnalysisRun>().Property(x => x.Mode).HasMaxLength(30).HasDefaultValue("legacy-demo");
        model.Entity<AnalysisRun>().HasMany(x => x.Detections).WithOne().HasForeignKey(x => x.AnalysisRunId);
        model.Entity<VehicleDetection>().HasKey(x => new { x.AnalysisRunId, x.VehicleId });
        model.Entity<VehicleDetection>().Property(x => x.ClassName).HasMaxLength(20);
        model.Entity<AnalysisImage>().HasKey(x => x.AnalysisRunId);
        model.Entity<AnalysisImage>().Property(x => x.ContentType).HasMaxLength(30);
        model.Entity<AnalysisRun>().HasOne(x => x.Image).WithOne().HasForeignKey<AnalysisImage>(x => x.AnalysisRunId);
        model.Entity<AnalysisRun>().HasMany(x => x.Results).WithOne().HasForeignKey(x => x.AnalysisRunId);
        model.Entity<OccupancyResult>().HasKey(x => new { x.AnalysisRunId, x.ParkingSpaceId });
        model.Entity<OccupancyResult>().HasOne<ParkingSpace>().WithMany().HasForeignKey(x => x.ParkingSpaceId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ParkingLot>().HasData(new ParkingLot { Id = DemoLotId, Name = "Central Park · Lot A" });
        model.Entity<ParkingSpace>().HasData(Enumerable.Range(1, 24).Select(i => new ParkingSpace
        {
            Id = i,
            ParkingLotId = DemoLotId,
            Label = $"{(i <= 12 ? "A" : "B")}{(i - 1) % 12 + 1:00}"
        }));
    }
}
