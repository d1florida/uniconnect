using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixedRouteMultipleDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "RouteDays",
                table: "FixedRouteTemplates",
                type: "integer[]",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "FixedRouteTemplates"
                SET "RouteDays" = ARRAY["DayOfWeek"]
                WHERE "RouteDays" IS NULL;
                """);

            migrationBuilder.AlterColumn<int[]>(
                name: "RouteDays",
                table: "FixedRouteTemplates",
                type: "integer[]",
                nullable: false,
                oldClrType: typeof(int[]),
                oldType: "integer[]",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "FixedRouteTemplates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DayOfWeek",
                table: "FixedRouteTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "FixedRouteTemplates"
                SET "DayOfWeek" = COALESCE("RouteDays"[1], 0)
                WHERE cardinality("RouteDays") > 0;
                """);

            migrationBuilder.DropColumn(
                name: "RouteDays",
                table: "FixedRouteTemplates");
        }
    }
}
