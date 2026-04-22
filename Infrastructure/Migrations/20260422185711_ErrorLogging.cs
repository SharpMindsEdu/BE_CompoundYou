using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ErrorLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exception_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    exception_type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    stack_trace = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    capture_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_handled = table.Column<bool>(type: "boolean", nullable: false),
                    request_path = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    request_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    user_identifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exception_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exception_logs_exception_type",
                schema: "public",
                table: "exception_logs",
                column: "exception_type");

            migrationBuilder.CreateIndex(
                name: "ix_exception_logs_occurred_on_utc",
                schema: "public",
                table: "exception_logs",
                column: "occurred_on_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exception_logs",
                schema: "public");
        }
    }
}
