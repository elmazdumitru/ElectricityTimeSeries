using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectricityTimeSeries.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261122235329_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TimeSeries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                BiddingZone = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                ToBiddingZone = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                Resolution = table.Column<TimeSpan>(type: "time", nullable: false),
                Start = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                End = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TimeSeries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TimeSeriesPoints",
            columns: table => new
            {
                Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                TimeSeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Value = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TimeSeriesPoints", x => new { x.TimeSeriesId, x.Timestamp });
                table.ForeignKey(
                    name: "FK_TimeSeriesPoints_TimeSeries_TimeSeriesId",
                    column: x => x.TimeSeriesId,
                    principalTable: "TimeSeries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TimeSeries_BiddingZone_Start_End",
            table: "TimeSeries",
            columns: new[] { "BiddingZone", "Start", "End" });

        migrationBuilder.CreateIndex(
            name: "IX_TimeSeries_ToBiddingZone_Start_End",
            table: "TimeSeries",
            columns: new[] { "ToBiddingZone", "Start", "End" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TimeSeriesPoints");

        migrationBuilder.DropTable(
            name: "TimeSeries");
    }
}
