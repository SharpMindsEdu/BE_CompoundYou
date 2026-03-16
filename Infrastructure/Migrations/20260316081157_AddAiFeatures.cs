using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "riftbound_deck_optimization_run",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    requested_by_user_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    seed = table.Column<long>(type: "bigint", nullable: false),
                    population_size = table.Column<int>(type: "integer", nullable: false),
                    generations = table.Column<int>(type: "integer", nullable: false),
                    seeds_per_match = table.Column<int>(type: "integer", nullable: false),
                    max_autoplay_steps = table.Column<int>(type: "integer", nullable: false),
                    current_generation = table.Column<int>(type: "integer", nullable: false),
                    progress_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    error_message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    started_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_deck_optimization_run", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_optimization_run_user_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_deck_sideboard_card",
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
                    table.PrimaryKey("pk_riftbound_deck_sideboard_card", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_sideboard_card_riftbound_card_card_id",
                        column: x => x.card_id,
                        principalSchema: "public",
                        principalTable: "riftbound_card",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_sideboard_card_riftbound_deck_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_deck_optimization_candidate",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<long>(type: "bigint", nullable: false),
                    deck_id = table.Column<long>(type: "bigint", nullable: false),
                    legend_id = table.Column<long>(type: "bigint", nullable: false),
                    generation = table.Column<int>(type: "integer", nullable: false),
                    wins = table.Column<int>(type: "integer", nullable: false),
                    losses = table.Column<int>(type: "integer", nullable: false),
                    draws = table.Column<int>(type: "integer", nullable: false),
                    games_played = table.Column<int>(type: "integer", nullable: false),
                    win_rate = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    sonneborn_berger = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    head_to_head_score = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    rank_global = table.Column<int>(type: "integer", nullable: false),
                    rank_in_legend = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_deck_optimization_candidate", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_optimization_candidate_riftbound_deck_deck_id",
                        column: x => x.deck_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_optimization_candidate_riftbound_deck_optimi",
                        column: x => x.run_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck_optimization_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "riftbound_deck_optimization_matchup",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<long>(type: "bigint", nullable: false),
                    generation = table.Column<int>(type: "integer", nullable: false),
                    deck_a_id = table.Column<long>(type: "bigint", nullable: false),
                    deck_b_id = table.Column<long>(type: "bigint", nullable: false),
                    deck_a_wins = table.Column<int>(type: "integer", nullable: false),
                    deck_b_wins = table.Column<int>(type: "integer", nullable: false),
                    draws = table.Column<int>(type: "integer", nullable: false),
                    games_played = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_deck_optimization_matchup", x => x.id);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_optimization_matchup_riftbound_deck_deck_a_id",
                        column: x => x.deck_a_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_optimization_matchup_riftbound_deck_deck_b_id",
                        column: x => x.deck_b_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_riftbound_deck_optimization_matchup_riftbound_deck_optimiza",
                        column: x => x.run_id,
                        principalSchema: "public",
                        principalTable: "riftbound_deck_optimization_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_candidate_deck_id",
                schema: "public",
                table: "riftbound_deck_optimization_candidate",
                column: "deck_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_candidate_run_id_generation_dec",
                schema: "public",
                table: "riftbound_deck_optimization_candidate",
                columns: new[] { "run_id", "generation", "deck_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_candidate_run_id_generation_leg",
                schema: "public",
                table: "riftbound_deck_optimization_candidate",
                columns: new[] { "run_id", "generation", "legend_id", "rank_in_legend" });

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_candidate_run_id_generation_ran",
                schema: "public",
                table: "riftbound_deck_optimization_candidate",
                columns: new[] { "run_id", "generation", "rank_global" });

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_matchup_deck_a_id",
                schema: "public",
                table: "riftbound_deck_optimization_matchup",
                column: "deck_a_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_matchup_deck_b_id",
                schema: "public",
                table: "riftbound_deck_optimization_matchup",
                column: "deck_b_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_matchup_run_id_generation",
                schema: "public",
                table: "riftbound_deck_optimization_matchup",
                columns: new[] { "run_id", "generation" });

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_matchup_run_id_generation_deck_",
                schema: "public",
                table: "riftbound_deck_optimization_matchup",
                columns: new[] { "run_id", "generation", "deck_a_id", "deck_b_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_run_created_on",
                schema: "public",
                table: "riftbound_deck_optimization_run",
                column: "created_on");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_run_requested_by_user_id",
                schema: "public",
                table: "riftbound_deck_optimization_run",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_optimization_run_status",
                schema: "public",
                table: "riftbound_deck_optimization_run",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_sideboard_card_card_id",
                schema: "public",
                table: "riftbound_deck_sideboard_card",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_deck_sideboard_card_deck_id_card_id",
                schema: "public",
                table: "riftbound_deck_sideboard_card",
                columns: new[] { "deck_id", "card_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "riftbound_deck_optimization_candidate",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_optimization_matchup",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_sideboard_card",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_optimization_run",
                schema: "public");
        }
    }
}
