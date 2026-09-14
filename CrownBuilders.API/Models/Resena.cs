using System;
using System.ComponentModel.DataAnnotations;

namespace CrownBuilders.API.Models
{
    public class Resena
    {
        [Key]
        public int Id { get; set; }

        public int EmisorId { get; set; }

        public int ReceptorId { get; set; }

        public string TipoReceptor { get; set; } = string.Empty; // "Trabajador" o "Compania"

        public int Calificacion1 { get; set; } // Puntualidad (para ambos)

        public int Calificacion2 { get; set; } // Pagos a tiempo (compañía) / Experiencia (trabajador)

        public int Calificacion3 { get; set; } // Trato humano (compañía) / Actitud (trabajador)

        public string? Comentario { get; set; }

        public DateTime FechaCreacion { get; set; }
    }
}
