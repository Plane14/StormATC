using System;
using Microsoft.Extensions.Options;
using StormATC.Application.Flight;
using StormATC.Application.Options;
using StormATC.Domain.Aircraft;
using StormATC.Domain.State;
using StormATC.Domain.ValueObjects;
using Xunit;
using FlightPhase = StormATC.Domain.State.FlightPhase;

namespace StormATC.Application.Tests;

/// <summary>Tests for hybrid autopilot control target generation.</summary>
public sealed class HybridAutopilotServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly HybridAutopilotService _service = new(
        Microsoft.Extensions.Options.Options.Create(new FlightOptions()));

    [Fact]
    public void TaildraggerClimbUsesReducedSpeed()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Climb, "Piper J-3 Cub"),
            PhaseDecision.Silent());

        Assert.True(targets.Taildragger);
        Assert.Equal(AutopilotMode.VerticalSpeedHold, targets.Mode);
        Assert.Equal(85, targets.TargetSpeedKnots!.Value.Knots);
        Assert.Equal(8, targets.TargetPitchDegrees);
    }

    [Fact]
    public void TricycleClimbUsesDefaultSpeed()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Climb, "Cessna 172 Skyhawk"),
            PhaseDecision.Silent());

        Assert.False(targets.Taildragger);
        Assert.Equal(140, targets.TargetSpeedKnots!.Value.Knots);
        Assert.Null(targets.TargetPitchDegrees);
    }

    [Fact]
    public void TaildraggerApproachUsesReducedSpeed()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.FinalApproach, "Douglas DC-3"),
            PhaseDecision.Silent());

        Assert.True(targets.Taildragger);
        Assert.Equal(AutopilotMode.ApproachHold, targets.Mode);
        Assert.Equal(75, targets.TargetSpeedKnots!.Value.Knots);
    }

    [Fact]
    public void TricycleApproachUsesStandardSpeed()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Approach, "Boeing 737-800"),
            PhaseDecision.Silent());

        Assert.False(targets.Taildragger);
        Assert.Equal(120, targets.TargetSpeedKnots!.Value.Knots);
    }

    [Fact]
    public void GoAroundProducesClimbTargets()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.GoAround, "Cessna 172 Skyhawk"),
            PhaseDecision.Silent());

        Assert.Equal(AutopilotMode.VerticalSpeedHold, targets.Mode);
        Assert.NotNull(targets.TargetVerticalSpeedFpm);
        Assert.True(targets.TargetVerticalSpeedFpm!.Value.Fpm > 0);
    }

    [Fact]
    public void RolloutDisengagesAutopilot()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Rollout, "Piper J-3 Cub"),
            PhaseDecision.Silent());

        Assert.Equal(AutopilotMode.Disengaged, targets.Mode);
        Assert.True(targets.Taildragger);
    }

    private static PhaseEvaluationContext Context(FlightPhase phase, string title) => new()
    {
        CurrentPhase = phase,
        AircraftState = AircraftState.Unknown(Now) with
        {
            AircraftTitle = title,
            IsOnGround = phase is FlightPhase.Rollout or FlightPhase.Preflight,
        },
        FlightPlan = null,
        DepartureAirport = null,
        DestinationAirport = null,
        Weather = null,
        TerrainProfile = null,
        Traffic = [],
        Realism = RealismOptions.FromProfile(FlightProfile.Standard),
        CurrentTime = Now,
    };
}
