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
    /// <summary>Current autopilot flight phase.</summary>
    public required FlightPhase CurrentPhase { get; init; }

    /// <summary>Current aircraft state.</summary>
    public required Domain.Aircraft.AircraftState AircraftState { get; init; }

    /// <summary>Active flight plan.</summary>
    public required Domain.FlightPlanning.FlightPlan FlightPlan { get; init; }

    /// <summary>Departure aerodrome data.</summary>
    public required Domain.Airports.AirportData? DepartureAirport { get; init; }

    /// <summary>Destination aerodrome data.</summary>
    public required Domain.Airports.AirportData? DestinationAirport { get; init; }

    /// <summary>Current weather.</summary>
    public required Domain.Atc.WeatherSnapshot? Weather { get; init; }

    /// <summary>Terrain profile ahead.</summary>
    public required TerrainProfile? TerrainProfile { get; init; }

    /// <summary>Nearby traffic.</summary>
    public required IReadOnlyList<TrafficSnapshot> Traffic { get; init; } = new List<TrafficSnapshot>();

    /// <summary>Realism options.</summary>
    public required RealismOptions Realism { get; init; }

    /// <summary>Current time.</summary>
    public required DateTimeOffset CurrentTime { get; init; }
}

/// <summary>Result of a flight phase evaluation.</summary>
public sealed record PhaseDecision
{
    /// <summary>Whether the transition/decision was accepted.</summary>
    public bool Accepted { get; init; }

    /// <summary>New phase to transition to, when accepted.</summary>
    public FlightPhase? NewPhase { get; init; }

    /// <summary>Control targets for the autopilot.</summary>
    public ControlTargets? ControlTargets { get; init; }

    /// <summary>Log message for diagnostics.</summary>
    public string? LogMessage { get; init; }

    /// <summary>Warnings raised during evaluation.</summary>
    public IReadOnlyList<PhaseWarning> Warnings { get; init; } = new List<PhaseWarning>();

    /// <summary>Creates a silent (no-change) decision.</summary>
    public static PhaseDecision Silent() => new() { Accepted = true };

    /// <summary>Creates a phase transition decision.</summary>
    public static PhaseDecision TransitionTo(FlightPhase phase) =>
        new() { Accepted = true, NewPhase = phase };
}

/// <summary>Control targets produced by the autopilot/guidance system.</summary>
public sealed record ControlTargets
{
    /// <summary>Target pitch in degrees.</summary>
    public double? TargetPitchDegrees { get; init; }

    /// <summary>Target bank in degrees.</summary>
    public double? TargetBankDegrees { get; init; }

    /// <summary>Target heading in degrees.</summary>
    public double? TargetHeadingDegrees { get; init; }

    /// <summary>Target altitude in feet.</summary>
    public AltitudeFeet? TargetAltitudeFeet { get; init; }

    /// <summary>Target speed in knots.</summary>
    public SpeedKnots? TargetSpeedKnots { get; init; }

    /// <summary>Target vertical speed in feet per minute.</summary>
    public VerticalSpeedFpm? TargetVerticalSpeedFpm { get; init; }

    /// <summary>When true, use the native simulator autopilot instead of PID.</summary>
    public bool UseNativeAutopilot { get; init; }

    /// <summary>Autopilot mode.</summary>
    public AutopilotMode Mode { get; init; } = AutopilotMode.HeadingHold;

    /// <summary>When true, gear should be down.</summary>
    public bool GearDown { get; init; }
}

/// <summary>A warning generated during phase evaluation.</summary>
public sealed record PhaseWarning
{
    /// <summary>Warning code.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Warning message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Severity (0-1).</summary>
    public double Severity { get; init; } = 1.0;

    /// <summary>When the warning was raised.</summary>
    public DateTimeOffset RaisedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>PID loop gains for autopilot control.</summary>
public sealed record PidGains
{
    /// <summary>Proportional gain.</summary>
    public double Kp { get; init; }

    /// <summary>Integral gain.</summary>
    public double Ki { get; init; }

    /// <summary>Derivative gain.</summary>
    public double Kd { get; init; }

    /// <summary>Minimum output.</summary>
    public double MinOutput { get; init; } = -1.0;

    /// <summary>Maximum output.</summary>
    public double MaxOutput { get; init; } = 1.0;

