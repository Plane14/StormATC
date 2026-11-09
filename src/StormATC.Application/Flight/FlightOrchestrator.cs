using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StormATC.Application.Abstractions;

namespace StormATC.Application.Flight;

/// <summary>Orchestrates flight phases and coordinates all flight-related services.</summary>
public sealed class FlightOrchestrator : IFlightOrchestrator, Microsoft.Extensions.Hosting.IHostedService
{
    private readonly IFlightPhaseEngine _phaseEngine;
    private readonly StormATC.Domain.Atc.IAtcStateEngine _atcEngine;
    private readonly IAutopilotService _autopilot;
    private readonly ILogger<FlightOrchestrator> _logger;
    private Domain.State.FlightPhase _currentPhase = Domain.State.FlightPhase.Unknown;

    /// <summary>Creates a flight orchestrator.</summary>
    public FlightOrchestrator(
        IFlightPhaseEngine phaseEngine,
        StormATC.Domain.Atc.IAtcStateEngine atcEngine,
        IAutopilotService autopilot,
        ILogger<FlightOrchestrator> logger)
    {
        _phaseEngine = phaseEngine ?? throw new ArgumentNullException(nameof(phaseEngine));
        _atcEngine = atcEngine ?? throw new ArgumentNullException(nameof(atcEngine));
        _autopilot = autopilot ?? throw new ArgumentNullException(nameof(autopilot));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    Task Microsoft.Extensions.Hosting.IHostedService.StartAsync(CancellationToken ct) => StartAsync(ct);

    /// <inheritdoc />
    Task Microsoft.Extensions.Hosting.IHostedService.StopAsync(CancellationToken ct) => StopAsync(ct);

    /// <summary>Starts the flight orchestrator tick loop.</summary>
    public Task StartAsync(CancellationToken ct)
    {
        _currentPhase = Domain.State.FlightPhase.Preflight;
        _logger.LogInformation("Flight orchestrator started. Phase: {Phase}", _currentPhase);
        return Task.CompletedTask;
    }

    /// <summary>Stops the flight orchestrator tick loop.</summary>
    public Task StopAsync(CancellationToken ct)
    {
        _currentPhase = Domain.State.FlightPhase.Shutdown;
        _logger.LogInformation("Flight orchestrator stopped.");
        return Task.CompletedTask;
    }

    /// <summary>Performs one tick of flight phase evaluation and control target generation.</summary>
    public async Task TickAsync(CancellationToken ct)
    {
        var context = new Domain.State.PhaseEvaluationContext
        {
            CurrentPhase = _currentPhase,
            AircraftState = StormATC.Domain.Aircraft.AircraftState.Unknown(DateTimeOffset.UtcNow),
            FlightPlan = new StormATC.Domain.FlightPlanning.FlightPlan
            {
                FlightPlanId = string.Empty,
                Callsign = StormATC.Domain.ValueObjects.Callsign.Create("N00000"),
                DepartureIcao = string.Empty,
                DestinationIcao = string.Empty,
            },
            DepartureAirport = null,
            DestinationAirport = null,
            Weather = null,
            TerrainProfile = null,
            Traffic = [],
            Realism = new Domain.State.RealismOptions(),
            CurrentTime = DateTimeOffset.UtcNow,
        };

        var decision = _phaseEngine.Evaluate(context);

        if (decision.NewPhase is { } newPhase)
        {
            _logger.LogInformation("Phase transition: {OldPhase} -> {NewPhase}", _currentPhase, newPhase);
            _currentPhase = newPhase;
        }

        if (decision.ControlTargets is { } targets)
        {
            _autopilot.Update(context, decision);
        }

        await Task.CompletedTask;
    }
}
