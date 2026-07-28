using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IHardwareIdProvider
{
    string GetHardwareId();
    HardwareFingerprint GetHardwareFingerprint();
}
