using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _03_CareerFramework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_family",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_family", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_family_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_skill_requirement",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    team_id = table.Column<long>(type: "bigint", nullable: false),
                    skill_id = table.Column<long>(type: "bigint", nullable: false),
                    required_skill_level_id = table.Column<long>(type: "bigint", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team_skill_requirement", x => x.id);
                    table.ForeignKey(
                        name: "fk_team_skill_requirement_skill_level_required_skill_level_id",
                        column: x => x.required_skill_level_id,
                        principalSchema: "public",
                        principalTable: "skill_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_team_skill_requirement_skill_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "public",
                        principalTable: "skill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_team_skill_requirement_team_team_id",
                        column: x => x.team_id,
                        principalSchema: "public",
                        principalTable: "team",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_team_skill_requirement_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "career_level",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    job_family_id = table.Column<long>(type: "bigint", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_career_level", x => x.id);
                    table.ForeignKey(
                        name: "fk_career_level_job_family_job_family_id",
                        column: x => x.job_family_id,
                        principalSchema: "public",
                        principalTable: "job_family",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_career_level_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_profile",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    job_family_id = table.Column<long>(type: "bigint", nullable: false),
                    career_level_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    description = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_profile", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_profile_career_level_career_level_id",
                        column: x => x.career_level_id,
                        principalSchema: "public",
                        principalTable: "career_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_profile_job_family_job_family_id",
                        column: x => x.job_family_id,
                        principalSchema: "public",
                        principalTable: "job_family",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_profile_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "career_path_snapshot",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    current_role_profile_id = table.Column<long>(type: "bigint", nullable: true),
                    target_role_profile_id = table.Column<long>(type: "bigint", nullable: true),
                    readiness_score = table.Column<int>(type: "integer", nullable: true),
                    skill_fit_score = table.Column<int>(type: "integer", nullable: true),
                    validation_coverage_score = table.Column<int>(type: "integer", nullable: true),
                    goal_completion_score = table.Column<int>(type: "integer", nullable: true),
                    band = table.Column<int>(type: "integer", nullable: true),
                    scored_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_career_path_snapshot", x => x.id);
                    table.ForeignKey(
                        name: "fk_career_path_snapshot_employee_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "public",
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_career_path_snapshot_role_profile_current_role_profile_id",
                        column: x => x.current_role_profile_id,
                        principalSchema: "public",
                        principalTable: "role_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_career_path_snapshot_role_profile_target_role_profile_id",
                        column: x => x.target_role_profile_id,
                        principalSchema: "public",
                        principalTable: "role_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_career_path_snapshot_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_role_profile",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    role_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_role_profile", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_role_profile_employee_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "public",
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_employee_role_profile_role_profile_role_profile_id",
                        column: x => x.role_profile_id,
                        principalSchema: "public",
                        principalTable: "role_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_role_profile_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_profile_skill_requirement",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    role_profile_id = table.Column<long>(type: "bigint", nullable: false),
                    skill_id = table.Column<long>(type: "bigint", nullable: false),
                    required_skill_level_id = table.Column<long>(type: "bigint", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_profile_skill_requirement", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_profile_skill_requirement_role_profile_role_profile_id",
                        column: x => x.role_profile_id,
                        principalSchema: "public",
                        principalTable: "role_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_profile_skill_requirement_skill_level_required_skill_l",
                        column: x => x.required_skill_level_id,
                        principalSchema: "public",
                        principalTable: "skill_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_profile_skill_requirement_skill_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "public",
                        principalTable: "skill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_profile_skill_requirement_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_career_level_job_family_id_order",
                schema: "public",
                table: "career_level",
                columns: new[] { "job_family_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_career_level_tenant_id_job_family_id",
                schema: "public",
                table: "career_level",
                columns: new[] { "tenant_id", "job_family_id" });

            migrationBuilder.CreateIndex(
                name: "ix_career_path_snapshot_current_role_profile_id",
                schema: "public",
                table: "career_path_snapshot",
                column: "current_role_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_career_path_snapshot_employee_id",
                schema: "public",
                table: "career_path_snapshot",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_career_path_snapshot_target_role_profile_id",
                schema: "public",
                table: "career_path_snapshot",
                column: "target_role_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_career_path_snapshot_tenant_id_employee_id_scored_on",
                schema: "public",
                table: "career_path_snapshot",
                columns: new[] { "tenant_id", "employee_id", "scored_on" });

            migrationBuilder.CreateIndex(
                name: "ix_career_path_snapshot_tenant_id_target_role_profile_id",
                schema: "public",
                table: "career_path_snapshot",
                columns: new[] { "tenant_id", "target_role_profile_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_role_profile_employee_id",
                schema: "public",
                table: "employee_role_profile",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_role_profile_role_profile_id",
                schema: "public",
                table: "employee_role_profile",
                column: "role_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_role_profile_tenant_id_employee_id",
                schema: "public",
                table: "employee_role_profile",
                columns: new[] { "tenant_id", "employee_id" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_employee_role_profile_tenant_id_role_profile_id",
                schema: "public",
                table: "employee_role_profile",
                columns: new[] { "tenant_id", "role_profile_id" });

            migrationBuilder.CreateIndex(
                name: "ix_job_family_tenant_id_is_active",
                schema: "public",
                table: "job_family",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_job_family_tenant_id_name",
                schema: "public",
                table: "job_family",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_profile_career_level_id",
                schema: "public",
                table: "role_profile",
                column: "career_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_profile_job_family_id_career_level_id",
                schema: "public",
                table: "role_profile",
                columns: new[] { "job_family_id", "career_level_id" });

            migrationBuilder.CreateIndex(
                name: "ix_role_profile_tenant_id_is_active",
                schema: "public",
                table: "role_profile",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_role_profile_tenant_id_name",
                schema: "public",
                table: "role_profile",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_profile_skill_requirement_required_skill_level_id",
                schema: "public",
                table: "role_profile_skill_requirement",
                column: "required_skill_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_profile_skill_requirement_role_profile_id_skill_id",
                schema: "public",
                table: "role_profile_skill_requirement",
                columns: new[] { "role_profile_id", "skill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_profile_skill_requirement_skill_id",
                schema: "public",
                table: "role_profile_skill_requirement",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_profile_skill_requirement_tenant_id_skill_id",
                schema: "public",
                table: "role_profile_skill_requirement",
                columns: new[] { "tenant_id", "skill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_team_skill_requirement_required_skill_level_id",
                schema: "public",
                table: "team_skill_requirement",
                column: "required_skill_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_skill_requirement_skill_id",
                schema: "public",
                table: "team_skill_requirement",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_skill_requirement_team_id_skill_id",
                schema: "public",
                table: "team_skill_requirement",
                columns: new[] { "team_id", "skill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_team_skill_requirement_tenant_id_team_id",
                schema: "public",
                table: "team_skill_requirement",
                columns: new[] { "tenant_id", "team_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "career_path_snapshot",
                schema: "public");

            migrationBuilder.DropTable(
                name: "employee_role_profile",
                schema: "public");

            migrationBuilder.DropTable(
                name: "role_profile_skill_requirement",
                schema: "public");

            migrationBuilder.DropTable(
                name: "team_skill_requirement",
                schema: "public");

            migrationBuilder.DropTable(
                name: "role_profile",
                schema: "public");

            migrationBuilder.DropTable(
                name: "career_level",
                schema: "public");

            migrationBuilder.DropTable(
                name: "job_family",
                schema: "public");
        }
    }
}
