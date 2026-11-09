using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;

namespace StormATC.Desktop.ViewModels;

/// <summary>View model for the Flight Monitor page.</summary>
public sealed partial class FlightMonitorViewModel : ObservableObject, IRefreshablePage
{
    private readonly IFlightOrchestrator _flightOrchestrator;
    private readonly IAutopilotService _autopilot;
    private readonly IAutopilotActuator _actuator;

    [ObservableProperty]
    private string _currentPhase = "Unknown";

    [ObservableProperty]
    private string _autopilotMode = "Disengaged";

    [ObservableProperty]
    private string _aircraftHandling = "Tricycle gear";

    [ObservableProperty]
    private double? _targetAltitudeFeet;

    [ObservableProperty]
    private double? _targetHeadingDegrees;

    [ObservableProperty]
    private double? _targetSpeedKnots;

    [ObservableProperty]
    private double? _targetVerticalSpeedFpm;

    [ObservableProperty]
    private double? _requiredAltitudeAheadFt;

    [ObservableProperty]
    private double? _minimumSafeAltitudeFt;

    [ObservableProperty]
    private string _statusMessage = "Flight monitor ready.";

    [ObservableProperty]
    private bool _autoflyEnabled;

    [ObservableProperty]
    private string _autoflyStatus = "Autofly disabled.";

    /// <summary>Creates a flight monitor view model.</summary>
    public FlightMonitorViewModel(
        IFlightOrchestrator flightOrchestrator,
        IAutopilotService autopilot,
        IAutopilotActuator actuator,
        IOptions<AutoflyOptions> autoflyOptions)
    {
        _flightOrchestrator = flightOrchestrator ?? throw new ArgumentNullException(nameof(flightOrchestrator));
        _autopilot = autopilot ?? throw new ArgumentNullException(nameof(autopilot));
        _actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        AutoflyEnabled = autoflyOptions?.Value.Enabled ?? false;
    }

    /// <inheritdoc />
    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            // The hosted orchestrator ticks on its own; one explicit tick keeps the monitor
            // responsive even when the page is opened before the first interval elapses.
            await _flightOrchestrator.TickAsync(CancellationToken.None);
            UpdateFromServices();
            StatusMessage = _flightOrchestrator.LastAircraftState is null
                ? "Waiting for simulator data — phase engine has no aircraft state yet."
                : "Flight monitor active.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
    }

    /// <summary>Enables or disables user-aircraft autofly.</summary>
    [RelayCommand]
    public async Task ToggleAutoflyAsync()
    {
        try
        {
            await _actuator.SetEnabledAsync(AutoflyEnabled, CancellationToken.None);
            AutoflyStatus = _actuator.StatusMessage;
        }
        catch (Exception ex)
        {
            AutoflyStatus = $"Autofly toggle failed: {ex.Message}";
        }
    }

    private void UpdateFromServices()
    {
        CurrentPhase = _flightOrchestrator.CurrentPhase.ToString();
        AutoflyStatus = _actuator.StatusMessage;

        Domain.State.ControlTargets? targets = _autopilot.CurrentTargets;
        AutopilotMode = targets?.Mode.ToString() ?? "Disengaged";
        AircraftHandling = targets?.Taildragger == true ? "Taildragger" : "Tricycle gear";
        TargetAltitudeFeet = targets?.TargetAltitudeFeet?.Feet;
        TargetHeadingDegrees = targets?.TargetHeadingDegrees;
        TargetSpeedKnots = targets?.TargetSpeedKnots?.Knots;
        TargetVerticalSpeedFpm = targets?.TargetVerticalSpeedFpm?.Fpm;

        Domain.State.TerrainProfile? terrain = _flightOrchestrator.LastTerrainProfile;
        RequiredAltitudeAheadFt = terrain?.MaxRequiredAltitudeFt;
        MinimumSafeAltitudeFt = terrain?.IsFallback == true
            ? null
            : terrain?.MaxRequiredAltitudeFt;
    }
}
