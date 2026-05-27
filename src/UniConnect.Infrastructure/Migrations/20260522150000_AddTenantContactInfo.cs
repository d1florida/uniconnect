using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniConnect.Infrastructure.Migrations;

public partial class AddTenantContactInfo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ContactEmail",
            table: "Fleets",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ContactName",
            table: "Fleets",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ContactPhone",
            table: "Fleets",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ContactEmail", table: "Fleets");
        migrationBuilder.DropColumn(name: "ContactName", table: "Fleets");
        migrationBuilder.DropColumn(name: "ContactPhone", table: "Fleets");
    }
}
