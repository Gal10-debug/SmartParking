using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using SmartParking.Api.Data;
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
                using var scope = services.BuildServiceProvider().CreateScope();
                scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.EnsureCreated();
            });
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
    }
}
