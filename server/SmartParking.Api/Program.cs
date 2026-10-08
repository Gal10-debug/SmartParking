using Microsoft.EntityFrameworkCore;
using SmartParking.Api.Data;
using SmartParking.Api.Middleware;
using SmartParking.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ParkingDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("ParkingDb")
        ?? throw new InvalidOperationException("Set ConnectionStrings__ParkingDb.")));
builder.Services.AddScoped<ParkingService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IAiAnalyzer, AiAnalyzerClient>(client =>
{
    var aiUrl = builder.Configuration["AiService:BaseUrl"]
        ?? throw new InvalidOperationException("Set AiService__BaseUrl.");
    client.BaseAddress = new Uri(aiUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
var app = builder.Build();
app.UseExceptionHandler();
app.UseCors();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", async (ParkingDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync();
}
await app.RunAsync();
public partial class Program;
