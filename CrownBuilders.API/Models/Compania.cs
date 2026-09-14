namespace CrownBuilders.API.Models
{
    public class Compania
    {
        public int Id { get; set; }
        public string FotoPerfil { get; set; }
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
        public bool AceptaTerminos { get; set; }
        public string? FcmToken { get; set; }
    }
}