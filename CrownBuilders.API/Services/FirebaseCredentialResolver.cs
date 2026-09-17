using System.Text;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;

namespace CrownBuilders.API.Services;

public static class FirebaseCredentialResolver
{
    public static GoogleCredential? Resolve(IConfiguration configuration, string baseDirectory)
    {
        var encodedCredentials = configuration["Firebase:CredentialsBase64"];
        if (!string.IsNullOrWhiteSpace(encodedCredentials))
        {
            var credentialsJson = DecodeBase64(encodedCredentials);
            return CredentialFactory
                .FromJson<ServiceAccountCredential>(credentialsJson)
                .ToGoogleCredential();
        }

        var configuredPath = configuration["Firebase:CredentialPath"];
        var credentialPath = FindCredentialPath(configuredPath, baseDirectory);
        if (credentialPath is null)
        {
            return null;
        }

        return CredentialFactory
            .FromFile<ServiceAccountCredential>(credentialPath)
            .ToGoogleCredential();
    }

    public static string DecodeBase64(string encodedCredentials)
    {
        try
        {
            var bytes = Convert.FromBase64String(encodedCredentials);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "La configuración Firebase:CredentialsBase64 no contiene Base64 válido.",
                exception);
        }
    }

    private static string? FindCredentialPath(string? configuredPath, string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var configurationDirectory = Path.Combine(baseDirectory, "FirebaseConfig");
        return Directory.Exists(configurationDirectory)
            ? Directory.EnumerateFiles(configurationDirectory, "*.json").FirstOrDefault()
            : null;
    }
}
