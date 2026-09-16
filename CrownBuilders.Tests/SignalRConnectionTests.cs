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

    [Fact]
    public void TypingNotifications_AreWiredThroughTheHubAndBothChatPages()
    {
        var hub = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders.API", "Hub", "ChatHub.cs")));
        var messagingService = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Services", "MessagingService.cs")));
        var workerPage = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "Mensajeriatrabajor.razor")));
        var companyPage = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "Mensajeriacompania.razor")));

        Assert.Contains("public async Task Typing(int conversationId, bool isTyping)", hub);
        Assert.Contains("TypingChanged", hub);
        Assert.Contains("connection.On<int, string, bool>(\"TypingChanged\"", messagingService);
        Assert.Contains("_hubConnection.InvokeAsync(\n                \"Typing\"", messagingService);
        Assert.Contains("Escribiendo...", workerPage);
        Assert.Contains("Escribiendo...", companyPage);
        Assert.Contains("TypingChanged += OnTypingChangedAsync", workerPage);
        Assert.Contains("TypingChanged += OnTypingChangedAsync", companyPage);
    }

    [Fact]
    public void SwitchingAuthenticatedAccounts_ReleasesThePreviousChatConnection()
    {
        var messagingService = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Services", "MessagingService.cs")));
        var companyPortal = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders", "Components", "Pages", "PortalCompania.razor")));

        Assert.Contains("_connectionAuthToken", messagingService);
        Assert.Contains("StopConnectionAsync", messagingService);
        Assert.Contains("await MessagingService.StopAsync()", companyPortal);
        Assert.Contains("await ApiService.SetAuthTokenAsync(null)", companyPortal);
    }

    [Fact]
    public void ApiDelivery_IsNotCoupledToRequestCancellation()
    {
        var controller = File.ReadAllText(FindWorkspaceFile(
            Path.Combine("CrownBuilders.API", "Controllers", "MensajesController.cs")));

        Assert.Contains("await _messageDeliveryService.DeliverAsync(response);", controller);
        Assert.DoesNotContain(
            "await _messageDeliveryService.DeliverAsync(response, cancellationToken);",
            controller);
    }

    private static string FindProjectPath(string projectFileName)
    {
        var configuredWorkspace = Environment.GetEnvironmentVariable("CROWN_BUILDERS_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(configuredWorkspace))
        {
            var configuredCandidates = new[]
            {
                Path.Combine(configuredWorkspace, projectFileName),
                Path.Combine(configuredWorkspace, "CrownBuilders", projectFileName)
            };

            var configuredProjectPath = configuredCandidates.FirstOrDefault(File.Exists);
            if (configuredProjectPath is not null)
            {
                return configuredProjectPath;
            }
        }

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
        var configuredWorkspace = Environment.GetEnvironmentVariable("CROWN_BUILDERS_WORKSPACE");
        if (!string.IsNullOrWhiteSpace(configuredWorkspace))
        {
            var configuredPath = Path.Combine(configuredWorkspace, relativePath);
            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

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
