using StormATC.Domain;
using StormATC.Domain.Aircraft;
using StormATC.Domain.Airports;
using StormATC.Domain.Atc;
using StormATC.Domain.FlightPlanning;

namespace StormATC.Application.Abstractions;

/// <summary>
/// Supplies aerodrome data. Implementations may read bundled JSON, a user file, or a future
/// licensed navigation database.
/// </summary>
public interface IAirportDataProvider
{
    /// <summary>Loads an aerodrome by ICAO code, or returns null when it is not available.</summary>
    Task<AirportData?> GetAirportAsync(string icao, CancellationToken cancellationToken);

    /// <summary>Lists the ICAO codes this provider can supply.</summary>
    Task<IReadOnlyList<string>> ListAvailableAirportsAsync(CancellationToken cancellationToken);
}

/// <summary>Supplies en-route sector definitions.</summary>
public interface IAirspaceDataProvider
{
    /// <summary>Finds the sector that owns a position and level, or null when none does.</summary>
    Task<SectorDefinition?> FindSectorAsync(
        Domain.ValueObjects.GeoPosition position,
        double altitudeFeet,
        CancellationToken cancellationToken);

    /// <summary>Lists every sector this provider knows about.</summary>
    Task<IReadOnlyList<SectorDefinition>> ListSectorsAsync(CancellationToken cancellationToken);
}

/// <summary>Supplies nearby traffic for the runway-occupancy and separation model.</summary>
public interface ITrafficProvider
{
    /// <summary>Returns traffic relevant to the given aircraft state.</summary>
    Task<IReadOnlyList<TrafficContact>> GetRelevantTrafficAsync(
        AircraftState aircraftState,
        CancellationToken cancellationToken);
}

/// <summary>
/// Supplies weather. Returning null is meaningful: it tells the engine there is no weather
/// source, so wind and QNH must be omitted rather than invented.
/// </summary>
public interface IWeatherProvider
{
    /// <summary>Returns the observation for an aerodrome, or null when none is available.</summary>
    Task<WeatherSnapshot?> GetAirportWeatherAsync(string icao, CancellationToken cancellationToken);
}

/// <summary>Imports a flight plan from an external planning system.</summary>
public interface IFlightPlanProvider
{
    /// <summary>A stable identifier for this provider, shown in the UI.</summary>
    string ProviderName { get; }

    /// <summary>Imports the most recent plan for the requested account.</summary>
    Task<FlightPlanImportResult> ImportLatestAsync(
        FlightPlanImportRequest request,
        CancellationToken cancellationToken);
}

/// <summary>A request to import a flight plan.</summary>
public sealed record FlightPlanImportRequest
{
    /// <summary>The account identifier at the planning service, for example a SimBrief user ID.</summary>
    public string? UserId { get; init; }

    /// <summary>When true, bypass any cached copy and fetch a fresh plan.</summary>
    public bool ForceRefresh { get; init; }
}

/// <summary>The outcome of a flight-plan import.</summary>
public sealed record FlightPlanImportResult
{
    /// <summary>Whether a plan was successfully imported.</summary>
    public required bool Success { get; init; }

    /// <summary>The imported plan, when the import succeeded.</summary>
    public FlightPlan? FlightPlan { get; init; }

    /// <summary>A user-facing explanation of what went wrong.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>One concrete thing the user can do about the failure.</summary>
    public string? SuggestedAction { get; init; }

    /// <summary>Non-fatal problems found while parsing, such as missing optional fields.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Builds a success result.</summary>
    public static FlightPlanImportResult Ok(FlightPlan plan, IReadOnlyList<string>? warnings = null) => new()
    {
        Success = true,
        FlightPlan = plan,
        Warnings = warnings ?? [],
    };

    /// <summary>Builds a failure result.</summary>
    public static FlightPlanImportResult Fail(string message, string suggestedAction) => new()
    {
        Success = false,
        ErrorMessage = message,
        SuggestedAction = suggestedAction,
    };
}

/// <summary>
/// A connection to a flight simulator. Implementations must never block and must survive the
/// simulator restarting underneath them.
/// </summary>
public interface ISimulatorConnector : IAsyncDisposable
{
    /// <summary>A stable identifier for this connector, shown on the Diagnostics page.</summary>
    string ConnectorName { get; }

    /// <summary>Current link state.</summary>
    SimulatorConnectionState ConnectionState { get; }

    /// <summary>Raised roughly five times a second while connected.</summary>
    event EventHandler<AircraftStateChangedEventArgs>? AircraftStateChanged;

    /// <summary>Raised whenever <see cref="ConnectionState"/> changes.</summary>
    event EventHandler<SimulatorConnectionChangedEventArgs>? ConnectionChanged;

    /// <summary>Opens the connection. Returns once connected or definitively unavailable.</summary>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Closes the connection.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Tunes COM1 active to a published frequency and selects COM1 for transmission.
    /// Returns false when the active connector cannot apply radio commands.
    /// </summary>
    Task<bool> TuneCom1Async(
        Domain.ValueObjects.Frequency frequency,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies one deterministic aircraft-control command. Implementations must report whether
    /// the command was accepted; a missing capability is a normal result, not an exception.
    /// </summary>
    Task<AutopilotCommandResult> ApplyAutopilotCommandAsync(
        AutopilotCommand command,
        CancellationToken cancellationToken);
}

/// <summary>The kind of aircraft-control command StormATC can ask a connector to apply.</summary>
public enum AutopilotCommandKind
{
    /// <summary>Engage the aircraft autopilot or flight director.</summary>
    EngageAutopilot,

    /// <summary>Disengage the aircraft autopilot or flight director.</summary>
    DisengageAutopilot,

