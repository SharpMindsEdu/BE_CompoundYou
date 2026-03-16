using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "chat_room",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    is_direct = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_room", x => x.id);
                });

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
                    power = table.Column<int>(type: "integer", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    supertype = table.Column<string>(type: "text", nullable: true),
                    might = table.Column<int>(type: "integer", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: true),
                    gameplay_keywords = table.Column<List<string>>(type: "text[]", nullable: true),
                    set_name = table.Column<string>(type: "text", nullable: true),
                    rarity = table.Column<string>(type: "text", nullable: true),
                    cycle = table.Column<string>(type: "text", nullable: true),
                    image = table.Column<string>(type: "text", nullable: true),
                    promo = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riftbound_card", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    sign_in_secret = table.Column<string>(type: "text", nullable: true),
                    sign_in_tries = table.Column<int>(type: "integer", nullable: true),
                    display_name_search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false)
                        .Annotation("Npgsql:TsVectorConfig", "german")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "display_name" }),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chat_message",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chat_room_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    attachment_url = table.Column<string>(type: "text", nullable: true),
                    attachment_type = table.Column<string>(type: "text", nullable: true),
                    reply_to_message_id = table.Column<long>(type: "bigint", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_message", x => x.id);
                    table.ForeignKey(
                        name: "fk_chat_message_chat_message_reply_to_message_id",
                        column: x => x.reply_to_message_id,
                        principalSchema: "public",
                        principalTable: "chat_message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_chat_message_chat_room_chat_room_id",
                        column: x => x.chat_room_id,
                        principalSchema: "public",
                        principalTable: "chat_room",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chat_message_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_room_user",
                schema: "public",
                columns: table => new
                {
                    chat_room_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_room_user", x => new { x.chat_room_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_chat_room_user_chat_room_chat_room_id",
                        column: x => x.chat_room_id,
                        principalSchema: "public",
                        principalTable: "chat_room",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chat_room_user_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "user_block",
                schema: "public",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    blocked_user_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_block", x => new { x.user_id, x.blocked_user_id });
                    table.ForeignKey(
                        name: "fk_user_block_user_blocked_user_id",
                        column: x => x.blocked_user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_block_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "ix_chat_message_chat_room_id",
                schema: "public",
                table: "chat_message",
                column: "chat_room_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_message_reply_to_message_id",
                schema: "public",
                table: "chat_message",
                column: "reply_to_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_message_user_id",
                schema: "public",
                table: "chat_message",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_room_user_user_id",
                schema: "public",
                table: "chat_room_user",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_card_is_active",
                schema: "public",
                table: "riftbound_card",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_riftbound_card_reference_id",
                schema: "public",
                table: "riftbound_card",
                column: "reference_id",
                unique: true);

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
                name: "ix_riftbound_deck_rating_deck_id_user_id",
                schema: "public",
                table: "riftbound_deck_rating",
                columns: new[] { "deck_id", "user_id" },
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
                name: "ix_riftbound_deck_share_deck_id_user_id",
                schema: "public",
                table: "riftbound_deck_share",
                columns: new[] { "deck_id", "user_id" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_user_display_name_search_vector",
                schema: "public",
                table: "user",
                column: "display_name_search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_user_email",
                schema: "public",
                table: "user",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_phone_number",
                schema: "public",
                table: "user",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_block_blocked_user_id",
                schema: "public",
                table: "user_block",
                column: "blocked_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_message",
                schema: "public");

            migrationBuilder.DropTable(
                name: "chat_room_user",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_battlefield",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_card",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_comment",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_optimization_candidate",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_optimization_matchup",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_rating",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_rune",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_share",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_sideboard_card",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_simulation_event",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_block",
                schema: "public");

            migrationBuilder.DropTable(
                name: "chat_room",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck_optimization_run",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_simulation_run",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_deck",
                schema: "public");

            migrationBuilder.DropTable(
                name: "riftbound_card",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user",
                schema: "public");
        }
    }
}
