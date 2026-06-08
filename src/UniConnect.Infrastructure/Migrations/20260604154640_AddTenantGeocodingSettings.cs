using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantGeocodingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantGeocodingSettings",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    AllowPostalFallback = table.Column<bool>(type: "boolean", nullable: false),
                    NominatimUserAgent = table.Column<string>(type: "text", nullable: true),
                    NominatimBaseUrl = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantGeocodingSettings", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantGeocodingSettings_Tenants_TenantId",
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
                name: "TenantGeocodingSettings");
        }
    }
}
