using SQLite;

namespace CrownBuilders.Models;

/// <summary>
/// Almacena únicamente los campos públicos del directorio para el modo sin conexión.
/// </summary>
public sealed class CompaniaDirectorioCache
{
    [PrimaryKey]
    public int Id { get; set; }

    public string NombreEmpresa { get; set; } = string.Empty;

    public string Especialidad { get; set; } = string.Empty;

    public string Ciudad { get; set; } = string.Empty;

    public string FormaPago { get; set; } = string.Empty;

    public decimal MontoPago { get; set; }

    public bool TieneFotoPerfil { get; set; }

    public int TotalResenas { get; set; }

    public double Puntualidad { get; set; }

    public double Pagos { get; set; }

    public double Trato { get; set; }
}
