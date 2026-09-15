using System.ComponentModel.DataAnnotations;

namespace CrownBuilders.API.Models;

public sealed class Conversacion
{
    [Key]
    public int Id { get; set; }

    public int TrabajadorId { get; set; }

    public int CompaniaId { get; set; }

    public DateTime FechaCreacionUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Mensaje> Mensajes { get; set; } = new List<Mensaje>();
}
