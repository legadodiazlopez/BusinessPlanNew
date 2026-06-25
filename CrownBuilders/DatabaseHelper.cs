using SQLite;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace CrownBuilders
{
    public static class DatabaseHelper
    {
        private static SQLiteAsyncConnection _db;

        public static async Task InitAsync()
        {
            if (_db != null) return;

            var dbPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "CrownBuilders.db3");
            _db = new SQLiteAsyncConnection(dbPath);

            await _db.CreateTableAsync<PerfilCompania>();
            await _db.CreateTableAsync<Trabajador>();
            await _db.CreateTableAsync<Garantia>();
            await _db.CreateTableAsync<Asistencia>();
        }

        public static async Task<int> GuardarPerfilCompaniaAsync(PerfilCompania perfil)
        {
            await InitAsync();
            return await _db.InsertOrReplaceAsync(perfil);
        }

        public static async Task<PerfilCompania> ObtenerPerfilCompaniaAsync()
        {
            await InitAsync();
            return await _db.Table<PerfilCompania>().FirstOrDefaultAsync();
        }

        public static async Task<PerfilCompania> ObtenerCompaniaPorIdAsync(int id)
        {
            await InitAsync();
            return await _db.Table<PerfilCompania>().Where(p => p.Id == id).FirstOrDefaultAsync();
        }

        public static async Task<int> EliminarPerfilCompaniaAsync(PerfilCompania perfil)
        {
            await InitAsync();
            if (perfil != null && perfil.Id > 0)
            {
                return await _db.DeleteAsync(perfil);
            }
            return 0;
        }

        public static async Task<int> EliminarPerfilCompaniaPorIdAsync(int id)
        {
            await InitAsync();
            var compania = await ObtenerCompaniaPorIdAsync(id);
            if (compania != null)
            {
                return await _db.DeleteAsync(compania);
            }
            return 0;
        }

        public static async Task<List<Trabajador>> ObtenerTrabajadoresAsync()
        {
            await InitAsync();
            return await _db.Table<Trabajador>().ToListAsync();
        }

        public static async Task<int> GuardarTrabajadorAsync(Trabajador trabajador)
        {
            await InitAsync();
            return await _db.InsertOrReplaceAsync(trabajador);
        }

        public static async Task<int> ActualizarTrabajadorAsync(Trabajador trabajador)
        {
            await InitAsync();
            return await _db.UpdateAsync(trabajador);
        }

        public static async Task<int> EliminarTrabajadorAsync(Trabajador trabajador)
        {
            await InitAsync();
            if (trabajador != null && trabajador.Id > 0)
            {
                return await _db.DeleteAsync(trabajador);
            }
            return 0;
        }

        // NUEVO MÉTODO APORTADO POR MANUALES DE DESARROLLO: Purgado masivo directo por sentencia SQL para evitar problemas de caché de objetos
        public static async Task PurgarTrabajadoresAsync()
        {
            await InitAsync();
            await _db.ExecuteAsync("DELETE FROM Trabajador");
        }

        public static async Task<int> GuardarGarantiaAsync(Garantia garantia)
        {
            await InitAsync();
            return await _db.InsertOrReplaceAsync(garantia);
        }

        public static async Task<int> GuardarAsistenciaAsync(Asistencia asistencia)
        {
            await InitAsync();
            return await _db.InsertOrReplaceAsync(asistencia);
        }

        public static async Task<string> ObtenerEmailCompaniaActivaAsync()
        {
            await InitAsync();
            var perfil = await ObtenerPerfilCompaniaAsync();
            return perfil?.NombreEmpresa;
        }

        public static async Task<int> ActualizarPerfilCompaniaAsync(PerfilCompania perfil)
        {
            return await _db.UpdateAsync(perfil);
        }

        public static async Task<List<GarantiaInasistencia>> ObtenerGarantiasAsync()
        {
            await InitAsync();
            return await _db.Table<GarantiaInasistencia>().ToListAsync();
        }

        public static async Task<int> GuardarGarantiaInasistenciaAsync(GarantiaInasistencia garantia)
        {
            return await _db.InsertOrReplaceAsync(garantia);
        }

        public static async Task<PerfilCompania> ValidarCredencialesCompaniaAsync(string correo, string password)
        {
            await InitAsync();
            return await _db.Table<PerfilCompania>()
                            .Where(c => c.Correo == correo && c.Password == password)
                            .FirstOrDefaultAsync();
        }

        public static async Task<Trabajador> ValidarCredencialesTrabajadorAsync(string correo, string password)
        {
            await InitAsync();
            return await _db.Table<Trabajador>()
                            .Where(t => t.Correo == correo && t.Password == password)
                            .FirstOrDefaultAsync();
        }
    }

    public class PerfilCompania
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        public string FotoPerfil { get; set; }
        public string NombreEmpresa { get; set; }
        public string Especialidad { get; set; }
        public string Ciudad { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
        public string Password { get; set; }
        public string FormaPago { get; set; }
        public string MontoPago { get; set; }
        public string NombreTitular { get; set; }
        public string NumeroTarjeta { get; set; }
        public string FechaExpiracion { get; set; }
        public string CVC { get; set; }
        public bool SuscripcionActiva { get; set; }
        public bool AceptaTerminos { get; set; }
    }

    public class Trabajador
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        public string Nombre { get; set; }
        public string Ciudad { get; set; }
        public int AniosExperiencia { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
        public string Password { get; set; }
        public bool TieneVehiculo { get; set; }
        public bool TieneHerramientas { get; set; }
    }

    public class Garantia { [PrimaryKey, AutoIncrement] public int Id { get; set; } public string Descripcion { get; set; } }
    public class Asistencia { [PrimaryKey, AutoIncrement] public int Id { get; set; } public string Fecha { get; set; } }

    public class GarantiaInasistencia
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        public string Descripcion { get; set; }
        public bool SincronizadoNube { get; set; }
        public decimal Monto { get; set; }
        public string Estado { get; set; }
        public System.DateTime FechaLimite { get; set; }
        public string FotoLlamadas { get; set; }
        public string FotoMensajes { get; set; }
        public string FotoSitioVacio { get; set; }
        public int ContratacionId { get; set; }
        public int TrabajadorId { get; set; }
    }
}