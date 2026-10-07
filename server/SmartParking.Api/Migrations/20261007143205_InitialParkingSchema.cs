using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartParking.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialParkingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParkingLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingLots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImageName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Analyzer = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisRuns_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParkingSpaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParkingLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingSpaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingSpaces_ParkingLots_ParkingLotId",
                        column: x => x.ParkingLotId,
                        principalTable: "ParkingLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OccupancyResults",
                columns: table => new
                {
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingSpaceId = table.Column<int>(type: "integer", nullable: false),
                    Occupied = table.Column<bool>(type: "boolean", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OccupancyResults", x => new { x.AnalysisRunId, x.ParkingSpaceId });
                    table.ForeignKey(
                        name: "FK_OccupancyResults_AnalysisRuns_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "AnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OccupancyResults_ParkingSpaces_ParkingSpaceId",
                        column: x => x.ParkingSpaceId,
                        principalTable: "ParkingSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ParkingLots",
                columns: new[] { "Id", "Name" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), "Central Park · Lot A" });

            migrationBuilder.InsertData(
                table: "ParkingSpaces",
                columns: new[] { "Id", "Label", "ParkingLotId" },
                values: new object[,]
                {
                    { 1, "A01", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 2, "A02", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 3, "A03", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 4, "A04", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 5, "A05", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 6, "A06", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 7, "A07", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 8, "A08", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 9, "A09", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 10, "A10", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 11, "A11", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 12, "A12", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 13, "B01", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 14, "B02", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 15, "B03", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 16, "B04", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 17, "B05", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 18, "B06", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 19, "B07", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 20, "B08", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 21, "B09", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 22, "B10", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 23, "B11", new Guid("11111111-1111-1111-1111-111111111111") },
                    { 24, "B12", new Guid("11111111-1111-1111-1111-111111111111") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRuns_ParkingLotId_CreatedAt",
                table: "AnalysisRuns",
                columns: new[] { "ParkingLotId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OccupancyResults_ParkingSpaceId",
                table: "OccupancyResults",
                column: "ParkingSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSpaces_ParkingLotId_Label",
                table: "ParkingSpaces",
                columns: new[] { "ParkingLotId", "Label" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OccupancyResults");

            migrationBuilder.DropTable(
                name: "AnalysisRuns");

            migrationBuilder.DropTable(
                name: "ParkingSpaces");

            migrationBuilder.DropTable(
                name: "ParkingLots");
        }
    }
}
