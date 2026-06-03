using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantUserRoleAndModuleAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ModuleAccess",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantRole",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Existing tenant users become active admins with full tenant module access.
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" u
                SET "TenantRole" = 1,
                    "ModuleAccess" = t."Modules",
                    "IsActive" = true
                FROM "Tenants" t
                WHERE u."TenantId" = t."Id";
                """);

            migrationBuilder.Sql("""
                UPDATE "AspNetUsers"
                SET "IsActive" = true
                WHERE "TenantId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ModuleAccess",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TenantRole",
                table: "AspNetUsers");
        }
    }
}
