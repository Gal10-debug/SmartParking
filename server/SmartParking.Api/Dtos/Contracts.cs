namespace SmartParking.Api.Dtos;

public sealed record SpaceDto(int Id, string Label);
public sealed record ParkingLotDto(Guid Id, string Name, IReadOnlyList<SpaceDto> Spaces);
public sealed record OccupancyDto(int SpaceId, string Label, bool Occupied, double Confidence);
public sealed record AnalysisDto(Guid Id, Guid ParkingLotId, DateTimeOffset CreatedAt, string ImageName,
    string Analyzer, int TotalSpaces, int OccupiedSpaces, int AvailableSpaces, double OccupancyPercentage,
    IReadOnlyList<OccupancyDto> Spaces);
public sealed record HistoryDto(IReadOnlyList<AnalysisDto> Items, int Total, int Page, int PageSize);
public sealed record AnalyzerResponse(string Analyzer, IReadOnlyList<AnalyzerSpace> Spaces);
public sealed record AnalyzerSpace(int SpaceId, bool Occupied, double Confidence);
