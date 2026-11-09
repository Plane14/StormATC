using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Checklists;
using StormATC.Application.Options;
using StormATC.Domain.Aircraft;
using StormATC.Domain.Checklists;
using StormATC.Domain.State;
using StormATC.Domain.ValueObjects;
using FlightPhase = StormATC.Domain.State.FlightPhase;

namespace StormATC.Application.Flight;

/// <summary>
/// Runs the aircraft checklist for the current flight phase alongside autofly.
/// </summary>
/// <remarks>
/// <para>
/// When autofly is enabled this service starts the checklist matching the aircraft and the
/// current phase category, fires any SimConnect event bindings, and ticks each step in order.
/// Steps it cannot verify against the aircraft state are still completed — that is what
/// "automation" means here — but every step is logged so the transcript shows exactly what
/// the automation did.
/// </para>
/// <para>
/// The service never blocks a phase transition and never invents checklist content: it only
/// uses definitions shipped in <c>data/flightchecklists</c>.
/// </para>
/// </remarks>
public sealed class FlightChecklistRunner : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly IFlightOrchestrator _orchestrator;
    private readonly IChecklistService _checklistService;
    private readonly IChecklistRepository _repository;
    private readonly ISimulatorConnector _connector;
    private readonly AutoflyOptions _autoflyOptions;
    private readonly ILogger<FlightChecklistRunner> _logger;

    private readonly object _sync = new();
    private readonly Dictionary<ChecklistCategory, string> _executions = new();
    private IReadOnlyList<ChecklistDefinition>? _definitions;

    /// <summary>Creates the runner.</summary>
    public FlightChecklistRunner(
        IFlightOrchestrator orchestrator,
        IChecklistService checklistService,
        IChecklistRepository repository,
        ISimulatorConnector connector,
        IOptions<AutoflyOptions> autoflyOptions,
        ILogger<FlightChecklistRunner> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _checklistService = checklistService ?? throw new ArgumentNullException(nameof(checklistService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        _autoflyOptions = autoflyOptions?.Value ?? throw new ArgumentNullException(nameof(autoflyOptions));
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
        if (!_autoflyOptions.Enabled)
        {
            return;
        }

        AircraftState? aircraft = _orchestrator.LastAircraftState;
        FlightPhase phase = _orchestrator.CurrentPhase;
        if (aircraft is null)
        {
            return;
        }

        ChecklistCategory? category = CategoryFor(phase);
        if (category is null)
        {
            return;
        }

        string? executionId = EnsureExecution(aircraft, category.Value);
        if (executionId is null)
        {
            return;
        }

        ChecklistExecutionState? state = _checklistService.GetExecutionState(executionId);
        ChecklistStep? step = state?.CurrentStep;
        if (step is null)
        {
            return;
        }

        if (step.EventBinding is EventBinding binding
            && !string.IsNullOrWhiteSpace(binding.EventName))
        {
            try
            {
                await _connector.ApplyAutopilotCommandAsync(
                    new AutopilotCommand
                    {
                        Kind = AutopilotCommandKind.TriggerEvent,
                        EventName = binding.EventName,
                        EventValue = binding.EventValue,
                    },
                    ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Checklist event {Event} failed.", binding.EventName);
            }
        }

        // Verification steps that carry a SimVar binding are checked against the live
        // aircraft state when the binding names something StormATC can read.
        if (step.SimVarBinding is SimVarBinding simVar
            && !SimVarMatches(simVar, aircraft))
        {
            return; // Leave the step open for the pilot; automation does not fake a check.
        }

        if (await _checklistService.ExecuteStepAsync(executionId, step.Id, ct).ConfigureAwait(false)
            && _checklistService.AdvanceStep(executionId))
        {
            _logger.LogInformation(
                "Autofly checklist {Checklist} completed step {Step} ({Title})",
                state!.ChecklistName, step.Id, step.Title);
        }
    }

    private string? EnsureExecution(AircraftState aircraft, ChecklistCategory category)
    {
        lock (_sync)
        {
            if (_executions.TryGetValue(category, out string? existing)
                && _checklistService.GetExecutionState(existing) is { IsCompleted: false })
            {
                return existing;
            }

            ChecklistDefinition? definition = FindDefinition(aircraft, category);
            if (definition is null)
            {
                return null;
            }

            string executionId = _checklistService.StartChecklist(definition);
            _executions[category] = executionId;
            _logger.LogInformation(
                "Autofly started checklist '{Checklist}' ({Category}) for {Aircraft}.",
                definition.Name, category, aircraft.AircraftTitle);
            return executionId;
        }
    }

    private ChecklistDefinition? FindDefinition(AircraftState aircraft, ChecklistCategory category)
    {
        IReadOnlyList<ChecklistDefinition> definitions =
            _definitions ??= _repository.LoadAllAsync(CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();

        string title = aircraft.AircraftTitle ?? string.Empty;

        ChecklistDefinition? aircraftMatch = definitions.FirstOrDefault(d =>
            d.Category == category
            && d.AircraftType.Length > 0
            && (title.Contains(d.AircraftType, StringComparison.OrdinalIgnoreCase)
                || d.AircraftType.Contains(title, StringComparison.OrdinalIgnoreCase)));

        return aircraftMatch
            ?? definitions.FirstOrDefault(d =>
                d.Category == category && d.AircraftType.Length == 0);
    }

    /// <summary>Maps a flight phase to the checklist category that applies.</summary>
    public static ChecklistCategory? CategoryFor(FlightPhase phase) => phase switch
    {
        FlightPhase.Preflight => ChecklistCategory.Preflight,
        FlightPhase.Startup => ChecklistCategory.Startup,
        FlightPhase.TaxiOut or FlightPhase.HoldingShort => ChecklistCategory.BeforeTaxi,
        FlightPhase.LineUp or FlightPhase.Takeoff => ChecklistCategory.Takeoff,
        FlightPhase.InitialClimb or FlightPhase.Climb => ChecklistCategory.Climb,
        FlightPhase.Cruise => ChecklistCategory.Cruise,
        FlightPhase.Descent => ChecklistCategory.Descent,
        FlightPhase.Approach => ChecklistCategory.Approach,
        FlightPhase.FinalApproach or FlightPhase.Rollout => ChecklistCategory.Landing,
        FlightPhase.TaxiIn or FlightPhase.Shutdown => ChecklistCategory.Shutdown,
        _ => null,
    };

    /// <summary>
    /// Evaluates the small set of SimVar bindings StormATC can actually read from
    /// <see cref="AircraftState"/>. Returns false only when a readable binding was evaluated
    /// and did not match — the step then stays open for the pilot. Steps with no readable
    /// binding are pilot-verification items automation cannot fake, so they are completed
    /// like any other automated step and logged.
    /// </summary>
    private static bool SimVarMatches(SimVarBinding binding, AircraftState aircraft)
    {
        string name = binding.SimVarName ?? string.Empty;
        object? actual = name.ToUpperInvariant() switch
        {
            var n when n.Contains("PARKING_BRAKE") || n.Contains("BRAKE_PARKING") =>
                (object)aircraft.ParkingBrakeSet,
            var n when n.Contains("ENG_MASTER") || n.Contains("ENGINE") =>
                (object)aircraft.EnginesRunning,
            var n when n.Contains("GEAR") && n.Contains("DOWN") =>
                (object)aircraft.GearHandleDown,
            var n when n.Contains("FLAP") =>
                (object)aircraft.FlapsHandleIndex,
            var n when n.Contains("ON_GROUND") || n.Contains("WEIGHT_ON_WHEELS") =>
                (object)aircraft.IsOnGround,
            var n when n.Contains("INDICATED_ALTITUDE") =>
                (object)aircraft.IndicatedAltitudeFeet,
            var n when n.Contains("AIRSPEED") || n.Contains("IAS") =>
                (object)aircraft.IndicatedAirspeedKnots,
            var n when n.Contains("GROUND_SPEED") || n.Contains("GS") =>
                (object)aircraft.GroundSpeedKnots,
            _ => null,
        };

        if (actual is null)
        {
            // No readable binding: treat as a pilot-verification step and let automation
            // complete it rather than blocking the checklist forever.
            return true;
        }

        return ValuesMatch(actual, binding.ExpectedValue, binding.Tolerance);
    }

    private static bool ValuesMatch(object actual, object expected, double tolerance)
    {
        if (expected is string text
            && bool.TryParse(text, out bool expectedBool)
            && actual is bool actualBool)
        {
            return actualBool == expectedBool;
        }

        if (expected is string numberText
            && double.TryParse(numberText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double expectedNumber)
            && actual is double or float or int or long)
        {
            double actualNumber = Convert.ToDouble(actual, System.Globalization.CultureInfo.InvariantCulture);
            return Math.Abs(actualNumber - expectedNumber) <= Math.Max(tolerance, 0.0001);
        }

        return string.Equals(actual.ToString(), expected?.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
