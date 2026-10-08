using Microsoft.EntityFrameworkCore;
using SmartParking.Api.Data;
using SmartParking.Api.Dtos;
using SmartParking.Api.Models;

namespace SmartParking.Api.Services;

public sealed class ParkingService(ParkingDbContext db, IAiAnalyzer analyzer, TimeProvider clock)
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> VehicleClasses = ["car", "motorcycle", "bus", "truck"];

    public async Task<IReadOnlyList<ParkingLotDto>> GetLotsAsync(CancellationToken ct) =>
        (await db.ParkingLots.AsNoTracking().Include(x => x.Spaces).OrderBy(x => x.Name).ToListAsync(ct)).Select(ToLot).ToList();

    public async Task<ParkingLotDto> GetLotAsync(Guid id, CancellationToken ct) => ToLot(await FindLotAsync(id, ct));

    public async Task<HistoryDto> GetHistoryAsync(Guid id, int page, int pageSize, CancellationToken ct)
    {
        await FindLotAsync(id, ct);
        if (page < 1 || pageSize is < 1 or > 100 || page > 100000)
            throw new ApiException(400, "Invalid pagination. Use page >= 1 and pageSize between 1 and 100.");
        var query = db.AnalysisRuns.AsNoTracking().Where(x => x.ParkingLotId == id);
        var total = await query.CountAsync(ct);
        var runs = await query.Include(x => x.Detections).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(runs.Select(ToAnalysis).ToList(), total, page, pageSize);
    }

    public async Task<AnalysisDto> GetAnalysisAsync(Guid lotId, Guid analysisId, CancellationToken ct)
    {
        var run = await db.AnalysisRuns.AsNoTracking().Include(x => x.Detections)
            .SingleOrDefaultAsync(x => x.Id == analysisId && x.ParkingLotId == lotId, ct)
            ?? throw new ApiException(404, "Analysis not found.");
        return ToAnalysis(run);
    }

    public async Task<ArchivedImageDto> GetImageAsync(Guid lotId, Guid analysisId, CancellationToken ct)
    {
        var image = await db.AnalysisImages.AsNoTracking().SingleOrDefaultAsync(image => image.AnalysisRunId == analysisId &&
            db.AnalysisRuns.Any(run => run.Id == analysisId && run.ParkingLotId == lotId), ct)
            ?? throw new ApiException(404, "Image not available for this analysis.");
        return new(image.Data, image.ContentType);
    }

    public async Task<AnalysisDto> AnalyzeAsync(Guid lotId, IFormFile image, CancellationToken ct)
    {
        await FindLotAsync(lotId, ct);
        if (image.Length is <= 0 or > MaxImageBytes) throw new ApiException(400, "Upload a non-empty image up to 5 MB.");
        if (image.ContentType is not ("image/jpeg" or "image/png")) throw new ApiException(400, "Only JPEG and PNG images are supported.");
        var result = await analyzer.AnalyzeAsync(image, ct);
        ValidateResult(result);
        var imageName = Path.GetFileName(image.FileName.Replace('\\', '/'));
        // PostgreSQL retains microseconds, so POST and subsequent GET must use that precision.
        var createdAt = clock.GetUtcNow();
        createdAt = createdAt.AddTicks(-(createdAt.Ticks % TimeSpan.TicksPerMicrosecond));
        await using var buffer = new MemoryStream();
        await using (var stream = image.OpenReadStream()) await stream.CopyToAsync(buffer, ct);
        var run = new AnalysisRun
        {
            Id = Guid.NewGuid(),
            ParkingLotId = lotId,
            CreatedAt = createdAt,
            ImageName = imageName[..Math.Min(imageName.Length, 255)],
            Analyzer = result.Analyzer,
            Mode = result.Mode,
            ImageWidth = result.ImageWidth,
            ImageHeight = result.ImageHeight,
            Detections = result.Detections.Select(x => new VehicleDetection
            {
                VehicleId = x.VehicleId,
                ClassName = x.ClassName,
                Confidence = x.Confidence,
                X = x.Box.X,
                Y = x.Box.Y,
                Width = x.Box.Width,
                Height = x.Box.Height
            }).ToList(),
            Image = new AnalysisImage { Data = buffer.ToArray(), ContentType = image.ContentType }
        };
        db.AnalysisRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return ToAnalysis(run);
    }

    private static void ValidateResult(AnalyzerResponse result)
    {
        var invalid = string.IsNullOrWhiteSpace(result.Analyzer) || result.Analyzer.Length > 80 ||
            result.Mode != "vehicle-detection" || result.ImageWidth < 1 || result.ImageHeight < 1 ||
            (long)result.ImageWidth * result.ImageHeight > 16_000_000 || result.Detections is null || result.Detections.Count > 300;
        if (!invalid)
        {
            var detections = result.Detections!;
            invalid = detections.Any(x => x is null || x.VehicleId < 1 || !VehicleClasses.Contains(x.ClassName) ||
                !double.IsFinite(x.Confidence) || x.Confidence is < 0 or > 1 || !ValidBox(x.Box)) ||
                detections.Select(x => x.VehicleId).Distinct().Count() != detections.Count;
        }
        if (invalid) throw new ApiException(502, "The analysis service returned invalid vehicle detections.");
    }

    private static bool ValidBox(BoundingBoxDto? box) => box is not null &&
        double.IsFinite(box.X) && double.IsFinite(box.Y) && double.IsFinite(box.Width) && double.IsFinite(box.Height) &&
        box.X >= 0 && box.Y >= 0 && box.Width > 0 && box.Height > 0 &&
        box.X + box.Width <= 1.000001 && box.Y + box.Height <= 1.000001;

    private async Task<ParkingLot> FindLotAsync(Guid id, CancellationToken ct) =>
        await db.ParkingLots.AsNoTracking().Include(x => x.Spaces).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "Parking lot not found.");

    private static ParkingLotDto ToLot(ParkingLot lot) => new(lot.Id, lot.Name,
        lot.Spaces.OrderBy(x => x.Id).Select(x => new SpaceDto(x.Id, x.Label)).ToList());

    private static AnalysisDto ToAnalysis(AnalysisRun run)
    {
        var real = run.Mode == "vehicle-detection";
        var detections = run.Detections.OrderBy(x => x.VehicleId).Select(x => new VehicleDetectionDto(
            x.VehicleId, x.ClassName, x.Confidence, new(x.X, x.Y, x.Width, x.Height))).ToList();
        return new(run.Id, run.ParkingLotId, run.CreatedAt, run.ImageName, run.Analyzer, run.Mode,
            real ? detections.Count : null, run.ImageWidth, run.ImageHeight,
            real ? $"/api/parking-lots/{run.ParkingLotId}/analyses/{run.Id}/image" : null,
            null, null, null, null, detections);
    }
}
