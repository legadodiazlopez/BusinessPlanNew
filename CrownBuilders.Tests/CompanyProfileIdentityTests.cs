using Xunit;

namespace CrownBuilders.Tests;

public sealed class CompanyProfileIdentityTests
{
    [Fact]
    public void CompanyProfileScreens_UseTheAuthenticatedCompanyId()
    {
        var portal = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "PortalCompania.razor")));
        var editProfile = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "EditarPerfilCompania.Razor")));
        var registration = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "RegistroCompania.Razor")));
        var databaseHelper = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "DatabaseHelper.cs")));

        Assert.Contains("ObtenerPerfilCompaniaAsync(companiaIdActual)", portal);
        Assert.Contains("ObtenerPerfilCompaniaAsync(ApiService.AuthenticatedUserId ?? 0)", editProfile);
        Assert.Contains("await ApiService.LoginCompaniaAsync(", registration);
        Assert.Contains("Where(p => p.Id == companiaId)", databaseHelper);
        Assert.DoesNotContain("ObtenerPerfilCompaniaAsync()", portal);
        Assert.DoesNotContain("ObtenerPerfilCompaniaAsync()", editProfile);
    }

    [Fact]
    public void WorkerProfileScreens_UseTheAuthenticatedWorkerId()
    {
        var editProfile = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "EditarPerfilTrabajador.razor")));
        var registration = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "RegistroTrabajador.Razor")));
        var databaseHelper = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "DatabaseHelper.cs")));

        Assert.Contains("await ApiService.RestoreAuthTokenAsync();", editProfile);
        Assert.Contains("ApiService.AuthenticatedUserId ?? 0", editProfile);
        Assert.Contains("ObtenerTrabajadorPorIdAsync(trabajadorIdActual)", editProfile);
        Assert.Contains("await ApiService.LoginTrabajadorAsync(", registration);
        Assert.Contains("Where(t => t.Id == trabajadorId)", databaseHelper);
        Assert.DoesNotContain("ObtenerTrabajadoresAsync()", editProfile);
        Assert.DoesNotContain("trabajador = lista[0]", editProfile);
    }

    private static string FindWorkspaceFile(string relativePath)
    {
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("CROWN_BUILDERS_WORKSPACE"),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        }.Where(root => !string.IsNullOrWhiteSpace(root))
         .Select(root => root!);

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
