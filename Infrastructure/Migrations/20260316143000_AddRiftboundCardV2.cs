using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddRiftboundCardV2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "gameplay_keywords",
                schema: "public",
                table: "riftbound_card",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "public",
                table: "riftbound_card",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "supertype",
                schema: "public",
                table: "riftbound_card",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_card_is_active",
                schema: "public",
                table: "riftbound_card",
                column: "is_active");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_riftbound_card_is_active",
                schema: "public",
                table: "riftbound_card");

            migrationBuilder.DropColumn(
                name: "gameplay_keywords",
                schema: "public",
                table: "riftbound_card");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "public",
                table: "riftbound_card");

            migrationBuilder.DropColumn(
                name: "supertype",
                schema: "public",
                table: "riftbound_card");
        }
    }
}
