using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TradingCalendarStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trading_calendar_days",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    open_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    close_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trading_calendar_days", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trading_calendar_days_date",
                schema: "public",
                table: "trading_calendar_days",
                column: "date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trading_calendar_days",
                schema: "public");
        }
    }
}
