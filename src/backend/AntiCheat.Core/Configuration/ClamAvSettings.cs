namespace AntiCheat.Core.Configuration;

public class ClamAvSettings
{
    public const string SectionName = "ClamAv";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 30;
    public bool Enabled { get; set; } = false;
    public int MaxFileSizeMb { get; set; } = 50;
}
