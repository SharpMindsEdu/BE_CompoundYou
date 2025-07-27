using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceAndImageMessagesAndReplyFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attachment_url",
                schema: "public",
                table: "chat_message",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reply_to_message_id",
                schema: "public",
                table: "chat_message",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_message_reply_to_message_id",
                schema: "public",
                table: "chat_message",
                column: "reply_to_message_id");

            migrationBuilder.AddForeignKey(
                name: "fk_chat_message_chat_message_reply_to_message_id",
                schema: "public",
                table: "chat_message",
                column: "reply_to_message_id",
                principalSchema: "public",
                principalTable: "chat_message",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_chat_message_chat_message_reply_to_message_id",
                schema: "public",
                table: "chat_message");

            migrationBuilder.DropIndex(
                name: "ix_chat_message_reply_to_message_id",
                schema: "public",
                table: "chat_message");

            migrationBuilder.DropColumn(
                name: "attachment_url",
                schema: "public",
                table: "chat_message");

            migrationBuilder.DropColumn(
                name: "reply_to_message_id",
                schema: "public",
                table: "chat_message");
        }
    }
}
