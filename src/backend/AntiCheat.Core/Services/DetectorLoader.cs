using System.Reflection;
using AntiCheat.Core.Interfaces;
using AntiCheat.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public class DetectorLoader
{
    private readonly ILogger<DetectorLoader> _logger;

    public DetectorLoader(ILogger<DetectorLoader> logger)
    {
        _logger = logger;
    }

    public List<IDetector> LoadFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        var detectors = new List<IDetector>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IDetector).IsAssignableFrom(t));

            foreach (var type in types)
            {
                try
                {
                    var attr = type.GetCustomAttribute<DetectionPluginAttribute>();
                    var detector = (IDetector)Activator.CreateInstance(type)!;
                    detectors.Add(detector);
                    var name = attr?.Name ?? type.Name;
                    _logger.LogInformation("Loaded detector: {Name} (v{Version})", name, attr?.Version ?? "1.0.0");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load detector type: {Type}", type.FullName);
                }
            }
        }

        return detectors;
    }

    public List<(string Name, string Version, string Description)> GetAvailablePlugins(IEnumerable<Assembly> assemblies)
    {
        var plugins = new List<(string, string, string)>();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<DetectionPluginAttribute>();
                if (attr != null)
                {
                    plugins.Add((attr.Name, attr.Version, attr.Description));
                }
            }
        }

        return plugins;
    }
}
