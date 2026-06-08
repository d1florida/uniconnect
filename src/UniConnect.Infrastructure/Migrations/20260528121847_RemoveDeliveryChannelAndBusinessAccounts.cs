using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeliveryChannelAndBusinessAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "BusinessApiKeys";""");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOrders_BusinessAccounts_BusinessAccountId",
                table: "DeliveryOrders");

            migrationBuilder.DropTable(
                name: "BusinessAccounts");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryOrders_BusinessAccountId",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "BusinessAccountId",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "DeliveryOrders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessAccountId",
                table: "DeliveryOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "DeliveryOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BusinessAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountCode = table.Column<string>(type: "text", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessAccounts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_BusinessAccountId",
                table: "DeliveryOrders",
                column: "BusinessAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAccounts_TenantId_AccountCode",
                table: "BusinessAccounts",
                columns: new[] { "TenantId", "AccountCode" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOrders_BusinessAccounts_BusinessAccountId",
                table: "DeliveryOrders",
                column: "BusinessAccountId",
                principalTable: "BusinessAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
