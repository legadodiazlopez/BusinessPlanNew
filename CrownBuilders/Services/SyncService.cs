using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Linq;

namespace CrownBuilders.Services
{
    public class SyncService
    {
        // Instancia estática compartida para reutilizar sockets y evitar saturar el sistema
        private static readonly HttpClient _httpClient = new HttpClient
        {
            // Producción/Azure: https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/
            // Desarrollo local:
            BaseAddress = new Uri(ApiEndpoints.ServerBaseUrl),
            Timeout = TimeSpan.FromSeconds(8) // Reducido a 8s para que si Azure no responde, pase a caché local de inmediato
        };

        // Proceso principal de subida de datos pendientes
        public async Task<int> SincronizarGarantiasPendientesAsync()
        {
            try
            {
                var todas = await DatabaseHelper.ObtenerGarantiasAsync();
                var pendientes = todas.Where(g => !g.SincronizadoNube).ToList();

                if (!pendientes.Any()) return 0;

                int subidosConExito = 0;
                int fallidos = 0;

                foreach (var garantia in pendientes)
                {
                    try
                    {
                        var response = await _httpClient.PostAsJsonAsync("garantias/reclamar", garantia);

                        if (response.IsSuccessStatusCode)
                        {
                            garantia.SincronizadoNube = true;
                            await DatabaseHelper.GuardarGarantiaAsync(garantia);
                            subidosConExito++;
                        }
                        else
                        {
                            fallidos++;
                            System.Diagnostics.Debug.WriteLine($"[SYNC] Garantía {garantia.Id} rechazada por el servidor: {response.StatusCode}");
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        fallidos++;
                        System.Diagnostics.Debug.WriteLine($"[SYNC] Error de red sincronizando garantía {garantia.Id}: {httpEx.Message}");
                    }
                    catch (TaskCanceledException)
                    {
                        fallidos++;
                        System.Diagnostics.Debug.WriteLine($"[SYNC] Timeout sincronizando garantía {garantia.Id}");
                    }
                    catch (Exception ex)
                    {
                        fallidos++;
                        System.Diagnostics.Debug.WriteLine($"[SYNC] Error sincronizando garantía {garantia.Id}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[SYNC] Sincronización completada: {subidosConExito} exitosos, {fallidos} fallidos");
                return subidosConExito;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SYNC ERROR] Error general en sincronización: {ex.Message}");
                return 0; // Retornar 0 en caso de error general para no romper el flujo
            }
        }
    }
}
