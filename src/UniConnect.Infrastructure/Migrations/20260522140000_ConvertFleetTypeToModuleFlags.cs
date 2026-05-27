using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations;

public partial class ConvertFleetTypeToModuleFlags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Legacy single-product values (0=General, 1=RoboTaxi, 2=Delivery) → flags (1, 2, 4).
        migrationBuilder.Sql("""
            UPDATE "Fleets" SET "FleetType" = CASE "FleetType"
                WHEN 0 THEN 1
                WHEN 1 THEN 2
                WHEN 2 THEN 4
                ELSE "FleetType"
            END
            WHERE "FleetType" IN (0, 1, 2);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Fleets" SET "FleetType" = CASE
                WHEN ("FleetType" & 1) = 1 AND ("FleetType" & 6) = 0 THEN 0
                WHEN ("FleetType" & 2) = 2 AND ("FleetType" & 5) = 0 THEN 1
                WHEN ("FleetType" & 4) = 4 AND ("FleetType" & 3) = 0 THEN 2
                ELSE 0
            END;
            """);
    }
}
