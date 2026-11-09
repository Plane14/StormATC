using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;
using StormATC.Domain.Checklists;

namespace StormATC.Infrastructure.Checklists;

/// <summary>Loads and saves checklist definitions from JSON files in the checklist directory.</summary>
public sealed class JsonChecklistRepository : IChecklistRepository
{
    private readonly IStoragePaths _storagePaths;
    private readonly ChecklistOptions _options;
    private readonly ILogger<JsonChecklistRepository> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>Creates a JSON checklist repository.</summary>
    public JsonChecklistRepository(
        IStoragePaths storagePaths,
        IOptions<ChecklistOptions> options,
        ILogger<JsonChecklistRepository> logger)
    {
        _storagePaths = storagePaths ?? throw new ArgumentNullException(nameof(storagePaths));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Loads all checklist definitions from the configured directory.</summary>
    public async Task<IReadOnlyList<ChecklistDefinition>> LoadAllAsync(CancellationToken ct = default)
    {
        var directory = GetChecklistDirectory();
        if (!Directory.Exists(directory))
        {
            _logger.LogDebug("Checklist directory does not exist: {Directory}", directory);
            return [];
        }

        var definitions = new List<ChecklistDefinition>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var definition = JsonSerializer.Deserialize<ChecklistDefinition>(json, _jsonOptions);
                if (definition is not null)
                {
                    definitions.Add(definition);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load checklist definition from {File}", file);
            }
        }

        return definitions;
    }

    /// <summary>Loads a checklist definition by ID.</summary>
    public async Task<ChecklistDefinition?> LoadByIdAsync(string id, CancellationToken ct = default)
    {
        var directory = GetChecklistDirectory();
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var file = Path.Combine(directory, $"{id}.json");
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(file, ct);
            return JsonSerializer.Deserialize<ChecklistDefinition>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load checklist definition {Id}", id);
            return null;
        }
    }

    private string GetChecklistDirectory() =>
        Path.Combine(_storagePaths.ApplicationDataDirectory, _options.ChecklistDirectory);
}
