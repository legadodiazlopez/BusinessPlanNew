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

    private static string FindWorkspaceFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"No se encontró {relativePath}.");
    }
}
