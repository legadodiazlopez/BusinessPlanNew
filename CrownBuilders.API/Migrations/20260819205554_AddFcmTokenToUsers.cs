using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrownBuilders.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFcmTokenToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FcmToken",
                table: "Trabajadores",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FcmToken",
                table: "Companias",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FcmToken",
                table: "Trabajadores");

            migrationBuilder.DropColumn(
                name: "FcmToken",
                table: "Companias");
        }
    }
}
