using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCustomerNoDeliveryDatesWithHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoDeliveryDates",
                table: "Customers");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "NoDeliveryEnd",
                table: "Customers",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "NoDeliveryStart",
                table: "Customers",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoDeliveryEnd",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "NoDeliveryStart",
                table: "Customers");

            migrationBuilder.AddColumn<string>(
                name: "NoDeliveryDates",
                table: "Customers",
                type: "text",
                nullable: true);
        }
    }
}
