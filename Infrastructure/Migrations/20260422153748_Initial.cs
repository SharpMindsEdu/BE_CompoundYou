using System;
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
                name: "trading_trades",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    alpaca_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    alpaca_take_profit_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    alpaca_stop_loss_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    alpaca_exit_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    planned_entry_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    planned_stop_loss_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    planned_take_profit_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    planned_risk_per_unit = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    actual_entry_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    actual_exit_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    realized_profit_loss = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    realized_r_multiple = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    exit_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    alpaca_order_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    alpaca_exit_order_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sentiment_score = table.Column<int>(type: "integer", nullable: true),
                    retest_score = table.Column<int>(type: "integer", nullable: true),
                    signal_retest_bar_timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    entry_filled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    exit_filled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    alpaca_order_payload_json = table.Column<string>(type: "text", nullable: true),
                    alpaca_exit_order_payload_json = table.Column<string>(type: "text", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trading_trades", x => x.id);
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
                name: "ix_trading_trades_alpaca_order_id",
                schema: "public",
                table: "trading_trades",
                column: "alpaca_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trading_trades_symbol_submitted_at_utc",
                schema: "public",
                table: "trading_trades",
                columns: new[] { "symbol", "submitted_at_utc" });

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
                name: "trading_trades",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user_block",
                schema: "public");

            migrationBuilder.DropTable(
                name: "chat_room",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user",
                schema: "public");
        }
    }
}
