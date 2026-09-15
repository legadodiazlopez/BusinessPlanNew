using CrownBuilders.API.Services;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class ActiveConnectionRegistryTests
{
    [Fact]
    public void RegisterAndRemove_TracksConnectionsPerUser()
    {
        var registry = new ActiveConnectionRegistry();

        registry.Register(new AuthenticatedUser("trabajador", 7), "connection-1");
        registry.Register(new AuthenticatedUser("trabajador", 7), "connection-2");

        Assert.True(registry.IsConnected(new AuthenticatedUser("trabajador", 7)));
        Assert.False(registry.IsConnected(new AuthenticatedUser("compania", 7)));

        registry.Remove("connection-1");
        Assert.True(registry.IsConnected(new AuthenticatedUser("trabajador", 7)));

        registry.Remove("connection-2");
        Assert.False(registry.IsConnected(new AuthenticatedUser("trabajador", 7)));
    }
}
