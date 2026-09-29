namespace CrownBuilders.Models
{
    public class Compania
    {
        public int Id { get; set; }
        public string? FotoPerfil { get; set; }
        public string NombreEmpresa { get; set; }
        public string Especialidad { get; set; }
        public string Ciudad { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
        public string Password { get; set; }
        public string FormaPago { get; set; }
        public decimal MontoPago { get; set; }
        public string NombreTitular { get; set; }
        public string NumeroTarjeta { get; set; }
        public string FechaExpiracion { get; set; }
        public string CVC { get; set; }
        public string? StripeCardholderName { get; set; }
        public string? StripeCardBrand { get; set; }
        public string? StripeCardLast4 { get; set; }
        public int? StripeCardExpMonth { get; set; }
        public int? StripeCardExpYear { get; set; }
        public bool AceptaTerminos { get; set; }
        public string? FcmToken { get; set; }
    }
}
