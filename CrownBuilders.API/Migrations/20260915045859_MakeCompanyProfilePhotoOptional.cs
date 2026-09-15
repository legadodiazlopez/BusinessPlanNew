using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrownBuilders.API.Migrations
{
    /// <inheritdoc />
    public partial class MakeCompanyProfilePhotoOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FotoPerfil",
                table: "Companias",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Companias] SET [FotoPerfil] = N'' WHERE [FotoPerfil] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "FotoPerfil",
                table: "Companias",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
