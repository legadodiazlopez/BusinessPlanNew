using CrownBuilders.API.Hubs;
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
}
