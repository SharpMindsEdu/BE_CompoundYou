using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Habits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "habit_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "habit_trigger",
                schema: "public");

            migrationBuilder.DropTable(
                name: "habit_time",
                schema: "public");

            migrationBuilder.DropTable(
                name: "habit",
                schema: "public");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "habit",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    habit_preparation_id = table.Column<long>(type: "bigint", nullable: true),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_preparation_habit = table.Column<bool>(type: "boolean", nullable: false),
                    motivation = table.Column<string>(type: "text", nullable: true),
                    score = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    title_search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false)
                        .Annotation("Npgsql:TsVectorConfig", "English")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "title" }),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit", x => x.id);
                    table.ForeignKey(
                        name: "fk_habit_habit_habit_preparation_id",
                        column: x => x.habit_preparation_id,
                        principalSchema: "public",
                        principalTable: "habit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_habit_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habit_time",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    habit_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    day = table.Column<int>(type: "integer", nullable: false),
                    time = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit_time", x => x.id);
                    table.ForeignKey(
                        name: "fk_habit_time_habit_habit_id",
                        column: x => x.habit_id,
                        principalSchema: "public",
                        principalTable: "habit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_habit_time_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "habit_trigger",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    habit_id = table.Column<long>(type: "bigint", nullable: false),
                    trigger_habit_id = table.Column<long>(type: "bigint", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit_trigger", x => x.id);
                    table.ForeignKey(
                        name: "fk_habit_trigger_habit_habit_id",
                        column: x => x.habit_id,
                        principalSchema: "public",
                        principalTable: "habit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_habit_trigger_habit_trigger_habit_id",
                        column: x => x.trigger_habit_id,
                        principalSchema: "public",
                        principalTable: "habit",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "habit_history",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    habit_history_id = table.Column<long>(type: "bigint", nullable: true),
                    habit_id = table.Column<long>(type: "bigint", nullable: false),
                    habit_time_id = table.Column<long>(type: "bigint", nullable: true),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() AT TIME ZONE 'UTC'"),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_habit_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_habit_history_habit_habit_id",
                        column: x => x.habit_id,
                        principalSchema: "public",
                        principalTable: "habit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_habit_history_habit_history_habit_history_id",
                        column: x => x.habit_history_id,
                        principalSchema: "public",
                        principalTable: "habit_history",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_habit_history_habit_time_habit_time_id",
                        column: x => x.habit_time_id,
                        principalSchema: "public",
                        principalTable: "habit_time",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_habit_history_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_habit_habit_preparation_id",
                schema: "public",
                table: "habit",
                column: "habit_preparation_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_title_search_vector",
                schema: "public",
                table: "habit",
                column: "title_search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_habit_user_id",
                schema: "public",
                table: "habit",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_history_habit_history_id",
                schema: "public",
                table: "habit_history",
                column: "habit_history_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_history_habit_id",
                schema: "public",
                table: "habit_history",
                column: "habit_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_history_habit_time_id",
                schema: "public",
                table: "habit_history",
                column: "habit_time_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_history_user_id",
                schema: "public",
                table: "habit_history",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_time_habit_id",
                schema: "public",
                table: "habit_time",
                column: "habit_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_time_user_id",
                schema: "public",
                table: "habit_time",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_trigger_habit_id",
                schema: "public",
                table: "habit_trigger",
                column: "habit_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_trigger_trigger_habit_id",
                schema: "public",
                table: "habit_trigger",
                column: "trigger_habit_id");
        }
    }
}
