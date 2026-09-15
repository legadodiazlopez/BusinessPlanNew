using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using CrownBuilders.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class ConversationServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_ReusesConversationForSameParticipants()
    {
        await using var context = CreateContext();
        var service = new ConversationService(context);

        var first = await service.GetOrCreateAsync(7, 11);
        var second = await service.GetOrCreateAsync(7, 11);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await context.Conversaciones.ToListAsync());
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData(7, 0)]
    public async Task GetOrCreateAsync_RejectsInvalidParticipantIds(int workerId, int companyId)
    {
        await using var context = CreateContext();
        var service = new ConversationService(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetOrCreateAsync(workerId, companyId));
    }

    [Fact]
    public async Task GetAuthorizedAsync_RejectsParticipantFromAnotherConversation()
    {
        await using var context = CreateContext();
        var conversation = new Conversacion { TrabajadorId = 7, CompaniaId = 11 };
        context.Conversaciones.Add(conversation);
        await context.SaveChangesAsync();
        var service = new ConversationService(context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetAuthorizedAsync(
                conversation.Id,
                new AuthenticatedUser("trabajador", 99)));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
