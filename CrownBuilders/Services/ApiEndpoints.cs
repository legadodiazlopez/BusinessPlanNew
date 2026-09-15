namespace CrownBuilders.Services;

public static class ApiEndpoints
{
    // Producción/Azure:
    // public const string ServerBaseUrl = "https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/";
    // Desarrollo local:
#if ANDROID
    // Emulador Android: 10.0.2.2 representa la computadora anfitriona.
    public const string ServerBaseUrl = "http://10.0.2.2:5136/";
#else
    // Windows y otras plataformas locales:
    public const string ServerBaseUrl = "http://localhost:5136/";
#endif
    public const string ApiBaseUrl = ServerBaseUrl + "api/";
    public const string SignalRHubUrl = ApiBaseUrl + "chathub";
}
