using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrownBuilders.API.Models;

public partial class Compania
{
    // Propiedades heredadas para compatibilidad con migraciones y pruebas antiguas.
    // Nunca forman parte de los DTOs ni de las respuestas JSON actuales.
    [NotMapped]
    [JsonIgnore]
    public string NombreTitular { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string NumeroTarjeta { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string FechaExpiracion { get; set; } = string.Empty;

    [NotMapped]
    [JsonIgnore]
    public string CVC { get; set; } = string.Empty;
}
