using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fee_breakdown_json",
                schema: "public",
                table: "trading_trades",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fees_last_synced_at_utc",
                schema: "public",
                table: "trading_trades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "realized_alpaca_fees",
                schema: "public",
                table: "trading_trades",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "realized_gross_profit_loss",
                schema: "public",
                table: "trading_trades",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "realized_spread_cost",
                schema: "public",
                table: "trading_trades",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "realized_total_fees",
                schema: "public",
                table: "trading_trades",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fee_breakdown_json",
                schema: "public",
                table: "trading_trades");

            migrationBuilder.DropColumn(
                name: "fees_last_synced_at_utc",
                schema: "public",
                table: "trading_trades");

            migrationBuilder.DropColumn(
                name: "realized_alpaca_fees",
                schema: "public",
                table: "trading_trades");

            migrationBuilder.DropColumn(
                name: "realized_gross_profit_loss",
                schema: "public",
                table: "trading_trades");

            migrationBuilder.DropColumn(
                name: "realized_spread_cost",
                schema: "public",
                table: "trading_trades");

            migrationBuilder.DropColumn(
                name: "realized_total_fees",
                schema: "public",
                table: "trading_trades");
        }
    }
}
