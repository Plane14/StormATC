using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;
using StormATC.Domain.Checklists;

namespace StormATC.Infrastructure.Checklists;

/// <summary>JSON file-backed checklist repository with <see cref="ChecklistOptions.ChecklistDirectory"/> override.</summary>
/// <remarks>
/// Definitions are read from the app-data checklist directory when configured, otherwise from the
/// bundled <c>flightchecklists</c> folder shipped next to the executable (copied to output and to
/// the installer payload). An explicit directory that does not exist is treated as empty rather
/// than falling back, so a typo cannot silently load the wrong set.
/// </remarks>
public sealed class JsonChecklistRepository : IChecklistRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;
    private readonly bool _enabled;
    private readonly ILogger<JsonChecklistRepository> _logger;

    /// <summary>Creates a JSON checklist repository.</summary>
    public JsonChecklistRepository(
        IOptions<ChecklistOptions> options,
        ILogger<JsonChecklistRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ChecklistOptions checklistOptions = options.Value;
        _enabled = checklistOptions.Enabled;

        string? configured = checklistOptions.ChecklistDirectory;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _directory = Path.GetFullPath(configured);
        }
        else
        {
            // The csproj links data/** under a "data" folder, so prefer the packaged location
            // and fall back to a flat folder for development layouts.
            string packaged = Path.Combine(AppContext.BaseDirectory, "data", "flightchecklists");
            string flat = Path.Combine(AppContext.BaseDirectory, "flightchecklists");
            _directory = Directory.Exists(packaged) ? packaged : flat;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ChecklistDefinition>> LoadAllAsync(CancellationToken ct)
    {
        if (!_enabled)
        {
            return Task.FromResult<IReadOnlyList<ChecklistDefinition>>(Array.Empty<ChecklistDefinition>());
        }

        var checklists = new List<ChecklistDefinition>();
        if (!Directory.Exists(_directory))
        {
            return Task.FromResult<IReadOnlyList<ChecklistDefinition>>(checklists);
        }

        foreach (string file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            ct.ThrowIfCancellationRequested();

            string fileName = Path.GetFileName(file);
            if (string.Equals(fileName, "index.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                ChecklistDefinition? checklist = LoadFile(file);
                if (checklist is not null)
                {
                    checklists.Add(checklist);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load checklist file '{File}'.", fileName);
            }
        }

        return Task.FromResult<IReadOnlyList<ChecklistDefinition>>(checklists);
    }

    /// <inheritdoc />
    public Task<ChecklistDefinition?> LoadByIdAsync(string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_enabled || string.IsNullOrWhiteSpace(id) || !IsSafeId(id))
        {
            return Task.FromResult<ChecklistDefinition?>(null);
        }

        string file = Path.Combine(_directory, id + ".json");
        if (!File.Exists(file))
        {
            return Task.FromResult<ChecklistDefinition?>(null);
        }

        try
        {
            return Task.FromResult(LoadFile(file));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load checklist '{Id}'.", id);
            return Task.FromResult<ChecklistDefinition?>(null);
        }
    }

    private static bool IsSafeId(string id)
    {
        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        return !id.Contains("..", StringComparison.Ordinal) && !Path.IsPathRooted(id);
    }

    private static ChecklistDefinition? LoadFile(string file)
    {
        using FileStream stream = File.OpenRead(file);
        ChecklistDefinition? checklist = JsonSerializer.Deserialize<ChecklistDefinition>(stream, JsonOptions);
        if (checklist is null || checklist.Steps.Count == 0)
        {
            return null;
        }

        return checklist;
    }
}
