namespace AntiCheat.Core.Interfaces;

public class MtasaSerialData
{
    public string Serial { get; set; } = string.Empty;
    public string CacheChecksum { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IMtasaSerialReader
{
    MtasaSerialData ReadSerial();
}
