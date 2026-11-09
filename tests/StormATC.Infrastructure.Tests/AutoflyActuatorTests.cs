using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Flight;
using StormATC.Application.Options;
using StormATC.Domain.Aircraft;
using StormATC.Domain.Autofly;
using StormATC.Domain.State;
using StormATC.Domain.ValueObjects;
using StormATC.Infrastructure.Simulator;
using Xunit;
using FlightPhase = StormATC.Domain.State.FlightPhase;

namespace StormATC.Infrastructure.Tests;

/// <summary>Tests for the autofly actuator against the mock simulator.</summary>
public sealed class AutoflyActuatorTests : IAsyncLifetime, IDisposable
{
    private readonly MockSimulatorConnector _simulator = new(
        NullLogger<MockSimulatorConnector>.Instance,
        new TestClock());

    private AutopilotActuatorService CreateActuator(
        bool enabled = true,
        AircraftAutoflyProfile? profile = null) => new(
        _simulator,
        new StubProfileProvider(profile),
        Options.Create(new AutoflyOptions { Enabled = enabled }),
        NullLogger<AutopilotActuatorService>.Instance);

    public async Task InitializeAsync() => await _simulator.ConnectAsync(CancellationToken.None);

    public async Task DisposeAsync() => await _simulator.DisposeAsync();

