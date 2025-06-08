using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SearchVektor_Changes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "title_search_vector",
                schema: "public",
                table: "habit",
                type: "tsvector",
                nullable: false,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector")
                .Annotation("Npgsql:TsVectorConfig", "English")
                .Annotation("Npgsql:TsVectorProperties", new[] { "title" })
                .OldAnnotation("Npgsql:TsVectorConfig", "german")
                .OldAnnotation("Npgsql:TsVectorProperties", new[] { "title" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "title_search_vector",
                schema: "public",
                table: "habit",
                type: "tsvector",
                nullable: false,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector")
                .Annotation("Npgsql:TsVectorConfig", "german")
                .Annotation("Npgsql:TsVectorProperties", new[] { "title" })
                .OldAnnotation("Npgsql:TsVectorConfig", "English")
                .OldAnnotation("Npgsql:TsVectorProperties", new[] { "title" });
        }
    }
}
