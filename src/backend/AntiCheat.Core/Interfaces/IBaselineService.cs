using AntiCheat.Core.Models;

namespace AntiCheat.Core.Interfaces;

public interface IBaselineService
{
    /// <summary>
    /// Captures a comprehensive baseline of the target process by:
    /// 1) Enumerating all modules via PEB + Toolhelp + PSAPI (triple-verified)
    /// 2) Walking all committed memory regions via VirtualQueryEx
    /// 3) Recording all threads with start addresses via NtQueryInformationThread
    /// 4) Hashing all executable MEM_IMAGE code sections for integrity
    /// </summary>
    Task<BaselineSnapshot> CaptureBaselineAsync(int processId, string processName, CancellationToken ct = default);

    /// <summary>
    /// Waits for MTA:SA modules to finish loading into the game process.
    /// Polls module count until it stabilizes (no change for pollInterval * stableCount consecutive polls).
    /// </summary>
    Task<bool> WaitForMtaInitializationAsync(int processId, TimeSpan pollInterval = default, int stableCount = 3, TimeSpan timeout = default, CancellationToken ct = default);
}