    /// <summary>Select or command a magnetic heading.</summary>
    SetHeading,

    /// <summary>Select or command a target altitude.</summary>
    SetAltitude,

    /// <summary>Select or command a vertical-speed target.</summary>
    SetVerticalSpeed,

    /// <summary>Select or command an indicated-airspeed or ground-speed target.</summary>
    SetSpeed,

    /// <summary>Fire a named simulator event, such as an aircraft-specific K-event.</summary>
    TriggerEvent,

    /// <summary>Write a named simulator variable, such as a flap or gear position.</summary>
    SetVariable,
}

/// <summary>One deterministic control request handed to a simulator connector.</summary>
public sealed record AutopilotCommand
{
    /// <summary>What the command asks the aircraft to do.</summary>
    public required AutopilotCommandKind Kind { get; init; }

    /// <summary>Heading target in degrees for <see cref="AutopilotCommandKind.SetHeading"/>.</summary>
    public double? HeadingDegrees { get; init; }

    /// <summary>Altitude target in feet for <see cref="AutopilotCommandKind.SetAltitude"/>.</summary>
    public double? AltitudeFeet { get; init; }

    /// <summary>Vertical-speed target in feet per minute.</summary>
    public double? VerticalSpeedFeetPerMinute { get; init; }

    /// <summary>Speed target in knots for <see cref="AutopilotCommandKind.SetSpeed"/>.</summary>
    public double? SpeedKnots { get; init; }

    /// <summary>
    /// Simulator event name for <see cref="AutopilotCommandKind.TriggerEvent"/>, for example
    /// an aircraft-profile K-event such as <c>K:AP_HDG_HOLD_ON</c>.
    /// </summary>
    public string? EventName { get; init; }

    /// <summary>Numeric payload for an event or variable write, where the binding needs one.</summary>
    public double? EventValue { get; init; }

    /// <summary>Simulator variable name for <see cref="AutopilotCommandKind.SetVariable"/>.</summary>
    public string? VariableName { get; init; }

    /// <summary>Value written by <see cref="AutopilotCommandKind.SetVariable"/>.</summary>
    public double? VariableValue { get; init; }
}

/// <summary>Whether a connector applied, refused, or could not deliver a control command.</summary>
public enum AutopilotCommandStatus
{
    /// <summary>The command was accepted by the connector.</summary>
    Applied,

    /// <summary>The connector has no binding for this command or aircraft.</summary>
    Unsupported,

    /// <summary>The command was rejected by a safety or engagement policy.</summary>
    Rejected,

    /// <summary>No connected simulator was available.</summary>
    Disconnected,

    /// <summary>The connector failed while applying the command.</summary>
    Failed,
}

/// <summary>The outcome of one <see cref="ISimulatorConnector.ApplyAutopilotCommandAsync"/> call.</summary>
public sealed record AutopilotCommandResult
{
    /// <summary>The delivery status.</summary>
    public required AutopilotCommandStatus Status { get; init; }

    /// <summary>A short diagnostic message.</summary>
    public string? Message { get; init; }

    /// <summary>True when the connector accepted the command.</summary>
    public bool IsSuccess => Status == AutopilotCommandStatus.Applied;
}

/// <summary>Carries a new aircraft snapshot.</summary>
public sealed class AircraftStateChangedEventArgs(AircraftState state) : EventArgs
{
    /// <summary>The new state.</summary>
    public AircraftState State { get; } = state;
}

/// <summary>Carries a simulator connection state change.</summary>
public sealed class SimulatorConnectionChangedEventArgs(
    SimulatorConnectionState state,
    string? message = null) : EventArgs
{
    /// <summary>The new state.</summary>
    public SimulatorConnectionState State { get; } = state;

    /// <summary>A user-facing explanation, where one is useful.</summary>
    public string? Message { get; } = message;
}

/// <summary>An en-route sector.</summary>
public sealed record SectorDefinition
{
    /// <summary>Sector identifier.</summary>
    public required string SectorId { get; init; }

    /// <summary>The controller name spoken on the radio.</summary>
    public required string ControllerName { get; init; }

    /// <summary>Sector frequency.</summary>
    public required Domain.ValueObjects.Frequency Frequency { get; init; }

    /// <summary>Southern boundary in degrees.</summary>
    public required double MinimumLatitude { get; init; }

    /// <summary>Northern boundary in degrees.</summary>
    public required double MaximumLatitude { get; init; }

    /// <summary>Western boundary in degrees.</summary>
    public required double MinimumLongitude { get; init; }

    /// <summary>Eastern boundary in degrees.</summary>
    public required double MaximumLongitude { get; init; }

    /// <summary>Lower vertical limit in feet.</summary>
    public required int MinimumAltitudeFeet { get; init; }

    /// <summary>Upper vertical limit in feet.</summary>
    public required int MaximumAltitudeFeet { get; init; }

    /// <summary>Identifiers of adjacent sectors.</summary>
    public IReadOnlyList<string> NeighborSectorIds { get; init; } = [];

    /// <summary>Which controller position works this sector.</summary>
    public ControllerPosition Position { get; init; } = ControllerPosition.Center;

    /// <summary>True when a position and level fall inside this sector.</summary>
    public bool Contains(Domain.ValueObjects.GeoPosition position, double altitudeFeet) =>
        position.Latitude >= MinimumLatitude
        && position.Latitude <= MaximumLatitude
        && position.Longitude >= MinimumLongitude
        && position.Longitude <= MaximumLongitude
        && altitudeFeet >= MinimumAltitudeFeet
        && altitudeFeet <= MaximumAltitudeFeet;
}
