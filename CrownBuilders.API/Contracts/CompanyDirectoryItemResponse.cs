namespace CrownBuilders.API.Contracts;

/// <summary>
/// Representa únicamente la información pública necesaria para el directorio de compañías.
/// </summary>
public sealed class CompanyDirectoryItemResponse
{
    public int Id { get; init; }

    public string NombreEmpresa { get; init; } = string.Empty;

    public string Especialidad { get; init; } = string.Empty;

    public string Ciudad { get; init; } = string.Empty;

    public string FormaPago { get; init; } = string.Empty;

    public decimal MontoPago { get; init; }

    public bool TieneFotoPerfil { get; init; }

    public int TotalResenas { get; init; }

    public double Puntualidad { get; init; }

    public double Pagos { get; init; }

    public double Trato { get; init; }
}
