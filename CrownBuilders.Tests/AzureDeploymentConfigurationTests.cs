using System.Reflection;
using System.Text;
using System.Text.Json;
using CrownBuilders.API.Services;
using CrownBuilders.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class AzureDeploymentConfigurationTests
{
    private const string AzureBaseUrl =
        "https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/";

    [Fact]
    public void ClientEndpoints_UseAzureHttps()
    {
        Assert.Equal(AzureBaseUrl, ApiEndpoints.ServerBaseUrl);
        Assert.Equal($"{AzureBaseUrl}api/", ApiEndpoints.ApiBaseUrl);
        Assert.Equal($"{AzureBaseUrl}api/chathub", ApiEndpoints.SignalRHubUrl);
        Assert.DoesNotContain("localhost", ApiEndpoints.ServerBaseUrl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("10.0.2.2", ApiEndpoints.ServerBaseUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HealthEndpoint_ReturnsHealthyResponse()
    {
        var controllerType = typeof(FirebaseNotificationService).Assembly.GetType(
            "CrownBuilders.API.Controllers.HealthController");

        Assert.NotNull(controllerType);
        var controller = Activator.CreateInstance(controllerType);
        var getMethod = controllerType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(getMethod);

        var result = Assert.IsType<OkObjectResult>(getMethod.Invoke(controller, null));
        var payload = JsonSerializer.Serialize(result.Value);

        Assert.Equal(200, result.StatusCode);
        Assert.Contains("\"status\":\"healthy\"", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FirebaseCredentialsBase64_DecodesValidJson()
    {
        const string credentialsJson = "{\"type\":\"service_account\"}";
        var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentialsJson));
        var decodeMethod = FindFirebaseDecodeMethod();

        var decodedCredentials = decodeMethod.Invoke(null, new object[] { encodedCredentials });

        Assert.Equal(credentialsJson, decodedCredentials);
    }

    [Fact]
    public void FirebaseCredentialsBase64_RejectsInvalidValue()
    {
        var decodeMethod = FindFirebaseDecodeMethod();

        var exception = Assert.Throws<TargetInvocationException>(() =>
            decodeMethod.Invoke(null, new object[] { "not-base64" }));

        var configurationException = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("Base64", configurationException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SourceAndPublishConfiguration_DoNotBypassTlsValidation()
    {
        var workspaceDirectory = FindWorkspaceDirectory(string.Empty);
        var clientDirectory = FindWorkspaceDirectory("CrownBuilders");
        var insecureFiles = Directory
            .EnumerateFiles(clientDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains(
                "ServerCertificateCustomValidationCallback",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(workspaceDirectory, path))
            .ToArray();

        Assert.Empty(insecureFiles);
    }

    [Fact]
    public void SourceAndPublishConfiguration_DoNotContainProductionSecrets()
    {
        var appSettings = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders.API", "appsettings.json")));
        var apiProject = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders.API", "CrownBuilders.API.csproj")));
        var program = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders.API", "Program.cs")));
        var gitIgnore = File.ReadAllText(FindWorkspaceFile(".gitignore"));
        var firebaseKeyPath = Path.Combine(
            FindWorkspaceDirectory("CrownBuilders.API"),
            "FirebaseConfig",
            "crown-builders-firebase-adminsdk-fbsvc-1404d7f1a4.json");

        Assert.DoesNotContain("Password=", appSettings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accesskey=", appSettings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "FirebaseConfig\\crown-builders-firebase-adminsdk-fbsvc-1404d7f1a4.json",
            apiProject,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("?? \"Server=(localdb)", program, StringComparison.Ordinal);
        Assert.Contains("CrownBuilders.API/FirebaseConfig/*.json", gitIgnore, StringComparison.Ordinal);
        Assert.False(File.Exists(firebaseKeyPath));
    }

    private static MethodInfo FindFirebaseDecodeMethod()
    {
        var resolverType = typeof(FirebaseNotificationService).Assembly.GetType(
            "CrownBuilders.API.Services.FirebaseCredentialResolver");

        Assert.NotNull(resolverType);
        var decodeMethod = resolverType.GetMethod("DecodeBase64", BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(decodeMethod);
        return decodeMethod;
    }

    private static string FindWorkspaceFile(string relativePath)
    {
        var filePath = Path.Combine(FindWorkspaceDirectory(string.Empty), relativePath);
        if (File.Exists(filePath))
        {
            return filePath;
        }

        throw new FileNotFoundException($"No se encontró {relativePath}.");
    }

    private static string FindWorkspaceDirectory(string relativePath)
    {
        var configuredWorkspace = Environment.GetEnvironmentVariable("CROWN_BUILDERS_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(configuredWorkspace))
        {
            var configuredPath = Path.Combine(configuredWorkspace, relativePath);
            if (Directory.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidatePath = Path.Combine(directory.FullName, relativePath);
            if (Directory.Exists(Path.Combine(directory.FullName, "CrownBuilders.API")) &&
                Directory.Exists(candidatePath))
            {
                return candidatePath;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"No se encontró el directorio {relativePath}.");
    }
}
