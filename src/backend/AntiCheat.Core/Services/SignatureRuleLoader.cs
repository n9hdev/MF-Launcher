using System.Reflection;
using System.Text.Json;
using AntiCheat.Core.Models;
using Microsoft.Extensions.Logging;

namespace AntiCheat.Core.Services;

public static class SignatureRuleLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static List<SignatureRuleModel> LoadFromDirectory(string rulesDirectory, ILogger? logger = null)
    {
        var rules = new List<SignatureRuleModel>();
        var errors = 0;

        if (!Directory.Exists(rulesDirectory))
        {
            logger?.LogWarning("Rules directory not found: {Dir}", rulesDirectory);
            return rules;
        }

        foreach (var filePath in Directory.GetFiles(rulesDirectory, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var fileRules = JsonSerializer.Deserialize<List<SignatureRuleModel>>(content, JsonOptions);

                if (fileRules == null || fileRules.Count == 0)
                {
                    logger?.LogWarning("No rules found in file: {File}", Path.GetFileName(filePath));
                    continue;
                }

                foreach (var rule in fileRules)
                {
                    if (string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(rule.MatchType))
                    {
                        logger?.LogWarning("Skipping invalid rule in {File}: missing name or matchType", Path.GetFileName(filePath));
                        errors++;
                        continue;
                    }

                    rule.Tags.Insert(0, $"file:{Path.GetFileNameWithoutExtension(filePath)}");
                    rules.Add(rule);
                }

                logger?.LogDebug("Loaded {Count} rules from {File}", fileRules.Count, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load rule file: {File}", filePath);
                errors++;
            }
        }

        logger?.LogInformation("SignatureRuleLoader: loaded {Total} rules from {Files} files ({Errors} errors)",
            rules.Count, Directory.GetFiles(rulesDirectory, "*.json", SearchOption.AllDirectories).Length, errors);

        return rules;
    }

    public static List<SignatureRuleModel> LoadFromAssembly(ILogger? logger = null)
    {
        var rules = new List<SignatureRuleModel>();
        var errors = 0;
        var assembly = typeof(SignatureRuleLoader).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n)
            .ToList();

        if (resourceNames.Count == 0)
        {
            logger?.LogWarning("No embedded rule resources found in assembly {Assembly}", assembly.FullName);
            return rules;
        }

        foreach (var resourceName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    logger?.LogWarning("Embedded resource stream is null: {Resource}", resourceName);
                    errors++;
                    continue;
                }

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                var fileRules = JsonSerializer.Deserialize<List<SignatureRuleModel>>(content, JsonOptions);

                if (fileRules == null || fileRules.Count == 0)
                {
                    logger?.LogWarning("No rules found in embedded resource: {Resource}", resourceName);
                    continue;
                }

                var fileName = resourceName.Split('.').Reverse().Skip(1).First() ?? resourceName;

                foreach (var rule in fileRules)
                {
                    if (string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(rule.MatchType))
                    {
                        logger?.LogWarning("Skipping invalid rule in {Resource}: missing name or matchType", resourceName);
                        errors++;
                        continue;
                    }

                    rule.Tags.Insert(0, $"file:{fileName}");
                    rules.Add(rule);
                }

                logger?.LogDebug("Loaded {Count} rules from embedded resource {Resource}", fileRules.Count, resourceName);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load embedded rule resource: {Resource}", resourceName);
                errors++;
            }
        }

        logger?.LogInformation("SignatureRuleLoader: loaded {Total} rules from {Files} embedded resources ({Errors} errors)",
            rules.Count, resourceNames.Count, errors);

        return rules;
    }
}
