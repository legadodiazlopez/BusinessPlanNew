using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CrownBuilders.API.Data;

#nullable disable

namespace CrownBuilders.API.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928000000_AddStripeCheckoutState")]
public partial class AddStripeCheckoutState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CVC",
            table: "Companias");

        migrationBuilder.DropColumn(
            name: "FechaExpiracion",
            table: "Companias");

        migrationBuilder.DropColumn(
            name: "NombreTitular",
            table: "Companias");

        migrationBuilder.DropColumn(
            name: "NumeroTarjeta",
            table: "Companias");

        migrationBuilder.AddColumn<string>(
            name: "StripeCheckoutSessionId",
            table: "Companias",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StripeCustomerId",
            table: "Companias",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StripePaymentStatus",
            table: "Companias",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "pending");

        migrationBuilder.AddColumn<string>(
            name: "StripeSubscriptionId",
            table: "Companias",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CVC",
            table: "Companias",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "FechaExpiracion",
            table: "Companias",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "NombreTitular",
            table: "Companias",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "NumeroTarjeta",
            table: "Companias",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");

        migrationBuilder.DropColumn(
            name: "StripeCheckoutSessionId",
            table: "Companias");

        migrationBuilder.DropColumn(
            name: "StripeCustomerId",
            table: "Companias");

        migrationBuilder.DropColumn(
            name: "StripePaymentStatus",
            table: "Companias");

        migrationBuilder.DropColumn(
            name: "StripeSubscriptionId",
            table: "Companias");
    }
}
