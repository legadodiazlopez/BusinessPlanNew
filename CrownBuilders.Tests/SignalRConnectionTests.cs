using CrownBuilders.API.Hubs;
using System.Xml.Linq;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class SignalRConnectionTests
{
    [Theory]
    [InlineData(1, "conversation:1")]
    [InlineData(42, "conversation:42")]
    public void GetConversationGroup_UsesOnlyTheStableConversationId(
        int conversationId,
        string expectedGroup)
    {
        Assert.Equal(expectedGroup, ChatHub.GetConversationGroup(conversationId));
    }

    [Fact]
    public void GetConversationGroup_RejectsInvalidConversationId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatHub.GetConversationGroup(0));
    }

    [Fact]
    public void ClientProject_DoesNotReferenceLegacyServerSignalRPackage()
    {
        var projectPath = FindProjectPath("CrownBuilders.csproj");
        var project = XDocument.Load(projectPath);
        var packageNames = project
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain("Microsoft.AspNetCore.SignalR", packageNames);
    }

    [Fact]
    public void ChatPages_UseTheAuthenticatedUserId()
    {
        var workerPage = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "Mensajeriatrabajor.razor")));
        var companyPage = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "Mensajeriacompania.razor")));

        Assert.Contains("trabajadorIdActual = ApiService.AuthenticatedUserId ?? 0;", workerPage);
        Assert.DoesNotContain("ObtenerTrabajadoresAsync()", workerPage);
        Assert.Contains("companiaIdActual = ApiService.AuthenticatedUserId ?? 0;", companyPage);
        Assert.DoesNotContain("ObtenerPerfilCompaniaAsync()", companyPage);
    }

    [Fact]
    public void AndroidMessaging_UsesTheAuthenticatedHttpSendPath()
    {
        var messagingService = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Services", "MessagingService.cs")));

        Assert.Contains("#if ANDROID", messagingService);
        Assert.Contains("_apiService.EnviarMensajeAsync(", messagingService);
        Assert.Contains("SignalR queda como canal de recepción en tiempo real", messagingService);
    }

    private static string FindProjectPath(string projectFileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidates = new[]
            {
                Path.Combine(directory.FullName, projectFileName),
                Path.Combine(directory.FullName, "CrownBuilders", projectFileName)
            };

            var projectPath = candidates.FirstOrDefault(File.Exists);
            if (projectPath is not null)
            {
                return projectPath;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"No se encontró {projectFileName}.");
    }

    private static string FindWorkspaceFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var filePath = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"No se encontró {relativePath}.");
    }
}
