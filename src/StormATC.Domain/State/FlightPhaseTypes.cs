namespace StormATC.Domain.State;

/// <summary>Autopilot control modes for the hybrid autopilot system.</summary>
public enum AutopilotMode
{
    HeadingHold,
    AltitudeHold,
    SpeedHold,
    VerticalSpeedHold,
    NavHold,
    ApproachHold,
    Disengaged,
}

/// <summary>Flight phase as tracked by the autopilot phase engine.</summary>
/// <remarks>
/// This is separate from StormATC's <see cref="Domain.FlightPhase"/> which tracks ATC
/// jurisdiction. The autopilot phase engine manages physical flight phases for control
/// targeting and terrain awareness.
/// </remarks>
public enum FlightPhase
{
    Unknown,
    Preflight,
    Startup,
    TaxiOut,
    HoldingShort,
    LineUp,
    Takeoff,
    InitialClimb,
    Climb,
    Cruise,
    Descent,
    Approach,
    FinalApproach,
    GoAround,
    Rollout,
    TaxiIn,
    Shutdown,
    Abnormal,
}

/// <summary>Context provided to the flight phase engine for evaluation.</summary>
public sealed record PhaseEvaluationContext
{
    public required FlightPhase CurrentPhase { get; init; }
    public required Domain.Aircraft.AircraftState AircraftState { get; init; }
    public required Domain.FlightPlanning.FlightPlan FlightPlan { get; init; }
    public required AirportData? DepartureAirport { get; init; }
    public required AirportData? DestinationAirport { get; init; }
    public required Domain.Atc.WeatherSnapshot? Weather { get; init; }
    public required TerrainProfile? TerrainProfile { get; init; }
    public required IReadOnlyList<TrafficSnapshot> Traffic { get; init; } = new List<TrafficSnapshot>();
    public required RealismOptions Realism { get; init; }
    public required DateTimeOffset CurrentTime { get; init; }
}

/// <summary>Result of a flight phase evaluation.</summary>
public sealed record PhaseDecision
{
    public bool Accepted { get; init; }
    public FlightPhase? NewPhase { get; init; }
    public ControlTargets? ControlTargets { get; init; }
    public string? LogMessage { get; init; }
    public IReadOnlyList<PhaseWarning> Warnings { get; init; } = new List<PhaseWarning>();

    public static PhaseDecision Silent() => new() { Accepted = true };
    public static PhaseDecision TransitionTo(FlightPhase phase) =>
        new() { Accepted = true, NewPhase = phase };
}

/// <summary>Control targets produced by the autopilot/guidance system.</summary>
public sealed record ControlTargets
{
    public double? TargetPitchDegrees { get; init; }
    public double? TargetBankDegrees { get; init; }
    public double? TargetHeadingDegrees { get; init; }
    public AltitudeFeet? TargetAltitudeFeet { get; init; }
    public SpeedKnots? TargetSpeedKnots { get; init; }
    public VerticalSpeedFpm? TargetVerticalSpeedFpm { get; init; }
    public bool UseNativeAutopilot { get; init; }
    public AutopilotMode Mode { get; init; } = AutopilotMode.HeadingHold;
    public bool GearDown { get; init; }
}

/// <summary>A warning generated during phase evaluation.</summary>
public sealed record PhaseWarning
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public double Severity { get; init; } = 1.0;
    public DateTimeOffset RaisedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>PID loop gains for autopilot control.</summary>
public sealed record PidGains
{
    public double Kp { get; init; }
    public double Ki { get; init; }
    public double Kd { get; init; }
    public double MinOutput { get; init; } = -1.0;
    public double MaxOutput { get; init; } = 1.0;
    public double Deadband { get; init; } = 0.004;
}

/// <summary>Aircraft control profile per flight phase.</summary>
public sealed record AircraftControlProfile
{
    public FlightPhase Phase { get; init; }
    public PidGains Pitch { get; init; } = new() { Kp = 0.1, Ki = 0.01, Kd = 0.05 };
    public PidGains Bank { get; init; } = new() { Kp = 0.1, Ki = 0.01, Kd = 0.05 };
    public PidGains Speed { get; init; } = new() { Kp = 0.05, Ki = 0.005, Kd = 0.02 };
    public PidGains Altitude { get; init; } = new() { Kp = 0.05, Ki = 0.005, Kd = 0.02 };
}

