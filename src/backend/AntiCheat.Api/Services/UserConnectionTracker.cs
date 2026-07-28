using System.Collections.Concurrent;
using AntiCheat.Core.Interfaces;

namespace AntiCheat.Api.Services;

public class UserConnectionTracker : IUserConnectionTracker
{
    private readonly ConcurrentDictionary<string, int> _connections = new();

    public int AddConnection(string userId)
    {
        return _connections.AddOrUpdate(userId, 1, (_, count) => count + 1);
    }

    public int RemoveConnection(string userId)
    {
        if (!_connections.TryGetValue(userId, out var count) || count <= 0)
            return 0;
        var newCount = count - 1;
        if (newCount <= 0)
            _connections.TryRemove(userId, out _);
        else
            _connections.TryUpdate(userId, newCount, count);
        return newCount;
    }

    public int GetConnectionCount(string userId)
    {
        return _connections.TryGetValue(userId, out var count) ? count : 0;
    }
}
