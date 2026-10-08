using System.Text.Json.Serialization;

namespace SmartParking.Api.Dtos;

public sealed record SpaceDto(int Id, string Label);
public sealed record ParkingLotDto(Guid Id, string Name, IReadOnlyList<SpaceDto> Spaces);
public sealed record BoundingBoxDto(
    [property: JsonRequired] double X, [property: JsonRequired] double Y,
    [property: JsonRequired] double Width, [property: JsonRequired] double Height);
public sealed record VehicleDetectionDto(
    [property: JsonRequired] int VehicleId, [property: JsonRequired] string ClassName,
    [property: JsonRequired] double Confidence, [property: JsonRequired] BoundingBoxDto Box);
public sealed record AnalysisDto(Guid Id, Guid ParkingLotId, DateTimeOffset CreatedAt, string ImageName,
    string Analyzer, string Mode, int? VehicleCount, int ImageWidth, int ImageHeight, string? ImageUrl,
    int? TotalSpaces, int? OccupiedSpaces, int? AvailableSpaces, double? OccupancyPercentage,
    IReadOnlyList<VehicleDetectionDto> Detections);
public sealed record HistoryDto(IReadOnlyList<AnalysisDto> Items, int Total, int Page, int PageSize);
public sealed record AnalyzerResponse(
    [property: JsonRequired] string Analyzer, [property: JsonRequired] string Mode,
    [property: JsonRequired] int ImageWidth, [property: JsonRequired] int ImageHeight,
    [property: JsonRequired] IReadOnlyList<VehicleDetectionDto> Detections);
public sealed record ArchivedImageDto(byte[] Data, string ContentType);
