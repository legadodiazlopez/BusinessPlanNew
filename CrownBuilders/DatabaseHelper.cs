using SQLite;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System;
using System.Linq;
using System.Threading;
using CrownBuilders.Services;

namespace CrownBuilders
{
    public static class DatabaseHelper
    {
        private static SQLiteAsyncConnection _db;
        private static readonly SemaphoreSlim _initializationLock = new(1, 1);
        private static readonly TimeSpan RemoteDataTimeout = TimeSpan.FromSeconds(8);

        // Producción/Azure:
        // private static readonly string _apiBaseUrl = "https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/api/";
        // Desarrollo local:
        private static readonly string _apiBaseUrl = ApiEndpoints.ApiBaseUrl;

        // UN SOLO CLIENTE ESTÁTICO PARA TODA LA APP (Evita colapso de conexiones y salta errores SSL en emulador)
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        })
        {
            BaseAddress = new Uri(_apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(20)
        };

        public static async Task InitAsync()
        {
            if (_db != null) return;

            await _initializationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_db != null) return;

                var dbPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "CrownBuilders.db3");
                var db = new SQLiteAsyncConnection(dbPath);

                // Crear tablas como caché local
                await db.CreateTableAsync<Trabajador>();
                await db.CreateTableAsync<PerfilCompania>();
                await db.CreateTableAsync<Garantia>();
                await db.CreateTableAsync<Asistencia>();
                await db.CreateTableAsync<CrownBuilders.Models.Mensaje>();

                string[] columnasMensaje = {
                    "ClientMessageId TEXT", "ConversacionId INTEGER", "IdRemoto INTEGER", "Pendiente BOOLEAN", "Intentos INTEGER"
                };

                foreach (var col in columnasMensaje)
                {
                    try
                    {
                        await db.ExecuteAsync($"ALTER TABLE Mensaje ADD COLUMN {col};");
                    }
                    catch { }
                }

                // Asegurar columnas para Trabajador
                string[] columnasTrabajador = {
                    "FotoPerfil TEXT", "Oficio TEXT", "Experiencia INTEGER",
                    "Ciudad TEXT", "Telefono TEXT", "TieneVehiculo BOOLEAN",
                    "TieneHerramientas BOOLEAN", "Salario DECIMAL", "AceptaTerminos BOOLEAN", "EstaDisponible BOOLEAN", "FcmToken TEXT"
                };

                foreach (var col in columnasTrabajador)
                {
                    try
                    {
                        await db.ExecuteAsync($"ALTER TABLE Trabajador ADD COLUMN {col};");
                    }
                    catch { }
                }

                _db = db;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        public static HttpClient GetHttpClient()
        {
            return _httpClient;
        }

        public static void SetAuthToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        private static async Task<HttpResponseMessage> GetRemoteAsync(
            string route,
            CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(RemoteDataTimeout);
            return await _httpClient.GetAsync(route, timeoutCts.Token).ConfigureAwait(false);
        }


        // --- MÉTODOS DE CACHÉ LOCAL (OFFLINE) ---

        public static async Task<bool> GuardarTrabajadorAsync(Trabajador trabajador)
        {
            await InitAsync();
            await _db.InsertOrReplaceAsync(trabajador);
            return true;
        }

        public static async Task<bool> GuardarTrabajadorAsync(CrownBuilders.Models.Trabajador trabajadorUI)
        {
            await InitAsync();

            var trabajadorDB = new CrownBuilders.Trabajador
            {
                Id = trabajadorUI.Id,
                FotoPerfil = trabajadorUI.FotoPerfil,
                Nombre = trabajadorUI.Nombre,
                Oficio = trabajadorUI.Oficio,
                Ciudad = trabajadorUI.Ciudad,
                Experiencia = trabajadorUI.Experiencia ?? 0,
                Salario = trabajadorUI.Salario,
                Correo = trabajadorUI.Correo,
                Telefono = trabajadorUI.Telefono,
                Password = trabajadorUI.Password,
                TieneVehiculo = trabajadorUI.TieneVehiculo,
                TieneHerramientas = trabajadorUI.TieneHerramientas,
                EstaDisponible = trabajadorUI.EstaDisponible ?? true,
                FcmToken = trabajadorUI.FcmToken
            };

            await _db.InsertOrReplaceAsync(trabajadorDB);
            return true;
        }

        public static async Task<Trabajador?> ObtenerTrabajadorPorIdAsync(int trabajadorId)
        {
            await InitAsync().ConfigureAwait(false);

            if (trabajadorId <= 0)
            {
                return null;
            }

            return await _db.Table<Trabajador>()
                .Where(t => t.Id == trabajadorId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public static async Task<bool> GuardarPerfilCompaniaAsync(PerfilCompania perfil)
        {
            await InitAsync();
            await _db.InsertOrReplaceAsync(perfil);
            return true;
        }

        public static async Task<PerfilCompania?> ObtenerPerfilCompaniaAsync(int companiaId)
        {
            await InitAsync().ConfigureAwait(false);

            if (companiaId <= 0)
            {
                return null;
            }

            return await _db.Table<PerfilCompania>()
                .Where(p => p.Id == companiaId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public static async Task<string> ObtenerEmailCompaniaActivaAsync(int companiaId)
        {
            await InitAsync().ConfigureAwait(false);
            if (companiaId <= 0)
            {
                return string.Empty;
            }

            var compania = await _db.Table<PerfilCompania>()
                .Where(p => p.Id == companiaId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            return compania?.Correo ?? string.Empty;
        }

        public static async Task<List<Trabajador>> ObtenerTrabajadoresAsync(
            CancellationToken cancellationToken = default)
        {
            await InitAsync().ConfigureAwait(false);

            // Primero intentar obtener de la API (fuente de verdad) con optimización
            try
            {
                System.Diagnostics.Debug.WriteLine("[TRABAJADORES] Obteniendo datos desde Azure...");

                // Usamos el cliente estático
                using var response = await GetRemoteAsync("auth/trabajadores", cancellationToken).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[TRABAJADORES] Respuesta del servidor: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var trabajadoresApi = await response.Content.ReadFromJsonAsync<List<CrownBuilders.Models.Trabajador>>().ConfigureAwait(false);
                    if (trabajadoresApi != null && trabajadoresApi.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TRABAJADORES] {trabajadoresApi.Count} trabajadores obtenidos de Azure");

                        // Sincronizar con la base de datos local de forma optimizada usando InsertOrReplace
                        foreach (var trabajadorApi in trabajadoresApi)
                        {
                            var trabajadorDB = MapearTrabajadorParaCache(trabajadorApi);
                            await _db.InsertOrReplaceAsync(trabajadorDB).ConfigureAwait(false);
                        }
                        System.Diagnostics.Debug.WriteLine("[TRABAJADORES] Sincronización local completada");

                        // Retornar directamente desde Azure (foco en nube)
                        return trabajadoresApi.Select(MapearTrabajadorParaCache).ToList();
                    }
                }

            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[TRABAJADORES] Timeout al obtener datos de Azure - usando caché local");
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TRABAJADORES] Error de red: {ex.Message} - usando caché local");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TRABAJADORES] Error al obtener trabajadores de la API: {ex.Message} - usando caché local");
            }

            // Si falla la API, usar datos locales (caché offline)
            return await _db.Table<Trabajador>().ToListAsync().ConfigureAwait(false);
        }

        private static Trabajador MapearTrabajadorParaCache(
            CrownBuilders.Models.Trabajador trabajadorApi)
        {
            ArgumentNullException.ThrowIfNull(trabajadorApi);

            return new Trabajador
            {
                Id = trabajadorApi.Id,
                FotoPerfil = trabajadorApi.FotoPerfil,
                Nombre = trabajadorApi.Nombre,
                Oficio = trabajadorApi.Oficio,
                Ciudad = trabajadorApi.Ciudad,
                Experiencia = trabajadorApi.Experiencia ?? 0,
                Salario = trabajadorApi.Salario,
                Correo = trabajadorApi.Correo,
                Telefono = trabajadorApi.Telefono,
                Password = trabajadorApi.Password,
                TieneVehiculo = trabajadorApi.TieneVehiculo,
                TieneHerramientas = trabajadorApi.TieneHerramientas,
                EstaDisponible = trabajadorApi.EstaDisponible ?? true,
                FcmToken = trabajadorApi.FcmToken
            };
        }

        public static async Task<List<CrownBuilders.Components.Pages.CompaniasDisponibles.Compania>> ObtenerCompaniasAsync(
            CancellationToken cancellationToken = default)
        {
            var listaLocal = await ObtenerCompaniasLocalesAsync(cancellationToken).ConfigureAwait(false);
            if (listaLocal.Count > 0)
            {
                return listaLocal;
            }

            return await ActualizarCompaniasDesdeApiAsync(cancellationToken).ConfigureAwait(false)
                ?? listaLocal;
        }

        public static async Task<List<CrownBuilders.Components.Pages.CompaniasDisponibles.Compania>> ObtenerCompaniasLocalesAsync(
            CancellationToken cancellationToken = default)
        {
            await InitAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var perfilesLocales = await _db.Table<PerfilCompania>()
                .ToListAsync()
                .ConfigureAwait(false);

            var listaLocal = perfilesLocales.Select(p =>
            {
                double.TryParse(p.MontoPago, out var montoParsed);
                return new CrownBuilders.Components.Pages.CompaniasDisponibles.Compania
                {
                    Id = p.Id,
                    Nombre = p.NombreEmpresa ?? "Compañía",
                    Ciudad = p.Ciudad ?? "Long Island",
                    Especialidad = p.Especialidad ?? "General",
                    FormaPago = p.FormaPago ?? string.Empty,
                    MontoPago = montoParsed > 0 ? montoParsed : 40.0,
                    Puntualidad = 5,
                    Pagos = 5,
                    Trato = 5
                };
            }).ToList();

            System.Diagnostics.Debug.WriteLine($"[COMPAÑÍAS] Caché local: {listaLocal.Count} registros");
            return listaLocal;
        }

        public static async Task<List<CrownBuilders.Components.Pages.CompaniasDisponibles.Compania>?> ActualizarCompaniasDesdeApiAsync(
            CancellationToken cancellationToken = default)
        {
            await InitAsync().ConfigureAwait(false);

            try
            {
                using var response = await GetRemoteAsync("Perfil/listar-companias", cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[COMPAÑÍAS] Respuesta remota no exitosa: {response.StatusCode}");
                    return null;
                }

                var companiasApi = await response.Content
                    .ReadFromJsonAsync<List<CrownBuilders.Models.Compania>>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (companiasApi is null)
                {
                    return null;
                }

                foreach (var companiaApi in companiasApi)
                {
                    await _db.InsertOrReplaceAsync(new PerfilCompania
                    {
                        Id = companiaApi.Id,
                        FotoPerfil = companiaApi.FotoPerfil,
                        NombreEmpresa = companiaApi.NombreEmpresa,
                        Especialidad = companiaApi.Especialidad,
                        Ciudad = companiaApi.Ciudad,
                        Correo = companiaApi.Correo,
                        Telefono = companiaApi.Telefono,
                        Password = companiaApi.Password,
                        FormaPago = companiaApi.FormaPago,
                        MontoPago = companiaApi.MontoPago.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        NombreTitular = companiaApi.NombreTitular,
                        NumeroTarjeta = companiaApi.NumeroTarjeta,
                        FechaExpiracion = companiaApi.FechaExpiracion,
                        CVC = companiaApi.CVC,
                        FcmToken = companiaApi.FcmToken
                    }).ConfigureAwait(false);
                }

                System.Diagnostics.Debug.WriteLine($"[COMPAÑÍAS] Directorio remoto sincronizado: {companiasApi.Count} registros");
                return companiasApi.Select(c => new CrownBuilders.Components.Pages.CompaniasDisponibles.Compania
                {
                    Id = c.Id,
                    Nombre = c.NombreEmpresa ?? string.Empty,
                    Ciudad = c.Ciudad ?? string.Empty,
                    Especialidad = c.Especialidad ?? string.Empty,
                    FormaPago = c.FormaPago ?? string.Empty,
                    MontoPago = (double)c.MontoPago,
                    Puntualidad = 5,
                    Pagos = 5,
                    Trato = 5
                }).ToList();
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[COMPAÑÍAS] Tiempo remoto agotado; se conserva la caché local");
            }
            catch (HttpRequestException exception)
            {
                System.Diagnostics.Debug.WriteLine($"[COMPAÑÍAS] Error de red; se conserva la caché local: {exception.Message}");
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"[COMPAÑÍAS] Error de actualización: {exception.Message}");
            }

            return null;
        }

        public static async Task<PerfilCompania> ObtenerCompaniaPorIdAsync(int id)
        {
            await InitAsync().ConfigureAwait(false);
            return await _db.Table<PerfilCompania>().Where(p => p.Id == id).FirstOrDefaultAsync().ConfigureAwait(false);
        }

        public static async Task<int> EliminarPerfilCompaniaAsync(PerfilCompania perfil)
        {
            await InitAsync().ConfigureAwait(false);
            if (perfil != null && perfil.Id > 0)
            {
                return await _db.DeleteAsync(perfil).ConfigureAwait(false);
            }
            return 0;
        }

        public static async Task<int> ActualizarTrabajadorAsync(Trabajador trabajador)
        {
            await InitAsync().ConfigureAwait(false);
            return await _db.UpdateAsync(trabajador).ConfigureAwait(false);
        }

        public static async Task<int> EliminarTrabajadorAsync(Trabajador trabajador)
        {
            await InitAsync().ConfigureAwait(false);
            if (trabajador != null && trabajador.Id > 0)
            {
                return await _db.DeleteAsync(trabajador).ConfigureAwait(false);
            }
            return 0;
        }

        public static async Task PurgarTrabajadoresAsync()
        {
            await InitAsync().ConfigureAwait(false);
            await _db.ExecuteAsync("DELETE FROM Trabajador").ConfigureAwait(false);
        }

        // --- MÉTODOS DE GARANTÍAS ---

        public static async Task<List<Garantia>> ObtenerGarantiasAsync()
        {
            await InitAsync().ConfigureAwait(false);
            return await _db.Table<Garantia>().ToListAsync().ConfigureAwait(false);
        }

        public static async Task<int> GuardarGarantiaAsync(Garantia garantia)
        {
            await InitAsync().ConfigureAwait(false);
            return await _db.InsertAsync(garantia).ConfigureAwait(false);
        }

        public static async Task<int> GuardarGarantiaInasistenciaAsync(GarantiaInasistencia garantia)
        {
            await InitAsync().ConfigureAwait(false);
            return await _db.InsertAsync(garantia).ConfigureAwait(false);
        }

        // --- MÉTODOS DE ASISTENCIA ---

        public static async Task<List<Asistencia>> ObtenerAsistenciasAsync()
        {
            await InitAsync().ConfigureAwait(false);
            return await _db.Table<Asistencia>().ToListAsync().ConfigureAwait(false);
        }

        public static async Task<int> GuardarAsistenciaAsync(Asistencia asistencia)
        {
            await InitAsync().ConfigureAwait(false);
            return await _db.InsertAsync(asistencia).ConfigureAwait(false);
        }

        // --- MÉTODOS DE MENSAJERÍA (CACHÉ LOCAL) ---

        public static async Task<int> GuardarMensajeAsync(CrownBuilders.Models.Mensaje mensaje)
        {
            return await GuardarMensajeLocalAsync(mensaje).ConfigureAwait(false);
        }

        public static async Task<int> EncolarMensajeAsync(CrownBuilders.Models.Mensaje mensaje)
        {
            mensaje.Pendiente = true;
            return await GuardarMensajeLocalAsync(mensaje).ConfigureAwait(false);
        }

        public static async Task<int> GuardarMensajeLocalAsync(CrownBuilders.Models.Mensaje mensaje)
        {
            ArgumentNullException.ThrowIfNull(mensaje);
            await InitAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(mensaje.ClientMessageId))
            {
                mensaje.ClientMessageId = Guid.NewGuid().ToString("D");
            }

            var existing = await _db.Table<CrownBuilders.Models.Mensaje>()
                .Where(item => item.ClientMessageId == mensaje.ClientMessageId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (existing is not null)
            {
                mensaje.Id = existing.Id;
                return await _db.UpdateAsync(mensaje).ConfigureAwait(false);
            }

            return await _db.InsertAsync(mensaje).ConfigureAwait(false);
        }

        public static async Task MarcarMensajeSincronizadoAsync(
            string clientMessageId,
            int remoteMessageId,
            int conversationId)
        {
            await InitAsync().ConfigureAwait(false);
            var local = await _db.Table<CrownBuilders.Models.Mensaje>()
                .Where(item => item.ClientMessageId == clientMessageId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (local is null)
            {
                return;
            }

            local.IdRemoto = remoteMessageId;
            local.ConversacionId = conversationId;
            local.Pendiente = false;
            local.Intentos = 0;
            await _db.UpdateAsync(local).ConfigureAwait(false);
        }

        public static async Task RegistrarFalloMensajeAsync(string clientMessageId)
        {
            await InitAsync().ConfigureAwait(false);
            var local = await _db.Table<CrownBuilders.Models.Mensaje>()
                .Where(item => item.ClientMessageId == clientMessageId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (local is null)
            {
                return;
            }

            local.Intentos++;
            await _db.UpdateAsync(local).ConfigureAwait(false);
        }

        public static async Task<List<CrownBuilders.Models.Mensaje>> ObtenerMensajesPendientesAsync(
            int conversationId,
            int maximo = 20)
        {
            await InitAsync().ConfigureAwait(false);
            return await _db.Table<CrownBuilders.Models.Mensaje>()
                .Where(item => item.ConversacionId == conversationId && item.Pendiente)
                .OrderBy(item => item.FechaEnvio)
                .Take(maximo)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public static async Task GuardarMensajeRemotoAsync(MensajeApiResponse mensaje)
        {
            ArgumentNullException.ThrowIfNull(mensaje);
            await InitAsync().ConfigureAwait(false);

            var local = await _db.Table<CrownBuilders.Models.Mensaje>()
                .Where(item => item.ClientMessageId == mensaje.ClientMessageId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            var entity = new CrownBuilders.Models.Mensaje
            {
                Id = local?.Id ?? 0,
                IdRemoto = mensaje.Id,
                ClientMessageId = mensaje.ClientMessageId,
                ConversacionId = mensaje.ConversationId,
                TrabajadorId = mensaje.WorkerId,
                CompaniaId = mensaje.CompanyId,
                Remitente = mensaje.Sender,
                Texto = mensaje.Text,
                FechaEnvio = mensaje.SentAtUtc,
                Pendiente = false
            };

            if (local is null)
            {
                await _db.InsertAsync(entity).ConfigureAwait(false);
            }
            else
            {
                await _db.UpdateAsync(entity).ConfigureAwait(false);
            }
        }

        public static async Task<List<CrownBuilders.Models.Mensaje>> ObtenerHistorialAsync(int trabajadorId, int companiaId)
        {
            await InitAsync().ConfigureAwait(false);

            System.Diagnostics.Debug.WriteLine($"[MENSAJES] Obteniendo historial: TrabajadorID={trabajadorId}, CompaniaID={companiaId}");

            // Primero intentar obtener de la API (fuente de verdad)
            try
            {
                var apiUrl = $"mensajes/historial/{trabajadorId}/{companiaId}";
                var fullUrl = $"{_apiBaseUrl}{apiUrl}";
                System.Diagnostics.Debug.WriteLine($"[MENSAJES] URL completa: {fullUrl}");

                var response = await _httpClient.GetAsync(apiUrl).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[MENSAJES] Respuesta API: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    var mensajesApi = await response.Content.ReadFromJsonAsync<List<MensajeApiResponse>>().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"[MENSAJES] Mensajes recibidos de API: {mensajesApi?.Count ?? 0}");
                    
                    if (mensajesApi != null && mensajesApi.Count > 0)
                    {
                        // Sincronizar con la base de datos local
                        foreach (var msgApi in mensajesApi)
                        {
                            await GuardarMensajeRemotoAsync(msgApi).ConfigureAwait(false);
                        }
                        System.Diagnostics.Debug.WriteLine($"[MENSAJES] Sincronizados {mensajesApi.Count} mensajes a DB local");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[MENSAJES] Error API ({response.StatusCode}): {errorContent}");
                }
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[MENSAJES] Timeout al obtener historial de la API");
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MENSAJES] Error de red al obtener historial: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MENSAJES] Error al obtener mensajes de la API: {ex.Message}");
            }

            // Fallback 100% seguro: Si falla la API (error 500) o no hay internet, retorna datos locales
            System.Diagnostics.Debug.WriteLine("[MENSAJES] Usando fallback local");
            return await _db.Table<CrownBuilders.Models.Mensaje>()
                           .Where(m => m.TrabajadorId == trabajadorId && m.CompaniaId == companiaId)
                           .OrderBy(m => m.FechaEnvio)
                           .ToListAsync().ConfigureAwait(false);
        }

        // --- MÉTODOS DE VALIDACIÓN (SOLO PARA OFFLINE) ---

        public static async Task<Trabajador> ValidarCredencialesTrabajadorAsync(string correo, string password)
        {
            await InitAsync();
            return await _db.Table<Trabajador>()
                           .FirstOrDefaultAsync(t => t.Correo == correo && t.Password == password);
        }

        public static async Task<PerfilCompania> ValidarCredencialesCompaniaAsync(string correo, string password)
        {
            await InitAsync();
            return await _db.Table<PerfilCompania>()
                           .FirstOrDefaultAsync(c => c.Correo == correo && c.Password == password);
        }
    }

    // --- MODELOS LOCALES (CACHÉ) ---

    public class PerfilCompania
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        public string? FotoPerfil { get; set; }
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
        public string? FcmToken { get; set; }
    }

    public class Trabajador
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        public string FotoPerfil { get; set; }
        public string Nombre { get; set; }
        public string Oficio { get; set; }
        public string Especialidad { get; set; }
        public string Ciudad { get; set; }
        public int Experiencia { get; set; }
        public int Salario { get; set; }
        public string Correo { get; set; }
        public string Telefono { get; set; }
        public string Password { get; set; }
        public bool TieneVehiculo { get; set; }
        public bool TieneHerramientas { get; set; }
        public bool EstaDisponible { get; set; }
        public string? FcmToken { get; set; }
    }

    public class Garantia { [PrimaryKey, AutoIncrement] public int Id { get; set; } public string Descripcion { get; set; } public bool SincronizadoNube { get; set; } = false; }
    public class Asistencia { [PrimaryKey, AutoIncrement] public int Id { get; set; } public string Fecha { get; set; } }

    public sealed class MensajeApiResponse
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public int WorkerId { get; set; }
        public int CompanyId { get; set; }
        public string ClientMessageId { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime SentAtUtc { get; set; }
    }

    public class GarantiaInasistencia
    {
        [PrimaryKey, AutoIncrement] public int Id { get; set; }
        public string Descripcion { get; set; }
        public bool SincronizadoNube { get; set; } = false;
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
