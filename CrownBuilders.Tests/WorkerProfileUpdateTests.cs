using System.Net;
using System.Text;
using CrownBuilders.Services;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class WorkerProfileUpdateTests
{
    [Fact]
    public async Task ObtenerTrabajadorAsync_PreservesTheSalaryFromTheApi()
    {
        using var httpClient = CreateHttpClient(new StaticResponseHandler(
            """{"id":17,"nombre":"Mario","salario":50000,"aceptaTerminos":true}"""));
        var apiService = new ApiService(httpClient);

        var trabajador = await apiService.ObtenerTrabajadorAsync(17);

        Assert.NotNull(trabajador);
        Assert.Equal(50000, trabajador.Salario);
        Assert.True(trabajador.AceptaTerminos);
    }

    [Fact]
    public async Task ActualizarTrabajadorAsync_ReturnsFalse_WhenApiRejectsTheUpdateInItsResponseBody()
    {
        using var httpClient = CreateHttpClient(new StaticResponseHandler(
            """{"exitoso":false,"mensaje":"No se pudo actualizar"}"""));
        var apiService = new ApiService(httpClient);

        var actualizado = await apiService.ActualizarTrabajadorAsync(new CrownBuilders.Models.Trabajador
        {
            Id = 17,
            Nombre = "Perfil de prueba"
        });

        Assert.False(actualizado);
    }

    [Fact]
    public async Task ActualizarTrabajadorAsync_ReturnsTrue_WhenApiConfirmsTheUpdate()
    {
        var handler = new StaticResponseHandler("""{"exitoso":true,"mensaje":"Perfil actualizado"}""");
        using var httpClient = CreateHttpClient(handler);
        var apiService = new ApiService(httpClient);

        var actualizado = await apiService.ActualizarTrabajadorAsync(new CrownBuilders.Models.Trabajador
        {
            Id = 17,
            Nombre = "Perfil de prueba",
            AceptaTerminos = true,
            FcmToken = "token-de-prueba"
        });

        Assert.True(actualizado);
        Assert.Contains("\"aceptaTerminos\":true", handler.RequestBody);
        Assert.Contains("\"fcmToken\":\"token-de-prueba\"", handler.RequestBody);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.crownbuilders.test/")
        };
    }

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            return response;
        }
    }
}
