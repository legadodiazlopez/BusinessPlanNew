using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using CrownBuilders.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrownBuilders.Tests;

public class MessagePersistenceServiceTests
{
    [Fact]
    public async Task SendAsync_RepeatedClientMessageIdStoresOnlyOneMessage()
    {
        await using var context = CreateContext();
        context.Conversaciones.Add(new Conversacion
        {
            Id = 1,
            TrabajadorId = 10,
            CompaniaId = 20
        });
        await context.SaveChangesAsync();

        var service = new MessagePersistenceService(context);
        var clientMessageId = Guid.NewGuid();
        var request = new SendMessageRequest(1, clientMessageId, "  Mensaje único  ");
        var user = new AuthenticatedUser("trabajador", 10);

        var first = await service.SendAsync(request, user);
        var second = await service.SendAsync(request, user);

        Assert.Equal(MessageWriteOutcome.Created, first.Outcome);
        Assert.Equal(MessageWriteOutcome.AlreadyExists, second.Outcome);
        Assert.Equal(1, await context.Mensajes.CountAsync());
        Assert.Equal("Mensaje único", second.Message.Texto);
    }

    [Fact]
    public async Task SendAsync_ReusedClientMessageIdWithDifferentTextThrowsConflict()
    {
        await using var context = CreateContextWithConversation();
        var service = new MessagePersistenceService(context);
        var clientMessageId = Guid.NewGuid();
        var user = new AuthenticatedUser("trabajador", 10);

        await service.SendAsync(new SendMessageRequest(1, clientMessageId, "Original"), user);

        await Assert.ThrowsAsync<MessageConflictException>(() =>
            service.SendAsync(new SendMessageRequest(1, clientMessageId, "Modificado"), user));
    }

    [Fact]
    public async Task SendAsync_RejectsUserOutsideConversation()
    {
        await using var context = CreateContextWithConversation();
        var service = new MessagePersistenceService(context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SendAsync(
                new SendMessageRequest(1, Guid.NewGuid(), "No autorizado"),
                new AuthenticatedUser("compania", 30)));
    }

    private static ApplicationDbContext CreateContextWithConversation()
    {
        var context = CreateContext();
        context.Conversaciones.Add(new Conversacion
        {
            Id = 1,
            TrabajadorId = 10,
            CompaniaId = 20
        });
        context.SaveChanges();
        return context;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
