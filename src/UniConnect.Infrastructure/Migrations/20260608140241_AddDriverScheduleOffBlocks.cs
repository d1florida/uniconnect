using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverScheduleOffBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "OffBlockEndTime",
                table: "DriverScheduleExceptions",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "OffBlockStartTime",
                table: "DriverScheduleExceptions",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OffBlockEndTime",
                table: "DriverScheduleExceptions");

            migrationBuilder.DropColumn(
                name: "OffBlockStartTime",
                table: "DriverScheduleExceptions");
        }
    }
}
