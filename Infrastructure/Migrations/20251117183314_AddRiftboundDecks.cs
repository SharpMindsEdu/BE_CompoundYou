using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRiftboundDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "riftbound_deck",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    colors = table.Column<List<string>>(type: "text[]", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    legend_id = table.Column<long>(type: "bigint", nullable: false),
                    champion_id = table.Column<long>(type: "bigint", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_deck", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_riftbound_card_champion_id",
                        column: x => x.champion_id,
                        principalSchema: "public",
                        principalTable: "riftbound_card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_riftbound_card_legend_id",
                        column: x => x.legend_id,
                        principalSchema: "public",
                        principalTable: "riftbound_card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_deck_card",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    deck_id = table.Column<long>(type: "bigint", nullable: false),
                    card_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_deck_card", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_card_riftbound_card_card_id",
                        column: x => x.card_id,
                        principalSchema: "public",
                        principalTable: "riftbound_card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_card_riftbound_deck_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_deck_comment",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    deck_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    parent_comment_id = table.Column<long>(type: "bigint", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_deck_comment", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_comment_riftbound_deck_comment_parent_commen",
                        column: x => x.parent_comment_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck_comment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_comment_riftbound_deck_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_comment_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_deck_rating",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    deck_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_deck_rating", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_rating_riftbound_deck_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_deck_share",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    deck_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_deck_share", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_share_riftbound_deck_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_champion_id",
                schema: "public",
                table: "riftbound_deck",
                column: "champion_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_legend_id",
                schema: "public",
                table: "riftbound_deck",
                column: "legend_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_owner_id",
                schema: "public",
                table: "riftbound_deck",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_card_card_id",
                schema: "public",
                table: "riftbound_deck_card",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_card_deck_id_card_id",
                schema: "public",
                table: "riftbound_deck_card",
                columns: new[] { "deck_id", "card_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_comment_deck_id",
                schema: "public",
                table: "riftbound_deck_comment",
                column: "deck_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_comment_parent_comment_id",
                schema: "public",
                table: "riftbound_deck_comment",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_comment_user_id",
                schema: "public",
                table: "riftbound_deck_comment",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_rating_deck_id_user_id",
                schema: "public",
                table: "riftbound_deck_rating",
                columns: new[] { "deck_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_share_deck_id_user_id",
                schema: "public",
                table: "riftbound_deck_share",
                columns: new[] { "deck_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "riftbound_deck_card",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_comment",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_rating",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_share",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck",
                schema: "public");
        }
    }
}
