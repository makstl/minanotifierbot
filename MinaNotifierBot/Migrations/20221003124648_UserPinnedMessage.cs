using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinaNotifierBot.Migrations
{
    public partial class UserPinnedMessage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PinnedMessageId",
                table: "User",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinnedMessageId",
                table: "User");
        }
    }
}
