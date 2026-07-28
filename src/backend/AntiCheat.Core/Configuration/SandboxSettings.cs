namespace AntiCheat.Core.Configuration;

public class SandboxSettings
{
    public const string SectionName = "Sandbox";
    public bool Enabled { get; set; } = false;
    public int AnalysisTimeoutSeconds { get; set; } = 30;
    public string TempDirectory { get; set; } = "sandbox_temp";
    public bool CleanupOnExit { get; set; } = true;
    public int MaxFileSizeMb { get; set; } = 20;
}