    /// <summary>Deadband for error.</summary>
    public double Deadband { get; init; } = 0.004;
}

/// <summary>Aircraft control profile per flight phase.</summary>
public sealed record AircraftControlProfile
{
    /// <summary>Flight phase this profile applies to.</summary>
    public FlightPhase Phase { get; init; }

    /// <summary>Pitch PID gains.</summary>
    public PidGains Pitch { get; init; } = new() { Kp = 0.1, Ki = 0.01, Kd = 0.05 };

    /// <summary>Bank PID gains.</summary>
    public PidGains Bank { get; init; } = new() { Kp = 0.1, Ki = 0.01, Kd = 0.05 };

    /// <summary>Speed PID gains.</summary>
    public PidGains Speed { get; init; } = new() { Kp = 0.05, Ki = 0.005, Kd = 0.02 };

    /// <summary>Altitude PID gains.</summary>
    public PidGains Altitude { get; init; } = new() { Kp = 0.05, Ki = 0.005, Kd = 0.02 };
}

/// <summary>Realism options for ATC/phase engine.</summary>
public sealed record RealismOptions
{
    /// <summary>Realism profile.</summary>
    public FlightProfile Profile { get; init; } = FlightProfile.Standard;

    /// <summary>Require readback before progressing.</summary>
    public bool RequireReadbackBeforeProgressing { get; init; } = true;

    /// <summary>Enforce hold short.</summary>
    public bool EnforceHoldShort { get; init; } = true;

    /// <summary>Require correct callsign.</summary>
    public bool RequireCorrectCallsign { get; init; } = true;

    /// <summary>Minimum intent confidence threshold.</summary>
    public double MinimumIntentConfidence { get; init; } = 0.6;

    /// <summary>Creates options from a profile.</summary>
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
    /// <summary>Elevation above which terrain is considered mountainous.</summary>
    public const double MountainousThresholdFt = 4500.0;

    /// <summary>Normal terrain margin in feet.</summary>
    public const double NormalMarginFt = 1000.0;

    /// <summary>Mountainous terrain margin in feet.</summary>
    public const double MountainMarginFt = 2000.0;

    /// <summary>Fallback margin in feet.</summary>
    public const double FallbackMarginFt = 3000.0;

    /// <summary>Default sample spacing in nautical miles.</summary>
    public const double SampleSpacingNm = 8.0;

    /// <summary>Terrain segments.</summary>
    public IReadOnlyList<TerrainSegment> Segments { get; init; } = new List<TerrainSegment>();

    /// <summary>True when terrain is mountainous.</summary>
    public bool Mountainous { get; init; }

    /// <summary>True when using fallback terrain data.</summary>
    public bool IsFallback { get; init; }

    /// <summary>Maximum required altitude in feet.</summary>
    public double MaxRequiredAltitudeFt { get; init; }
}

/// <summary>A single terrain segment.</summary>
public sealed record TerrainSegment
{
    /// <summary>Distance from start in nautical miles.</summary>
    public double DistanceNm { get; init; }

    /// <summary>Terrain elevation in feet.</summary>
    public double TerrainElevationFt { get; init; }

    /// <summary>Required altitude in feet.</summary>
    public double RequiredAltitudeFt { get; init; }

    /// <summary>True when this segment is mountainous.</summary>
    public bool IsMountainous { get; init; }
}

/// <summary>Terrain analysis segment result.</summary>
public sealed record TerrainAnalysisSegment
{
    /// <summary>Distance from start in nautical miles.</summary>
    public double DistanceNm { get; init; }

    /// <summary>Terrain elevation in feet.</summary>
    public double TerrainElevationFt { get; init; }

    /// <summary>Required altitude in feet.</summary>
    public double RequiredAltitudeFt { get; init; }
}

/// <summary>Traffic snapshot for conflict assessment.</summary>
public sealed record TrafficSnapshot
{
    /// <summary>Traffic callsign.</summary>
    public string Callsign { get; init; } = string.Empty;

    /// <summary>Aircraft model title.</summary>
    public string ModelTitle { get; init; } = string.Empty;

    /// <summary>Position.</summary>
    public Domain.ValueObjects.GeoPosition Position { get; init; } = Domain.ValueObjects.GeoPosition.Create(0, 0);

    /// <summary>Altitude in feet.</summary>
    public AltitudeFeet AltitudeFeet { get; init; } = AltitudeFeet.Invalid;

    /// <summary>Ground speed in knots.</summary>
    public SpeedKnots GroundSpeedKnots { get; init; } = SpeedKnots.Zero;

