using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations;

public partial class RenameFleetToTenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
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

        migrationBuilder.DropForeignKey(
            name: "FK_Vehicles_Fleets_FleetId",
            table: "Vehicles");

        migrationBuilder.RenameTable(
            name: "Fleets",
            newName: "Tenants");

        migrationBuilder.RenameColumn(
            name: "FleetType",
            table: "Tenants",
            newName: "Modules");

        migrationBuilder.RenameColumn(
            name: "FleetId",
            table: "BusinessAccounts",
            newName: "TenantId");

        migrationBuilder.RenameColumn(
            name: "FleetId",
            table: "DeliveryOrders",
            newName: "TenantId");

        migrationBuilder.RenameColumn(
            name: "FleetId",
            table: "DeliveryRoutes",
            newName: "TenantId");

        migrationBuilder.RenameColumn(
            name: "FleetId",
            table: "Vehicles",
            newName: "TenantId");

        migrationBuilder.RenameColumn(
            name: "FleetId",
            table: "AspNetUsers",
            newName: "TenantId");

        migrationBuilder.RenameIndex(
            name: "IX_BusinessAccounts_FleetId_AccountCode",
            table: "BusinessAccounts",
            newName: "IX_BusinessAccounts_TenantId_AccountCode");

        migrationBuilder.RenameIndex(
            name: "IX_DeliveryOrders_FleetId",
            table: "DeliveryOrders",
            newName: "IX_DeliveryOrders_TenantId");

        migrationBuilder.RenameIndex(
            name: "IX_DeliveryRoutes_FleetId_ScheduledDate",
            table: "DeliveryRoutes",
            newName: "IX_DeliveryRoutes_TenantId_ScheduledDate");

        migrationBuilder.RenameIndex(
            name: "IX_Vehicles_FleetId",
            table: "Vehicles",
            newName: "IX_Vehicles_TenantId");

        migrationBuilder.RenameIndex(
            name: "IX_AspNetUsers_FleetId",
            table: "AspNetUsers",
            newName: "IX_AspNetUsers_TenantId");

        migrationBuilder.AddForeignKey(
            name: "FK_BusinessAccounts_Tenants_TenantId",
            table: "BusinessAccounts",
            column: "TenantId",
            principalTable: "Tenants",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_DeliveryOrders_Tenants_TenantId",
            table: "DeliveryOrders",
            column: "TenantId",
            principalTable: "Tenants",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_DeliveryRoutes_Tenants_TenantId",
            table: "DeliveryRoutes",
            column: "TenantId",
            principalTable: "Tenants",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Vehicles_Tenants_TenantId",
            table: "Vehicles",
            column: "TenantId",
            principalTable: "Tenants",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BusinessAccounts_Tenants_TenantId",
            table: "BusinessAccounts");

        migrationBuilder.DropForeignKey(
            name: "FK_DeliveryOrders_Tenants_TenantId",
            table: "DeliveryOrders");

        migrationBuilder.DropForeignKey(
            name: "FK_DeliveryRoutes_Tenants_TenantId",
            table: "DeliveryRoutes");

        migrationBuilder.DropForeignKey(
            name: "FK_Vehicles_Tenants_TenantId",
            table: "Vehicles");

        migrationBuilder.RenameIndex(
            name: "IX_AspNetUsers_TenantId",
            table: "AspNetUsers",
            newName: "IX_AspNetUsers_FleetId");

        migrationBuilder.RenameIndex(
            name: "IX_Vehicles_TenantId",
            table: "Vehicles",
            newName: "IX_Vehicles_FleetId");

        migrationBuilder.RenameIndex(
            name: "IX_DeliveryRoutes_TenantId_ScheduledDate",
            table: "DeliveryRoutes",
            newName: "IX_DeliveryRoutes_FleetId_ScheduledDate");

        migrationBuilder.RenameIndex(
            name: "IX_DeliveryOrders_TenantId",
            table: "DeliveryOrders",
            newName: "IX_DeliveryOrders_FleetId");

        migrationBuilder.RenameIndex(
            name: "IX_BusinessAccounts_TenantId_AccountCode",
            table: "BusinessAccounts",
            newName: "IX_BusinessAccounts_FleetId_AccountCode");

        migrationBuilder.RenameColumn(
            name: "TenantId",
            table: "AspNetUsers",
            newName: "FleetId");

        migrationBuilder.RenameColumn(
            name: "TenantId",
            table: "Vehicles",
            newName: "FleetId");

        migrationBuilder.RenameColumn(
            name: "TenantId",
            table: "DeliveryRoutes",
            newName: "FleetId");

        migrationBuilder.RenameColumn(
            name: "TenantId",
            table: "DeliveryOrders",
            newName: "FleetId");

        migrationBuilder.RenameColumn(
            name: "TenantId",
            table: "BusinessAccounts",
            newName: "FleetId");

        migrationBuilder.RenameColumn(
            name: "Modules",
            table: "Tenants",
            newName: "FleetType");

        migrationBuilder.RenameTable(
            name: "Tenants",
            newName: "Fleets");

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

        migrationBuilder.AddForeignKey(
            name: "FK_Vehicles_Fleets_FleetId",
            table: "Vehicles",
            column: "FleetId",
            principalTable: "Fleets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
