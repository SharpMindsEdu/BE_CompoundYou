using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _02_CoreDataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learning_resource",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    media_file_id = table.Column<long>(type: "bigint", nullable: true),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: true),
                    points_awarded = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learning_resource", x => x.id);
                    table.ForeignKey(
                        name: "fk_learning_resource_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skill_category",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill_category", x => x.id);
                    table.ForeignKey(
                        name: "fk_skill_category_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skill",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    skill_category_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    parent_skill_id = table.Column<long>(type: "bigint", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill", x => x.id);
                    table.ForeignKey(
                        name: "fk_skill_skill_category_skill_category_id",
                        column: x => x.skill_category_id,
                        principalSchema: "public",
                        principalTable: "skill_category",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_skill_skill_parent_skill_id",
                        column: x => x.parent_skill_id,
                        principalSchema: "public",
                        principalTable: "skill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_skill_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goal",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    author_employee_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    period = table.Column<int>(type: "integer", nullable: false),
                    target_type = table.Column<int>(type: "integer", nullable: false),
                    target_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    current_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    target_skill_id = table.Column<long>(type: "bigint", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goal", x => x.id);
                    table.ForeignKey(
                        name: "fk_goal_employee_author_employee_id",
                        column: x => x.author_employee_id,
                        principalSchema: "public",
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goal_employee_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "public",
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_goal_skill_target_skill_id",
                        column: x => x.target_skill_id,
                        principalSchema: "public",
                        principalTable: "skill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_goal_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "skill_level",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    skill_id = table.Column<long>(type: "bigint", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    points_threshold = table.Column<int>(type: "integer", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_skill_level", x => x.id);
                    table.ForeignKey(
                        name: "fk_skill_level_skill_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "public",
                        principalTable: "skill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goal_check_in",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    goal_id = table.Column<long>(type: "bigint", nullable: false),
                    author_employee_id = table.Column<long>(type: "bigint", nullable: false),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    progress_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goal_check_in", x => x.id);
                    table.ForeignKey(
                        name: "fk_goal_check_in_employee_author_employee_id",
                        column: x => x.author_employee_id,
                        principalSchema: "public",
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_goal_check_in_goal_goal_id",
                        column: x => x.goal_id,
                        principalSchema: "public",
                        principalTable: "goal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_goal_check_in_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_skill_assessment",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    skill_id = table.Column<long>(type: "bigint", nullable: false),
                    claimed_skill_level_id = table.Column<long>(type: "bigint", nullable: false),
                    validated_skill_level_id = table.Column<long>(type: "bigint", nullable: true),
                    validated_by_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    validated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_skill_assessment", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_skill_assessment_employee_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "public",
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_employee_skill_assessment_employee_validated_by_employee_id",
                        column: x => x.validated_by_employee_id,
                        principalSchema: "public",
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_employee_skill_assessment_skill_level_claimed_skill_level_id",
                        column: x => x.claimed_skill_level_id,
                        principalSchema: "public",
                        principalTable: "skill_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_skill_assessment_skill_level_validated_skill_level",
                        column: x => x.validated_skill_level_id,
                        principalSchema: "public",
                        principalTable: "skill_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_employee_skill_assessment_skill_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "public",
                        principalTable: "skill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_skill_assessment_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_skill_assessment_claimed_skill_level_id",
                schema: "public",
                table: "employee_skill_assessment",
                column: "claimed_skill_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_skill_assessment_employee_id_skill_id",
                schema: "public",
                table: "employee_skill_assessment",
                columns: new[] { "employee_id", "skill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_skill_assessment_skill_id",
                schema: "public",
                table: "employee_skill_assessment",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_skill_assessment_tenant_id_status",
                schema: "public",
                table: "employee_skill_assessment",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_skill_assessment_validated_by_employee_id",
                schema: "public",
                table: "employee_skill_assessment",
                column: "validated_by_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_skill_assessment_validated_skill_level_id",
                schema: "public",
                table: "employee_skill_assessment",
                column: "validated_skill_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_goal_author_employee_id",
                schema: "public",
                table: "goal",
                column: "author_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_goal_due_on",
                schema: "public",
                table: "goal",
                column: "due_on");

            migrationBuilder.CreateIndex(
                name: "ix_goal_employee_id",
                schema: "public",
                table: "goal",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_goal_target_skill_id",
                schema: "public",
                table: "goal",
                column: "target_skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_goal_tenant_id_employee_id_status",
                schema: "public",
                table: "goal",
                columns: new[] { "tenant_id", "employee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_goal_check_in_author_employee_id",
                schema: "public",
                table: "goal_check_in",
                column: "author_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_goal_check_in_goal_id_created_on",
                schema: "public",
                table: "goal_check_in",
                columns: new[] { "goal_id", "created_on" });

            migrationBuilder.CreateIndex(
                name: "ix_goal_check_in_tenant_id",
                schema: "public",
                table: "goal_check_in",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_learning_resource_tenant_id_title",
                schema: "public",
                table: "learning_resource",
                columns: new[] { "tenant_id", "title" });

            migrationBuilder.CreateIndex(
                name: "ix_learning_resource_type",
                schema: "public",
                table: "learning_resource",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_skill_parent_skill_id",
                schema: "public",
                table: "skill",
                column: "parent_skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_skill_skill_category_id",
                schema: "public",
                table: "skill",
                column: "skill_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_skill_tenant_id_name",
                schema: "public",
                table: "skill",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skill_category_tenant_id_name",
                schema: "public",
                table: "skill_category",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skill_level_skill_id_order",
                schema: "public",
                table: "skill_level",
                columns: new[] { "skill_id", "order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_skill_assessment",
                schema: "public");

            migrationBuilder.DropTable(
                name: "goal_check_in",
                schema: "public");

            migrationBuilder.DropTable(
                name: "learning_resource",
                schema: "public");

            migrationBuilder.DropTable(
                name: "skill_level",
                schema: "public");

            migrationBuilder.DropTable(
                name: "goal",
                schema: "public");

            migrationBuilder.DropTable(
                name: "skill",
                schema: "public");

            migrationBuilder.DropTable(
                name: "skill_category",
                schema: "public");
        }
    }
}
