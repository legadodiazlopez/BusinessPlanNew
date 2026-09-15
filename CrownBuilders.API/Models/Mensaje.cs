using System;
using System.ComponentModel.DataAnnotations;

namespace CrownBuilders.API.Models;

public sealed class Mensaje
{
    [Key]
    public int Id { get; set; }

    public int ConversacionId { get; set; }

    public int TrabajadorId { get; set; }

    public int CompaniaId { get; set; }

    public Guid ClientMessageId { get; set; }

    public string Remitente { get; set; } = string.Empty;

    public string Texto { get; set; } = string.Empty;

    public DateTime FechaEnvio { get; set; }

    public Conversacion Conversacion { get; set; } = null!;
}
