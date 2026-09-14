using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CrownBuilders.Services
{
    public class StripeService
    {
        private readonly HttpClient _httpClient;

        // Inyectamos el HttpClient configurado para apuntar a tu API
        public StripeService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Envía los datos de la compañía a la API para crear el cliente y la suscripción mensual en Stripe de forma segura.
        /// </summary>
        /// <param name="nombreCompania">Nombre comercial de la empresa</param>
        /// <param name="emailCompania">Correo de contacto para facturación</param>
        /// <param name="telefonoCompania">Teléfono de la compañía</param>
        /// <returns>El ID de la suscripción creada para confirmar el proceso</returns>
        public async Task<string> CrearSuscripcionMensualAsync(string nombreCompania, string emailCompania, string telefonoCompania)
        {
            try
            {
                var requestData = new
                {
                    NombreCompania = nombreCompania,
                    EmailCompania = emailCompania,
                    TelefonoCompania = telefonoCompania
                };

                // Realiza la petición POST al controlador de la API
                var response = await _httpClient.PostAsJsonAsync("api/stripe/create-subscription", requestData);

                if (response.IsSuccessStatusCode)
                {
                    var resultado = await response.Content.ReadFromJsonAsync<SubscriptionResponseDto>();
                    return resultado?.SubscriptionId ?? throw new Exception("No se pudo obtener el ID de la suscripción.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error del servidor: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error general al procesar la suscripción: {ex.Message}");
            }
        }

        /// <summary>
        /// Aplica un saldo a favor comunicándose con el endpoint correspondiente de la API.
        /// </summary>
        /// <param name="emailCompania">Correo de la compañía</param>
        /// <returns>True si el balance se aplicó correctamente</returns>
        public async Task<bool> AplicarGarantiaCreditoAsync(string emailCompania)
        {
            try
            {
                var requestData = new { EmailCompania = emailCompania };

                var response = await _httpClient.PostAsJsonAsync("api/stripe/aplicar-garantia", requestData);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error del servidor al aplicar garantía: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error general al aplicar garantía: {ex.Message}");
            }
        }
    }

    // DTOs auxiliares para deserializar la respuesta de la API
    public class SubscriptionResponseDto
    {
        public string SubscriptionId { get; set; }
        public string ClientSecret { get; set; }
    }
}