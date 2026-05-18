using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _03_TenantSkillLevelSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_skill_level_skill_id_order",
                schema: "public",
                table: "skill_level");

            migrationBuilder.AlterColumn<long>(
                name: "skill_id",
                schema: "public",
                table: "skill_level",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "public",
                table: "skill_level",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "tenant_id",
                schema: "public",
                table: "skill_level",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_skill_level_skill_id_order",
                schema: "public",
                table: "skill_level",
                columns: new[] { "skill_id", "order" },
                unique: true,
                filter: "skill_id IS NOT NULL AND is_active");

            migrationBuilder.CreateIndex(
                name: "ix_skill_level_tenant_id_order",
                schema: "public",
                table: "skill_level",
                columns: new[] { "tenant_id", "order" },
                unique: true,
                filter: "skill_id IS NULL AND tenant_id IS NOT NULL AND is_active");

            migrationBuilder.AddCheckConstraint(
                name: "ck_skill_level_tenant_wide_or_inactive",
                schema: "public",
                table: "skill_level",
                sql: "skill_id IS NULL OR NOT is_active");

            migrationBuilder.AddForeignKey(
                name: "fk_skill_level_tenant_tenant_id",
                schema: "public",
                table: "skill_level",
                column: "tenant_id",
                principalSchema: "public",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_skill_level_tenant_tenant_id",
                schema: "public",
                table: "skill_level");

            migrationBuilder.DropIndex(
                name: "ix_skill_level_skill_id_order",
                schema: "public",
                table: "skill_level");

            migrationBuilder.DropIndex(
                name: "ix_skill_level_tenant_id_order",
                schema: "public",
                table: "skill_level");

            migrationBuilder.DropCheckConstraint(
                name: "ck_skill_level_tenant_wide_or_inactive",
                schema: "public",
                table: "skill_level");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "public",
                table: "skill_level");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "skill_level");

            migrationBuilder.AlterColumn<long>(
                name: "skill_id",
                schema: "public",
                table: "skill_level",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_skill_level_skill_id_order",
                schema: "public",
                table: "skill_level",
                columns: new[] { "skill_id", "order" },
                unique: true);
        }
    }
}
