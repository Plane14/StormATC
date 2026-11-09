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

        return context.CurrentPhase switch
        {
            FlightPhase.InitialClimb or FlightPhase.Climb => new ControlTargets
            {
                Mode = AutopilotMode.VerticalSpeedHold,
                TargetVerticalSpeedFpm = new VerticalSpeedFpm(_flightOptions.DefaultClimbRateFpm),
                TargetAltitudeFeet = new AltitudeFeet(
                    context.FlightPlan?.CruiseAltitude?.Feet
                    ?? context.TerrainProfile?.MaxRequiredAltitudeFt
                    ?? aircraft.IndicatedAltitudeFeet),
                TargetSpeedKnots = new SpeedKnots(_flightOptions.DefaultCruiseSpeedKt),
                GearDown = true,
            },
            FlightPhase.Cruise => new ControlTargets
            {
                Mode = AutopilotMode.AltitudeHold,
                TargetAltitudeFeet = new AltitudeFeet(
                    context.FlightPlan?.CruiseAltitude?.Feet
                    ?? context.TerrainProfile?.MaxRequiredAltitudeFt
                    ?? aircraft.IndicatedAltitudeFeet),
                TargetHeadingDegrees = BearingToDestination(context),
                TargetSpeedKnots = new SpeedKnots(_flightOptions.DefaultCruiseSpeedKt),
                GearDown = true,
            },
            FlightPhase.Descent => new ControlTargets
            {
                Mode = AutopilotMode.VerticalSpeedHold,
                TargetVerticalSpeedFpm = new VerticalSpeedFpm(-_flightOptions.DefaultDescentRateFpm),
                TargetAltitudeFeet = new AltitudeFeet(
                    context.DestinationAirport?.ElevationFeet ?? 0),
                TargetHeadingDegrees = BearingToDestination(context),
                TargetSpeedKnots = new SpeedKnots(_flightOptions.DefaultCruiseSpeedKt),
                GearDown = true,
            },
            FlightPhase.Approach or FlightPhase.FinalApproach => new ControlTargets
            {
                Mode = AutopilotMode.ApproachHold,
                TargetAltitudeFeet = new AltitudeFeet(
                    context.DestinationAirport?.ElevationFeet ?? 0),
                TargetHeadingDegrees = BearingToDestination(context),
                TargetSpeedKnots = new SpeedKnots(120),
                GearDown = true,
            },
            FlightPhase.Rollout or FlightPhase.TaxiIn or FlightPhase.Shutdown => new ControlTargets
            {
                Mode = AutopilotMode.Disengaged,
                GearDown = context.CurrentPhase != FlightPhase.Shutdown,
            },
            _ => new ControlTargets
            {
                Mode = AutopilotMode.Disengaged,
                GearDown = aircraft.IsOnGround ? aircraft.GearHandleDown : true,
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
