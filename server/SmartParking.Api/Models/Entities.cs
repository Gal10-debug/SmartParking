namespace SmartParking.Api.Models;

public sealed class ParkingLot
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public List<ParkingSpace> Spaces { get; set; } = [];
}

public sealed class ParkingSpace
{
    public int Id { get; set; }
    public Guid ParkingLotId { get; set; }
    public required string Label { get; set; }
}

public sealed class AnalysisRun
{
    public Guid Id { get; set; }
    public Guid ParkingLotId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public required string ImageName { get; set; }
    public required string Analyzer { get; set; }
    public List<OccupancyResult> Results { get; set; } = [];
}

public sealed class OccupancyResult
{
    public Guid AnalysisRunId { get; set; }
    public int ParkingSpaceId { get; set; }
    public bool Occupied { get; set; }
    public double Confidence { get; set; }
}
