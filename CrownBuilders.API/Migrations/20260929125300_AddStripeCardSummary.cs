using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrownBuilders.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeCardSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeCardBrand",
                table: "Companias",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StripeCardExpMonth",
                table: "Companias",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StripeCardExpYear",
                table: "Companias",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCardLast4",
                table: "Companias",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCardholderName",
                table: "Companias",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripeCardBrand",
                table: "Companias");

            migrationBuilder.DropColumn(
                name: "StripeCardExpMonth",
                table: "Companias");

            migrationBuilder.DropColumn(
                name: "StripeCardExpYear",
                table: "Companias");

            migrationBuilder.DropColumn(
                name: "StripeCardLast4",
                table: "Companias");

            migrationBuilder.DropColumn(
                name: "StripeCardholderName",
                table: "Companias");
        }
    }
}
