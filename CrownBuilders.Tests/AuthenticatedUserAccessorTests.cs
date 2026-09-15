using System.Security.Claims;
using CrownBuilders.API.Services;
using Xunit;

namespace CrownBuilders.Tests;

public sealed class AuthenticatedUserAccessorTests
{
    [Fact]
    public void FromPrincipal_ReadsValidatedIdAndTypeClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim("user_type", "compania")
        ],
        "test"));

        var user = AuthenticatedUserAccessor.FromPrincipal(principal);

        Assert.Equal(42, user.UserId);
        Assert.Equal("compania", user.UserType);
    }

    [Fact]
    public void FromPrincipal_RejectsMissingIdentity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Throws<UnauthorizedAccessException>(() =>
            AuthenticatedUserAccessor.FromPrincipal(principal));
    }
}
