using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using CrownBuilders.API.Services;
using Xunit;

namespace CrownBuilders.Tests;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_ContainsUserIdentityClaims()
    {
        var service = new JwtTokenService(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Key = "test-signing-key-with-at-least-32-characters"
        });
        var token = service.CreateToken(42, "trabajador", "Ana");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("42", jwt.Subject);
        Assert.Equal("trabajador", jwt.Claims.Single(c => c.Type == "user_type").Value);
        Assert.Equal("Ana", jwt.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.Name).Value);
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
    }

    [Fact]
    public void CreateValidationParameters_RejectsTokenWithInvalidSignature()
    {
        var service = new JwtTokenService(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Key = "test-signing-key-with-at-least-32-characters"
        });
        var token = service.CreateToken(42, "trabajador", "Ana");
        var parts = token.Split('.');
        parts[2] = "invalid-signature";
        var tamperedToken = string.Join('.', parts);

        Assert.Throws<SecurityTokenInvalidSignatureException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                tamperedToken,
                service.CreateValidationParameters(),
                out _));
    }
}
