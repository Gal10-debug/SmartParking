using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using SmartParking.Api.Data;
using SmartParking.Api.Dtos;
using SmartParking.Api.Services;
using Xunit;

namespace SmartParking.Tests;

public sealed class ApiTests
{
    [Fact]
    public async Task HttpEndpointsReturnLotsAndProblemDetails()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/parking-lots")).StatusCode);
        var openApi = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        Assert.Contains("multipart/form-data", await openApi.Content.ReadAsStringAsync());
        var missing = await client.GetAsync($"/api/parking-lots/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("Parking lot not found", await missing.Content.ReadAsStringAsync());
        var invalid = await client.GetAsync($"/api/parking-lots/{ParkingDbContext.DemoLotId}/analyses?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var form = new MultipartFormDataContent();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/parking-lots/{ParkingDbContext.DemoLotId}/analyses", form)).StatusCode);
    }
    [Fact]
    public async Task UploadReturnsPersistedDetectionsAndArchivedImageOverHttp()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        var bytes = new byte[] { 1, 2, 3 };
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "image", "lot.png");
        var response = await client.PostAsync($"/api/parking-lots/{ParkingDbContext.DemoLotId}/analyses", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AnalysisDto>())!;
        Assert.Equal(1, result.VehicleCount); Assert.Null(result.AvailableSpaces);
        var saved = await client.GetAsync(response.Headers.Location);
        Assert.Equal(await response.Content.ReadAsStringAsync(), await saved.Content.ReadAsStringAsync());
        var image = await client.GetAsync(result.ImageUrl);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await image.Content.ReadAsByteArrayAsync());
        var wrongLot = await client.GetAsync($"/api/parking-lots/{Guid.NewGuid()}/analyses/{result.Id}/image");
        Assert.Equal(HttpStatusCode.NotFound, wrongLot.StatusCode);
    }

    // HTTP tests isolate inference; real YOLO decoding is verified by Python and the smoke test.
    private sealed class TestAnalyzer : IAiAnalyzer
    {
        public Task<AnalyzerResponse> AnalyzeAsync(Microsoft.AspNetCore.Http.IFormFile image, CancellationToken ct) =>
            Task.FromResult(new AnalyzerResponse("test", "vehicle-detection", 100, 100,
                [new VehicleDetectionDto(1, "car", .9, new BoundingBoxDto(.1, .2, .3, .4))]));
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public ApiFactory() => connection.Open();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ParkingDb"] = "unused",
                ["AiService:BaseUrl"] = "http://analyzer/",
                ["Database:ApplyMigrations"] = "false"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ParkingDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ParkingDbContext>>();
                services.AddDbContext<ParkingDbContext>(options => options.UseSqlite(connection));
                services.RemoveAll<IAiAnalyzer>();
                services.AddSingleton<IAiAnalyzer, TestAnalyzer>();
                using var scope = services.BuildServiceProvider().CreateScope();
                scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.EnsureCreated();
            });
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
    }
}
