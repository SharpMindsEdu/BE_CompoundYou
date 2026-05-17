using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _01_Tenancy_Org : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_platform_admin",
                schema: "public",
                table: "user",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "audit_log_entry",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    actor_user_id = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_id = table.Column<long>(type: "bigint", nullable: true),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log_entry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    plan = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    owner_user_id = table.Column<long>(type: "bigint", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_user_owner_user_id",
                        column: x => x.owner_user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "department",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_department_id = table.Column<long>(type: "bigint", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_department", x => x.id);
                    table.ForeignKey(
                        name: "fk_department_department_parent_department_id",
                        column: x => x.parent_department_id,
                        principalSchema: "public",
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_department_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_invitation",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_invitation", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_invitation_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_membership",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    joined_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_membership", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_membership_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tenant_membership_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    first_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    last_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    team_id = table.Column<long>(type: "bigint", nullable: true),
                    manager_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    external_source_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_employee_manager_employee_id",
                        column: x => x.manager_employee_id,
                        principalSchema: "public",
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employee_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_employee_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    department_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    manager_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    created_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_team", x => x.id);
                    table.ForeignKey(
                        name: "fk_team_department_department_id",
                        column: x => x.department_id,
                        principalSchema: "public",
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_team_employee_manager_employee_id",
                        column: x => x.manager_employee_id,
                        principalSchema: "public",
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_team_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entry_actor_user_id",
                schema: "public",
                table: "audit_log_entry",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entry_tenant_id_entity_type_entity_id",
                schema: "public",
                table: "audit_log_entry",
                columns: new[] { "tenant_id", "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entry_tenant_id_occurred_on",
                schema: "public",
                table: "audit_log_entry",
                columns: new[] { "tenant_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_department_parent_department_id",
                schema: "public",
                table: "department",
                column: "parent_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_department_tenant_id_name",
                schema: "public",
                table: "department",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_manager_employee_id",
                schema: "public",
                table: "employee",
                column: "manager_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_team_id",
                schema: "public",
                table: "employee",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_tenant_id_employee_number",
                schema: "public",
                table: "employee",
                columns: new[] { "tenant_id", "employee_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_tenant_id_external_source_id",
                schema: "public",
                table: "employee",
                columns: new[] { "tenant_id", "external_source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_tenant_id_user_id",
                schema: "public",
                table: "employee",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_user_id",
                schema: "public",
                table: "employee",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_department_id",
                schema: "public",
                table: "team",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_manager_employee_id",
                schema: "public",
                table: "team",
                column: "manager_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_team_tenant_id_department_id_name",
                schema: "public",
                table: "team",
                columns: new[] { "tenant_id", "department_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_owner_user_id",
                schema: "public",
                table: "tenant",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_slug",
                schema: "public",
                table: "tenant",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_invitation_tenant_id_email",
                schema: "public",
                table: "tenant_invitation",
                columns: new[] { "tenant_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_invitation_token",
                schema: "public",
                table: "tenant_invitation",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_membership_tenant_id_user_id",
                schema: "public",
                table: "tenant_membership",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_membership_user_id",
                schema: "public",
                table: "tenant_membership",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_employee_team_team_id",
                schema: "public",
                table: "employee",
                column: "team_id",
                principalSchema: "public",
                principalTable: "team",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_department_tenant_tenant_id",
                schema: "public",
                table: "department");

            migrationBuilder.DropForeignKey(
                name: "fk_employee_tenant_tenant_id",
                schema: "public",
                table: "employee");

            migrationBuilder.DropForeignKey(
                name: "fk_team_tenant_tenant_id",
                schema: "public",
                table: "team");

            migrationBuilder.DropForeignKey(
                name: "fk_employee_team_team_id",
                schema: "public",
                table: "employee");

            migrationBuilder.DropTable(
                name: "audit_log_entry",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tenant_invitation",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tenant_membership",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tenant",
                schema: "public");

            migrationBuilder.DropTable(
                name: "team",
                schema: "public");

            migrationBuilder.DropTable(
                name: "department",
                schema: "public");

            migrationBuilder.DropTable(
                name: "employee",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "is_platform_admin",
                schema: "public",
                table: "user");
        }
    }
}
