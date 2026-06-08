using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FixedRouteTemplateId",
                table: "RoutePlanRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FixedRouteTemplateId",
                table: "DeliveryRoutes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FixedRouteTemplateId",
                table: "DeliveryOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "HeldUntil",
                table: "DeliveryOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryZoneId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeliveryZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MatchType = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryZones_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedRouteTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DeliveryZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    DepotId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultVehicleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultDriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedRouteTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedRouteTemplates_DeliveryZones_DeliveryZoneId",
                        column: x => x.DeliveryZoneId,
                        principalTable: "DeliveryZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedRouteTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoutePlanRuns_FixedRouteTemplateId",
                table: "RoutePlanRuns",
                column: "FixedRouteTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRoutes_FixedRouteTemplateId",
                table: "DeliveryRoutes",
                column: "FixedRouteTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_FixedRouteTemplateId",
                table: "DeliveryOrders",
                column: "FixedRouteTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_DeliveryZoneId",
                table: "Customers",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryZones_TenantId_Name",
                table: "DeliveryZones",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedRouteTemplates_DeliveryZoneId",
                table: "FixedRouteTemplates",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedRouteTemplates_TenantId_DeliveryZoneId",
                table: "FixedRouteTemplates",
                columns: new[] { "TenantId", "DeliveryZoneId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixedRouteTemplates");

            migrationBuilder.DropTable(
                name: "DeliveryZones");

            migrationBuilder.DropIndex(
                name: "IX_RoutePlanRuns_FixedRouteTemplateId",
                table: "RoutePlanRuns");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryRoutes_FixedRouteTemplateId",
                table: "DeliveryRoutes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryOrders_FixedRouteTemplateId",
                table: "DeliveryOrders");

            migrationBuilder.DropIndex(
                name: "IX_Customers_DeliveryZoneId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "FixedRouteTemplateId",
                table: "RoutePlanRuns");

            migrationBuilder.DropColumn(
                name: "FixedRouteTemplateId",
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "FixedRouteTemplateId",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "HeldUntil",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryZoneId",
                table: "Customers");
        }
    }
}