    /// <inheritdoc />
    public void Dispose() => _simulator.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));

    [Fact]
    public async Task DisabledActuatorSendsNothing()
    {
        AutopilotActuatorService actuator = CreateActuator(enabled: false);
        await actuator.UpdateAsync(Targets(), FlightPhase.Cruise, State(), CancellationToken.None);

        Assert.Empty(_simulator.AppliedAutopilotCommands);
    }

    [Fact]
    public async Task CruiseTargetsEngageAndCommandHeadingAltitudeSpeed()
    {
        AutopilotActuatorService actuator = CreateActuator();
        ControlTargets targets = Targets() with
        {
            Mode = AutopilotMode.AltitudeHold,
            TargetHeadingDegrees = 270,
            TargetAltitudeFeet = new AltitudeFeet(5000),
            TargetSpeedKnots = new SpeedKnots(120),
        };

        await actuator.UpdateAsync(targets, FlightPhase.Cruise, State(), CancellationToken.None);

        Assert.True(actuator.IsEngaged);
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.Kind == AutopilotCommandKind.EngageAutopilot);
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.Kind == AutopilotCommandKind.SetHeading && c.HeadingDegrees == 270);
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.Kind == AutopilotCommandKind.SetAltitude && c.AltitudeFeet == 5000);
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.Kind == AutopilotCommandKind.SetSpeed && c.SpeedKnots == 120);
    }

    [Fact]
    public async Task IdenticalTargetsAreNotResent()
    {
        AutopilotActuatorService actuator = CreateActuator();
        ControlTargets targets = Targets() with { TargetHeadingDegrees = 90 };

        await actuator.UpdateAsync(targets, FlightPhase.Cruise, State(), CancellationToken.None);
        int count = _simulator.AppliedAutopilotCommands.Count;
        await actuator.UpdateAsync(targets, FlightPhase.Cruise, State(), CancellationToken.None);

        Assert.Equal(count, _simulator.AppliedAutopilotCommands.Count);
    }

    [Fact]
    public async Task ManualDisconnectPausesAutofly()
    {
        AutopilotActuatorService actuator = CreateActuator();
        await actuator.UpdateAsync(Targets(), FlightPhase.Cruise, State(), CancellationToken.None);
        Assert.True(actuator.IsEngaged);

        // The aircraft now reports its autopilot off — the pilot took over.
        AircraftState manual = State() with { AutopilotEngaged = false };
        await actuator.UpdateAsync(Targets() with { TargetHeadingDegrees = 45 }, FlightPhase.Cruise, manual, CancellationToken.None);

        Assert.False(actuator.IsEngaged);
        Assert.Contains("manual", actuator.StatusMessage, StringComparison.OrdinalIgnoreCase);
        int count = _simulator.AppliedAutopilotCommands.Count;
        await actuator.UpdateAsync(Targets() with { TargetHeadingDegrees = 10 }, FlightPhase.Cruise, manual, CancellationToken.None);
        Assert.Equal(count, _simulator.AppliedAutopilotCommands.Count);
    }

    [Fact]
    public async Task DisengagedTargetsReleaseTheAutopilot()
    {
        AutopilotActuatorService actuator = CreateActuator();
        await actuator.UpdateAsync(Targets(), FlightPhase.Cruise, State(), CancellationToken.None);
        Assert.True(actuator.IsEngaged);

        await actuator.UpdateAsync(
            Targets() with { Mode = AutopilotMode.Disengaged },
            FlightPhase.Shutdown,
            State(),
            CancellationToken.None);

        Assert.False(actuator.IsEngaged);
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.Kind == AutopilotCommandKind.DisengageAutopilot);
    }

    [Fact]
    public async Task GroundConfigurationEventsReachTheMock()
    {
        AutopilotActuatorService actuator = CreateActuator();
        ControlTargets targets = Targets() with
        {
            Mode = AutopilotMode.Disengaged,
            EnginesRunning = true,
            ParkingBrakeSet = false,
            GearDown = false,
            TargetFlapIndex = 2,
            ThrottlePercent = 0,
        };

        await actuator.UpdateAsync(targets, FlightPhase.Startup, State(), CancellationToken.None);

        AircraftState state = _simulator.GetCurrentState();
        Assert.True(state.EnginesRunning);
        Assert.False(state.ParkingBrakeSet);
        Assert.False(state.GearHandleDown);
        Assert.Equal(2, state.FlapsHandleIndex);
    }

    [Fact]
    public async Task StaleStateIsNeverCommanded()
    {
        AutopilotActuatorService actuator = CreateActuator();
        AircraftState stale = State() with { Timestamp = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5) };

        await actuator.UpdateAsync(Targets(), FlightPhase.Cruise, stale, CancellationToken.None);

        Assert.Empty(_simulator.AppliedAutopilotCommands);
        Assert.Contains("stale", actuator.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OfficialEventBindingsDriveConfiguration()
    {
        AircraftAutoflyProfile profile = new()
        {
            Name = "Official bindings",
            CanAutofly = true,
            ThrottleScale = 163.83,
            Events = new Dictionary<string, string>
            {
                ["engage"] = "AUTOPILOT_ON",
                ["gearDown"] = "GEAR_DOWN",
                ["gearUp"] = "GEAR_UP",
                ["parkingBrakeOn"] = "PARKING_BRAKES_ON",
                ["parkingBrakeOff"] = "PARKING_BRAKES_OFF",
                ["landingLightsOn"] = "LANDING_LIGHTS_ON",
                ["landingLightsOff"] = "LANDING_LIGHTS_OFF",
                ["throttle"] = "THROTTLE_SET",
                ["approach"] = "AP_APR_HOLD_ON",
            },
            FlapEvents = new Dictionary<int, string> { [2] = "FLAPS_2" },
            EventSequences = new Dictionary<string, IReadOnlyList<string>>
            {
                ["enginesStart"] = ["MAGNETO_BOTH", "STARTER1_SET"],
                ["enginesStop"] = ["MAGNETO_OFF"],
            },
        };
        AutopilotActuatorService actuator = CreateActuator(profile: profile);

        ControlTargets targets = Targets() with
        {
            Mode = AutopilotMode.ApproachHold,
            EnginesRunning = true,
            ParkingBrakeSet = false,
            GearDown = false,
            TargetFlapIndex = 2,
            ThrottlePercent = 50,
            LandingLightsOn = true,
            TargetHeadingDegrees = 180,
        };

        await actuator.UpdateAsync(targets, FlightPhase.Approach, State(), CancellationToken.None);

        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "AUTOPILOT_ON");
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "GEAR_UP");
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "PARKING_BRAKES_OFF");
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "LANDING_LIGHTS_ON");
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "FLAPS_2");
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "MAGNETO_BOTH" && c.EventValue == 1);
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "STARTER1_SET" && c.EventValue == 1);
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "THROTTLE_SET"
                && c.EventValue is double throttle && Math.Abs(throttle - 8191.5) < 0.01);
        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "AP_APR_HOLD_ON");

        AircraftState state = _simulator.GetCurrentState();
        Assert.True(state.EnginesRunning);
        Assert.False(state.ParkingBrakeSet);
        Assert.False(state.GearHandleDown);
        Assert.Equal(2, state.FlapsHandleIndex);
    }

    [Fact]
    public async Task ContinuousFlapScaleMapsDetentToEventValue()
    {
        AircraftAutoflyProfile profile = new()
        {
            Name = "Continuous flaps",
            CanAutofly = true,
            FlapValueScale = 4095.75,
            Events = new Dictionary<string, string> { ["flaps"] = "FLAPS_CONTINUOUS_SET" },
        };
        AutopilotActuatorService actuator = CreateActuator(profile: profile);

        await actuator.UpdateAsync(
            Targets() with { Mode = AutopilotMode.Disengaged, TargetFlapIndex = 3 },
            FlightPhase.Takeoff,
            State(),
            CancellationToken.None);

        Assert.Contains(_simulator.AppliedAutopilotCommands,
            c => c.EventName == "FLAPS_CONTINUOUS_SET" && c.EventValue == 12287.25);
        Assert.Equal(3, _simulator.GetCurrentState().FlapsHandleIndex);
    }

    private static ControlTargets Targets() => new()
    {
        Mode = AutopilotMode.HeadingHold,
        GearDown = true,
    };

    private static AircraftState State() => AircraftState.Unknown(DateTimeOffset.UtcNow) with
    {
        AircraftTitle = "StormATC Mock A320",
        AutopilotEngaged = true,
    };

    private sealed class StubProfileProvider(AircraftAutoflyProfile? profile = null)
        : IAircraftAutoflyProfileProvider
    {
        public Task<AircraftAutoflyProfile> GetProfileAsync(
            AircraftState aircraftState,
            CancellationToken ct = default) =>
            Task.FromResult(profile ?? new AircraftAutoflyProfile
            {
                Name = "Mock profile",
                CanAutofly = true,
                Events = new Dictionary<string, string>
                {
                    ["engage"] = "MOCK_ENGAGE",
                    ["disengage"] = "MOCK_DISENGAGE",
                },
            });
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
