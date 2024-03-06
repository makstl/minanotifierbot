using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinaNotifierBot.Migrations
{
    public partial class UserUserState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserState",
                table: "User",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserState",
                table: "User");
        }
    }
}
