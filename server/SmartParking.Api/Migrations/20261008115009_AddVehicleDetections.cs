using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartParking.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleDetections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImageHeight",
                table: "AnalysisRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ImageWidth",
                table: "AnalysisRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Mode",
                table: "AnalysisRuns",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "legacy-demo");

            migrationBuilder.CreateTable(
                name: "AnalysisImages",
                columns: table => new
                {
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisImages", x => x.AnalysisRunId);
                    table.ForeignKey(
                        name: "FK_AnalysisImages_AnalysisRuns_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "AnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleDetections",
                columns: table => new
                {
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    ClassName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    Width = table.Column<double>(type: "double precision", nullable: false),
                    Height = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleDetections", x => new { x.AnalysisRunId, x.VehicleId });
                    table.ForeignKey(
                        name: "FK_VehicleDetections_AnalysisRuns_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "AnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisImages");

            migrationBuilder.DropTable(
                name: "VehicleDetections");

            migrationBuilder.DropColumn(
                name: "ImageHeight",
                table: "AnalysisRuns");

            migrationBuilder.DropColumn(
                name: "ImageWidth",
                table: "AnalysisRuns");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "AnalysisRuns");
        }
    }
}
