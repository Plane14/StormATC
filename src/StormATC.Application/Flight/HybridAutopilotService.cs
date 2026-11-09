using System;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;
using StormATC.Domain.State;
using StormATC.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace StormATC.Application.Flight;

/// <summary>Hybrid autopilot service - native AP for enroute, PID for ground/precision.</summary>
public sealed class HybridAutopilotService : IAutopilotService
{
    private readonly FlightOptions _flightOptions;
    private readonly object _sync = new();
    private ControlTargets? _currentTargets;

    /// <summary>Creates a hybrid autopilot service.</summary>
    public HybridAutopilotService(IOptions<FlightOptions> flightOptions)
    {
        _flightOptions = flightOptions?.Value ?? throw new ArgumentNullException(nameof(flightOptions));
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

    private ControlTargets ComputeTargets(PhaseEvaluationContext context)
    {
        Domain.Aircraft.AircraftState aircraft = context.AircraftState;
        bool taildragger = aircraft.IsTaildragger;

        // Taildraggers fly slower profiles: a shallow climb, a low approach speed, and no
        // aggressive pitch or bank demands during rollout.
        SpeedKnots climbSpeed = new(taildragger
            ? _flightOptions.TaildraggerClimbSpeedKnots
            : _flightOptions.DefaultCruiseSpeedKt);
        SpeedKnots approachSpeed = new(taildragger
            ? _flightOptions.TaildraggerApproachSpeedKnots
            : 120);
        SpeedKnots descentSpeed = new(taildragger
            ? _flightOptions.TaildraggerApproachSpeedKnots
            : _flightOptions.DefaultCruiseSpeedKt);

        return context.CurrentPhase switch
        {
            FlightPhase.InitialClimb or FlightPhase.Climb or FlightPhase.GoAround => new ControlTargets
            {
                Mode = AutopilotMode.VerticalSpeedHold,
                TargetVerticalSpeedFpm = new VerticalSpeedFpm(_flightOptions.DefaultClimbRateFpm),
                TargetAltitudeFeet = new AltitudeFeet(
                    context.FlightPlan?.CruiseAltitude?.Feet
                    ?? context.TerrainProfile?.MaxRequiredAltitudeFt
                    ?? aircraft.IndicatedAltitudeFeet),
                TargetSpeedKnots = climbSpeed,
                TargetPitchDegrees = taildragger ? 8 : null,
                GearDown = true,
                Taildragger = taildragger,
            },
            FlightPhase.Cruise => new ControlTargets
            {
                Mode = AutopilotMode.AltitudeHold,
                TargetAltitudeFeet = new AltitudeFeet(
                    context.FlightPlan?.CruiseAltitude?.Feet
                    ?? context.TerrainProfile?.MaxRequiredAltitudeFt
                    ?? aircraft.IndicatedAltitudeFeet),
                TargetHeadingDegrees = BearingToDestination(context),
                TargetSpeedKnots = climbSpeed,
                GearDown = true,
                Taildragger = taildragger,
            },
            FlightPhase.Descent => new ControlTargets
            {
                Mode = AutopilotMode.VerticalSpeedHold,
                TargetVerticalSpeedFpm = new VerticalSpeedFpm(-_flightOptions.DefaultDescentRateFpm),
                TargetAltitudeFeet = new AltitudeFeet(
                    context.DestinationAirport?.ElevationFeet ?? 0),
                TargetHeadingDegrees = BearingToDestination(context),
                TargetSpeedKnots = descentSpeed,
                GearDown = true,
                Taildragger = taildragger,
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
            },
            FlightPhase.Rollout or FlightPhase.TaxiIn or FlightPhase.Shutdown => new ControlTargets
            {
                Mode = AutopilotMode.Disengaged,
                GearDown = context.CurrentPhase != FlightPhase.Shutdown,
                Taildragger = taildragger,
            },
            _ => new ControlTargets
            {
                Mode = AutopilotMode.Disengaged,
                GearDown = aircraft.IsOnGround ? aircraft.GearHandleDown : true,
                Taildragger = taildragger,
            },
        };
    }

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
