using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerDeliveryLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryFormattedAddress",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryHours",
                table: "Customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryLatitude",
                table: "Customers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryLongitude",
                table: "Customers",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DeliveryWindowEnd",
                table: "Customers",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DeliveryWindowStart",
                table: "Customers",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Customers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeliveryFormattedAddress",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeliveryHours",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeliveryLatitude",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeliveryLongitude",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeliveryWindowEnd",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DeliveryWindowStart",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Customers");
        }
    }
}
