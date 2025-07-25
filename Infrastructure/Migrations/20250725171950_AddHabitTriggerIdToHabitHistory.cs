using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHabitTriggerIdToHabitHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "habit_trigger_id",
                schema: "public",
                table: "habit_history",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_habit_history_habit_trigger_id",
                schema: "public",
                table: "habit_history",
                column: "habit_trigger_id");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_habit_history_habit_trigger_habit_trigger_id",
                schema: "public",
                table: "habit_history");

            migrationBuilder.DropIndex(
                name: "ix_habit_history_habit_trigger_id",
                schema: "public",
                table: "habit_history");

            migrationBuilder.DropColumn(
                name: "habit_trigger_id",
                schema: "public",
                table: "habit_history");
        }
    }
}
