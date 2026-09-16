using Xunit;

namespace CrownBuilders.Tests;

public sealed class DataLoadingOptimizationTests
{
    [Fact]
    public void CompanyDirectory_UsesCentralizedRefreshWithoutDuplicateHttpCall()
    {
        var page = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "CompaniasDisponibles.Razor")));

        Assert.Contains("ObtenerCompaniasLocalesAsync()", page);
        Assert.Contains("ActualizarCompaniasDesdeApiAsync()", page);
        Assert.DoesNotContain("_httpClient.GetAsync(\"Perfil/listar-companias\")", page);
    }

    [Fact]
    public void DatabaseHelper_UsesSharedBoundedRemoteDataLoad()
    {
        var helper = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "DatabaseHelper.cs")));

        Assert.Contains("RemoteDataTimeout", helper);
        Assert.Contains("CancelAfter(RemoteDataTimeout)", helper);
        Assert.Contains("GetRemoteAsync", helper);
        Assert.DoesNotContain("using var client = new HttpClient();", helper);
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
