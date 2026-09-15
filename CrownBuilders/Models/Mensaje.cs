using SQLite;
using System;

namespace CrownBuilders.Models
{
    public class Mensaje
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string ClientMessageId { get; set; } = Guid.NewGuid().ToString("D");
        public int ConversacionId { get; set; }
        public int IdRemoto { get; set; }
        public int TrabajadorId { get; set; }
        public int CompaniaId { get; set; }
        public bool Pendiente { get; set; }
        public int Intentos { get; set; }

        // Para saber si lo envió el Trabajador o la Compañia
        public string Remitente { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public DateTime FechaEnvio { get; set; }
    }
}
