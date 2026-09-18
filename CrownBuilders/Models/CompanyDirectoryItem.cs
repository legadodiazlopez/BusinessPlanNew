namespace CrownBuilders.Models;

/// <summary>
/// Contiene los datos públicos recibidos desde el directorio de compañías.
/// </summary>
public sealed class CompanyDirectoryItem
{
    public int Id { get; set; }

    public string NombreEmpresa { get; set; } = string.Empty;

    public string Especialidad { get; set; } = string.Empty;

    public string Ciudad { get; set; } = string.Empty;

    public string FormaPago { get; set; } = string.Empty;

    public decimal MontoPago { get; set; }
}
