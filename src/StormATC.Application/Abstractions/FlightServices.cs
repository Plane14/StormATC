using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StormATC.Application.Abstractions;

/// <summary>Orchestrates flight phases and coordinates all flight-related services.</summary>
public interface IFlightOrchestrator
{
    /// <summary>Starts the flight orchestrator tick loop.</summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>Stops the flight orchestrator tick loop.</summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>Performs one tick of flight phase evaluation and control target generation.</summary>
    Task TickAsync(CancellationToken ct);
}

/// <summary>Autopilot service - computes control targets from phase decisions.</summary>
public interface IAutopilotService
{
    /// <summary>Computes control targets from a phase decision.</summary>
    Domain.State.ControlTargets Update(
        Domain.State.PhaseEvaluationContext context,
        Domain.State.PhaseDecision decision);
}

/// <summary>Deterministic flight phase state machine (separate from IAtcStateEngine).</summary>
public interface IFlightPhaseEngine
{
    /// <summary>Evaluates a phase context and returns a phase decision.</summary>
    Domain.State.PhaseDecision Evaluate(Domain.State.PhaseEvaluationContext context);

    /// <summary>Evaluates state changes without pilot input (autonomous transitions).</summary>
    Domain.State.PhaseDecision EvaluateStateChange(Domain.State.PhaseEvaluationContext context);
}

/// <summary>Terrain elevation provider.</summary>
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

/// <summary>Continuous flight service - destination selection, service turnaround.</summary>
public interface IContinuousFlightService
{
    /// <summary>Plans the next continuous flight leg.</summary>
    Task PlanNextLegAsync(CancellationToken ct = default);
}

/// <summary>Checklist service - checklist execution.</summary>
public interface IChecklistService
{
    /// <summary>Executes a checklist step.</summary>
    Task ExecuteStepAsync(string stepId, CancellationToken ct = default);
}

/// <summary>Checklist repository - load/save checklist definitions.</summary>
public interface IChecklistRepository
{
    /// <summary>Loads all checklist definitions from the configured directory.</summary>
    Task<IReadOnlyList<Domain.Checklists.ChecklistDefinition>> LoadAllAsync(CancellationToken ct = default);

    /// <summary>Loads a checklist definition by ID.</summary>
    Task<Domain.Checklists.ChecklistDefinition?> LoadByIdAsync(string id, CancellationToken ct = default);
}

/// <summary>Route builder - builds flight plans between airports.</summary>
public interface IRouteBuilder
{
    /// <summary>Builds a route between two airports.</summary>
    Domain.FlightPlanning.FlightPlan BuildRoute(string departureIcao, string destinationIcao, string departureRunway);
}

/// <summary>Performance calculator - climb/descent/cruise performance.</summary>
public interface IPerformanceCalculator
{
    /// <summary>Climb rate in feet per minute.</summary>
    double ClimbRateFpm { get; }

    /// <summary>Descent rate in feet per minute.</summary>
    double DescentRateFpm { get; }

    /// <summary>Cruise speed in knots.</summary>
    double CruiseSpeed { get; }
}

/// <summary>Terrain analyzer - required altitude, path blocking.</summary>
public interface ITerrainAnalyzer
{
    /// <summary>Returns required altitude ahead in feet.</summary>
    double RequiredAltitudeAheadFt(Domain.ValueObjects.GeoPosition position, double aheadNm);

    /// <summary>True when a path is blocked by terrain at current altitude.</summary>
    bool IsPathBlocked(Domain.ValueObjects.GeoPosition position, double currentAltitudeFt, double lookAheadNm);
}

/// <summary>Taxi router over ground graph.</summary>
public interface ITaxiRouter
{
    /// <summary>Finds a route to the holding point for a runway.</summary>
    Domain.State.TaxiRoute? FindRouteToHoldingPoint(Domain.State.AirportData airport, Domain.ValueObjects.GeoPosition position, string runway);
}
