using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BacktestCandleStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trading_candle_bars",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    open = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    high = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    low = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    close = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    volume = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trading_candle_bars", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trading_candle_bars_symbol_timestamp_utc",
                schema: "public",
                table: "trading_candle_bars",
                columns: new[] { "symbol", "timestamp_utc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trading_candle_bars",
                schema: "public");
        }
    }
}
