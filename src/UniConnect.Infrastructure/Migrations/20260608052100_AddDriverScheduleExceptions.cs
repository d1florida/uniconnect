using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverScheduleExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverScheduleExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsWorking = table.Column<bool>(type: "boolean", nullable: false),
                    ShiftStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ShiftEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    LunchMinutes = table.Column<int>(type: "integer", nullable: false),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxRouteMinutes = table.Column<int>(type: "integer", nullable: true),
                    ReturnByTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverScheduleExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverScheduleExceptions_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverScheduleExceptions_DriverId_Date",
                table: "DriverScheduleExceptions",
                columns: new[] { "DriverId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverScheduleExceptions");
        }
    }
}
