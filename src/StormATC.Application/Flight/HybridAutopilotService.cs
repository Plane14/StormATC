using System;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;
using StormATC.Domain.Autofly;
using StormATC.Domain.State;
using StormATC.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace StormATC.Application.Flight;

/// <summary>
/// Hybrid autopilot service — produces deterministic control targets per flight phase.
/// </summary>
/// <remarks>
/// Speeds, rates, flap schedules, and pitch/bank limits come from the aircraft autofly
/// profile matched to the simulator-reported title. When no profile matches, the generic
/// profile's conservative defaults are used and every simulator command without an event
/// binding is reported as unsupported rather than guessed.
/// </remarks>
public sealed class HybridAutopilotService : IAutopilotService
{
    /// <summary>Taxi speed target in knots.</summary>
    private const double TaxiSpeedKnots = 15;

    /// <summary>Distance in metres at which a ground target counts as reached.</summary>
    private const double GroundArrivalDistanceMeters = 60;

    private readonly FlightOptions _flightOptions;
    private readonly IAircraftAutoflyProfileProvider _profileProvider;
    private readonly object _sync = new();
    private ControlTargets? _currentTargets;
    private AircraftAutoflyProfile? _profile;
    private string? _profileAircraftTitle;

    /// <summary>Creates a hybrid autopilot service.</summary>
    public HybridAutopilotService(
        IOptions<FlightOptions> flightOptions,
        IAircraftAutoflyProfileProvider profileProvider)
    {
        _flightOptions = flightOptions?.Value ?? throw new ArgumentNullException(nameof(flightOptions));
        _profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
    }

    /// <inheritdoc />
    public ControlTargets? CurrentTargets
    {
        get { lock (_sync) { return _currentTargets; } }
    }

    /// <summary>Computes control targets from a phase decision.</summary>
    public ControlTargets Update(PhaseEvaluationContext context, PhaseDecision decision)
    {
        ControlTargets targets = decision.ControlTargets ?? ComputeTargets(context);

        lock (_sync)
        {
            _currentTargets = targets;
        }

        return targets;
    }

    private AircraftAutoflyProfile ProfileFor(Domain.Aircraft.AircraftState aircraft)
    {
        string? title = aircraft.AircraftTitle;
        lock (_sync)
        {
            if (_profile is not null
                && string.Equals(_profileAircraftTitle, title, StringComparison.OrdinalIgnoreCase))
            {
                return _profile;
            }
        }

        AircraftAutoflyProfile profile = _profileProvider
            .GetProfileAsync(aircraft)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        lock (_sync)
        {
            _profile = profile;
            _profileAircraftTitle = title;
        }

        return profile;
    }

