using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace CrownBuilders.Services
{
    public class PushNotificationHandler
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        // Producción/Azure:
        // private static readonly string _apiBaseUrl = "https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/api";
        // Desarrollo local:
        private static readonly string _apiBaseUrl = ApiEndpoints.ApiBaseUrl.TrimEnd('/');

        /// <summary>
        /// Envía el FCM Token del usuario logueado hacia la API de Azure
        /// </summary>
        /// <param name="userId">ID del usuario (trabajador o compañía)</param>
        /// <param name="fcmToken">Token FCM del dispositivo</param>
        /// <param name="userType">Tipo de usuario: "trabajador" o "compania"</param>
        public static async Task<bool> SendFcmTokenAsync(int userId, string fcmToken, string userType)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[PushHandler] Enviando FCM token para {userType} ID: {userId}");

                var endpoint = userType.ToLower() == "trabajador" 
                    ? $"{_apiBaseUrl}/Perfil/trabajador/{userId}/token"
                    : $"{_apiBaseUrl}/Perfil/compania/{userId}/token";

                var request = new { FcmToken = fcmToken };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[PushHandler] FCM token enviado exitosamente");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[PushHandler] Error enviando FCM token: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PushHandler] Excepción enviando FCM token: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Método simulado para obtener el FCM token del dispositivo
        /// En producción, esto debería usar el SDK de Firebase/OneSignal
        /// </summary>
        public static async Task<string> GetDeviceFcmTokenAsync()
        {
            // Placeholder - en producción esto debe ser implementado con el SDK de Firebase Cloud Messaging
            // para obtener el token real del dispositivo
            System.Diagnostics.Debug.WriteLine("[PushHandler] Obteniendo FCM token del dispositivo (simulado)");
            
            // Simular un delay de obtención de token
            await Task.Delay(100);
            
            // Retornar un token placeholder para pruebas
            return "simulated_fcm_token_" + Guid.NewGuid().ToString();
        }
    }
}
