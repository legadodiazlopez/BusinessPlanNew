namespace CrownBuilders.API.Models
{
    public class Trabajador
    {
        public int Id { get; set; }
        public string FotoPerfil { get; set; }
        public string Nombre { get; set; }
        public string Oficio { get; set; }
        public int? Experiencia { get; set; }
        public string Ciudad { get; set; }
        public string Telefono { get; set; }
        public string Correo { get; set; }
        public string Password { get; set; }
        public bool TieneVehiculo { get; set; }
        public bool TieneHerramientas { get; set; }
        public int Salario { get; set; }
        public bool AceptaTerminos { get; set; }
        public bool? EstaDisponible { get; set; }
        public string? FcmToken { get; set; }
    }
}