    private ControlTargets ComputeTargets(PhaseEvaluationContext context)
    {
        Domain.Aircraft.AircraftState aircraft = context.AircraftState;
        AircraftAutoflyProfile profile = ProfileFor(aircraft);
        AutoflyPerformance performance = profile.Performance;
        bool taildragger = aircraft.IsTaildragger;

        SpeedKnots climbSpeed = new(performance.ClimbSpeedKnots);
        SpeedKnots cruiseSpeed = new(performance.CruiseSpeedKnots);
        SpeedKnots approachSpeed = new(performance.ApproachSpeedKnots);
        SpeedKnots descentSpeed = new(performance.CruiseSpeedKnots * 0.8);

        int? flapIndex = FlapIndexFor(context.CurrentPhase, performance);

        ControlTargets targets = context.CurrentPhase switch
        {
            FlightPhase.Preflight => GroundHold(aircraft, profile, taildragger, engines: null),
            FlightPhase.Startup => GroundHold(
                aircraft,
                profile,
                taildragger,
                engines: true,
                pushback: context.PushbackCleared && aircraft.IsStationary),
            FlightPhase.Shutdown => ShutdownTargets(aircraft, taildragger),
            FlightPhase.TaxiOut or FlightPhase.TaxiIn => TaxiTargets(context, profile, taildragger),
            FlightPhase.HoldingShort => HoldingTargets(context, profile, taildragger),
            FlightPhase.LineUp or FlightPhase.Takeoff or FlightPhase.Rollout => RunwayTargets(context, profile, taildragger),
            FlightPhase.InitialClimb or FlightPhase.Climb or FlightPhase.GoAround => new ControlTargets
            {
                Mode = AutopilotMode.VerticalSpeedHold,
                TargetVerticalSpeedFpm = new VerticalSpeedFpm(performance.ClimbRateFpm),
                TargetAltitudeFeet = new AltitudeFeet(
                    context.FlightPlan?.CruiseAltitude?.Feet
                    ?? context.TerrainProfile?.MaxRequiredAltitudeFt
                    ?? aircraft.IndicatedAltitudeFeet),
                TargetSpeedKnots = climbSpeed,
                TargetPitchDegrees = taildragger ? 8 : performance.MaxPitchUpDegrees / 2,
                GearDown = true,
                Taildragger = taildragger,
                TargetFlapIndex = flapIndex,
                LandingLightsOn = true,
                BeaconOn = true,
                TaxiLightsOn = false,
            },
            FlightPhase.Cruise => new ControlTargets
            {
                Mode = AutopilotMode.AltitudeHold,
                TargetAltitudeFeet = new AltitudeFeet(
                    context.FlightPlan?.CruiseAltitude?.Feet
                    ?? context.TerrainProfile?.MaxRequiredAltitudeFt
                    ?? aircraft.IndicatedAltitudeFeet),
                TargetHeadingDegrees = BearingToDestination(context),
                TargetSpeedKnots = cruiseSpeed,
                GearDown = true,
                Taildragger = taildragger,
                TargetFlapIndex = 0,
                LandingLightsOn = false,
                BeaconOn = true,
                TaxiLightsOn = false,
            },
            FlightPhase.Descent => new ControlTargets
            {
                Mode = AutopilotMode.VerticalSpeedHold,
                TargetVerticalSpeedFpm = new VerticalSpeedFpm(-performance.DescentRateFpm),
                TargetAltitudeFeet = new AltitudeFeet(
                    context.DestinationAirport?.ElevationFeet ?? 0),
                TargetHeadingDegrees = BearingToDestination(context),
                TargetSpeedKnots = descentSpeed,
                GearDown = true,
                Taildragger = taildragger,
                TargetFlapIndex = performance.DownwindFlaps,
                LandingLightsOn = true,
                BeaconOn = true,
                TaxiLightsOn = false,
            },
            FlightPhase.Approach or FlightPhase.FinalApproach => new ControlTargets
            {
                Mode = AutopilotMode.ApproachHold,
                TargetAltitudeFeet = new AltitudeFeet(
                    context.DestinationAirport?.ElevationFeet ?? 0),
                TargetHeadingDegrees = BearingToDestination(context),
                TargetSpeedKnots = approachSpeed,
                GearDown = true,
                Taildragger = taildragger,
                TargetFlapIndex = performance.LandingFlaps,
                LandingLightsOn = true,
                BeaconOn = true,
                TaxiLightsOn = false,
            },
            _ => new ControlTargets
            {
                Mode = AutopilotMode.Disengaged,
                GearDown = aircraft.IsOnGround ? aircraft.GearHandleDown : true,
                Taildragger = taildragger,
            },
        };

        return targets with
        {
            EnginesRunning = targets.EnginesRunning
                ?? (context.CurrentPhase switch
                {
                    FlightPhase.Preflight => (bool?)null,
                    FlightPhase.Shutdown => false,
                    _ => true,
                }),
            ThrottlePercent = context.CurrentPhase switch
            {
                FlightPhase.Takeoff => 100,
                FlightPhase.Rollout or FlightPhase.Shutdown => 0,
                _ => targets.ThrottlePercent,
            },
        };
    }

    private static ControlTargets GroundHold(
        Domain.Aircraft.AircraftState aircraft,
        AircraftAutoflyProfile profile,
        bool taildragger,
        bool? engines,
        bool pushback = false) => new()
        {
            Mode = AutopilotMode.Disengaged,
            TargetSpeedKnots = SpeedKnots.Zero,
            ParkingBrakeSet = true,
            BeaconOn = true,
            TaxiLightsOn = true,
            LandingLightsOn = false,
            GearDown = aircraft.GearHandleDown,
            TargetFlapIndex = profile.Performance.TakeoffFlaps,
            EnginesRunning = engines,
            PushbackRequested = pushback,
            Taildragger = taildragger,
        };

    private static ControlTargets ShutdownTargets(
        Domain.Aircraft.AircraftState aircraft,
        bool taildragger) => new()
        {
            Mode = AutopilotMode.Disengaged,
            TargetSpeedKnots = SpeedKnots.Zero,
            ParkingBrakeSet = true,
            BeaconOn = false,
            TaxiLightsOn = false,
            LandingLightsOn = false,
            GearDown = aircraft.GearHandleDown,
            EnginesRunning = false,
            ThrottlePercent = 0,
            Taildragger = taildragger,
        };

    private static ControlTargets TaxiTargets(
        PhaseEvaluationContext context,
        AircraftAutoflyProfile profile,
        bool taildragger)
    {
        Domain.Aircraft.AircraftState aircraft = context.AircraftState;
        GroundNavigationTarget? target = context.GroundTarget;

        if (target is null)
        {
            // No published geometry: hold position honestly rather than steering somewhere
            // the data cannot describe.
            return new ControlTargets
            {
                Mode = AutopilotMode.Disengaged,
                TargetSpeedKnots = SpeedKnots.Zero,
                ParkingBrakeSet = true,
                BeaconOn = true,
                TaxiLightsOn = true,
                Taildragger = taildragger,
            };
        }

        double distance = aircraft.Position.DistanceMetersTo(target.Position);
        if (target.StopAtTarget && distance <= GroundArrivalDistanceMeters)
        {
            return new ControlTargets
            {
                Mode = AutopilotMode.Disengaged,
                TargetSpeedKnots = SpeedKnots.Zero,
                ParkingBrakeSet = true,
                BeaconOn = true,
                TaxiLightsOn = true,
                TargetHeadingDegrees = target.HeadingDegrees
                    ?? aircraft.Position.TrueBearingTo(target.Position).Degrees,
                Taildragger = taildragger,
            };
        }

        return new ControlTargets
        {
            Mode = AutopilotMode.HeadingHold,
            TargetHeadingDegrees = target.HeadingDegrees
                ?? aircraft.Position.TrueBearingTo(target.Position).Degrees,
            TargetSpeedKnots = new SpeedKnots(TaxiSpeedKnots),
            ParkingBrakeSet = false,
            BeaconOn = true,
            TaxiLightsOn = true,
            Taildragger = taildragger,
        };
    }

