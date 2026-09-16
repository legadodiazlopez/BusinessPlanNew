using System.Security.Claims;
using CrownBuilders.API.Controllers;
using CrownBuilders.API.Data;
using CrownBuilders.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class WorkerProfileAuthorizationTests
{
    [Fact]
    public async Task ActualizarTrabajador_ReturnsForbid_WhenTheProfileBelongsToAnotherWorker()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, authenticatedWorkerId: 44);

        var result = await controller.ActualizarTrabajador(
            17,
            new CrownBuilders.API.Models.Trabajador
            {
                Id = 17,
                Nombre = "Perfil ajeno"
            });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ActualizarTrabajador_ReturnsOk_WhenTheProfileBelongsToTheAuthenticatedWorker()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, authenticatedWorkerId: 17);

        var result = await controller.ActualizarTrabajador(
            17,
            new CrownBuilders.API.Models.Trabajador
            {
                Id = 17,
                Nombre = "Perfil propio"
            });

        Assert.IsType<OkObjectResult>(result);
    }

    private static AuthController CreateController(ApplicationDbContext context, int authenticatedWorkerId)
    {
        var controller = new AuthController(context, new JwtTokenService(new JwtOptions
        {
            Key = "clave-de-prueba-con-al-menos-treinta-y-dos-caracteres",
            Issuer = "CrownBuilders.Tests",
            Audience = "CrownBuilders.Tests"
        }));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, authenticatedWorkerId.ToString()),
                    new Claim(ClaimTypes.Role, "trabajador")
                ],
                "prueba"))
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
