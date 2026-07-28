namespace AntiCheat.Core.Configuration;

public class RemoteApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int HeartbeatIntervalSeconds { get; set; } = 5;
    public int RequestTimeoutSeconds { get; set; } = 10;
    public bool Enabled { get; set; } = false;
}
