namespace AntiCheat.Core.Interfaces;

public interface IUserConnectionTracker
{
    int AddConnection(string userId);
    int RemoveConnection(string userId);
    int GetConnectionCount(string userId);
}
