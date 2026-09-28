namespace CrownBuilders.Models;

public sealed class CompanyRegistrationRequest
{
    public string? FotoPerfil { get; set; }
    public string NombreEmpresa { get; set; } = string.Empty;
    public string Especialidad { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FormaPago { get; set; } = string.Empty;
    public decimal MontoPago { get; set; }
    public bool AceptaTerminos { get; set; }
}
