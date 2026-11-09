using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StormATC.Application.Abstractions;

namespace StormATC.Application.Flight;

/// <summary>Orchestrates flight phases and coordinates all flight-related services.</summary>
public sealed class FlightOrchestrator : IFlightOrchestrator
{
    private readonly IFlightPhaseEngine _phaseEngine;
    private readonly StormATC.Domain.Atc.IAtcStateEngine _atcEngine;
    private readonly IAutopilotService _autopilot;
    private readonly ILogger<FlightOrchestrator> _logger;
    private Domain.State.FlightPhase _currentPhase = Domain.State.FlightPhase.Unknown;

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

    public Task StartAsync(CancellationToken ct)
    {
        _currentPhase = Domain.State.FlightPhase.Preflight;
        _logger.LogInformation("Flight orchestrator started. Phase: {Phase}", _currentPhase);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _currentPhase = Domain.State.FlightPhase.Shutdown;
        _logger.LogInformation("Flight orchestrator stopped.");
        return Task.CompletedTask;
    }

    public async Task TickAsync(CancellationToken ct)
    {
        // Build the phase evaluation context from available services
        // Note: AircraftState, FlightPlan, etc. come from the session context
        // This is a simplified version; full integration passes real state
        var context = new Domain.State.PhaseEvaluationContext
        {
            CurrentPhase = _currentPhase,
            AircraftState = new StormATC.Domain.Aircraft.AircraftState
            {
                Timestamp = DateTimeOffset.UtcNow,
                Position = StormATC.Domain.ValueObjects.GeoPosition.Create(0, 0),
                IndicatedAltitudeFeet = 0,
                PressureAltitudeFeet = 0,
                GroundSpeedKnots = 0,
                IndicatedAirspeedKnots = 0,
                TrueHeadingDegrees = 360,
                MagneticHeadingDegrees = 360,
                VerticalSpeedFeetPerMinute = 0,
                IsOnGround = true,
                ParkingBrakeSet = true,
                EnginesRunning = false,
                AircraftTitle = "Unknown",
                AtcIdentifier = string.Empty,
                AltitudeAboveGroundFeet = 0,
                AutopilotEngaged = false,
                GearHandleDown = true,
                FlapsHandleIndex = 0,
                Radio = StormATC.Domain.Aircraft.RadioState.Default,
                Transponder = StormATC.Domain.Aircraft.TransponderState.Default,
            },
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
