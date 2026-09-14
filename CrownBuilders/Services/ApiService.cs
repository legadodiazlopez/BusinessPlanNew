using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CrownBuilders.Models;

namespace CrownBuilders.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private string? _authToken;

        // --- CONEXIÓN DIRECTA Y PERMANENTE A AZURE EN LA NUBE ---
        private const string BaseUrl = "https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/api/";

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
                        SetAuthToken(result.Token);
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
                        SetAuthToken(result.Token);
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
}