    /// <summary>Magnetic heading in degrees.</summary>
    public HeadingDegrees MagneticHeadingDegrees { get; init; } = HeadingDegrees.Zero;

    /// <summary>Vertical speed in feet per minute.</summary>
    public VerticalSpeedFpm VerticalSpeedFpm { get; init; } = VerticalSpeedFpm.Zero;

    /// <summary>True when on the ground.</summary>
    public bool IsOnGround { get; init; }
}

/// <summary>Traffic assessment result.</summary>
public sealed record TrafficAssessment
{
    /// <summary>True when the runway is occupied.</summary>
    public bool RunwayOccupied { get; init; }

    /// <summary>True when traffic is on final approach.</summary>
    public bool OnFinal { get; init; }

    /// <summary>True when there is an airborne conflict.</summary>
    public bool AirborneConflict { get; init; }

    /// <summary>Conflicting traffic.</summary>
    public IReadOnlyList<TrafficSnapshot> Conflicts { get; init; } = new List<TrafficSnapshot>();
}

/// <summary>Taxi route result.</summary>
public sealed record TaxiRoute
{
    /// <summary>Whether a route was found.</summary>
    public bool Found { get; init; }

    /// <summary>Node indices in the route.</summary>
    public List<uint> Nodes { get; init; } = new();

    /// <summary>Taxiway names along the route.</summary>
    public List<string> TaxiwayNames { get; init; } = new();

    /// <summary>Total length in meters.</summary>
    public double TotalLengthMeters { get; init; }
}

/// <summary>Interface for taxi routing over the ground graph.</summary>
public interface ITaxiRouter
{
    /// <summary>Finds a route to the holding point for a runway.</summary>
    TaxiRoute? FindRouteToHoldingPoint(AirportData airport, Domain.ValueObjects.GeoPosition position, string runway);
}

/// <summary>Interface for route building.</summary>
public interface IRouteBuilder
{
    /// <summary>Builds a route between two airports.</summary>
    Domain.FlightPlanning.FlightPlan BuildRoute(string departureIcao, string destinationIcao, string departureRunway);
}

/// <summary>Interface for performance calculations.</summary>
public interface IPerformanceCalculator
{
    /// <summary>Climb rate in feet per minute.</summary>
    double ClimbRateFpm { get; }

    /// <summary>Descent rate in feet per minute.</summary>
    double DescentRateFpm { get; }

    /// <summary>Cruise speed in knots.</summary>
    double CruiseSpeed { get; }
}

/// <summary>Interface for terrain analysis.</summary>
public interface ITerrainAnalyzer
{
    /// <summary>Returns required altitude ahead in feet.</summary>
    double RequiredAltitudeAheadFt(Domain.ValueObjects.GeoPosition position, double aheadNm);

    /// <summary>True when a path is blocked by terrain at current altitude.</summary>
    bool IsPathBlocked(Domain.ValueObjects.GeoPosition position, double currentAltitudeFt, double lookAheadNm);
}

/// <summary>Airport data extended with taxi routing information.</summary>
public sealed record AirportData
{
    /// <summary>ICAO identifier.</summary>
    public string Icao { get; init; } = string.Empty;

    /// <summary>Aerodrome name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Reference position.</summary>
    public Domain.ValueObjects.GeoPosition ReferencePosition { get; init; } = Domain.ValueObjects.GeoPosition.Create(0, 0);

    /// <summary>Reference elevation in feet.</summary>
    public AltitudeFeet ReferenceAltitudeFeet { get; init; } = AltitudeFeet.Invalid;

    /// <summary>Magnetic variation in degrees.</summary>
    public double MagneticVariationDegrees { get; init; }

    /// <summary>Runways.</summary>
    public IReadOnlyList<RunwayData> Runways { get; init; } = new List<RunwayData>();

    /// <summary>Taxiway nodes.</summary>
    public IReadOnlyList<TaxiwayNode> TaxiwayNodes { get; init; } = new List<TaxiwayNode>();

    /// <summary>Taxiway edges.</summary>
    public IReadOnlyList<TaxiwayEdge> TaxiwayEdges { get; init; } = new List<TaxiwayEdge>();

    /// <summary>Parking spots.</summary>
    public IReadOnlyList<ParkingSpot> ParkingSpots { get; init; } = new List<ParkingSpot>();

    /// <summary>Frequencies.</summary>
    public IReadOnlyList<FrequencyData> Frequencies { get; init; } = new List<FrequencyData>();