/// <summary>Realism options for ATC/phase engine.</summary>
public sealed record RealismOptions
{
    public FlightProfile Profile { get; init; } = FlightProfile.Standard;
    public bool RequireReadbackBeforeProgressing { get; init; } = true;
    public bool EnforceHoldShort { get; init; } = true;
    public bool RequireCorrectCallsign { get; init; } = true;
    public double MinimumIntentConfidence { get; init; } = 0.6;

    public static RealismOptions FromProfile(FlightProfile profile) => profile switch
    {
        FlightProfile.Relaxed => new()
        {
            RequireReadbackBeforeProgressing = false,
            EnforceHoldShort = false,
            RequireCorrectCallsign = false,
            MinimumIntentConfidence = 0.6,
        },
        FlightProfile.Standard => new(),
        FlightProfile.Strict => new()
        {
            MinimumIntentConfidence = 0.75,
            RequireReadbackBeforeProgressing = true,
            EnforceHoldShort = true,
            RequireCorrectCallsign = true,
        },
        _ => new(),
    };
}

/// <summary>Realism profile name for flight phases.</summary>
public enum FlightProfile
{
    Relaxed,
    Standard,
    Strict,
}

/// <summary>Terrain profile along a route.</summary>
public sealed record TerrainProfile
{
    public const double MountainousThresholdFt = 4500.0;
    public const double NormalMarginFt = 1000.0;
    public const double MountainMarginFt = 2000.0;
    public const double FallbackMarginFt = 3000.0;
    public const double SampleSpacingNm = 8.0;

    public IReadOnlyList<TerrainSegment> Segments { get; init; } = new List<TerrainSegment>();
    public bool Mountainous { get; init; }
    public bool IsFallback { get; init; }
    public double MaxRequiredAltitudeFt { get; init; }
}

/// <summary>A single terrain segment.</summary>
public sealed record TerrainSegment
{
    public double DistanceNm { get; init; }
    public double TerrainElevationFt { get; init; }
    public double RequiredAltitudeFt { get; init; }
    public bool IsMountainous { get; init; }
}

/// <summary>Terrain analysis segment result.</summary>
public sealed record TerrainAnalysisSegment
{
    public double DistanceNm { get; init; }
    public double TerrainElevationFt { get; init; }
    public double RequiredAltitudeFt { get; init; }
}

/// <summary>Traffic snapshot for conflict assessment.</summary>
public sealed record TrafficSnapshot
{
    public string Callsign { get; init; } = string.Empty;
    public string ModelTitle { get; init; } = string.Empty;
    public GeoPosition Position { get; init; } = GeoPosition.Create(0, 0);
    public AltitudeFeet AltitudeFeet { get; init; } = AltitudeFeet.Invalid;
    public SpeedKnots GroundSpeedKnots { get; init; } = SpeedKnots.Zero;
    public HeadingDegrees MagneticHeadingDegrees { get; init; } = HeadingDegrees.Zero;
    public VerticalSpeedFpm VerticalSpeedFpm { get; init; } = VerticalSpeedFpm.Zero;
    public bool IsOnGround { get; init; }
}

/// <summary>Traffic assessment result.</summary>
public sealed record TrafficAssessment
{
    public bool RunwayOccupied { get; init; }
    public bool OnFinal { get; init; }
    public bool AirborneConflict { get; init; }
    public IReadOnlyList<TrafficSnapshot> Conflicts { get; init; } = new List<TrafficSnapshot>();
}

