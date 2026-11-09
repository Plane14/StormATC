using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;
using StormATC.Application.Sessions;
using StormATC.Domain.Airports;
using StormATC.Domain.Atc;
using StormATC.Domain.FlightPlanning;
using StormATC.Domain.Sessions;
using StormATC.Domain.State;
using StormATC.Domain.ValueObjects;
using IFlightPhaseEngine = StormATC.Domain.Atc.IFlightPhaseEngine;

namespace StormATC.Application.Flight;

/// <summary>Orchestrates flight phases and coordinates all flight-related services.</summary>
public sealed class FlightOrchestrator : IFlightOrchestrator, Microsoft.Extensions.Hosting.IHostedService, IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly IFlightPhaseEngine _phaseEngine;
    private readonly IAutopilotService _autopilot;
    private readonly AtcSessionContext _sessionContext;
    private readonly ITerrainProvider _terrainProvider;
    private readonly FlightOptions _flightOptions;
    private readonly TerrainOptions _terrainOptions;
    private readonly bool _featuresEnabled;
    private readonly ILogger<FlightOrchestrator> _logger;

    private readonly object _sync = new();
    private readonly SemaphoreSlim _tickLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private FlightPhase _currentPhase = FlightPhase.Unknown;
    private PhaseDecision? _lastDecision;
    private TerrainProfile? _lastTerrainProfile;
    private Domain.Aircraft.AircraftState? _lastAircraftState;
    private DateTimeOffset _lastTerrainBuild = DateTimeOffset.MinValue;

    /// <summary>Creates a flight orchestrator.</summary>
    public FlightOrchestrator(
        IFlightPhaseEngine phaseEngine,
        IAutopilotService autopilot,
        AtcSessionContext sessionContext,
        ITerrainProvider terrainProvider,
        IOptions<FlightOptions> flightOptions,
        IOptions<TerrainOptions> terrainOptions,
        IOptions<ApplicationOptions> applicationOptions,
        ILogger<FlightOrchestrator> logger)
    {
        _phaseEngine = phaseEngine ?? throw new ArgumentNullException(nameof(phaseEngine));
        _autopilot = autopilot ?? throw new ArgumentNullException(nameof(autopilot));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _terrainProvider = terrainProvider ?? throw new ArgumentNullException(nameof(terrainProvider));
        _flightOptions = flightOptions?.Value ?? throw new ArgumentNullException(nameof(flightOptions));
        _terrainOptions = terrainOptions?.Value ?? throw new ArgumentNullException(nameof(terrainOptions));
        _featuresEnabled = applicationOptions?.Value.EnableAICopilotFeatures ?? false;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public FlightPhase CurrentPhase
    {
        get { lock (_sync) { return _currentPhase; } }
    }

    /// <inheritdoc />
    public PhaseDecision? LastDecision
    {
        get { lock (_sync) { return _lastDecision; } }
    }

    /// <inheritdoc />
    public TerrainProfile? LastTerrainProfile
    {
        get { lock (_sync) { return _lastTerrainProfile; } }
    }

    /// <inheritdoc />
    public Domain.Aircraft.AircraftState? LastAircraftState
    {
        get { lock (_sync) { return _lastAircraftState; } }
    }

    /// <inheritdoc />
    Task Microsoft.Extensions.Hosting.IHostedService.StartAsync(CancellationToken ct) => StartAsync(ct);

    /// <inheritdoc />
    Task Microsoft.Extensions.Hosting.IHostedService.StopAsync(CancellationToken ct) => StopAsync(ct);

    /// <summary>Starts the flight orchestrator tick loop.</summary>
    public Task StartAsync(CancellationToken ct)
    {
        lock (_sync)
        {
            if (_loopTask is not null || !_featuresEnabled)
            {
                return Task.CompletedTask;
            }

            _currentPhase = FlightPhase.Preflight;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _loopTask = Task.Run(() => TickLoopAsync(_cts.Token), CancellationToken.None);
        }

        _logger.LogInformation("Flight orchestrator started. Phase: {Phase}", _currentPhase);
        return Task.CompletedTask;
    }

    /// <summary>Stops the flight orchestrator tick loop.</summary>
    public async Task StopAsync(CancellationToken ct)
    {
        Task? loop;
        CancellationTokenSource? cts;
        lock (_sync)
        {
            loop = _loopTask;
            cts = _cts;
            _loopTask = null;
            _cts = null;
            _currentPhase = FlightPhase.Shutdown;
        }

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (loop is not null)
        {
            try
            {
                await loop.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        _logger.LogInformation("Flight orchestrator stopped.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _tickLock.Dispose();
        _cts?.Dispose();
    }

    /// <summary>Performs one tick of flight phase evaluation and control target generation.</summary>
    public async Task TickAsync(CancellationToken ct)
    {
        // The hosted loop and the Flight Monitor refresh can both tick; serialize them so a
        // phase transition is never evaluated twice against the same stale phase.
        await _tickLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await TickCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _tickLock.Release();
        }
    }

    private async Task TickCoreAsync(CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AtcSession session = _sessionContext.CurrentSession;

        AtcEvaluationContext atcContext = await _sessionContext
            .BuildEvaluationContextAsync(session, intent: null, transcript: string.Empty, now, ct)
            .ConfigureAwait(false);

        TerrainProfile? terrain = await BuildTerrainProfileAsync(session, ct).ConfigureAwait(false);

        var context = new PhaseEvaluationContext
        {
            CurrentPhase = _currentPhase,
            AircraftState = session.AircraftState,
            FlightPlan = session.FlightPlan,
            DepartureAirport = atcContext.DepartureAirport,
            DestinationAirport = atcContext.DestinationAirport,
            Weather = atcContext.Weather,
            TerrainProfile = terrain,
            Traffic = MapTraffic(atcContext.Traffic),
            Realism = MapRealism(_sessionContext.Realism),
            CurrentTime = now,
            DescentRateFpm = _flightOptions.DefaultDescentRateFpm,
            CruiseSpeedKnots = _flightOptions.DefaultCruiseSpeedKt,
        };

        PhaseDecision autonomous;
        PhaseDecision decision;
        lock (_sync)
        {
            autonomous = _phaseEngine.EvaluateStateChange(context);
            if (autonomous.NewPhase is { } autonomousPhase && autonomousPhase != _currentPhase)
            {
                _logger.LogInformation("Phase transition: {OldPhase} -> {NewPhase}", _currentPhase, autonomousPhase);
                _currentPhase = autonomousPhase;
                context = context with { CurrentPhase = autonomousPhase };
            }

            decision = _phaseEngine.Evaluate(context);
            if (decision.NewPhase is { } newPhase && newPhase != _currentPhase)
            {
                _logger.LogInformation("Phase transition: {OldPhase} -> {NewPhase}", _currentPhase, newPhase);
                _currentPhase = newPhase;
                context = context with { CurrentPhase = newPhase };
            }
        }

        _autopilot.Update(context, decision);

        lock (_sync)
        {
            _lastDecision = decision;
            _lastTerrainProfile = terrain;
            _lastAircraftState = session.AircraftState;
        }
    }

    private async Task<TerrainProfile?> BuildTerrainProfileAsync(AtcSession session, CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - _lastTerrainBuild < _terrainOptions.InFlightUpdateInterval)
        {
            return _lastTerrainProfile;
        }

        _lastTerrainBuild = now;

        List<GeoPosition> points = GetTerrainSamplePoints(session);
        if (points.Count == 0)
        {
            return null;
        }

        try
        {
            IReadOnlyList<double?>? elevations = await _terrainProvider
                .GetElevationsAsync(
                    points.Select(p => (p.Latitude, p.Longitude)).ToList(),
                    ct)
                .ConfigureAwait(false);

            if (elevations is null || elevations.Count == 0)
            {
                return null;
            }

            var segments = new List<TerrainSegment>(elevations.Count);
            double maxRequired = 0;
            bool mountainous = false;
            bool any = false;
            double cumulativeNm = 0;
            GeoPosition previous = session.AircraftState.Position;

            for (int i = 0; i < elevations.Count; i++)
            {
                double? elevation = elevations[i];
                if (elevation is not double elev || !double.IsFinite(elev))
                {
                    continue;
                }

                any = true;
                bool isMountainous = elev > TerrainProfile.MountainousThresholdFt;
                double margin = isMountainous ? TerrainProfile.MountainMarginFt : TerrainProfile.NormalMarginFt;
                double required = elev + margin;
                maxRequired = Math.Max(maxRequired, required);
                mountainous |= isMountainous;

                if (i < points.Count)
                {
                    cumulativeNm += previous.DistanceNauticalMilesTo(points[i]);
                    previous = points[i];
                }

                segments.Add(new TerrainSegment
                {
                    DistanceNm = cumulativeNm,
                    TerrainElevationFt = elev,
                    RequiredAltitudeFt = required,
                    IsMountainous = isMountainous,
                });
            }

            if (!any)
            {
                return null;
            }

            return new TerrainProfile
            {
                Segments = segments,
                Mountainous = mountainous,
                IsFallback = false,
                MaxRequiredAltitudeFt = maxRequired,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Terrain profile build failed; continuing without terrain data.");
            return null;
        }
    }

    private static List<GeoPosition> GetTerrainSamplePoints(AtcSession session)
    {
        var points = new List<GeoPosition>();
        if (session.FlightPlan?.RouteLegs is { Count: > 0 } legs)
        {
            foreach (RouteLeg leg in legs)
            {
                if (leg.Position is GeoPosition position)
                {
                    points.Add(position);
                }
            }
        }

        return points;
    }

    private static List<TrafficSnapshot> MapTraffic(IReadOnlyList<TrafficContact> traffic) =>
        traffic.Select(t => new TrafficSnapshot
        {
            Callsign = t.Callsign,
            Position = t.Position,
            AltitudeFeet = new AltitudeFeet(t.AltitudeFeet),
            GroundSpeedKnots = new SpeedKnots(t.GroundSpeedKnots),
            MagneticHeadingDegrees = new HeadingDegrees(0),
            VerticalSpeedFpm = VerticalSpeedFpm.Zero,
            IsOnGround = t.IsOnRunway,
        }).ToList();

    private static RealismOptions MapRealism(RealismProfile profile) =>
        ReferenceEquals(profile, RealismProfile.Relaxed)
            ? RealismOptions.FromProfile(FlightProfile.Relaxed)
            : ReferenceEquals(profile, RealismProfile.Strict)
                ? RealismOptions.FromProfile(FlightProfile.Strict)
                : RealismOptions.FromProfile(FlightProfile.Standard);

    private async Task TickLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await TickAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flight orchestrator tick loop failed.");
        }
    }
}
