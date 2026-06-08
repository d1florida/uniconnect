using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningRulesAndDriverCaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxRouteMinutes",
                table: "Drivers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReturnByTime",
                table: "Drivers",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantPlanningRules",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Markdown = table.Column<string>(type: "text", nullable: false),
                    CompiledPolicyJson = table.Column<string>(type: "text", nullable: true),
                    CompiledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompileWarningsJson = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPlanningRules", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantPlanningRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantPlanningRules");

            migrationBuilder.DropColumn(
                name: "MaxRouteMinutes",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ReturnByTime",
                table: "Drivers");
        }
    }
}
