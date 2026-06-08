using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderGeocodingAndGeocodeCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryLatitude",
                table: "DeliveryOrders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryLongitude",
                table: "DeliveryOrders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupLatitude",
                table: "DeliveryOrders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupLongitude",
                table: "DeliveryOrders",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GeocodedAddresses",
                columns: table => new
                {
                    NormalizedAddress = table.Column<string>(type: "text", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric", nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    GeocodedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeocodedAddresses", x => x.NormalizedAddress);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeocodedAddresses");

            migrationBuilder.DropColumn(
                name: "DeliveryLatitude",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryLongitude",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "PickupLatitude",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "PickupLongitude",
                table: "DeliveryOrders");
        }
    }
}
