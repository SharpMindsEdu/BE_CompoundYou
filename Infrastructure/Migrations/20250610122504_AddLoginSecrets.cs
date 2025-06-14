using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sign_in_secret",
                schema: "public",
                table: "user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sign_in_tries",
                schema: "public",
                table: "user",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sign_in_secret",
                schema: "public",
                table: "user");

            migrationBuilder.DropColumn(
                name: "sign_in_tries",
                schema: "public",
                table: "user");
        }
    }
}
