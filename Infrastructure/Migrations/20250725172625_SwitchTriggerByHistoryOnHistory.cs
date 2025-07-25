using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SwitchTriggerByHistoryOnHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_habit_history_habit_trigger_habit_trigger_id",
                schema: "public",
                table: "habit_history");

            migrationBuilder.RenameColumn(
                name: "habit_trigger_id",
                schema: "public",
                table: "habit_history",
                newName: "habit_history_id");

            migrationBuilder.RenameIndex(
                name: "ix_habit_history_habit_trigger_id",
                schema: "public",
                table: "habit_history",
                newName: "ix_habit_history_habit_history_id");

            migrationBuilder.AddForeignKey(
                name: "fk_habit_history_habit_history_habit_history_id",
                schema: "public",
                table: "habit_history",
                column: "habit_history_id",
                principalSchema: "public",
                principalTable: "habit_history",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_habit_history_habit_history_habit_history_id",
                schema: "public",
                table: "habit_history");

            migrationBuilder.RenameColumn(
                name: "habit_history_id",
                schema: "public",
                table: "habit_history",
                newName: "habit_trigger_id");

            migrationBuilder.RenameIndex(
                name: "ix_habit_history_habit_history_id",
                schema: "public",
                table: "habit_history",
                newName: "ix_habit_history_habit_trigger_id");

            migrationBuilder.AddForeignKey(
                name: "fk_habit_history_habit_trigger_habit_trigger_id",
                schema: "public",
                table: "habit_history",
                column: "habit_trigger_id",
                principalSchema: "public",
                principalTable: "habit_trigger",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
