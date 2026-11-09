using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Flight;
using StormATC.Application.Options;
using StormATC.Domain.Aircraft;
using StormATC.Domain.Autofly;
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
        Microsoft.Extensions.Options.Options.Create(new FlightOptions()),
        new StubProfileProvider());

    [Fact]
    public void TaildraggerClimbUsesProfileSpeed()
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
    public void TricycleClimbUsesProfileSpeed()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Climb, "Cessna 172 Skyhawk"),
            PhaseDecision.Silent());

        Assert.False(targets.Taildragger);
        Assert.Equal(74, targets.TargetSpeedKnots!.Value.Knots);
    }

    [Fact]
    public void TaildraggerApproachUsesProfileSpeed()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.FinalApproach, "Douglas DC-3"),
            PhaseDecision.Silent());

        Assert.True(targets.Taildragger);
        Assert.Equal(AutopilotMode.ApproachHold, targets.Mode);
        Assert.Equal(75, targets.TargetSpeedKnots!.Value.Knots);
        Assert.Equal(3, targets.TargetFlapIndex);
    }

    [Fact]
    public void TricycleApproachUsesProfileSpeed()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Approach, "Boeing 737-800"),
            PhaseDecision.Silent());

        Assert.False(targets.Taildragger);
        Assert.Equal(140, targets.TargetSpeedKnots!.Value.Knots);
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
    public void RolloutCommandsZeroSpeedAndIdleThrottle()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Rollout, "Piper J-3 Cub"),
            PhaseDecision.Silent());

        Assert.Equal(0, targets.TargetSpeedKnots!.Value.Knots);
        Assert.Equal(0, targets.ThrottlePercent);
        Assert.True(targets.Taildragger);
    }

    [Fact]
    public void StartupStartsEnginesAndRequestsPushbackWhenCleared()
    {
        PhaseEvaluationContext context = Context(FlightPhase.Startup, "Cessna 172 Skyhawk")
            with { PushbackCleared = true };

        ControlTargets targets = _service.Update(context, PhaseDecision.Silent());

        Assert.Equal(true, targets.EnginesRunning);
        Assert.True(targets.PushbackRequested);
        Assert.True(targets.ParkingBrakeSet);
    }

    [Fact]
    public void StartupDoesNotRequestPushbackWithoutClearance()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Startup, "Cessna 172 Skyhawk"),
            PhaseDecision.Silent());

        Assert.False(targets.PushbackRequested);
    }

    [Fact]
    public void ShutdownStopsEngines()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Shutdown, "Cessna 172 Skyhawk"),
            PhaseDecision.Silent());

        Assert.Equal(false, targets.EnginesRunning);
        Assert.Equal(AutopilotMode.Disengaged, targets.Mode);
    }

    [Fact]
    public void TakeoffDemandsFullThrottle()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.Takeoff, "Cessna 172 Skyhawk"),
            PhaseDecision.Silent());

        Assert.Equal(100, targets.ThrottlePercent);
    }

    [Fact]
    public void TaxiWithoutGeometryHoldsPosition()
    {
        ControlTargets targets = _service.Update(
            Context(FlightPhase.TaxiOut, "Cessna 172 Skyhawk"),
            PhaseDecision.Silent());

        Assert.Equal(AutopilotMode.Disengaged, targets.Mode);
        Assert.Equal(0, targets.TargetSpeedKnots!.Value.Knots);
        Assert.True(targets.ParkingBrakeSet);
    }

    private static PhaseEvaluationContext Context(FlightPhase phase, string title) => new()
    {
        CurrentPhase = phase,
        AircraftState = AircraftState.Unknown(Now) with
        {
            AircraftTitle = title,
            IsOnGround = phase is FlightPhase.Rollout or FlightPhase.Preflight
                or FlightPhase.Startup or FlightPhase.TaxiOut or FlightPhase.HoldingShort
                or FlightPhase.LineUp or FlightPhase.Takeoff or FlightPhase.Shutdown
                or FlightPhase.TaxiIn,
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

    /// <summary>Returns a fixed profile per test aircraft title.</summary>
    private sealed class StubProfileProvider : IAircraftAutoflyProfileProvider
    {
        public Task<AircraftAutoflyProfile> GetProfileAsync(
            AircraftState aircraftState,
            CancellationToken ct = default) =>
            Task.FromResult(ProfileFor(aircraftState.AircraftTitle));

        private static AircraftAutoflyProfile ProfileFor(string title) => title switch
        {
            "Piper J-3 Cub" => Profile("Piper J-3 Cub", climb: 85, approach: 75, landingFlaps: 3),
            "Douglas DC-3" => Profile("Douglas DC-3", climb: 140, approach: 75, landingFlaps: 3),
            "Boeing 737-800" => Profile("Boeing 737-800", climb: 250, approach: 140, landingFlaps: 3),
            "Cessna 172 Skyhawk" => Profile("Cessna 172 Skyhawk", climb: 74, approach: 60, landingFlaps: 3),
            _ => Profile("Generic", climb: 90, approach: 70, landingFlaps: 2),
        };

        private static AircraftAutoflyProfile Profile(
            string name,
            double climb,
            double approach,
            int landingFlaps) => new()
            {
                Name = name,
                CanAutofly = true,
                Performance = new AutoflyPerformance
                {
                    ClimbSpeedKnots = climb,
                    CruiseSpeedKnots = climb + 30,
                    ApproachSpeedKnots = approach,
                    LandingFlaps = landingFlaps,
                },
            };
    }
}
