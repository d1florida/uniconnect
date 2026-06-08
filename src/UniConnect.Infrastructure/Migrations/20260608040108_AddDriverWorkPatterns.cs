using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverWorkPatterns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverWorkPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "boolean", nullable: false),
                    ShiftStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ShiftEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    LunchMinutes = table.Column<int>(type: "integer", nullable: false),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxRouteMinutes = table.Column<int>(type: "integer", nullable: true),
                    ReturnByTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverWorkPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverWorkPatterns_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantDeliverySettings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowMultipleRoutesPerDriverPerDay = table.Column<bool>(type: "boolean", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDeliverySettings", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantDeliverySettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverWorkPatterns_DriverId_DayOfWeek",
                table: "DriverWorkPatterns",
                columns: new[] { "DriverId", "DayOfWeek" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "DriverWorkPatterns" (
                    "Id", "DriverId", "DayOfWeek", "IsWorkingDay",
                    "ShiftStartTime", "ShiftEndTime", "LunchMinutes", "BreakMinutes",
                    "MaxRouteMinutes", "ReturnByTime")
                SELECT
                    gen_random_uuid(),
                    d."Id",
                    dow,
                    dow BETWEEN 1 AND 5,
                    d."ShiftStartTime",
                    d."ShiftEndTime",
                    d."LunchMinutes",
                    d."BreakMinutes",
                    d."MaxRouteMinutes",
                    d."ReturnByTime"
                FROM "Drivers" d
                CROSS JOIN generate_series(0, 6) AS dow
                WHERE NOT EXISTS (
                    SELECT 1 FROM "DriverWorkPatterns" p WHERE p."DriverId" = d."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverWorkPatterns");

            migrationBuilder.DropTable(
                name: "TenantDeliverySettings");
        }
    }
}
