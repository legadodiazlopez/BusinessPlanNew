using System.Collections.Concurrent;

namespace CrownBuilders.API.Services;

public sealed class ActiveConnectionRegistry
{
    private readonly ConcurrentDictionary<string, byte> _connections = new();
    private readonly ConcurrentDictionary<string, string> _connectionUsers = new();

    public void Register(AuthenticatedUser user, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        var userKey = GetUserKey(user);
        _connections.TryAdd(GetConnectionKey(userKey, connectionId), 0);
        _connectionUsers[connectionId] = userKey;
    }

    public void Remove(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) ||
            !_connectionUsers.TryRemove(connectionId, out var userKey))
        {
            return;
        }

        _connections.TryRemove(GetConnectionKey(userKey, connectionId), out _);
    }

    public bool IsConnected(AuthenticatedUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var userKey = GetUserKey(user);
        return _connectionUsers.Values.Any(value => value == userKey);
    }

    private static string GetUserKey(AuthenticatedUser user) =>
        $"{user.UserType.Trim().ToLowerInvariant()}:{user.UserId}";

    private static string GetConnectionKey(string userKey, string connectionId) =>
        $"{userKey}:{connectionId}";
}
