using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrownBuilders.API.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Conversaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrabajadorId = table.Column<int>(type: "int", nullable: false),
                    CompaniaId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacionUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversaciones", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "ConversacionId",
                table: "Mensajes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientMessageId",
                table: "Mensajes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO Conversaciones (TrabajadorId, CompaniaId, FechaCreacionUtc)
                SELECT DISTINCT TrabajadorId, CompaniaId, SYSUTCDATETIME()
                FROM Mensajes;
                """);

            migrationBuilder.Sql("""
                UPDATE mensaje
                SET ConversacionId = conversacion.Id
                FROM Mensajes AS mensaje
                INNER JOIN Conversaciones AS conversacion
                    ON conversacion.TrabajadorId = mensaje.TrabajadorId
                   AND conversacion.CompaniaId = mensaje.CompaniaId;
                """);

            migrationBuilder.Sql("""
                UPDATE Mensajes
                SET ClientMessageId = NEWID()
                WHERE ClientMessageId IS NULL;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Mensajes WHERE ConversacionId IS NULL)
                    THROW 51000, 'No se pudo asociar un mensaje existente a una conversación.', 1;

                IF EXISTS (SELECT 1 FROM Mensajes WHERE ClientMessageId IS NULL)
                    THROW 51001, 'No se pudo asignar una clave de idempotencia a un mensaje existente.', 1;

                IF EXISTS (SELECT 1 FROM Mensajes WHERE LEN(Texto) > 4000)
                    THROW 51002, 'Existen mensajes históricos que superan el límite de 4000 caracteres.', 1;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "ConversacionId",
                table: "Mensajes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientMessageId",
                table: "Mensajes",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Texto",
                table: "Mensajes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Conversaciones_TrabajadorId_CompaniaId",
                table: "Conversaciones",
                columns: new[] { "TrabajadorId", "CompaniaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_ConversacionId_ClientMessageId",
                table: "Mensajes",
                columns: new[] { "ConversacionId", "ClientMessageId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Mensajes_Conversaciones_ConversacionId",
                table: "Mensajes",
                column: "ConversacionId",
                principalTable: "Conversaciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mensajes_Conversaciones_ConversacionId",
                table: "Mensajes");

            migrationBuilder.DropIndex(
                name: "IX_Mensajes_ConversacionId_ClientMessageId",
                table: "Mensajes");

            migrationBuilder.DropIndex(
                name: "IX_Conversaciones_TrabajadorId_CompaniaId",
                table: "Conversaciones");

            migrationBuilder.DropColumn(
                name: "ClientMessageId",
                table: "Mensajes");

            migrationBuilder.DropColumn(
                name: "ConversacionId",
                table: "Mensajes");

            migrationBuilder.AlterColumn<string>(
                name: "Texto",
                table: "Mensajes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.DropTable(
                name: "Conversaciones");
        }
    }
}
