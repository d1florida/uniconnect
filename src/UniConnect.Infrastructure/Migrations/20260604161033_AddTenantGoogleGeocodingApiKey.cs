using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantGoogleGeocodingApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleApiKeyHint",
                table: "TenantGeocodingSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleApiKeyProtected",
                table: "TenantGeocodingSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleApiKeyHint",
                table: "TenantGeocodingSettings");

            migrationBuilder.DropColumn(
                name: "GoogleApiKeyProtected",
                table: "TenantGeocodingSettings");
        }
    }
}
