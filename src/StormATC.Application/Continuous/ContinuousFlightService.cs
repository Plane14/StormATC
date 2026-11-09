using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StormATC.Application.Abstractions;
using StormATC.Application.Sessions;
using StormATC.Domain.Airports;
using StormATC.Domain.FlightPlanning;
using StormATC.Domain.ValueObjects;

namespace StormATC.Application.Continuous;

/// <summary>Service for continuous flight planning (destination selection, service turnaround).</summary>
/// <remarks>
/// The service plans one leg at a time against the live session: it picks the nearest available
/// aerodrome inside the configured range band, builds a minimal <see cref="FlightPlan"/>, and
/// attaches it through <see cref="AtcSessionContext.LoadFlightPlanAsync"/>. It does not invent
/// navdata it does not have — legs carry only the origin and destination fixes.
/// </remarks>
public sealed class ContinuousFlightService : IContinuousFlightService
{
    private const int MaxAirportsScanned = 250;

    private readonly AtcSessionContext _sessionContext;
    private readonly IAirportDataProvider _airportDataProvider;
    private readonly ILogger<ContinuousFlightService> _logger;
    private readonly object _sync = new();

    private ContinuousFlightRequest? _request;
    private bool _isRunning;
    private string? _lastPlannedRoute;
    private int _legCounter;

    /// <summary>Creates a continuous flight service.</summary>
    public ContinuousFlightService(
        AtcSessionContext sessionContext,
        IAirportDataProvider airportDataProvider,
        ILogger<ContinuousFlightService> logger)
    {
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _airportDataProvider = airportDataProvider ?? throw new ArgumentNullException(nameof(airportDataProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsRunning
    {
        get { lock (_sync) { return _isRunning; } }
    }

    /// <inheritdoc />
    public string? LastPlannedRoute
    {
        get { lock (_sync) { return _lastPlannedRoute; } }
    }

    /// <inheritdoc />
    public async Task StartMissionAsync(ContinuousFlightRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        lock (_sync)
        {
            _request = request;
            _isRunning = true;
            _legCounter = 0;
            _lastPlannedRoute = null;
        }

        _logger.LogInformation(
            "Continuous flight mission started: range {Min}-{Max} NM, service {Service} min, reserve {Reserve}.",
            request.MinRangeNm, request.MaxRangeNm, request.ServiceTimeMinutes, request.ReserveFactor);

        await PlanNextLegAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopMissionAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _isRunning = false;
            _request = null;
        }

        _logger.LogInformation("Continuous flight mission stopped.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PlanNextLegAsync(CancellationToken ct = default)
    {
        ContinuousFlightRequest? request;
        lock (_sync)
        {
            if (!_isRunning || _request is null)
            {
                return;
            }

            request = _request;
        }

        AirportData? destination = await SelectDestinationAsync(request, ct).ConfigureAwait(false);
        if (destination is null)
        {
            _logger.LogWarning("No suitable destination found within {Min}-{Max} NM.", request.MinRangeNm, request.MaxRangeNm);
            return;
        }

        Domain.Sessions.AtcSession session = _sessionContext.CurrentSession;
        string? departureIcao = session.FlightPlan?.DestinationIcao ?? session.FlightPlan?.DepartureIcao;
        if (string.IsNullOrWhiteSpace(departureIcao))
        {
            _logger.LogWarning("Cannot plan a continuous leg without a current departure aerodrome.");
            return;
        }

        string callsignText = session.Callsign?.Original
            ?? $"STORM{Random.Shared.Next(100, 999)}";
        Callsign callsign = Callsign.TryCreate(callsignText, out Callsign? parsed) && parsed is not null
            ? parsed
            : Callsign.Create("STORM1");

        var plan = new FlightPlan
        {
            FlightPlanId = $"CF-{Guid.NewGuid():N}",
            Callsign = callsign,
            DepartureIcao = departureIcao,
            DestinationIcao = destination.Icao,
            FlightRules = session.FlightPlan?.FlightRules ?? Domain.FlightRules.Ifr,
            CruiseAltitude = Altitude.FromFeet(8000),
            GeneratedAt = DateTimeOffset.UtcNow,
            SourceIdentifier = "ContinuousFlight",
            RouteLegs =
            [
                new RouteLeg { Identifier = departureIcao },
                new RouteLeg { Identifier = destination.Icao },
            ],
        };

        try
        {
            await _sessionContext.LoadFlightPlanAsync(plan, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to attach continuous-flight plan {Summary}.", plan.ToSummary());
            return;
        }

        lock (_sync)
        {
            _legCounter++;
            _lastPlannedRoute = $"Leg {_legCounter}: {plan.ToSummary()} ({destination.Icao})";
        }

        _logger.LogInformation(
            "Planned continuous leg {Summary} ({Distance:F0} NM, service {Service} min).",
            plan.ToSummary(),
            DistanceTo(session, destination),
            request.ServiceTimeMinutes);
    }

    private async Task<AirportData?> SelectDestinationAsync(ContinuousFlightRequest request, CancellationToken ct)
    {
        Domain.Sessions.AtcSession session = _sessionContext.CurrentSession;
        string? currentIcao = session.FlightPlan?.DestinationIcao ?? session.FlightPlan?.DepartureIcao;

        IReadOnlyList<string> icaos;
        try
        {
            icaos = await _airportDataProvider.ListAvailableAirportsAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Airport provider failed; cannot plan a continuous leg.");
            return null;
        }

        double targetDistance = (request.MinRangeNm + request.MaxRangeNm) / 2;
        AirportData? best = null;
        double bestScore = double.MaxValue;
        int scanned = 0;

        foreach (string icao in icaos)
        {
            ct.ThrowIfCancellationRequested();
            if (scanned++ >= MaxAirportsScanned)
            {
                break;
            }

            if (string.Equals(icao, currentIcao, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AirportData? airport;
            try
            {
                airport = await _airportDataProvider.GetAirportAsync(icao, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                continue;
            }

            if (airport is null)
            {
                continue;
            }

            double distance = DistanceTo(session, airport);
            if (distance < request.MinRangeNm || distance > request.MaxRangeNm)
            {
                continue;
            }

            double score = Math.Abs(distance - targetDistance);
            if (score < bestScore
                || (score == bestScore && string.CompareOrdinal(icao, best?.Icao) < 0))
            {
                bestScore = score;
                best = airport;
            }
        }

        return best;
    }

    private static double DistanceTo(Domain.Sessions.AtcSession session, AirportData airport) =>
        session.AircraftState.Position.DistanceNauticalMilesTo(airport.ReferencePosition);

    private static void ValidateRequest(ContinuousFlightRequest request)
    {
        if (!double.IsFinite(request.MinRangeNm) || request.MinRangeNm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MinRangeNm must be a positive number.");
        }

        if (!double.IsFinite(request.MaxRangeNm) || request.MaxRangeNm < request.MinRangeNm)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MaxRangeNm must be at least MinRangeNm.");
        }

        if (request.MaxRangeNm > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MaxRangeNm cannot exceed 5000 NM.");
        }

        if (request.ServiceTimeMinutes is < 1 or > 480)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "ServiceTimeMinutes must be between 1 and 480.");
        }

        if (!double.IsFinite(request.ReserveFactor) || request.ReserveFactor is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "ReserveFactor must be between 0 and 1.");
        }
    }
}
