using AntiCheat.Shared.Models;

namespace AntiCheat.Core.Interfaces;

public interface IRuleManagerService
{
    Task<List<DetectionRuleDto>> GetRulesAsync();
    Task<DetectionRuleDto?> GetRuleAsync(string id);
    Task<DetectionRuleDto> AddRuleAsync(DetectionRuleDto rule);
    Task<DetectionRuleDto?> UpdateRuleAsync(DetectionRuleDto rule);
    Task<bool> DeleteRuleAsync(string id);
    Task<bool> ToggleRuleAsync(string id, bool enabled);
    Task<List<DetectionRuleDto>> GetRulesForDetectorAsync(string detectorType);
}
