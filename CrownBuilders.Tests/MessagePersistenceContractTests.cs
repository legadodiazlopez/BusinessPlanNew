using CrownBuilders.API.Models;
using Xunit;

namespace CrownBuilders.Tests;

public class MessagePersistenceContractTests
{
    [Fact]
    public void Message_ContainsConversationAndClientIdentity()
    {
        Assert.NotNull(typeof(Mensaje).GetProperty("ConversacionId"));
        Assert.NotNull(typeof(Mensaje).GetProperty("ClientMessageId"));
    }
}
