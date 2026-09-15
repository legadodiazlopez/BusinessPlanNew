using CrownBuilders.API.Services;
using Xunit;

namespace CrownBuilders.Tests;

public class ConversationAccessPolicyTests
{
    [Fact]
    public void CanAccess_AllowsOnlyTheWorkerOrCompanyParticipant()
    {
        var workerCanAccess = ConversationAccessPolicy.CanAccess(10, 20, "trabajador", 10);
        var foreignCompanyCanAccess = ConversationAccessPolicy.CanAccess(10, 20, "compania", 30);

        Assert.True(workerCanAccess);
        Assert.False(foreignCompanyCanAccess);
    }
}
