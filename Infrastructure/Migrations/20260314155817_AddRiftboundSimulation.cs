using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRiftboundSimulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "riftbound_deck_battlefield",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    deck_id = table.Column<long>(type: "bigint", nullable: false),
                    card_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_deck_battlefield", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_battlefield_riftbound_card_card_id",
                        column: x => x.card_id,
                        principalSchema: "public",
                        principalTable: "riftbound_card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_battlefield_riftbound_deck_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_deck_rune",
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
                    table.PrimaryKey("pk_riftbound_deck_rune", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_rune_riftbound_card_card_id",
                        column: x => x.card_id,
                        principalSchema: "public",
                        principalTable: "riftbound_card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_rune_riftbound_deck_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_simulation_run",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    requested_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    challenger_deck_id = table.Column<long>(type: "bigint", nullable: false),
                    opponent_deck_id = table.Column<long>(type: "bigint", nullable: false),
                    seed = table.Column<long>(type: "bigint", nullable: false),
                    ruleset_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    challenger_policy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    opponent_policy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    winner_player_index = table.Column<int>(type: "integer", nullable: true),
                    score_summary_json = table.Column<string>(type: "jsonb", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_simulation_run", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_simulation_run_riftbound_deck_challenger_deck_id",
                        column: x => x.challenger_deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_riftbound_simulation_run_riftbound_deck_opponent_deck_id",
                        column: x => x.opponent_deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_riftbound_simulation_run_user_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_simulation_event",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    simulation_run_id = table.Column<long>(type: "bigint", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_simulation_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_simulation_event_riftbound_simulation_run_simulat",
                        column: x => x.simulation_run_id,
                        principalSchema: "public",
                        principalTable: "riftbound_simulation_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_battlefield_card_id",
                schema: "public",
                table: "riftbound_deck_battlefield",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_battlefield_deck_id_card_id",
                schema: "public",
                table: "riftbound_deck_battlefield",
                columns: new[] { "deck_id", "card_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_rune_card_id",
                schema: "public",
                table: "riftbound_deck_rune",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_rune_deck_id_card_id",
                schema: "public",
                table: "riftbound_deck_rune",
                columns: new[] { "deck_id", "card_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_simulation_event_simulation_run_id_sequence",
                schema: "public",
                table: "riftbound_simulation_event",
                columns: new[] { "simulation_run_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_simulation_run_challenger_deck_id",
                schema: "public",
                table: "riftbound_simulation_run",
                column: "challenger_deck_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_simulation_run_created_on",
                schema: "public",
                table: "riftbound_simulation_run",
                column: "created_on");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_simulation_run_opponent_deck_id",
                schema: "public",
                table: "riftbound_simulation_run",
                column: "opponent_deck_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_simulation_run_requested_by_user_id",
                schema: "public",
                table: "riftbound_simulation_run",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_simulation_run_status",
                schema: "public",
                table: "riftbound_simulation_run",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "riftbound_deck_battlefield",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_rune",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_simulation_event",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_simulation_run",
                schema: "public");
        }
    }
}
