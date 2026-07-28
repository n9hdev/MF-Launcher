using System.Security.Cryptography;
using System.Text;

namespace AntiCheat.Shared.Models;

public static class DetectionFingerprint
{
    public static string Generate(DetectionEventDto dto)
    {
        var type = (dto.Type ?? "").ToLowerInvariant().Trim();
        var evidencePath = (dto.EvidencePath ?? "").Trim();
        var processName = (dto.ProcessName ?? "").ToLowerInvariant().Trim();
        var description = (dto.Description ?? "").ToLowerInvariant().Trim();

        string raw;
        if (!string.IsNullOrEmpty(evidencePath))
            raw = $"{type}|{evidencePath.ToLowerInvariant()}";
        else if (!string.IsNullOrEmpty(processName) && !string.IsNullOrEmpty(description))
            raw = $"{type}|{processName}|{description}";
        else
            raw = type;

        return HexHash(raw);
    }

    public static string Generate(EvidenceFact fact)
    {
        var category = (fact.Category ?? "").ToLowerInvariant().Trim();
        var observation = (fact.Observation ?? "").ToLowerInvariant().Trim();
        var processName = (fact.ProcessName ?? "").ToLowerInvariant().Trim();

        var raw = !string.IsNullOrEmpty(processName)
            ? $"{category}:{observation}|{processName}"
            : $"{category}:{observation}";

        return HexHash(raw);
    }

    private static string HexHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
