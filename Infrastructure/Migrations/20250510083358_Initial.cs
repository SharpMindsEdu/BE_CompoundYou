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
                name: "user",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
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
                name: "habit",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    habit_preparation_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    is_preparation_habit = table.Column<bool>(type: "boolean", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    motivation = table.Column<string>(type: "text", nullable: true),
                    title_search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false)
                        .Annotation("Npgsql:TsVectorConfig", "german")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "title" }),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "ix_habit_time_habit_id",
                schema: "public",
                table: "habit_time",
                column: "habit_id");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "habit_time",
                schema: "public");

            migrationBuilder.DropTable(
                name: "habit_trigger",
                schema: "public");

            migrationBuilder.DropTable(
                name: "habit",
                schema: "public");

            migrationBuilder.DropTable(
                name: "user",
                schema: "public");
        }
    }
}
