using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartParking.Api.Data;

public sealed class ParkingDbContextFactory : IDesignTimeDbContextFactory<ParkingDbContext>
{
    public ParkingDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ParkingDb")
            ?? throw new InvalidOperationException("Set ConnectionStrings__ParkingDb before running EF tools.");
        return new(new DbContextOptionsBuilder<ParkingDbContext>().UseNpgsql(connection).Options);
    }
}
