using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;
using StormATC.Domain.Autofly;
using StormATC.Domain.State;

namespace StormATC.Application.Flight;

/// <summary>
/// Translates deterministic <see cref="ControlTargets"/> into simulator commands.
/// </summary>
/// <remarks>
/// <para>
/// The actuator is the only component allowed to move from "the autopilot wants X" to "send
/// event Y to the simulator". It deduplicates targets so a one-second tick does not spam the
/// event queue, honours the aircraft profile's event bindings, and reports
/// <see cref="AutopilotCommandStatus.Unsupported"/> verbatim — it never invents a binding.
/// </para>
/// <para>
/// A manual autopilot disconnect on the aircraft side pauses command delivery instead of
/// fighting the pilot, matching <see cref="AutoflyOptions.ManualOverridePausesAutofly"/>.
/// </para>
/// </remarks>
public sealed class AutopilotActuatorService : IAutopilotActuator, IDisposable
{
    private readonly ISimulatorConnector _connector;
    private readonly IAircraftAutoflyProfileProvider _profileProvider;
    private readonly AutoflyOptions _options;
    private readonly ILogger<AutopilotActuatorService> _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);

    private bool _enabled;
    private bool _engaged;
    private bool _pausedForManualOverride;
    private AircraftAutoflyProfile? _profile;
    private string? _profileTitle;
    private AutopilotCommandResult? _lastResult;
    private string _status = "Autofly disabled.";

    private readonly Dictionary<string, string> _lastSent = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unsupportedKeys = new(StringComparer.Ordinal);

    /// <summary>Creates the actuator.</summary>
    public AutopilotActuatorService(
        ISimulatorConnector connector,
        IAircraftAutoflyProfileProvider profileProvider,
        IOptions<AutoflyOptions> options,
        ILogger<AutopilotActuatorService> logger)
    {
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        _profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _enabled = _options.Enabled;
        _status = _enabled ? "Autofly armed." : "Autofly disabled.";
    }

    /// <inheritdoc />
    public bool IsEngaged
    {
        get { lock (_sync) { return _engaged; } }
    }

    /// <inheritdoc />
    public string StatusMessage
    {
        get { lock (_sync) { return _status; } }
    }

    /// <inheritdoc />
    public AutopilotCommandResult? LastResult
    {
        get { lock (_sync) { return _lastResult; } }
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _enabled = enabled;
            _pausedForManualOverride = false;
            _lastSent.Clear();
            _unsupportedKeys.Clear();
            _status = enabled ? "Autofly armed." : "Autofly disabled.";

            if (!enabled && _engaged)
            {
                await SendAsync(
                    new AutopilotCommand { Kind = AutopilotCommandKind.DisengageAutopilot },
                    "autopilot",
                    ct).ConfigureAwait(false);
                _engaged = false;
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisengageAsync(string reason, CancellationToken ct = default)
    {
        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _enabled = false;
            _pausedForManualOverride = false;
            _lastSent.Clear();
            _unsupportedKeys.Clear();
            _status = $"Autofly disengaged: {reason}";

            if (_engaged)
            {
                await SendAsync(
                    new AutopilotCommand { Kind = AutopilotCommandKind.DisengageAutopilot },
                    "autopilot",
                    ct).ConfigureAwait(false);
                _engaged = false;
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        ControlTargets targets,
        FlightPhase phase,
        Domain.Aircraft.AircraftState aircraftState,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(aircraftState);

        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_enabled)
            {
                return;
            }

            // Never command a stale snapshot: acting on seconds-old state is how autofly
            // becomes a hazard rather than an aid.
            if (aircraftState.Timestamp < DateTimeOffset.UtcNow - _options.MaximumStateAge)
            {
                _status = "Autofly paused: simulator state is stale.";
                return;
            }

            AircraftAutoflyProfile profile = ProfileFor(aircraftState);
            if (!profile.CanAutofly)
            {
                _status = $"Autofly unavailable for {profile.Name}.";
                return;
            }

            DetectManualOverride(aircraftState);
            if (_pausedForManualOverride)
            {
                return;
            }

            bool wantsEngagement = targets.Mode != AutopilotMode.Disengaged
                && phase is not (FlightPhase.Shutdown or FlightPhase.Preflight);

            if (wantsEngagement)
            {
                if (!_engaged && _options.EngageSimulatorAutopilot)
                {
                    await SendAsync(
                        new AutopilotCommand
                        {
                            Kind = AutopilotCommandKind.EngageAutopilot,
                            EventName = profile.EventFor("engage"),
                        },
                        "autopilot",
                        ct).ConfigureAwait(false);
                    _engaged = true;
                }
            }
            else if (_engaged)
            {
                await SendAsync(
                    new AutopilotCommand
                    {
                        Kind = AutopilotCommandKind.DisengageAutopilot,
                        EventName = profile.EventFor("disengage"),
                    },
                    "autopilot",
                    ct).ConfigureAwait(false);
                _engaged = false;
            }

            if (targets.Mode == AutopilotMode.Disengaged)
            {
                await ApplyConfigurationAsync(targets, profile, ct).ConfigureAwait(false);
                _status = "Autofly tracking (autopilot disengaged).";
                return;
            }

            await ApplyTargetsAsync(targets, profile, ct).ConfigureAwait(false);
            _status = _engaged
                ? $"Autofly engaged on {profile.Name}."
                : $"Autofly tracking targets on {profile.Name}.";
        }
        finally
        {
            _sync.Release();
        }
    }

    private void DetectManualOverride(Domain.Aircraft.AircraftState aircraftState)
    {
        if (!_options.ManualOverridePausesAutofly || !_engaged)
        {
            return;
        }

        // The aircraft reports its own autopilot state. If it dropped while we believed it
        // was engaged, the pilot took over — stop sending and say so.
        if (!aircraftState.AutopilotEngaged)
        {
            _engaged = false;
            _pausedForManualOverride = true;
            _lastSent.Clear();
            _status = "Autofly paused: manual control detected.";
            _logger.LogInformation("Autofly paused after a manual autopilot disconnect.");
        }
    }

    private AircraftAutoflyProfile ProfileFor(Domain.Aircraft.AircraftState aircraftState)
    {
        string? title = aircraftState.AircraftTitle;
        if (_profile is not null
            && string.Equals(_profileTitle, title, StringComparison.OrdinalIgnoreCase))
        {
            return _profile;
        }

        _profile = _profileProvider
            .GetProfileAsync(aircraftState)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
        _profileTitle = title;
        _lastSent.Clear();
        _unsupportedKeys.Clear();
        return _profile;
    }

    private async Task ApplyTargetsAsync(
        ControlTargets targets,
        AircraftAutoflyProfile profile,
        CancellationToken ct)
    {
        await ApplyConfigurationAsync(targets, profile, ct).ConfigureAwait(false);

        if (targets.TargetHeadingDegrees is double heading)
        {
            await SendOnceAsync(
                "heading",
                () => new AutopilotCommand
                {
                    Kind = AutopilotCommandKind.SetHeading,
                    HeadingDegrees = heading,
                    EventName = profile.EventFor("heading"),
                    EventValue = heading,
                },
                ct).ConfigureAwait(false);

            if (targets.Mode is AutopilotMode.HeadingHold or AutopilotMode.ApproachHold)
            {
                await SendOnceAsync(
                    "headingMode",
                    () => new AutopilotCommand
                    {
                        Kind = AutopilotCommandKind.TriggerEvent,
                        EventName = profile.EventFor("headingMode"),
                    },
                    ct).ConfigureAwait(false);
            }
        }

        if (targets.TargetAltitudeFeet is { Feet: double altitude })
        {
            await SendOnceAsync(
                "altitude",
                () => new AutopilotCommand
                {
                    Kind = AutopilotCommandKind.SetAltitude,
                    AltitudeFeet = altitude,
                    EventName = profile.EventFor("altitude"),
                    EventValue = altitude,
                },
                ct).ConfigureAwait(false);

            if (targets.Mode is AutopilotMode.AltitudeHold or AutopilotMode.VerticalSpeedHold)
            {
                await SendOnceAsync(
                    "altitudeMode",
                    () => new AutopilotCommand
                    {
                        Kind = AutopilotCommandKind.TriggerEvent,
                        EventName = profile.EventFor("altitudeMode"),
                    },
                    ct).ConfigureAwait(false);
            }
        }

        if (targets.TargetVerticalSpeedFpm is { Fpm: double vs })
        {
            await SendOnceAsync(
                "verticalSpeed",
                () => new AutopilotCommand
                {
                    Kind = AutopilotCommandKind.SetVerticalSpeed,
                    VerticalSpeedFeetPerMinute = vs,
                    EventName = profile.EventFor("verticalSpeed"),
                    EventValue = vs,
                },
                ct).ConfigureAwait(false);

            if (targets.Mode == AutopilotMode.VerticalSpeedHold)
            {
                await SendOnceAsync(
                    "vsMode",
                    () => new AutopilotCommand
                    {
                        Kind = AutopilotCommandKind.TriggerEvent,
                        EventName = profile.EventFor("vsMode"),
                    },
                    ct).ConfigureAwait(false);
            }
        }

        if (targets.TargetSpeedKnots is { Knots: double speed })
        {
            await SendOnceAsync(
                "speed",
                () => new AutopilotCommand
                {
                    Kind = AutopilotCommandKind.SetSpeed,
                    SpeedKnots = speed,
                    EventName = profile.EventFor("speed"),
                    EventValue = speed,
                },
                ct).ConfigureAwait(false);

            if (targets.Mode == AutopilotMode.ApproachHold)
            {
                await SendOnceAsync(
                    "speedMode",
                    () => new AutopilotCommand
                    {
                        Kind = AutopilotCommandKind.TriggerEvent,
                        EventName = profile.EventFor("speedMode"),
                    },
                    ct).ConfigureAwait(false);
            }
        }

        if (targets.Mode == AutopilotMode.ApproachHold)
        {
            await SendOnceAsync(
                "approachMode",
                () => new AutopilotCommand
                {
                    Kind = AutopilotCommandKind.TriggerEvent,
                    EventName = profile.EventFor("approach"),
                },
                ct).ConfigureAwait(false);
        }
    }

    private async Task ApplyConfigurationAsync(
        ControlTargets targets,
        AircraftAutoflyProfile profile,
        CancellationToken ct)
    {
        if (targets.GearDown)
        {
            await SendEventAsync(
                "gear",
                profile.EventFor("gearDown") ?? "STORMATC_GEAR_DOWN",
                ct).ConfigureAwait(false);
        }
        else
        {
            await SendEventAsync(
                "gear",
                profile.EventFor("gearUp") ?? "STORMATC_GEAR_UP",
                ct).ConfigureAwait(false);
        }

        if (targets.TargetFlapIndex is int flap)
        {
            string? binding = profile.FlapEvents.TryGetValue(flap, out string? flapEvent)
                && !string.IsNullOrWhiteSpace(flapEvent)
                ? flapEvent
                : profile.FlapValueScale is double flapScale && profile.EventFor("flaps") is string flapEventName
                    ? flapEventName
                    : $"STORMATC_FLAPS_{flap}";

            double? flapValue = flapScaleOrDefault(profile, flap);
            await SendOnceAsync(
                $"flaps:{flap}",
                () => new AutopilotCommand
                {
                    Kind = AutopilotCommandKind.TriggerEvent,
                    EventName = binding,
                    EventValue = flapValue,
                },
                ct).ConfigureAwait(false);
        }

        if (targets.ParkingBrakeSet is bool brake)
        {
            string binding = brake
                ? profile.EventFor("parkingBrakeOn") ?? "STORMATC_PARKING_BRAKE_ON"
                : profile.EventFor("parkingBrakeOff") ?? "STORMATC_PARKING_BRAKE_OFF";
            await SendEventAsync($"parkingBrake:{brake}", binding, ct).ConfigureAwait(false);
        }

        if (targets.LandingLightsOn is bool landingLights)
        {
            string binding = landingLights
                ? profile.EventFor("landingLightsOn") ?? "STORMATC_LANDING_LIGHTS_ON"
                : profile.EventFor("landingLightsOff") ?? "STORMATC_LANDING_LIGHTS_OFF";
            await SendEventAsync($"landingLights:{landingLights}", binding, ct).ConfigureAwait(false);
        }

        if (targets.TaxiLightsOn is bool taxiLights)
        {
            string binding = taxiLights
                ? profile.EventFor("taxiLightsOn") ?? "STORMATC_TAXI_LIGHTS_ON"
                : profile.EventFor("taxiLightsOff") ?? "STORMATC_TAXI_LIGHTS_OFF";
            await SendEventAsync($"taxiLights:{taxiLights}", binding, ct).ConfigureAwait(false);
        }

        if (targets.BeaconOn is bool beacon)
        {
            string binding = beacon
                ? profile.EventFor("beaconOn") ?? "STORMATC_BEACON_ON"
                : profile.EventFor("beaconOff") ?? "STORMATC_BEACON_OFF";
            await SendEventAsync($"beacon:{beacon}", binding, ct).ConfigureAwait(false);
        }

        if (targets.EnginesRunning is bool engines)
        {
            // Some aircraft need a sequence (mags, then starter). Sequences win over the
            // single-event binding; a 1/0 payload covers bool-style events such as
            // STARTER1_SET while ignorable parameters on the other events are harmless.
            IReadOnlyList<string>? sequence = profile.SequenceFor(engines ? "enginesStart" : "enginesStop");
            if (sequence is not null)
            {
                foreach (string eventName in sequence)
                {
                    await SendEventAsync(
                        $"engines:{engines}:{eventName}",
                        eventName,
                        engines ? 1 : 0,
                        ct).ConfigureAwait(false);
                }
            }
            else
            {
                string binding = engines
                    ? profile.EventFor("enginesStart") ?? "STORMATC_ENGINES_START"
                    : profile.EventFor("enginesStop") ?? "STORMATC_ENGINES_STOP";
                await SendEventAsync($"engines:{engines}", binding, engines ? 1 : 0, ct).ConfigureAwait(false);
            }
        }

        if (targets.PushbackRequested)
        {
            // One-shot: the dedup key never changes, so this fires once per engagement.
            await SendEventAsync(
                "pushback",
                profile.EventFor("pushback") ?? "STORMATC_PUSHBACK",
                ct).ConfigureAwait(false);
        }

        if (targets.ThrottlePercent is double throttle)
        {
            double scaled = throttle * profile.ThrottleScale;
            await SendOnceAsync(
                $"throttle:{Math.Round(scaled)}",
                () => new AutopilotCommand
                {
                    Kind = AutopilotCommandKind.TriggerEvent,
                    EventName = profile.EventFor("throttle") ?? "STORMATC_THROTTLE",
                    EventValue = scaled,
                },
                ct).ConfigureAwait(false);
        }
    }

    private static double? flapScaleOrDefault(AircraftAutoflyProfile profile, int flap) =>
        profile.FlapEvents.TryGetValue(flap, out string? flapEvent)
            && !string.IsNullOrWhiteSpace(flapEvent)
            ? null
            : profile.FlapValueScale is double scale ? flap * scale : null;

    private async Task SendEventAsync(string key, string? eventName, CancellationToken ct) =>
        await SendEventAsync(key, eventName, null, ct).ConfigureAwait(false);

    private async Task SendEventAsync(
        string key,
        string? eventName,
        double? value,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return;
        }

        await SendOnceAsync(
            key,
            () => new AutopilotCommand
            {
                Kind = AutopilotCommandKind.TriggerEvent,
                EventName = eventName,
                EventValue = value,
            },
            ct).ConfigureAwait(false);
    }

    private async Task SendOnceAsync(
        string key,
        Func<AutopilotCommand> commandFactory,
        CancellationToken ct)
    {
        AutopilotCommand command = commandFactory();
        string signature = Signature(command);

        if (_unsupportedKeys.Contains(key))
        {
            return;
        }

        if (_lastSent.TryGetValue(key, out string? previous) && previous == signature)
        {
            return;
        }

        await SendAsync(command, key, ct).ConfigureAwait(false);
        if (_lastResult?.Status == AutopilotCommandStatus.Unsupported)
        {
            // Remember the unsupported binding so it is not retried every tick.
            _unsupportedKeys.Add(key);
        }
        else
        {
            _lastSent[key] = signature;
        }
    }

    private async Task SendAsync(AutopilotCommand command, string key, CancellationToken ct)
    {
        try
        {
            AutopilotCommandResult result = await _connector
                .ApplyAutopilotCommandAsync(command, ct)
                .ConfigureAwait(false);

            _lastResult = result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _lastResult = new AutopilotCommandResult
            {
                Status = AutopilotCommandStatus.Failed,
                Message = ex.GetBaseException().Message,
            };
            _logger.LogWarning(ex, "Autofly command {Kind} failed.", command.Kind);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _sync.Dispose();

    private static string Signature(AutopilotCommand command) =>
        $"{command.Kind}|{command.HeadingDegrees}|{command.AltitudeFeet}|" +
        $"{command.VerticalSpeedFeetPerMinute}|{command.SpeedKnots}|{command.EventName}|" +
        $"{command.EventValue}|{command.VariableName}|{command.VariableValue}";
}
