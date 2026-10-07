using Microsoft.AspNetCore.Mvc;
using SmartParking.Api.Dtos;
using SmartParking.Api.Services;

namespace SmartParking.Api.Controllers;

[ApiController]
[Route("api/parking-lots")]
public sealed class ParkingLotsController(ParkingService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParkingLotDto>>> GetLots(CancellationToken ct) => Ok(await service.GetLotsAsync(ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParkingLotDto>> GetLot(Guid id, CancellationToken ct) => Ok(await service.GetLotAsync(id, ct));
    [HttpGet("{id:guid}/analyses")]
    public async Task<ActionResult<HistoryDto>> GetHistory(Guid id, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await service.GetHistoryAsync(id, page, pageSize, ct));
    [HttpGet("{id:guid}/analyses/{analysisId:guid}")]
    public async Task<ActionResult<AnalysisDto>> GetAnalysis(Guid id, Guid analysisId, CancellationToken ct) => Ok(await service.GetAnalysisAsync(id, analysisId, ct));
    [HttpPost("{id:guid}/analyses")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<ActionResult<AnalysisDto>> Analyze(Guid id, IFormFile image, CancellationToken ct)
    {
        var result = await service.AnalyzeAsync(id, image, ct);
        return CreatedAtAction(nameof(GetAnalysis), new { id, analysisId = result.Id }, result);
    }
}
