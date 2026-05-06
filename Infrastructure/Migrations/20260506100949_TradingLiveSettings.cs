using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TradingLiveSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trading_live_settings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    min_opportunities = table.Column<int>(type: "integer", nullable: true),
                    max_opportunities = table.Column<int>(type: "integer", nullable: true),
                    minimum_sentiment_score = table.Column<int>(type: "integer", nullable: true),
                    minimum_retest_score = table.Column<int>(type: "integer", nullable: true),
                    minimum_minutes_from_market_open_for_entry = table.Column<int>(type: "integer", nullable: true),
                    maximum_minutes_from_market_open_for_entry = table.Column<int>(type: "integer", nullable: true),
                    minimum_entry_distance_from_range_fraction = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    max_minutes_breakout_to_retest = table.Column<int>(type: "integer", nullable: true),
                    stop_loss_buffer_fraction = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    reward_to_risk_ratio = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    order_quantity = table.Column<int>(type: "integer", nullable: true),
                    risk_per_trade_fraction = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    break_even_at_r_multiple = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    max_bars_in_trade_before_flat_exit = table.Column<int>(type: "integer", nullable: true),
                    max_trades_per_day = table.Column<int>(type: "integer", nullable: true),
                    max_daily_loss_fraction = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    use_trailing_stop_loss = table.Column<bool>(type: "boolean", nullable: true),
                    partial_take_profit_fraction = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    trailing_stop_risk_multiple = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    trailing_stop_break_even_protection = table.Column<bool>(type: "boolean", nullable: true),
                    use_retest_validation_agent = table.Column<bool>(type: "boolean", nullable: true),
                    use_directional_indicator_filter = table.Column<bool>(type: "boolean", nullable: true),
                    directional_indicator_require_all = table.Column<bool>(type: "boolean", nullable: true),
                    directional_indicator_modes_json = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() AT TIME ZONE 'UTC'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trading_live_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trading_live_settings",
                schema: "public");
        }
    }
}
