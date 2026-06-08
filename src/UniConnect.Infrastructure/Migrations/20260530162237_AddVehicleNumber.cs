using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_TenantId",
                table: "Vehicles");

            migrationBuilder.AddColumn<string>(
                name: "VehicleNumber",
                table: "Vehicles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Vehicles"
                SET "VehicleNumber" = "LicensePlate"
                WHERE "VehicleNumber" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId_VehicleNumber",
                table: "Vehicles",
                columns: new[] { "TenantId", "VehicleNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_TenantId_VehicleNumber",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleNumber",
                table: "Vehicles");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TenantId",
                table: "Vehicles",
                column: "TenantId");
        }
    }
}
