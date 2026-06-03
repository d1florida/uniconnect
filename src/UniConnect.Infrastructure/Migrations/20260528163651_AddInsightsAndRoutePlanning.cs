using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInsightsAndRoutePlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryOrderId",
                table: "DeliveryRouteStops",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoutePlanRunId",
                table: "DeliveryRoutes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "DeliveryOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "DeliveryOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "DeliveryOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "DeliveryOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "DeliveryAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    ExternalRef = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationalEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SchemaVersion = table.Column<string>(type: "text", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    StopId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverLabel = table.Column<string>(type: "text", nullable: true),
                    VehicleLabel = table.Column<string>(type: "text", nullable: true),
                    CustomerLabel = table.Column<string>(type: "text", nullable: true),
                    PlannerLabel = table.Column<string>(type: "text", nullable: true),
                    Narrative = table.Column<string>(type: "text", nullable: true),
                    MetricsJson = table.Column<string>(type: "text", nullable: false),
                    ContextJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoutePlanRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepotAddress = table.Column<string>(type: "text", nullable: false),
                    OrdersRequested = table.Column<int>(type: "integer", nullable: false),
                    OrdersPlanned = table.Column<int>(type: "integer", nullable: false),
                    OrdersUnassigned = table.Column<int>(type: "integer", nullable: false),
                    ProposalCount = table.Column<int>(type: "integer", nullable: false),
                    ComputeDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ProposalJson = table.Column<string>(type: "text", nullable: true),
                    DiscardReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutePlanRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRouteStops_DeliveryOrderId",
                table: "DeliveryRouteStops",
                column: "DeliveryOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_CustomerId",
                table: "DeliveryOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_DriverId",
                table: "DeliveryAssignments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Name_Phone",
                table: "Customers",
                columns: new[] { "TenantId", "Name", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TenantId",
                table: "Drivers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_UserId",
                table: "Drivers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_CustomerId",
                table: "OperationalEvents",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_DriverId",
                table: "OperationalEvents",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_PlanRunId",
                table: "OperationalEvents",
                column: "PlanRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_TenantId_Domain_OccurredAt",
                table: "OperationalEvents",
                columns: new[] { "TenantId", "Domain", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_TenantId_OccurredAt",
                table: "OperationalEvents",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_UserId",
                table: "OperationalEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvents_VehicleId",
                table: "OperationalEvents",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutePlanRuns_RequestedByUserId",
                table: "RoutePlanRuns",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutePlanRuns_TenantId_CreatedAt",
                table: "RoutePlanRuns",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropTable(
                name: "OperationalEvents");

            migrationBuilder.DropTable(
                name: "RoutePlanRuns");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryRouteStops_DeliveryOrderId",
                table: "DeliveryRouteStops");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryOrders_CustomerId",
                table: "DeliveryOrders");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryAssignments_DriverId",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "DeliveryOrderId",
                table: "DeliveryRouteStops");

            migrationBuilder.DropColumn(
                name: "RoutePlanRunId",
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "DeliveryAssignments");
        }
    }
}
