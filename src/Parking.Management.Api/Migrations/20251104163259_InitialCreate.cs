using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parking.Management.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GarageSectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxCapacity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarageSectors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GarageSpots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpotId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SectorName = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Lat = table.Column<double>(type: "float", nullable: false),
                    Lng = table.Column<double>(type: "float", nullable: false),
                    IsOccupied = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarageSpots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParkingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicensePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntryTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExitTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SectorName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    SpotId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lat = table.Column<double>(type: "float", nullable: true),
                    Lng = table.Column<double>(type: "float", nullable: true),
                    AppliedBasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GarageSectors_Name",
                table: "GarageSectors",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GarageSpots_SectorName_IsOccupied",
                table: "GarageSpots",
                columns: new[] { "SectorName", "IsOccupied" });

            migrationBuilder.CreateIndex(
                name: "IX_GarageSpots_SpotId",
                table: "GarageSpots",
                column: "SpotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_ExitTime",
                table: "ParkingSessions",
                column: "ExitTime");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_LicensePlate",
                table: "ParkingSessions",
                column: "LicensePlate");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingSessions_SectorName_EntryTime",
                table: "ParkingSessions",
                columns: new[] { "SectorName", "EntryTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GarageSectors");

            migrationBuilder.DropTable(
                name: "GarageSpots");

            migrationBuilder.DropTable(
                name: "ParkingSessions");
        }
    }
}
