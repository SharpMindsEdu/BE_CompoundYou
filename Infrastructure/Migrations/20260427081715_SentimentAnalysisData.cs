using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SentimentAnalysisData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "sentiment_analysis_id",
                schema: "public",
                table: "trading_trades",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trading_sentiment_analyses",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    analyzed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    trading_date = table.Column<DateOnly>(type: "date", nullable: false),
                    agent_text = table.Column<string>(type: "text", nullable: true),
                    all_opportunities_json = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trading_sentiment_analyses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_trading_trades_sentiment_analysis_id",
                schema: "public",
                table: "trading_trades",
                column: "sentiment_analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_trading_sentiment_analyses_analyzed_at_utc",
                schema: "public",
                table: "trading_sentiment_analyses",
                column: "analyzed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_trading_sentiment_analyses_trading_date",
                schema: "public",
                table: "trading_sentiment_analyses",
                column: "trading_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trading_sentiment_analyses",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_trading_trades_sentiment_analysis_id",
                schema: "public",
                table: "trading_trades");

            migrationBuilder.DropColumn(
                name: "sentiment_analysis_id",
                schema: "public",
                table: "trading_trades");
        }
    }
}
