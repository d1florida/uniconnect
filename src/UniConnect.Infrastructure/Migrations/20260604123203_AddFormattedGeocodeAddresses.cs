using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFormattedGeocodeAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormattedAddress",
                table: "GeocodedAddresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryFormattedAddress",
                table: "DeliveryOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupFormattedAddress",
                table: "DeliveryOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormattedAddress",
                table: "GeocodedAddresses");

            migrationBuilder.DropColumn(
                name: "DeliveryFormattedAddress",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "PickupFormattedAddress",
                table: "DeliveryOrders");
        }
    }
}
