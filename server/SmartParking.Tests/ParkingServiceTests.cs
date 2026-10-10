using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartParking.Api.Data;
using SmartParking.Api.Dtos;
using SmartParking.Api.Models;
using SmartParking.Api.Services;
using Xunit;

namespace SmartParking.Tests;

public sealed class ParkingServiceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ParkingDbContext db;
    public ParkingServiceTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<ParkingDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
    }
    private ParkingService Service(IAiAnalyzer? analyzer = null) => new(db, analyzer ?? new FakeAnalyzer(), TimeProvider.System);
    private static IFormFile Image(string type = "image/png", byte[]? bytes = null) => new FormFile(new MemoryStream(bytes ?? [1, 2, 3]), 0, (bytes ?? [1, 2, 3]).Length, "image", "../lot.png") { Headers = new HeaderDictionary(), ContentType = type };

    [Fact]
    public async Task AnalysisPersistsVehiclesAndImageWithoutInventingOccupancy()
    {
        var created = await Service().AnalyzeAsync(ParkingDbContext.DemoLotId, Image(), default);
        Assert.Equal(2, created.VehicleCount);
        Assert.Equal("vehicle-detection", created.Mode);
        Assert.Null(created.TotalSpaces); Assert.Null(created.OccupiedSpaces);
        Assert.Null(created.AvailableSpaces); Assert.Null(created.OccupancyPercentage);
        Assert.Equal("lot.png", created.ImageName);
        Assert.Equal(1, await db.AnalysisRuns.CountAsync()); Assert.Equal(2, await db.VehicleDetections.CountAsync());
        Assert.Equal(0, await db.OccupancyResults.CountAsync());
        var archived = await Service().GetImageAsync(ParkingDbContext.DemoLotId, created.Id, default);
        Assert.Equal(new byte[] { 1, 2, 3 }, archived.Data);
        Assert.Equal("image/png", archived.ContentType);
        var persisted = await Service().GetAnalysisAsync(ParkingDbContext.DemoLotId, created.Id, default);
        Assert.Equal(JsonSerializer.Serialize(created), JsonSerializer.Serialize(persisted));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(9)]
    public async Task CreatedTimestampUsesPostgresPrecisionAndRoundTripsExactly(int extraTicks)
    {
        var expected = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero).AddTicks(1234560);
        var service = new ParkingService(db, new FakeAnalyzer(), new FixedClock(expected.AddTicks(extraTicks)));
        var created = await service.AnalyzeAsync(ParkingDbContext.DemoLotId, Image(), default);
        var persisted = await service.GetAnalysisAsync(ParkingDbContext.DemoLotId, created.Id, default);
        Assert.Equal(expected, created.CreatedAt);
        Assert.Equal(JsonSerializer.Serialize(created), JsonSerializer.Serialize(persisted));
    }

    [Fact]
    public async Task NoVehiclesIsARealZeroRatherThanUnknownOrFakeOccupancy()
    {
        var result = await Service(new FakeAnalyzer("empty")).AnalyzeAsync(ParkingDbContext.DemoLotId, Image(), default);
        Assert.Equal(0, result.VehicleCount); Assert.Empty(result.Detections);
        Assert.Null(result.AvailableSpaces);
    }

    [Fact]
    public async Task LegacyRunsRemainClearlyMarkedWithoutRealVehicleCounts()
    {
        var run = new AnalysisRun
        {
            Id = Guid.NewGuid(),
            ParkingLotId = ParkingDbContext.DemoLotId,
            CreatedAt = DateTimeOffset.UtcNow,
            ImageName = "old.png",
            Analyzer = "deterministic-demo-v1"
        };
        db.Add(run); await db.SaveChangesAsync();
        var result = await Service().GetAnalysisAsync(ParkingDbContext.DemoLotId, run.Id, default);
        Assert.Equal("legacy-demo", result.Mode); Assert.Null(result.VehicleCount); Assert.Null(result.ImageUrl);
        Assert.Null(result.OccupiedSpaces); Assert.Empty(result.Detections);
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => Service().GetImageAsync(ParkingDbContext.DemoLotId, run.Id, default))).StatusCode);
    }

    [Fact]
    public async Task ImagesAndAnalysesCannotBeReadUnderAnotherLot()
    {
        var result = await Service().AnalyzeAsync(ParkingDbContext.DemoLotId, Image(), default);
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => Service().GetImageAsync(Guid.NewGuid(), result.Id, default))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => Service().GetAnalysisAsync(Guid.NewGuid(), result.Id, default))).StatusCode);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public async Task UnsupportedImagesAreRejectedWithoutSaving(string type)
    {
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => Service().AnalyzeAsync(ParkingDbContext.DemoLotId, Image(type), default))).StatusCode);
        Assert.Equal(0, await db.AnalysisRuns.CountAsync());
    }

    [Fact]
    public async Task EmptyAndOversizedImagesAreRejected()
    {
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[ParkingService.MaxImageBytes + 1] })
            Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => Service().AnalyzeAsync(ParkingDbContext.DemoLotId, Image(bytes: bytes), default))).StatusCode);
    }

    [Fact]
    public async Task MissingLotReturnsNotFound() =>
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => Service().AnalyzeAsync(Guid.NewGuid(), Image(), default))).StatusCode);

    [Theory]
    [InlineData("duplicate")]
    [InlineData("confidence")]
    [InlineData("nan")]
    [InlineData("class")]
    [InlineData("box")]
    [InlineData("null-box")]
    [InlineData("dimensions")]
    [InlineData("mode")]
    [InlineData("null")]
    public async Task InvalidAnalyzerResponsesAreNeverPersisted(string mode)
    {
        Assert.Equal(502, (await Assert.ThrowsAsync<ApiException>(() => Service(new FakeAnalyzer(mode)).AnalyzeAsync(ParkingDbContext.DemoLotId, Image(), default))).StatusCode);
        Assert.Equal(0, await db.AnalysisRuns.CountAsync()); Assert.Equal(0, await db.AnalysisImages.CountAsync());
    }

    [Fact]
    public async Task AnalyzerFailuresAreNotPersisted()
    {
        Assert.Equal(502, (await Assert.ThrowsAsync<ApiException>(() => Service(new FakeAnalyzer("failure")).AnalyzeAsync(ParkingDbContext.DemoLotId, Image(), default))).StatusCode);
        Assert.Equal(0, await db.AnalysisRuns.CountAsync());
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class FakeAnalyzer(string mode = "valid") : IAiAnalyzer
    {
        public Task<AnalyzerResponse> AnalyzeAsync(IFormFile image, CancellationToken ct)
        {
            if (mode == "failure") throw new ApiException(502, "Analyzer unavailable");
            var box = mode == "box" ? new BoundingBoxDto(.8, .1, .5, .3) : new BoundingBoxDto(.1, .2, .3, .4);
            var detections = new List<VehicleDetectionDto>
            {
                new(1, mode == "class" ? "person" : "car", mode == "confidence" ? 2 : mode == "nan" ? double.NaN : .9, mode == "null-box" ? null! : box),
                new(mode == "duplicate" ? 1 : 2, "truck", .8, new(.5, .1, .2, .3))
            };
            if (mode == "empty") detections.Clear();
            return Task.FromResult(new AnalyzerResponse("test", mode == "mode" ? "demo" : "vehicle-detection",
                mode == "dimensions" ? 10000 : 100, mode == "dimensions" ? 10000 : 100,
                mode == "null" ? null! : detections));
        }
    }
}