/// <summary>Airport data for taxi routing and facility information.</summary>
public sealed record AirportData
{
    public string Icao { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public GeoPosition ReferencePosition { get; init; } = GeoPosition.Create(0, 0);
    public AltitudeFeet ReferenceAltitudeFeet { get; init; } = AltitudeFeet.Invalid;
    public double MagneticVariationDegrees { get; init; }
    public IReadOnlyList<RunwayData> Runways { get; init; } = new List<RunwayData>();
    public IReadOnlyList<TaxiwayNode> TaxiwayNodes { get; init; } = new List<TaxiwayNode>();
    public IReadOnlyList<TaxiwayEdge> TaxiwayEdges { get; init; } = new List<TaxiwayEdge>();
    public IReadOnlyList<ParkingSpot> ParkingSpots { get; init; } = new List<ParkingSpot>();
    public IReadOnlyList<FrequencyData> Frequencies { get; init; } = new List<FrequencyData>();
    public bool SupportsTaxiRouting => TaxiwayNodes.Count > 0 && TaxiwayEdges.Count > 0;
}

/// <summary>Runway data.</summary>
public sealed record RunwayData
{
    public uint Index { get; init; }
    public GeoPosition Position { get; init; } = GeoPosition.Create(0, 0);
    public double HeadingDegrees { get; init; }
    public double LengthMeters { get; init; }
    public double WidthMeters { get; init; }
    public int Surface { get; init; }
    public int PrimaryNumber { get; init; }
    public string PrimaryDesignator { get; init; } = string.Empty;
    public int SecondaryNumber { get; init; }
    public string SecondaryDesignator { get; init; } = string.Empty;
}

/// <summary>Taxiway node (point).</summary>
public sealed record TaxiwayNode
{
    public uint Index { get; init; }
    public int Type { get; init; }
    public int Orientation { get; init; }
    public double BiasXMeters { get; init; }
    public double BiasZMeters { get; init; }
    public GeoPosition Position { get; init; } = GeoPosition.Create(0, 0);
}

/// <summary>Taxiway edge (path between nodes).</summary>
public sealed record TaxiwayEdge
{
    public uint Index { get; init; }
    public int Type { get; init; }
    public double WidthMeters { get; init; }
    public uint WeightLimitPounds { get; init; }
    public uint RunwayNumber { get; init; }
    public int RunwayDesignator { get; init; }
    public uint StartIndex { get; init; }
    public uint EndIndex { get; init; }
    public uint NameIndex { get; init; }
    public string TaxiwayName { get; init; } = string.Empty;
}

/// <summary>Parking spot.</summary>
public sealed record ParkingSpot
{
    public uint Index { get; init; }
    public int Type { get; init; }
    public int TaxiPointType { get; init; }
    public int Name { get; init; }
    public int Suffix { get; init; }
    public uint Number { get; init; }
    public int Orientation { get; init; }
    public double HeadingDegrees { get; init; }
    public double RadiusMeters { get; init; }
    public double BiasXMeters { get; init; }
    public double BiasZMeters { get; init; }
}

/// <summary>Frequency data.</summary>
public sealed record FrequencyData
{
    public uint Index { get; init; }
    public int Type { get; init; }
    public uint FrequencyHertz { get; init; }
    public FrequencyMhz Mhz => new(FrequencyHertz / 1_000_000.0);
}

/// <summary>Taxi route result.</summary>
public sealed record TaxiRoute
{
    public bool Found { get; init; }
    public List<uint> Nodes { get; init; } = new();
    public List<string> TaxiwayNames { get; init; } = new();
    public double TotalLengthMeters { get; init; }
}

/// <summary>Interface for taxi routing over the ground graph.</summary>
public interface ITaxiRouter
{
    TaxiRoute? FindRouteToHoldingPoint(AirportData airport, GeoPosition position, string runway);
}

/// <summary>Interface for route building.</summary>
public interface IRouteBuilder
{
    Domain.FlightPlanning.FlightPlan BuildRoute(string departureIcao, string destinationIcao, string departureRunway);
}

/// <summary>Interface for performance calculations.</summary>
public interface IPerformanceCalculator
{
    double ClimbRateFpm { get; }
    double DescentRateFpm { get; }
    double CruiseSpeed { get; }
}

/// <summary>Interface for terrain analysis.</summary>
public interface ITerrainAnalyzer
{
    double RequiredAltitudeAheadFt(GeoPosition position, double aheadNm);
    bool IsPathBlocked(GeoPosition position, double currentAltitudeFt, double lookAheadNm);
}