    private static ControlTargets HoldingTargets(
        PhaseEvaluationContext context,
        AircraftAutoflyProfile profile,
        bool taildragger)
    {
        Domain.Aircraft.AircraftState aircraft = context.AircraftState;
        return new ControlTargets
        {
            Mode = AutopilotMode.Disengaged,
            TargetSpeedKnots = SpeedKnots.Zero,
            ParkingBrakeSet = true,
            BeaconOn = true,
            TaxiLightsOn = true,
            TargetHeadingDegrees = context.GroundTarget?.HeadingDegrees
                ?? aircraft.MagneticHeadingDegrees,
            Taildragger = taildragger,
        };
    }

    private static ControlTargets RunwayTargets(
        PhaseEvaluationContext context,
        AircraftAutoflyProfile profile,
        bool taildragger)
    {
        Domain.Aircraft.AircraftState aircraft = context.AircraftState;
        GroundNavigationTarget? target = context.GroundTarget;
        double? runwayHeading = target?.HeadingDegrees;

        bool aligned = runwayHeading is double heading
            && Math.Abs(NormalizeDegrees(aircraft.MagneticHeadingDegrees - heading)) < 10;
        double distance = target is null
            ? double.MaxValue
            : aircraft.Position.DistanceMetersTo(target.Position);

        bool atThreshold = target is not null
            && target.StopAtTarget
            && distance <= 150;

        double? fallbackHeading = target?.Position is GeoPosition p
            ? aircraft.Position.TrueBearingTo(p).Degrees
            : (double?)aircraft.MagneticHeadingDegrees;

        return new ControlTargets
        {
            Mode = AutopilotMode.HeadingHold,
            TargetHeadingDegrees = runwayHeading ?? fallbackHeading,
            TargetSpeedKnots = new SpeedKnots(
                context.CurrentPhase == FlightPhase.Rollout ? 0 : TaxiSpeedKnots),
            TargetPitchDegrees = aligned && atThreshold
                ? profile.Performance.MaxPitchUpDegrees / 2
                : null,
            ParkingBrakeSet = context.CurrentPhase == FlightPhase.Rollout && aligned && atThreshold,
            BeaconOn = true,
            LandingLightsOn = context.CurrentPhase == FlightPhase.Rollout,
            TaxiLightsOn = context.CurrentPhase != FlightPhase.Rollout,
            GearDown = true,
            TargetFlapIndex = context.CurrentPhase == FlightPhase.Takeoff
                ? profile.Performance.TakeoffFlaps
                : context.CurrentPhase == FlightPhase.Rollout
                    ? profile.Performance.LandingFlaps
                    : profile.Performance.TakeoffFlaps,
            Taildragger = taildragger,
        };
    }

    private static int? FlapIndexFor(FlightPhase phase, AutoflyPerformance performance) =>
        phase switch
        {
            FlightPhase.InitialClimb or FlightPhase.Climb => performance.TakeoffFlaps,
            FlightPhase.Cruise => 0,
            FlightPhase.Descent or FlightPhase.GoAround => performance.DownwindFlaps,
            FlightPhase.Approach => performance.BaseFlaps,
            FlightPhase.FinalApproach => performance.LandingFlaps,
            _ => null,
        };

    private static double NormalizeDegrees(double degrees) =>
        ((degrees % 360) + 540) % 360 - 180;

    private static double BearingToDestination(PhaseEvaluationContext context)
    {
        GeoPosition? destination = context.DestinationAirport?.ReferencePosition
            ?? LastRouteLegPosition(context.FlightPlan);

        if (destination is not GeoPosition position)
        {
            return context.AircraftState.MagneticHeadingDegrees;
        }

        return context.AircraftState.Position.TrueBearingTo(position).Degrees;
    }

    private static GeoPosition? LastRouteLegPosition(Domain.FlightPlanning.FlightPlan? plan)
    {
        if (plan?.RouteLegs is not { Count: > 0 } legs)
        {
            return null;
        }

        for (int i = legs.Count - 1; i >= 0; i--)
        {
            if (legs[i].Position is GeoPosition position)
            {
                return position;
            }
        }

        return null;
    }
}
