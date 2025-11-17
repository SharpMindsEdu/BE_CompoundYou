using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRiftboundCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "riftbound_card",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reference_id = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    effect = table.Column<string>(type: "text", nullable: true),
                    color = table.Column<List<string>>(type: "text[]", nullable: true),
                    cost = table.Column<int>(type: "integer", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    might = table.Column<int>(type: "integer", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: true),
                    set_name = table.Column<string>(type: "text", nullable: true),
                    rarity = table.Column<string>(type: "text", nullable: true),
                    cycle = table.Column<string>(type: "text", nullable: true),
                    image = table.Column<string>(type: "text", nullable: true),
                    promo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_card", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_card_reference_id",
                schema: "public",
                table: "riftbound_card",
                column: "reference_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "riftbound_card",
                schema: "public");
        }
    }
}
