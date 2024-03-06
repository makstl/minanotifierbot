using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinaNotifierBot.Migrations
{
    public partial class PublicAddressUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PublicAddress_Address",
                table: "PublicAddress",
                column: "Address",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicAddress_Address",
                table: "PublicAddress");
        }
    }
}
