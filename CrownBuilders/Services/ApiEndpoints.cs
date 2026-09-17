namespace CrownBuilders.Services;

public static class ApiEndpoints
{
    public const string ServerBaseUrl =
        "https://crownbuilders-portal-drbqbfbphpe7hude.centralus-01.azurewebsites.net/";

    public const string ApiBaseUrl = ServerBaseUrl + "api/";
    public const string SignalRHubUrl = ApiBaseUrl + "chathub";
}
