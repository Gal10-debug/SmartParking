using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartParking.Api.Data;
using SmartParking.Api.Dtos;
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
    public async Task AnalysisPersistsOneResultPerSpaceAndReturnsAccurateTotals()
    {
        var result = await Service().AnalyzeAsync(ParkingDbContext.DemoLotId, Image(), default);
        Assert.Equal(24, result.TotalSpaces); Assert.Equal(12, result.OccupiedSpaces); Assert.Equal(12, result.AvailableSpaces); Assert.Equal(50, result.OccupancyPercentage);
        Assert.Equal("lot.png", result.ImageName);
        Assert.Equal(1, await db.AnalysisRuns.CountAsync()); Assert.Equal(24, await db.OccupancyResults.CountAsync());
        Assert.All(await db.OccupancyResults.ToListAsync(), x => Assert.Equal(result.Id, x.AnalysisRunId));
        Assert.Equal(result.Id, (await Service().GetAnalysisAsync(ParkingDbContext.DemoLotId, result.Id, default)).Id);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public async Task UnsupportedImagesAreRejectedWithoutSaving(string type)
    {
        var error = await Assert.ThrowsAsync<ApiException>(() => Service().AnalyzeAsync(ParkingDbContext.DemoLotId, Image(type), default));
        Assert.Equal(400, error.StatusCode); Assert.Equal(0, await db.AnalysisRuns.CountAsync());
    }

    [Fact]
    public async Task EmptyAndOversizedImagesAreRejected()
    {
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[ParkingService.MaxImageBytes + 1] })
            Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => Service().AnalyzeAsync(ParkingDbContext.DemoLotId, Image(bytes: bytes), default))).StatusCode);
    }

    [Fact]
    public async Task MissingLotReturnsNotFound()
    {
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => Service().AnalyzeAsync(Guid.NewGuid(), Image(), default))).StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("confidence")]
    [InlineData("null")]
    public async Task InvalidAnalyzerResponsesAreNeverPersisted(string mode)
    {
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(new FakeAnalyzer(mode)).AnalyzeAsync(ParkingDbContext.DemoLotId, Image(), default));
        Assert.Equal(502, error.StatusCode); Assert.Equal(0, await db.AnalysisRuns.CountAsync());
    }

    [Fact]
    public async Task AnalyzerFailuresAreNotPersisted()
    {
        Assert.Equal(502, (await Assert.ThrowsAsync<ApiException>(() => Service(new FakeAnalyzer("failure")).AnalyzeAsync(ParkingDbContext.DemoLotId, Image(), default))).StatusCode);
        Assert.Equal(0, await db.AnalysisRuns.CountAsync());
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
    private sealed class FakeAnalyzer(string mode = "valid") : IAiAnalyzer
    {
        public Task<AnalyzerResponse> AnalyzeAsync(IFormFile image, IReadOnlyList<int> ids, CancellationToken ct)
        {
            if (mode == "failure") throw new ApiException(502, "Analyzer unavailable");
            var results = ids.Select(i => new AnalyzerSpace(i, i % 2 == 0, mode == "confidence" ? 2 : .5)).ToList();
            if (mode == "missing") results.RemoveAt(0);
            if (mode == "duplicate") results[0] = results[1];
            return Task.FromResult(new AnalyzerResponse("test", mode == "null" ? null! : results));
        }
    }
}
