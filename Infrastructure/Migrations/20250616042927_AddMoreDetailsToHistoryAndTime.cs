using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreDetailsToHistoryAndTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "user_id",
                schema: "public",
                table: "habit_time",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "habit_time_id",
                schema: "public",
                table: "habit_history",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ix_habit_time_user_id",
                schema: "public",
                table: "habit_time",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_habit_history_habit_time_id",
                schema: "public",
                table: "habit_history",
                column: "habit_time_id");

            migrationBuilder.AddForeignKey(
                name: "fk_habit_history_habit_time_habit_time_id",
                schema: "public",
                table: "habit_history",
                column: "habit_time_id",
                principalSchema: "public",
                principalTable: "habit_time",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_habit_time_user_user_id",
                schema: "public",
                table: "habit_time",
                column: "user_id",
                principalSchema: "public",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_habit_history_habit_time_habit_time_id",
                schema: "public",
                table: "habit_history");

            migrationBuilder.DropForeignKey(
                name: "fk_habit_time_user_user_id",
                schema: "public",
                table: "habit_time");

            migrationBuilder.DropIndex(
                name: "ix_habit_time_user_id",
                schema: "public",
                table: "habit_time");

            migrationBuilder.DropIndex(
                name: "ix_habit_history_habit_time_id",
                schema: "public",
                table: "habit_history");

            migrationBuilder.DropColumn(
                name: "user_id",
                schema: "public",
                table: "habit_time");

            migrationBuilder.DropColumn(
                name: "habit_time_id",
                schema: "public",
                table: "habit_history");
        }
    }
}
