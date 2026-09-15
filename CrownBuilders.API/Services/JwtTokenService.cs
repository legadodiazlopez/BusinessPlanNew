using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CrownBuilders.API.Services;

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(JwtOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string CreateToken(int userId, string userType, string? displayName)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        var normalizedUserType = NormalizeUserType(userType);
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)),
            new("user_type", normalizedUserType),
            new(ClaimTypes.Role, normalizedUserType)
        };

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, displayName.Trim()));
        }

        var credentials = new SigningCredentials(
            CreateSigningKey(),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.LifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenValidationParameters CreateValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSigningKey(),
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    }

    private SymmetricSecurityKey CreateSigningKey()
    {
        if (string.IsNullOrWhiteSpace(_options.Key) || _options.Key.Length < 32)
        {
            throw new InvalidOperationException(
                "La clave JWT debe configurarse fuera del código y tener al menos 32 caracteres.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
    }

    private static string NormalizeUserType(string userType)
    {
        var normalized = userType?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "trabajador" => normalized,
            "compania" => normalized,
            _ => throw new ArgumentException("El tipo de usuario no es válido.", nameof(userType))
        };
    }
}
