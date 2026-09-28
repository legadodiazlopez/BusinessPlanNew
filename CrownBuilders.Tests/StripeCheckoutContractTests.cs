using System.Reflection;
using System.Text.Json.Serialization;
using CrownBuilders.API.Models;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class StripeCheckoutContractTests
{
    [Fact]
    public void StripeController_ExposesProtectedCheckoutAndSignedWebhook()
    {
        var controllerType = FindStripeControllerType();
        var checkoutMethod = controllerType.GetMethod("CreateCheckoutSession");
        var webhookMethod = controllerType.GetMethod("HandleWebhook");

        Assert.NotNull(checkoutMethod);
        Assert.NotNull(webhookMethod);

        var authorize = checkoutMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal("compania", authorize!.Roles);
        Assert.Contains("Stripe-Signature", File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders.API", "Controllers", "StripeController.cs"))));
    }

    [Fact]
    public void CompanyRegistration_DoesNotSendRawCardData()
    {
        var registration = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "RegistroCompania.Razor")));
        var apiModel = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders.API", "Models", "Compania.cs")));

        Assert.DoesNotContain("NumeroTarjeta", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("FechaExpiracion", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("CVC", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("NumeroTarjeta", apiModel, StringComparison.Ordinal);
        Assert.DoesNotContain("FechaExpiracion", apiModel, StringComparison.Ordinal);
        Assert.DoesNotContain("CVC", apiModel, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_UsesResponsiveHeaderClasses()
    {
        var layout = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Layout", "MainLayout.razor")));
        var styles = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Layout", "MainLayout.razor.css")));

        Assert.Contains("cb-topbar", layout, StringComparison.Ordinal);
        Assert.Contains("cb-topbar__brand", layout, StringComparison.Ordinal);
        Assert.Contains("cb-topbar__support", layout, StringComparison.Ordinal);
        Assert.Contains("text-overflow: ellipsis", styles, StringComparison.Ordinal);
        Assert.Contains("min-width: 0", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyPaymentFields_AreNeverSerializedByTheApiModel()
    {
        foreach (var propertyName in new[] { "NombreTitular", "NumeroTarjeta", "FechaExpiracion", "CVC" })
        {
            var property = typeof(Compania).GetProperty(propertyName);
            Assert.NotNull(property);
            Assert.NotNull(property!.GetCustomAttribute<JsonIgnoreAttribute>());
        }
    }

    private static Type FindStripeControllerType()
    {
        var controllerType = typeof(CrownBuilders.API.Services.FirebaseNotificationService).Assembly.GetType(
            "CrownBuilders.API.Controllers.StripeController");

        Assert.NotNull(controllerType);
        return controllerType!;
    }

    private static string FindWorkspaceFile(string relativePath)
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("CROWN_BUILDERS_WORKSPACE"),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        }.Where(root => !string.IsNullOrWhiteSpace(root)).Select(root => root!);

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"No se encontró {relativePath}.");
    }
}
