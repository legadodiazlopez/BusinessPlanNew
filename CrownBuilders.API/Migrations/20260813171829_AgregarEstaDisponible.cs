using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrownBuilders.API.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEstaDisponible : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstaDisponible",
                table: "Trabajadores",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstaDisponible",
                table: "Trabajadores");
        }
    }
}
