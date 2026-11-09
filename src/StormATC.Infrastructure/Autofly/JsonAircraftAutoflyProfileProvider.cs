using System.Text.Json;
using Microsoft.Extensions.Logging;
using StormATC.Application.Abstractions;
using StormATC.Domain.Autofly;

namespace StormATC.Infrastructure.Autofly;

/// <summary>
/// Loads aircraft autofly profiles from <c>data/autofly-profiles/*.json</c>.
/// </summary>
/// <remarks>
/// Profiles are matched against the simulator-reported aircraft title. When nothing matches,
/// a conservative generic profile is returned: autofly may still drive the mock connector, but
/// on a real simulator every command without an event binding reports
/// <see cref="AutopilotCommandStatus.Unsupported"/> rather than guessing at controls.
/// </remarks>
public sealed class JsonAircraftAutoflyProfileProvider : IAircraftAutoflyProfileProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IStoragePaths _storagePaths;
    private readonly ILogger<JsonAircraftAutoflyProfileProvider> _logger;
    private readonly Lock _sync = new();
    private IReadOnlyList<AircraftAutoflyProfile>? _profiles;

    /// <summary>Creates the provider.</summary>
    public JsonAircraftAutoflyProfileProvider(
        IStoragePaths storagePaths,
        ILogger<JsonAircraftAutoflyProfileProvider> logger)
    {
        _storagePaths = storagePaths ?? throw new ArgumentNullException(nameof(storagePaths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<AircraftAutoflyProfile> GetProfileAsync(
        Domain.Aircraft.AircraftState aircraftState,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(aircraftState);
        IReadOnlyList<AircraftAutoflyProfile> profiles = EnsureLoaded();
        string title = aircraftState.AircraftTitle ?? string.Empty;

        AircraftAutoflyProfile? match = profiles.FirstOrDefault(profile =>
            profile.TitleMatches.Count > 0
            && profile.TitleMatches.Any(fragment =>
                title.Contains(fragment, StringComparison.OrdinalIgnoreCase)));

        return Task.FromResult(match ?? GenericProfile);
    }

    private IReadOnlyList<AircraftAutoflyProfile> EnsureLoaded()
    {
        lock (_sync)
        {
            if (_profiles is not null)
            {
                return _profiles;
            }
        }

        var profiles = new List<AircraftAutoflyProfile>();
        string directory = Path.Combine(_storagePaths.DataDirectory, "autofly-profiles");

        if (Directory.Exists(directory))
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
            {
                try
                {
                    AircraftAutoflyProfile? profile =
                        JsonSerializer.Deserialize<AircraftAutoflyProfile>(
                            File.ReadAllText(file),
                            SerializerOptions);

                    if (profile is not null)
                    {
                        profiles.Add(profile);
                    }
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    _logger.LogWarning(ex, "Autofly profile {File} is invalid and was skipped.", file);
                }
            }
        }
        else
        {
            _logger.LogInformation(
                "No autofly profile directory at {Directory}; using the generic profile.",
                directory);
        }

        lock (_sync)
        {
            _profiles ??= profiles;
            return _profiles;
        }
    }

    /// <summary>The fallback profile used when no aircraft-specific file matches.</summary>
    public static AircraftAutoflyProfile GenericProfile { get; } = new()
    {
        Name = "Generic aircraft",
        TitleMatches = [],
        FlightTested = false,
        BorrowedProfile = false,
        CanAutofly = true,
        Performance = new AutoflyPerformance(),
        Events = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        FlapEvents = new Dictionary<int, string>(),
    };
}
