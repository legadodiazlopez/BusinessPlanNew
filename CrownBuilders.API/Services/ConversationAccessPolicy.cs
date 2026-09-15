namespace CrownBuilders.API.Services;

public static class ConversationAccessPolicy
{
    public static bool CanAccess(int workerId, int companyId, string? userType, int userId)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(userType))
        {
            return false;
        }

        return userType.Trim().ToLowerInvariant() switch
        {
            "trabajador" => userId == workerId,
            "compania" => userId == companyId,
            _ => false
        };
    }
}
