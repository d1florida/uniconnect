using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleHomeDepotAndRouteDepotId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HomeDepotId",
                table: "Vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepotId",
                table: "RoutePlanRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepotId",
                table: "DeliveryRoutes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_HomeDepotId",
                table: "Vehicles",
                column: "HomeDepotId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutePlanRuns_DepotId",
                table: "RoutePlanRuns",
                column: "DepotId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRoutes_DepotId",
                table: "DeliveryRoutes",
                column: "DepotId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryRoutes_Depots_DepotId",
                table: "DeliveryRoutes",
                column: "DepotId",
                principalTable: "Depots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_Depots_HomeDepotId",
                table: "Vehicles",
                column: "HomeDepotId",
                principalTable: "Depots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryRoutes_Depots_DepotId",
                table: "DeliveryRoutes");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_Depots_HomeDepotId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_HomeDepotId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_RoutePlanRuns_DepotId",
                table: "RoutePlanRuns");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryRoutes_DepotId",
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "HomeDepotId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DepotId",
                table: "RoutePlanRuns");

            migrationBuilder.DropColumn(
                name: "DepotId",
                table: "DeliveryRoutes");
        }
    }
}
