using System.Globalization;
using System.Security.Claims;

namespace CrownBuilders.API.Services;

public static class AuthenticatedUserAccessor
{
    public static AuthenticatedUser FromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("La solicitud requiere un usuario autenticado.");
        }

        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        var userType = principal.FindFirstValue("user_type")
            ?? principal.FindFirstValue(ClaimTypes.Role);

        if (!int.TryParse(idValue, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) ||
            userId <= 0 ||
            string.IsNullOrWhiteSpace(userType))
        {
            throw new UnauthorizedAccessException("El token no contiene una identidad válida.");
        }

        var normalizedType = userType.Trim().ToLowerInvariant();
        if (normalizedType is not ("trabajador" or "compania"))
        {
            throw new UnauthorizedAccessException("El token contiene un tipo de usuario no permitido.");
        }

        return new AuthenticatedUser(normalizedType, userId);
    }
}
