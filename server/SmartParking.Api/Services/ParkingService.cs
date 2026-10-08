using Microsoft.EntityFrameworkCore;
using SmartParking.Api.Data;
using SmartParking.Api.Dtos;
using SmartParking.Api.Models;

namespace SmartParking.Api.Services;

public sealed class ParkingService(ParkingDbContext db, IAiAnalyzer analyzer, TimeProvider clock)
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public async Task<IReadOnlyList<ParkingLotDto>> GetLotsAsync(CancellationToken ct) =>
        (await db.ParkingLots.AsNoTracking().Include(x => x.Spaces).OrderBy(x => x.Name).ToListAsync(ct)).Select(ToLot).ToList();

    public async Task<ParkingLotDto> GetLotAsync(Guid id, CancellationToken ct) => ToLot(await FindLotAsync(id, ct));

    public async Task<HistoryDto> GetHistoryAsync(Guid id, int page, int pageSize, CancellationToken ct)
    {
        await FindLotAsync(id, ct);
        if (page < 1 || pageSize is < 1 or > 100 || page > 100000) throw new ApiException(400, "Invalid pagination. Use page >= 1 and pageSize between 1 and 100.");
        var query = db.AnalysisRuns.AsNoTracking().Where(x => x.ParkingLotId == id);
        var total = await query.CountAsync(ct);
        var runs = await query.Include(x => x.Results).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var labels = await db.ParkingSpaces.Where(x => x.ParkingLotId == id).ToDictionaryAsync(x => x.Id, x => x.Label, ct);
        return new(runs.Select(x => ToAnalysis(x, labels)).ToList(), total, page, pageSize);
    }

    public async Task<AnalysisDto> GetAnalysisAsync(Guid lotId, Guid analysisId, CancellationToken ct)
    {
        var lot = await FindLotAsync(lotId, ct);
        var run = await db.AnalysisRuns.AsNoTracking().Include(x => x.Results)
            .SingleOrDefaultAsync(x => x.Id == analysisId && x.ParkingLotId == lotId, ct)
            ?? throw new ApiException(404, "Analysis not found.");
        return ToAnalysis(run, lot.Spaces.ToDictionary(x => x.Id, x => x.Label));
    }

    public async Task<AnalysisDto> AnalyzeAsync(Guid lotId, IFormFile image, CancellationToken ct)
    {
        var lot = await FindLotAsync(lotId, ct);
        if (image.Length is <= 0 or > MaxImageBytes) throw new ApiException(400, "Upload a non-empty image up to 5 MB.");
        if (image.ContentType is not ("image/jpeg" or "image/png")) throw new ApiException(400, "Only JPEG and PNG images are supported.");
        var ids = lot.Spaces.Select(x => x.Id).Order().ToArray();
        if (ids.Length == 0) throw new ApiException(409, "This parking lot has no configured spaces.");
        var result = await analyzer.AnalyzeAsync(image, ids, ct);
        if (string.IsNullOrWhiteSpace(result.Analyzer) || result.Analyzer.Length > 80 || result.Spaces is null ||
            result.Spaces.Any(x => x is null) ||
            !result.Spaces.Select(x => x.SpaceId).Order().SequenceEqual(ids) ||
            result.Spaces.Any(x => !double.IsFinite(x.Confidence) || x.Confidence is < 0 or > 1))
            throw new ApiException(502, "The analysis service returned inconsistent parking-space results.");
        var imageName = Path.GetFileName(image.FileName.Replace('\\', '/'));
        // PostgreSQL retains microseconds, so POST and subsequent GET must use that precision.
        var createdAt = clock.GetUtcNow();
        createdAt = createdAt.AddTicks(-(createdAt.Ticks % TimeSpan.TicksPerMicrosecond));
        var run = new AnalysisRun
        {
            Id = Guid.NewGuid(),
            ParkingLotId = lot.Id,
            CreatedAt = createdAt,
            ImageName = imageName[..Math.Min(imageName.Length, 255)],
            Analyzer = result.Analyzer,
            Results = result.Spaces.Select(x => new OccupancyResult { ParkingSpaceId = x.SpaceId, Occupied = x.Occupied, Confidence = x.Confidence }).ToList()
        };
        db.AnalysisRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return ToAnalysis(run, lot.Spaces.ToDictionary(x => x.Id, x => x.Label));
    }

    private async Task<ParkingLot> FindLotAsync(Guid id, CancellationToken ct) =>
        await db.ParkingLots.AsNoTracking().Include(x => x.Spaces).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "Parking lot not found.");
    private static ParkingLotDto ToLot(ParkingLot lot) => new(lot.Id, lot.Name, lot.Spaces.OrderBy(x => x.Id).Select(x => new SpaceDto(x.Id, x.Label)).ToList());
    private static AnalysisDto ToAnalysis(AnalysisRun run, IReadOnlyDictionary<int, string> labels)
    {
        var spaces = run.Results.OrderBy(x => x.ParkingSpaceId).Select(x => new OccupancyDto(x.ParkingSpaceId, labels[x.ParkingSpaceId], x.Occupied, x.Confidence)).ToList();
        var occupied = spaces.Count(x => x.Occupied);
        return new(run.Id, run.ParkingLotId, run.CreatedAt, run.ImageName, run.Analyzer, spaces.Count, occupied,
            spaces.Count - occupied, spaces.Count == 0 ? 0 : Math.Round(100.0 * occupied / spaces.Count, 1), spaces);
    }
}
