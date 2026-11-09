namespace StormATC.Domain.Autofly;

/// <summary>
/// Aircraft-specific data StormATC needs to fly the user's aircraft: performance numbers,
/// control-event bindings, and capability flags.
/// </summary>
/// <remarks>
/// Profiles are deliberately data, not code. A profile borrowed from a different airframe is
/// marked <see cref="BorrowedProfile"/> so the UI can warn that the numbers are unverified for
/// this aircraft — the same honesty rule the rest of StormATC applies to ATC facts.
/// </remarks>
public sealed record AircraftAutoflyProfile
{
    /// <summary>Human-readable aircraft name.</summary>
    public required string Name { get; init; }

    /// <summary>Simulator title fragments this profile matches, case-insensitive.</summary>
    public IReadOnlyList<string> TitleMatches { get; init; } = [];

    /// <summary>True when the profile was written for this exact aircraft.</summary>
    public bool FlightTested { get; init; }

    /// <summary>True when the profile was borrowed from a different airframe.</summary>
    public bool BorrowedProfile { get; init; }

    /// <summary>True when StormATC has enough data to attempt autofly for this aircraft.</summary>
    public bool CanAutofly { get; init; } = true;

    /// <summary>Performance numbers used for speeds, rotation, and configuration.</summary>
    public AutoflyPerformance Performance { get; init; } = new();

    /// <summary>Named simulator events StormATC may fire, keyed by binding name.</summary>
    public IReadOnlyDictionary<string, string> Events { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Flap-detent event names keyed by detent index.</summary>
    public IReadOnlyDictionary<int, string> FlapEvents { get; init; } =
        new Dictionary<int, string>();

    /// <summary>Looks up an event binding by name.</summary>
    public string? EventFor(string binding) =>
        Events.TryGetValue(binding, out string? value) && value.Length > 0 ? value : null;
}

/// <summary>Performance figures for one airframe.</summary>
public sealed record AutoflyPerformance
{
    /// <summary>Rotation indicated airspeed in knots.</summary>
    public double RotationSpeedKnots { get; init; } = 70;

    /// <summary>Climb indicated airspeed in knots.</summary>
    public double ClimbSpeedKnots { get; init; } = 90;

    /// <summary>Cruise indicated airspeed in knots.</summary>
    public double CruiseSpeedKnots { get; init; } = 120;

    /// <summary>Downwind indicated airspeed in knots.</summary>
    public double DownwindSpeedKnots { get; init; } = 90;

    /// <summary>Base-leg indicated airspeed in knots.</summary>
    public double BaseSpeedKnots { get; init; } = 80;

    /// <summary>Final-approach indicated airspeed in knots.</summary>
    public double ApproachSpeedKnots { get; init; } = 70;

    /// <summary>Touchdown target indicated airspeed in knots.</summary>
    public double TouchdownSpeedKnots { get; init; } = 65;

    /// <summary>Go-around indicated airspeed in knots.</summary>
    public double GoAroundSpeedKnots { get; init; } = 80;

    /// <summary>Takeoff flap detent.</summary>
    public int TakeoffFlaps { get; init; }

    /// <summary>Downwind flap detent.</summary>
    public int DownwindFlaps { get; init; }

    /// <summary>Base-leg flap detent.</summary>
    public int BaseFlaps { get; init; }

    /// <summary>Landing flap detent.</summary>
    public int LandingFlaps { get; init; }

    /// <summary>Maximum commanded pitch-up in degrees.</summary>
    public double MaxPitchUpDegrees { get; init; } = 12;

    /// <summary>Maximum commanded bank in degrees.</summary>
    public double MaxBankDegrees { get; init; } = 30;

    /// <summary>Climb vertical-speed target in feet per minute.</summary>
    public double ClimbRateFpm { get; init; } = 700;

    /// <summary>Descent vertical-speed target in feet per minute.</summary>
    public double DescentRateFpm { get; init; } = 500;
}
