using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CrownBuilders.Models;
using Microsoft.Maui.Storage;

namespace CrownBuilders.Services
{
    public class ApiService
    {
        private const string AuthTokenStorageKey = "crownbuilders.auth-token";
        private const string AuthenticatedUserIdStorageKey = "crownbuilders.auth-user-id";
        private readonly HttpClient _httpClient;
        private string? _authToken;
        private int? _authenticatedUserId;

        public string? AuthToken => _authToken;
        public int? AuthenticatedUserId => _authenticatedUserId;

        // Producción/Azure:
        // private const string BaseUrl = "https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/api/";
        // Desarrollo local:
        private const string BaseUrl = ApiEndpoints.ApiBaseUrl;

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(60) // Aumentado a 60s para tolerar el cold start de Azure
            };
        }

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? new HttpClient();
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(BaseUrl);
            }
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        /// <summary>
        /// Establece el token de autenticación para todas las peticiones posteriores
        /// </summary>
        public void SetAuthToken(string? token)
        {
            _authToken = token;
            DatabaseHelper.SetAuthToken(token);
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }

        public async Task SetAuthTokenAsync(string? token)
        {
            SetAuthToken(token);

            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    SecureStorage.Default.Remove(AuthTokenStorageKey);
                    SecureStorage.Default.Remove(AuthenticatedUserIdStorageKey);
                    _authenticatedUserId = null;
                    return;
                }

                await SecureStorage.Default.SetAsync(AuthTokenStorageKey, token);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTH] No se pudo guardar la sesión segura: {exception.Message}");
            }
        }

        public async Task RestoreAuthTokenAsync()
        {
            if (!string.IsNullOrWhiteSpace(_authToken))
            {
                return;
            }

            try
            {
                var token = await SecureStorage.Default.GetAsync(AuthTokenStorageKey);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    SetAuthToken(token);

                    var userId = await SecureStorage.Default.GetAsync(AuthenticatedUserIdStorageKey);
                    if (int.TryParse(userId, out var parsedUserId) && parsedUserId > 0)
                    {
                        _authenticatedUserId = parsedUserId;
                    }
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTH] No se pudo restaurar la sesión segura: {exception.Message}");
            }
        }

        public async Task<ConversationApiResponse?> EnsureConversationAsync(
            int workerId,
            int companyId,
            CancellationToken cancellationToken = default)
        {
            await RestoreAuthTokenAsync();

            var response = await _httpClient.PostAsJsonAsync(
                "mensajes/conversaciones",
                new { WorkerId = workerId, CompanyId = companyId },
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<ConversationApiResponse>(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<CrownBuilders.MensajeApiResponse> EnviarMensajeAsync(
            int conversationId,
            Guid clientMessageId,
            string text,
            CancellationToken cancellationToken = default)
        {
            if (conversationId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(conversationId));
            }

            if (clientMessageId == Guid.Empty)
            {
                throw new ArgumentException("La clave de cliente es obligatoria.", nameof(clientMessageId));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("El mensaje no puede estar vacío.", nameof(text));
            }

            await RestoreAuthTokenAsync();
            if (string.IsNullOrWhiteSpace(_authToken))
            {
                throw new UnauthorizedAccessException("La sesión no está autenticada.");
            }

            using var response = await _httpClient.PostAsJsonAsync(
                $"mensajes/conversaciones/{conversationId}/mensajes",
                new
                {
                    ClientMessageId = clientMessageId,
                    Text = text
                },
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"La API rechazó el mensaje ({(int)response.StatusCode} {response.StatusCode}). {detail}",
                    null,
                    response.StatusCode);
            }

            return await response.Content
                       .ReadFromJsonAsync<CrownBuilders.MensajeApiResponse>(cancellationToken: cancellationToken)
                       .ConfigureAwait(false)
                   ?? throw new InvalidOperationException("La API confirmó el mensaje sin devolver sus datos.");
        }

        // --- AUTENTICACIÓN ---

        public async Task<AuthResponse?> LoginTrabajadorAsync(string correo, string password)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Iniciando autenticación para: {correo}");

                var credenciales = new { Correo = correo, Password = password };
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Enviando solicitud a: {BaseUrl}Auth/login-trabajador");

                // Usamos _httpClient unificado para conservar headers y evitar sockets colgados
                var response = await _httpClient.PostAsJsonAsync("Auth/login-trabajador", credenciales);

                System.Diagnostics.Debug.WriteLine($"[LOGIN] Respuesta del servidor: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Autenticación exitosa: {result?.Exitoso}, ID: {result?.Id}");

                    // Inyectar automáticamente el token en el cliente compartido
                    if (!string.IsNullOrEmpty(result?.Token))
                    {
                        await SetAuthTokenAsync(result.Token);
                        await SetAuthenticatedUserIdAsync(result.Id);
                    }

                    return result;
                }

                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[DEBUG LOGIN TRABAJADOR ERROR]: {error}");
                return new AuthResponse { Exitoso = false, Mensaje = error };
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Timeout de conexión - posible cold start de Azure");
                return new AuthResponse { Exitoso = false, Mensaje = "Tiempo de espera agotado. Verifica tu conexión." };
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Error de red: {ex.Message}");
                return new AuthResponse { Exitoso = false, Mensaje = $"Error de red: {ex.Message}" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO LOGIN TRABAJADOR]: {ex.InnerException?.Message ?? ex.Message}");
                return new AuthResponse { Exitoso = false, Mensaje = "Error de conexión" };
            }
        }

        public async Task<AuthResponse?> LoginCompaniaAsync(string correo, string password)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Iniciando autenticación compañía: {correo}");

                var credenciales = new { Correo = correo, Password = password };
                var response = await _httpClient.PostAsJsonAsync("Auth/login-compania", credenciales);

                System.Diagnostics.Debug.WriteLine($"[LOGIN] Respuesta del servidor: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                    System.Diagnostics.Debug.WriteLine($"[LOGIN] Autenticación exitosa: {result?.Exitoso}");

                    // Inyectar automáticamente el token en el cliente compartido
                    if (!string.IsNullOrEmpty(result?.Token))
                    {
                        await SetAuthTokenAsync(result.Token);
                        await SetAuthenticatedUserIdAsync(result.Id);
                    }

                    return result;
                }

                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[DEBUG LOGIN COMPAÑÍA ERROR]: {error}");
                return new AuthResponse { Exitoso = false, Mensaje = error };
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Timeout de conexión");
                return new AuthResponse { Exitoso = false, Mensaje = "Tiempo de espera agotado. Verifica tu conexión." };
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Error de red: {ex.Message}");
                return new AuthResponse { Exitoso = false, Mensaje = $"Error de red: {ex.Message}" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO LOGIN COMPAÑÍA]: {ex.InnerException?.Message ?? ex.Message}");
                return new AuthResponse { Exitoso = false, Mensaje = "Error de conexión" };
            }
        }

        public async Task<bool> VerificarCorreoExistenteAsync(string correo)
        {
            try
            {
                var request = new { Correo = correo };
                var response = await _httpClient.PostAsJsonAsync("Auth/verificar-correo", request);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<VerificarCorreoResponse>();
                    return result?.Existe ?? false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO VERIFICAR CORREO]: {ex.InnerException?.Message ?? ex.Message}");
            }

            return false;
        }

        private async Task SetAuthenticatedUserIdAsync(int userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userId));
            }

            _authenticatedUserId = userId;

            try
            {
                await SecureStorage.Default.SetAsync(
                    AuthenticatedUserIdStorageKey,
                    userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AUTH] No se pudo guardar el ID de sesión: {exception.Message}");
            }
        }

        // --- REGISTRO ---

        public async Task<RegistroResponse?> RegistrarTrabajador(Trabajador trabajador)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("Perfil/guardar-trabajador", trabajador);
                var responseString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                System.Diagnostics.Debug.WriteLine($"[API PERFIL TRABAJADOR] Status: {response.StatusCode} | Respuesta: {responseString}");

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<RegistroResponse>(responseString, options);
                        if (result != null)
                        {
                            result.Exitoso = true;
                            return result;
                        }
                    }
                    catch
                    {
                        if (int.TryParse(responseString, out int newId))
                        {
                            return new RegistroResponse { Exitoso = true, Id = newId, Mensaje = "Registro exitoso." };
                        }
                    }

                    return new RegistroResponse { Exitoso = true, Mensaje = "Registro completado con éxito." };
                }

                try
                {
                    using var doc = JsonDocument.Parse(responseString);
                    var root = doc.RootElement;

                    string mensaje = root.TryGetProperty("mensaje", out var m) ? m.GetString() ?? "" : "";
                    string detalleError = root.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";

                    string mensajeFinal = !string.IsNullOrEmpty(detalleError)
                        ? $"{mensaje}: {detalleError}"
                        : (!string.IsNullOrEmpty(mensaje) ? mensaje : responseString);

                    return new RegistroResponse { Exitoso = false, Mensaje = mensajeFinal };
                }
                catch
                {
                    return new RegistroResponse { Exitoso = false, Mensaje = $"Error ({response.StatusCode}): {responseString}" };
                }
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[API ERROR] Timeout en RegistrarTrabajador.");
                return new RegistroResponse { Exitoso = false, Mensaje = "El servidor tardó demasiado en responder (Timeout)." };
            }
            catch (HttpRequestException httpEx)
            {
                System.Diagnostics.Debug.WriteLine($"[API ERROR DE RED TRABAJADOR]: {httpEx.Message} | Detalle: {httpEx.InnerException?.Message}");
                return new RegistroResponse { Exitoso = false, Mensaje = $"Error de conexión de red: {httpEx.Message}" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API EXCEPCIÓN DETALLADA] Error en RegistrarTrabajador:\n{ex}");
                return new RegistroResponse { Exitoso = false, Mensaje = $"Error de conexión: {ex.Message}" };
            }
        }

        public async Task<RegistroResponse?> RegistrarCompania(Compania compania)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("Perfil/guardar-compania", compania);
                var responseString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                System.Diagnostics.Debug.WriteLine($"[API PERFIL COMPAÑÍA] Status: {response.StatusCode} | Respuesta: {responseString}");

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<RegistroResponse>(responseString, options);
                        if (result != null)
                        {
                            result.Exitoso = true;
                            return result;
                        }
                    }
                    catch
                    {
                        if (int.TryParse(responseString, out int newId))
                        {
                            return new RegistroResponse { Exitoso = true, Id = newId, Mensaje = "Registro exitoso." };
                        }
                    }

                    return new RegistroResponse { Exitoso = true, Mensaje = "Registro completado con éxito." };
                }

                try
                {
                    using var doc = JsonDocument.Parse(responseString);
                    var root = doc.RootElement;

                    string mensaje = root.TryGetProperty("mensaje", out var m) ? m.GetString() ?? "" : "";
                    string detalleError = root.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";

                    string mensajeFinal = !string.IsNullOrEmpty(detalleError)
                        ? $"{mensaje}: {detalleError}"
                        : (!string.IsNullOrEmpty(mensaje) ? mensaje : responseString);

                    return new RegistroResponse { Exitoso = false, Mensaje = mensajeFinal };
                }
                catch
                {
                    return new RegistroResponse { Exitoso = false, Mensaje = $"Error ({response.StatusCode}): {responseString}" };
                }
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[API ERROR] Timeout en RegistrarCompania.");
                return new RegistroResponse { Exitoso = false, Mensaje = "El servidor tardó demasiado en responder (Timeout)." };
            }
            catch (HttpRequestException httpEx)
            {
                System.Diagnostics.Debug.WriteLine($"[API ERROR DE RED COMPAÑÍA]: {httpEx.Message} | Detalle: {httpEx.InnerException?.Message}");
                return new RegistroResponse { Exitoso = false, Mensaje = $"Error de conexión de red: {httpEx.Message}" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API EXCEPCIÓN DETALLADA] Error en RegistrarCompania:\n{ex}");
                return new RegistroResponse { Exitoso = false, Mensaje = $"Error de conexión: {ex.Message}" };
            }
        }

        // --- SERVICIO DE SMS ---

        public async Task<string> EnviarSmsAsync(string telefono, string mensaje)
        {
            try
            {
                var payload = new SmsRequest { Telefono = telefono, Mensaje = mensaje };
                var response = await _httpClient.PostAsJsonAsync("Sms/enviar", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SmsResponse>();
                    return result?.Codigo ?? "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO ENVIAR SMS]: {ex.InnerException?.Message ?? ex.Message}");
            }
            return "";
        }

        // --- OBTENER DATOS ---

        public async Task<Trabajador?> ObtenerTrabajadorAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<Trabajador>($"Perfil/trabajador/{id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO OBTENER TRABAJADOR]: {ex.InnerException?.Message ?? ex.Message}");
                return null;
            }
        }

        public async Task<Compania?> ObtenerCompaniaAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<Compania>($"Perfil/compania/{id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO OBTENER COMPAÑÍA]: {ex.InnerException?.Message ?? ex.Message}");
                return null;
            }
        }

        public async Task<List<Trabajador>?> ObtenerTrabajadoresAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Trabajador>>("Perfil/listar-trabajadores");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO LISTAR TRABAJADORES]: {ex.InnerException?.Message ?? ex.Message}");
                return new List<Trabajador>();
            }
        }

        public async Task<List<Compania>?> ObtenerCompaniasAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Compania>>("Perfil/listar-companias");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO LISTAR COMPAÑÍAS]: {ex.InnerException?.Message ?? ex.Message}");
                return new List<Compania>();
            }
        }

        // --- ACTUALIZAR DATOS ---

        public async Task<bool> ActualizarTrabajadorAsync(Trabajador trabajador)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"Auth/actualizar-trabajador/{trabajador.Id}", trabajador);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO ACTUALIZAR TRABAJADOR]: {ex.InnerException?.Message ?? ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActualizarCompaniaAsync(Compania compania)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"Auth/actualizar-compania/{compania.Id}", compania);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR DETALLADO ACTUALIZAR COMPAÑÍA]: {ex.InnerException?.Message ?? ex.Message}");
                return false;
            }
        }
    }

    // --- MODELOS DE RESPUESTA ---

    public class AuthResponse
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Correo { get; set; }
        public string Tipo { get; set; }
        public string? Token { get; set; }
    }

    public class RegistroResponse
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }
        public int Id { get; set; }
        public string Correo { get; set; }
    }

    public class VerificarCorreoResponse
    {
        public bool Existe { get; set; }
        public string Tipo { get; set; }
    }

    public class SmsRequest
    {
        public string Telefono { get; set; }
        public string Mensaje { get; set; }
    }

    public class SmsResponse
    {
        public bool Exitoso { get; set; }
        public string Codigo { get; set; }
    }

    public sealed class ConversationApiResponse
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public int CompanyId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
