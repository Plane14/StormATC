using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StormATC.Application.Checklists;

namespace StormATC.Application.Abstractions;

/// <summary>Orchestrates flight phases and coordinates all flight-related services.</summary>
public interface IFlightOrchestrator
{
    /// <summary>Current autopilot flight phase.</summary>
    Domain.State.FlightPhase CurrentPhase { get; }

    /// <summary>Most recent phase decision, when a tick has run.</summary>
    Domain.State.PhaseDecision? LastDecision { get; }

    /// <summary>Most recent terrain profile, when one was built.</summary>
    Domain.State.TerrainProfile? LastTerrainProfile { get; }

    /// <summary>Most recent aircraft state snapshot used by the phase engine.</summary>
    Domain.Aircraft.AircraftState? LastAircraftState { get; }

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
    /// <summary>Most recent control targets computed by the autopilot.</summary>
    Domain.State.ControlTargets? CurrentTargets { get; }

    /// <summary>Computes control targets from a phase decision.</summary>
    Domain.State.ControlTargets Update(
        Domain.State.PhaseEvaluationContext context,
        Domain.State.PhaseDecision decision);
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

/// <summary>Parameters for a continuous-flight mission, supplied by the Mission Builder page.</summary>
public sealed record ContinuousFlightRequest
{
    /// <summary>Minimum leg range in nautical miles.</summary>
    public required double MinRangeNm { get; init; }

    /// <summary>Maximum leg range in nautical miles.</summary>
    public required double MaxRangeNm { get; init; }

    /// <summary>Service time per stop in minutes.</summary>
    public required int ServiceTimeMinutes { get; init; }

    /// <summary>Fuel reserve factor (0.0 to 1.0).</summary>
    public required double ReserveFactor { get; init; }
}

/// <summary>Continuous flight service - destination selection, service turnaround.</summary>
public interface IContinuousFlightService
{
    /// <summary>True while a mission is running.</summary>
    bool IsRunning { get; }

    /// <summary>The most recent route summary planned by the service, or null.</summary>
    string? LastPlannedRoute { get; }

    /// <summary>Starts a mission with the supplied parameters and plans the first leg.</summary>
    Task StartMissionAsync(ContinuousFlightRequest request, CancellationToken ct = default);

    /// <summary>Stops the running mission.</summary>
    Task StopMissionAsync(CancellationToken ct = default);

    /// <summary>Plans the next continuous flight leg.</summary>
    Task PlanNextLegAsync(CancellationToken ct = default);
}

/// <summary>Checklist service - checklist execution.</summary>
public interface IChecklistService
{
    /// <summary>Marks a step complete within a specific execution.</summary>
    Task<bool> ExecuteStepAsync(string executionId, string stepId, CancellationToken ct = default);

    /// <summary>Starts executing a checklist definition and returns the execution id.</summary>
    string StartChecklist(Domain.Checklists.ChecklistDefinition definition);

    /// <summary>Gets the current step of an execution.</summary>
    Domain.Checklists.ChecklistStep? GetCurrentStep(string executionId);

    /// <summary>Advances to the next step; returns false when a required step is incomplete.</summary>
    bool AdvanceStep(string executionId);

    /// <summary>Gets the full state of an execution, or null when unknown.</summary>
    ChecklistExecutionState? GetExecutionState(string executionId);

    /// <summary>Gets all active executions.</summary>
    IReadOnlyList<ChecklistExecutionState> GetActiveExecutions();
}

/// <summary>Checklist repository - load/save checklist definitions.</summary>
public interface IChecklistRepository
{
    /// <summary>Loads all checklist definitions from the configured directory.</summary>
    Task<IReadOnlyList<Domain.Checklists.ChecklistDefinition>> LoadAllAsync(CancellationToken ct = default);

    /// <summary>Loads a checklist definition by ID.</summary>
    Task<Domain.Checklists.ChecklistDefinition?> LoadByIdAsync(string id, CancellationToken ct = default);
}
