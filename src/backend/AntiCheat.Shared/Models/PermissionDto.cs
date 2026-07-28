namespace AntiCheat.Shared.Models;

public class PermissionDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class FeatureFlagDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class UserPermissionsResponse
{
    public List<string> Permissions { get; set; } = new();
    public string Role { get; set; } = string.Empty;
}

public class AllPermissionsResponse
{
    public List<PermissionDto> AllPermissions { get; set; } = new();
    public UserPermissionsResponse User { get; set; } = null!;
}

public class PermissionCheckRequest
{
    public string Permission { get; set; } = string.Empty;
}

public class PermissionCheckResponse
{
    public string Permission { get; set; } = string.Empty;
    public bool Granted { get; set; }
}
