using System.Security.Claims;
using CrownBuilders.API.Contracts;
using CrownBuilders.API.Controllers;
using CrownBuilders.API.Data;
using CrownBuilders.API.Models;
using CrownBuilders.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class MensajesControllerTests
{
    [Fact]
    public async Task GetHistory_ReturnsMessagesInChronologicalOrder()
    {
        await using var context = CreateContext();
        var firstSentAt = DateTime.UtcNow.AddMinutes(-2);
        var secondSentAt = DateTime.UtcNow.AddMinutes(-1);
        context.Conversaciones.Add(new Conversacion
        {
            Id = 1,
            TrabajadorId = 10,
            CompaniaId = 20
        });
        context.Mensajes.AddRange(
            new Mensaje
            {
                Id = 2,
                ConversacionId = 1,
                TrabajadorId = 10,
                CompaniaId = 20,
                ClientMessageId = Guid.NewGuid(),
                Remitente = "compania",
                Texto = "Segundo",
                FechaEnvio = secondSentAt
            },
            new Mensaje
            {
                Id = 1,
                ConversacionId = 1,
                TrabajadorId = 10,
                CompaniaId = 20,
                ClientMessageId = Guid.NewGuid(),
                Remitente = "trabajador",
                Texto = "Primero",
                FechaEnvio = firstSentAt
            });
        await context.SaveChangesAsync();

        var result = await CreateController(context).GetHistory(1, 50, null, null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var messages = Assert.IsAssignableFrom<IReadOnlyList<MessageResponse>>(ok.Value);
        Assert.Equal(new[] { "Primero", "Segundo" }, messages.Select(message => message.Text));
    }

    [Fact]
    public async Task GetHistory_RejectsLimitOutsideSupportedRange()
    {
        await using var context = CreateContext();

        var result = await CreateController(context).GetHistory(1, 101, null, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetHistory_RejectsParticipantOutsideConversation()
    {
        await using var context = CreateContext();
        context.Conversaciones.Add(new Conversacion
        {
            Id = 1,
            TrabajadorId = 10,
            CompaniaId = 20
        });
        await context.SaveChangesAsync();

        var result = await CreateController(context, "compania", 30).GetHistory(1, 50, null, null);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetHistory_RejectsIncompleteCursor()
    {
        await using var context = CreateContext();
        context.Conversaciones.Add(new Conversacion
        {
            Id = 1,
            TrabajadorId = 10,
            CompaniaId = 20
        });
        await context.SaveChangesAsync();

        var result = await CreateController(context).GetHistory(
            1,
            50,
            DateTime.UtcNow,
            null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static MensajesController CreateController(
        ApplicationDbContext context,
        string userType = "trabajador",
        int userId = 10)
    {
        var controller = new MensajesController(
            context,
            new ConversationService(context),
            new MessagePersistenceService(context),
            null!);
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("user_type", userType)
            },
            "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return controller;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
