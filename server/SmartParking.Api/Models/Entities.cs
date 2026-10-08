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
    public string Mode { get; set; } = "legacy-demo";
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public List<VehicleDetection> Detections { get; set; } = [];
    public AnalysisImage? Image { get; set; }
    public List<OccupancyResult> Results { get; set; } = [];
}

public sealed class OccupancyResult
{
    public Guid AnalysisRunId { get; set; }
    public int ParkingSpaceId { get; set; }
    public bool Occupied { get; set; }
    public double Confidence { get; set; }
}

public sealed class VehicleDetection
{
    public Guid AnalysisRunId { get; set; }
    public int VehicleId { get; set; }
    public required string ClassName { get; set; }
    public double Confidence { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

// Keep image bytes separate so paginated history never loads the image payloads.
public sealed class AnalysisImage
{
    public Guid AnalysisRunId { get; set; }
    public required byte[] Data { get; set; }
    public required string ContentType { get; set; }
}