    /// <summary>True when taxi routing data is available.</summary>
    public bool SupportsTaxiRouting => TaxiwayNodes.Count > 0 && TaxiwayEdges.Count > 0;
}

/// <summary>Runway data.</summary>
public sealed record RunwayData
{
    /// <summary>Runway index.</summary>
    public uint Index { get; init; }

    /// <summary>Position.</summary>
    public Domain.ValueObjects.GeoPosition Position { get; init; } = Domain.ValueObjects.GeoPosition.Create(0, 0);

    /// <summary>Heading in degrees.</summary>
    public double HeadingDegrees { get; init; }

    /// <summary>Length in meters.</summary>
    public double LengthMeters { get; init; }

    /// <summary>Width in meters.</summary>
    public double WidthMeters { get; init; }

    /// <summary>Surface type.</summary>
    public int Surface { get; init; }

    /// <summary>Primary runway number.</summary>
    public int PrimaryNumber { get; init; }

    /// <summary>Primary runway designator.</summary>
    public string PrimaryDesignator { get; init; } = string.Empty;

    /// <summary>Secondary runway number.</summary>
    public int SecondaryNumber { get; init; }

    /// <summary>Secondary runway designator.</summary>
    public string SecondaryDesignator { get; init; } = string.Empty;
}

/// <summary>Taxiway node (point).</summary>
public sealed record TaxiwayNode
{
    /// <summary>Node index.</summary>
    public uint Index { get; init; }

    /// <summary>Node type.</summary>
    public int Type { get; init; }

    /// <summary>Node orientation.</summary>
    public int Orientation { get; init; }

    /// <summary>X bias in meters.</summary>
    public double BiasXMeters { get; init; }

    /// <summary>Z bias in meters.</summary>
    public double BiasZMeters { get; init; }

    /// <summary>Position.</summary>
    public Domain.ValueObjects.GeoPosition Position { get; init; } = Domain.ValueObjects.GeoPosition.Create(0, 0);
}

/// <summary>Taxiway edge (path between nodes).</summary>
public sealed record TaxiwayEdge
{
    /// <summary>Edge index.</summary>
    public uint Index { get; init; }

    /// <summary>Edge type.</summary>
    public int Type { get; init; }

    /// <summary>Width in meters.</summary>
    public double WidthMeters { get; init; }

    /// <summary>Weight limit in pounds.</summary>
    public uint WeightLimitPounds { get; init; }

    /// <summary>Runway number.</summary>
    public uint RunwayNumber { get; init; }

    /// <summary>Runway designator.</summary>
    public int RunwayDesignator { get; init; }

    /// <summary>Start node index.</summary>
    public uint StartIndex { get; init; }

    /// <summary>End node index.</summary>
    public uint EndIndex { get; init; }

    /// <summary>Name index.</summary>
    public uint NameIndex { get; init; }

    /// <summary>Taxiway name.</summary>
    public string TaxiwayName { get; init; } = string.Empty;
}

/// <summary>Parking spot.</summary>
public sealed record ParkingSpot
{
    /// <summary>Spot index.</summary>
    public uint Index { get; init; }

    /// <summary>Spot type.</summary>
    public int Type { get; init; }

    /// <summary>Taxi point type.</summary>
    public int TaxiPointType { get; init; }

    /// <summary>Spot name.</summary>
    public int Name { get; init; }

    /// <summary>Spot suffix.</summary>
    public int Suffix { get; init; }

    /// <summary>Spot number.</summary>
    public uint Number { get; init; }

    /// <summary>Spot orientation.</summary>
    public int Orientation { get; init; }

    /// <summary>Spot heading in degrees.</summary>
    public double HeadingDegrees { get; init; }

    /// <summary>Spot radius in meters.</summary>
    public double RadiusMeters { get; init; }

    /// <summary>X bias in meters.</summary>
    public double BiasXMeters { get; init; }

    /// <summary>Z bias in meters.</summary>
    public double BiasZMeters { get; init; }
}

/// <summary>Frequency data.</summary>
public sealed record FrequencyData
{
    /// <summary>Frequency index.</summary>
    public uint Index { get; init; }

    /// <summary>Frequency type.</summary>
    public int Type { get; init; }

    /// <summary>Frequency in hertz.</summary>
    public uint FrequencyHertz { get; init; }

    /// <summary>Frequency in megahertz.</summary>
    public FrequencyMhz Mhz => new(FrequencyHertz / 1_000_000.0);
}
