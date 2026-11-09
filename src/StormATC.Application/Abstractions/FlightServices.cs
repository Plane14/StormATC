using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StormATC.Application.Abstractions;

/// <summary>Interface for flight orchestrator - coordinates phase engine, ATC engine, and autopilot.</summary>
public interface IFlightOrchestrator
{
    /// <summary>Starts the flight orchestrator tick loop.</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>Stops the flight orchestrator tick loop.</summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>Performs one tick of flight phase evaluation and control target generation.</summary>
    Task TickAsync(CancellationToken ct);
}

/// <summary>Interface for autopilot service - native AP for enroute, PID for ground/precision.</summary>
public interface IAutopilotService
{
    /// <summary>Computes control targets from a phase decision.</summary>
    Domain.State.ControlTargets Update(
        Domain.State.PhaseEvaluationContext context,
        Domain.State.PhaseDecision decision);
}

/// <summary>Interface for flight phase engine - deterministic flight phase state machine.</summary>
public interface IFlightPhaseEngine
{
    /// <summary>Evaluates a phase context and returns a phase decision.</summary>
    Domain.State.PhaseDecision Evaluate(Domain.State.PhaseEvaluationContext context);

    /// <summary>Evaluates state changes without pilot input (autonomous transitions).</summary>
    Domain.State.PhaseDecision EvaluateStateChange(Domain.State.PhaseEvaluationContext context);
}

/// <summary>Interface for terrain elevation providers.</summary>
public interface ITerrainProvider
{
    /// <summary>Gets terrain elevations for a list of points.</summary>
    Task<IReadOnlyList<double?>?> GetElevationsAsync(
        IReadOnlyList<(double lat, double lon)> points,
        CancellationToken ct = default);

    /// <summary>Gets a ground elevation grid around a center point.</summary>
    Task<double[]?> GetGroundGridAsync(
        double centerLat,
        double centerLon,
        int widthPoints,
        int heightPoints,
        double spacingNm,
        CancellationToken ct = default);
}

/// <summary>Interface for continuous flight service - destination selection, service turnaround.</summary>
public interface IContinuousFlightService
{
    /// <summary>Plans the next continuous flight leg.</summary>
    Task PlanNextLegAsync(CancellationToken ct = default);
}

/// <summary>Interface for checklist service - checklist execution.</summary>
public interface IChecklistService
{
    /// <summary>Executes a checklist step.</summary>
    Task ExecuteStepAsync(string stepId, CancellationToken ct = default);
}

/// <summary>Interface for checklist repository - load/save checklist definitions.</summary>
public interface IChecklistRepository
{
    /// <summary>Loads all checklist definitions from the configured directory.</summary>
    Task<IReadOnlyList<Domain.Checklists.ChecklistDefinition>> LoadAllAsync(CancellationToken ct = default);

    /// <summary>Loads a checklist definition by ID.</summary>
    Task<Domain.Checklists.ChecklistDefinition?> LoadByIdAsync(string id, CancellationToken ct = default);
}

/// <summary>Interface for route builder - builds flight plans between airports.</summary>
public interface IRouteBuilder
{
    /// <summary>Builds a route between two airports.</summary>
    Domain.FlightPlanning.FlightPlan BuildRoute(string departureIcao, string destinationIcao, string departureRunway);
}

/// <summary>Interface for performance calculator - climb/descent/cruise performance.</summary>
public interface IPerformanceCalculator
{
    /// <summary>Climb rate in feet per minute.</summary>
    double ClimbRateFpm { get; }

    /// <summary>Descent rate in feet per minute.</summary>
    double DescentRateFpm { get; }

    /// <summary>Cruise speed in knots.</summary>
    double CruiseSpeed { get; }
}

/// <summary>Interface for terrain analyzer - required altitude, path blocking.</summary>
public interface ITerrainAnalyzer
{
    /// <summary>Returns required altitude ahead in feet.</summary>
    double RequiredAltitudeAheadFt(GeoPosition position, double aheadNm);

    /// <summary>True when a path is blocked by terrain at current altitude.</summary>
    bool IsPathBlocked(GeoPosition position, double currentAltitudeFt, double lookAheadNm);
}

/// <summary>Interface for taxi router over ground graph.</summary>
public interface ITaxiRouter
{
    /// <summary>Finds a route to the holding point for a runway.</summary>
    Domain.State.TaxiRoute? FindRouteToHoldingPoint(Domain.State.AirportData airport, GeoPosition position, string runway);
}

/// <summary>Simulator connection states.</summary>
public enum SimulatorConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Unavailable,
    Error,
}

/// <summary>Event args for aircraft state changes.</summary>
public sealed class AircraftStateChangedEventArgs : EventArgs
{
    /// <summary>The new aircraft state.</summary>
    public Domain.State.AircraftState State { get; }

    /// <summary>Creates new event args.</summary>
    public AircraftStateChangedEventArgs(Domain.State.AircraftState state) => State = state;
}

/// <summary>Event args for simulator connection changes.</summary>
public sealed class SimulatorConnectionChangedEventArgs : EventArgs
{
    /// <summary>The new connection state.</summary>
    public SimulatorConnectionState State { get; }

    /// <summary>Status message.</summary>
    public string Message { get; }

    /// <summary>Creates new event args.</summary>
    public SimulatorConnectionChangedEventArgs(SimulatorConnectionState state, string message)
    {
        State = state;
        Message = message;
    }
}

/// <summary>ATC decision (lightweight Application-layer view).</summary>
public sealed record AtcDecision
{
    /// <summary>Whether the transmission was accepted.</summary>
    public bool Accepted { get; init; }

    /// <summary>The kind of response the controller should make.</summary>
    public string? ResponseType { get; init; }

    /// <summary>Fallback phraseology when no model is configured.</summary>
    public string? FallbackPhraseology { get; init; }

    /// <summary>Approved facts for the response.</summary>
    public IReadOnlyDictionary<string, string> ApprovedFacts { get; init; } = new Dictionary<string, string>();

    /// <summary>Things the response must not say.</summary>
    public IReadOnlyList<string> ProhibitedFacts { get; init; } = [];

    /// <summary>Whether language model phraseology is allowed.</summary>
    public bool AllowLanguageModelPhraseology { get; init; }
}

/// <summary>ATC evaluation context (lightweight Application-layer view).</summary>
public sealed record AtcEvaluationContext
{
    /// <summary>Aircraft callsign.</summary>
    public string Callsign { get; init; } = string.Empty;

    /// <summary>Pilot transcript.</summary>
    public string Transcript { get; init; } = string.Empty;

    /// <summary>Current flight phase.</summary>
    public string CurrentPhase { get; init; } = string.Empty;

    /// <summary>Weather snapshot.</summary>
    public Domain.State.WeatherSnapshot? Weather { get; init; }

    /// <summary>Realism options.</summary>
    public Domain.State.RealismOptions Realism { get; init; } = new();

    /// <summary>Current time.</summary>
    public DateTimeOffset CurrentTime { get; init; } = DateTimeOffset.UtcNow;
}
