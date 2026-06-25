using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Linq;

namespace CrownBuilders
{
    public class SyncService
    {
        private readonly HttpClient _httpClient;

        // Constructor que inicializa el cliente para las llamadas API
        public SyncService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.crownbuildersapp.com/v1/"), // URL del backend central
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        // Proceso principal de subida de datos pendientes
        public async Task<int> SincronizarGarantiasPendientesAsync()
        {
            try
            {
                // 1. Leer los registros locales que no se han subido a la nube
                var todas = await DatabaseHelper.ObtenerGarantiasAsync();
                var pendientes = todas.Where(g => !g.SincronizadoNube).ToList();

                if (!pendientes.Any()) return 0; // Nada que subir

                int subidosConExito = 0;

                foreach (var garantia in pendientes)
                {
                    // 2. Enviar el reclamo al servidor central
                    var response = await _httpClient.PostAsJsonAsync("garantias/reclamar", garantia);

                    if (response.IsSuccessStatusCode)
                    {
                        // 3. Si el servidor lo recibe correctamente, lo marcamos como sincronizado
                        garantia.SincronizadoNube = true;
                        await DatabaseHelper.GuardarGarantiaInasistenciaAsync(garantia);
                        subidosConExito++;
                    }
                }

                return subidosConExito;
            }
            catch (Exception)
            {
                // Si no hay internet o el servidor no responde, fallará silenciosamente
                // manteniendo los datos seguros en el archivo local empleo.db
                return -1;
            }
        }
    }
}