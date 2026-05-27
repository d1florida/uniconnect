using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryFleetForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_FleetId",
                table: "DeliveryOrders",
                column: "FleetId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessAccounts_Fleets_FleetId",
                table: "BusinessAccounts",
                column: "FleetId",
                principalTable: "Fleets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOrders_Fleets_FleetId",
                table: "DeliveryOrders",
                column: "FleetId",
                principalTable: "Fleets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryRoutes_Fleets_FleetId",
                table: "DeliveryRoutes",
                column: "FleetId",
                principalTable: "Fleets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessAccounts_Fleets_FleetId",
                table: "BusinessAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOrders_Fleets_FleetId",
                table: "DeliveryOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryRoutes_Fleets_FleetId",
                table: "DeliveryRoutes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryOrders_FleetId",
                table: "DeliveryOrders");
        }
    }
}
