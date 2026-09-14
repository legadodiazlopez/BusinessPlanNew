using SQLite;
using System;

namespace CrownBuilders.Models
{
    public class Mensaje
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int TrabajadorId { get; set; }
        public int CompaniaId { get; set; }

        // Para saber si lo envió el Trabajador o la Compañia
        public string Remitente { get; set; }
        public string Texto { get; set; }
        public DateTime FechaEnvio { get; set; }
    }
}