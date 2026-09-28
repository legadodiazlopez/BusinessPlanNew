using System.Net.Http.Json;

namespace CrownBuilders.Services;

public sealed class StripeService
{
    private readonly HttpClient _httpClient;

    public StripeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> AplicarGarantiaCreditoAsync(
        string emailCompania,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "stripe/aplicar-garantia",
            new { EmailCompania = emailCompania },
            cancellationToken);

        return response.IsSuccessStatusCode;
    }
}
