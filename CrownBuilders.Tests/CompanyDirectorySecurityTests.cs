using System.Text.Json;
using CrownBuilders.API.Controllers;
using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using CrownBuilders.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class CompanyDirectorySecurityTests
{
    [Fact]
    public async Task PerfilDirectory_ReturnsOnlyPublicCompanyFields()
    {
        await using var context = CreateContext();
        context.Companias.Add(CreateCompany());
        await context.SaveChangesAsync();

        var result = await new PerfilController(context).GetCompanias();

        var ok = Assert.IsType<OkObjectResult>(result);
        AssertPublicDirectoryPayload(ok.Value);
    }

    [Fact]
    public async Task AuthDirectory_ReturnsOnlyPublicCompanyFields()
    {
        await using var context = CreateContext();
        context.Companias.Add(CreateCompany());
        await context.SaveChangesAsync();
        var controller = new AuthController(context, new JwtTokenService(new JwtOptions
        {
            Key = "clave-de-prueba-con-al-menos-treinta-y-dos-caracteres",
            Issuer = "CrownBuilders.Tests",
            Audience = "CrownBuilders.Tests"
        }));

        var result = await controller.ObtenerCompanias();

        var ok = Assert.IsType<OkObjectResult>(result);
        AssertPublicDirectoryPayload(ok.Value);
    }

    private static void AssertPublicDirectoryPayload(object? value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("nombreEmpresa", json);
        Assert.Contains("especialidad", json);
        Assert.Contains("ciudad", json);
        Assert.Contains("formaPago", json);
        Assert.Contains("montoPago", json);
        Assert.DoesNotContain("fotoPerfil", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correo", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("telefono", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nombreTitular", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("numeroTarjeta", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fechaExpiracion", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cvc", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fcmToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(json.Length < 1_000, $"El directorio público es demasiado grande: {json.Length} caracteres.");
    }

    private static Compania CreateCompany() => new()
    {
        Id = 1,
        FotoPerfil = $"data:image/jpeg;base64,{new string('a', 10_000)}",
        NombreEmpresa = "Compañía de prueba",
        Especialidad = "Construcción",
        Ciudad = "La Paz",
        Correo = "privado@example.com",
        Telefono = "00000000",
        Password = "no-debe-salir",
        FormaPago = "porJornada",
        MontoPago = 40m,
        NombreTitular = "Dato privado",
        NumeroTarjeta = "4111111111111111",
        FechaExpiracion = "12/30",
        CVC = "123",
        AceptaTerminos = true,
        FcmToken = "token-privado"
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
