using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;
using StormATC.Application.Sessions;
using StormATC.Domain.State;
using FlightPhase = StormATC.Domain.State.FlightPhase;

namespace StormATC.Application.Continuous;

/// <summary>
/// Advances a continuous-flight mission automatically: once the aircraft is parked after a
/// leg and the configured service time has elapsed, the next leg is planned.
/// </summary>
/// <remarks>
/// This runner only observes simulator truth and the orchestrator's phase. It never commands
/// the aircraft — the next leg is flown by the same deterministic autofly path as any other.
/// </remarks>
public sealed class ContinuousFlightRunner : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    private readonly IContinuousFlightService _service;
    private readonly IFlightOrchestrator _orchestrator;
    private readonly AtcSessionContext _sessionContext;
    private readonly ContinuousFlightOptions _options;
    private readonly ILogger<ContinuousFlightRunner> _logger;

    private DateTimeOffset? _nextLegDue;

    /// <summary>Creates the runner.</summary>
    public ContinuousFlightRunner(
        IContinuousFlightService service,
        IFlightOrchestrator orchestrator,
        AtcSessionContext sessionContext,
        IOptions<ContinuousFlightOptions> options,
        ILogger<ContinuousFlightRunner> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        if (!_service.IsRunning)
        {
            _nextLegDue = null;
            return;
        }

        Domain.Aircraft.AircraftState aircraft = _sessionContext.CurrentSession.AircraftState;
        FlightPhase phase = _orchestrator.CurrentPhase;

        bool parked = aircraft.IsOnGround
            && aircraft.GroundSpeedKnots < 1
            && phase is FlightPhase.Shutdown or FlightPhase.TaxiIn or FlightPhase.Rollout;

        if (!parked)
        {
            // Airborne or still moving: the turnaround clock only runs once the aircraft
            // is genuinely stopped on the ground.
            _nextLegDue = null;
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        _nextLegDue ??= now + TimeSpan.FromMinutes(_options.ServiceTimeMinutes);

        if (now < _nextLegDue.Value)
        {
            return;
        }

        _logger.LogInformation("Continuous-flight turnaround complete; planning the next leg.");
        _nextLegDue = null;
        await _service.PlanNextLegAsync(ct).ConfigureAwait(false);
    }
}
