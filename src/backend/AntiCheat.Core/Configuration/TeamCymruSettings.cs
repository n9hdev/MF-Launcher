namespace AntiCheat.Core.Configuration;

public class TeamCymruSettings
{
    public const string SectionName = "TeamCymru";
    public string Host { get; set; } = "hash.cymru.com";
    public int Port { get; set; } = 43;
    public int TimeoutSeconds { get; set; } = 15;
    public bool Enabled { get; set; } = false;
}
