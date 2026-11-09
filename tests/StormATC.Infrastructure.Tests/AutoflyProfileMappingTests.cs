using System.Text.Json;
using StormATC.Domain.Autofly;
using Xunit;

namespace StormATC.Infrastructure.Tests;

/// <summary>
/// Verifies the bundled autofly profiles bind official MSFS 2024 SimConnect event names
/// (docs.flightsimulator.com/msfs2024 key-events reference) rather than guessed identifiers.
/// </summary>
public sealed class AutoflyProfileMappingTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static TheoryData<string> ProfileFiles => new()
    {
        "cessna-172.json",
        "a320.json",
    };

    [Theory]
    [MemberData(nameof(ProfileFiles))]
    public void BundledProfileUsesOfficialEventNames(string fileName)
    {
        string path = Path.Combine(FindRepositoryRoot(), "data", "autofly-profiles", fileName);
        AircraftAutoflyProfile profile = JsonSerializer.Deserialize<AircraftAutoflyProfile>(
            File.ReadAllText(path),
            SerializerOptions)!;

        Assert.NotNull(profile);
        Assert.True(profile.CanAutofly);

        // No documentation-only prefixes: SimConnect wants the bare event identifier.
        foreach (string eventName in profile.Events.Values
                     .Concat(profile.FlapEvents.Values)
                     .Concat(profile.EventSequences.Values.SelectMany(v => v)))
        {
            Assert.False(eventName.StartsWith("K:", StringComparison.OrdinalIgnoreCase), eventName);
            Assert.False(eventName.StartsWith("KEY_", StringComparison.OrdinalIgnoreCase), eventName);
        }

        Assert.Equal("AUTOPILOT_ON", profile.EventFor("engage"));
        Assert.Equal("AUTOPILOT_OFF", profile.EventFor("disengage"));
        Assert.Equal("HEADING_BUG_SET", profile.EventFor("heading"));
        Assert.Equal("AP_ALT_VAR_SET_ENGLISH", profile.EventFor("altitude"));
        Assert.Equal("AP_VS_VAR_SET_ENGLISH", profile.EventFor("verticalSpeed"));
        Assert.Equal("AP_SPD_VAR_SET", profile.EventFor("speed"));
        Assert.Equal("GEAR_UP", profile.EventFor("gearUp"));
        Assert.Equal("GEAR_DOWN", profile.EventFor("gearDown"));
        Assert.Equal("AP_APR_HOLD_ON", profile.EventFor("approach"));

        // Official *_ON/*_OFF variants are preferred over toggles for known states.
        Assert.Equal("PARKING_BRAKES_ON", profile.EventFor("parkingBrakeOn"));
        Assert.Equal("PARKING_BRAKES_OFF", profile.EventFor("parkingBrakeOff"));
        Assert.Equal("LANDING_LIGHTS_ON", profile.EventFor("landingLightsOn"));
        Assert.Equal("LANDING_LIGHTS_OFF", profile.EventFor("landingLightsOff"));

        // MSFS throttle events take 0-16383, not percent.
        Assert.True(profile.ThrottleScale > 100);

        // Flaps are covered either by detent events or a scaled continuous event.
        bool flapCoverage = profile.FlapEvents.Count > 0
            || (profile.FlapValueScale is not null && profile.EventFor("flaps") is not null);
        Assert.True(flapCoverage, "Profile must bind flap control.");
    }

    [Fact]
    public void CessnaProfileStartsPistonEngineWithMagsThenStarter()
    {
        string path = Path.Combine(
            FindRepositoryRoot(), "data", "autofly-profiles", "cessna-172.json");
        AircraftAutoflyProfile profile = JsonSerializer.Deserialize<AircraftAutoflyProfile>(
            File.ReadAllText(path),
            SerializerOptions)!;

        Assert.Equal(["MAGNETO_BOTH", "STARTER1_SET"], profile.SequenceFor("enginesStart"));
        Assert.Equal(["MAGNETO_OFF"], profile.SequenceFor("enginesStop"));
        Assert.Equal("FLAPS_UP", profile.FlapEvents[0]);
        Assert.Equal("FLAPS_DOWN", profile.FlapEvents[4]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StormATC.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
