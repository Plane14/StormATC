using CommunityToolkit.Mvvm.ComponentModel;
using StormATC.Application.Abstractions;

namespace StormATC.Desktop.ViewModels;

/// <summary>View model for the Flight Monitor page.</summary>
public sealed partial class FlightMonitorViewModel : ObservableObject
{
    private readonly IFlightOrchestrator _flightOrchestrator;
    private readonly IAutopilotService _autopilot;

    [ObservableProperty]
    private string _currentPhase = "Unknown";

    [ObservableProperty]
    private string _autopilotMode = "Disengaged";

    [ObservableProperty]
    private double? _targetAltitudeFeet;

    [ObservableProperty]
    private double? _targetHeadingDegrees;

    [ObservableProperty]
    private double? _targetSpeedKnots;

    [ObservableProperty]
    private double? _targetVerticalSpeedFpm;

    [ObservableProperty]
    private string _statusMessage = "Flight monitor ready.";

    public FlightMonitorViewModel(IFlightOrchestrator flightOrchestrator, IAutopilotService autopilot)
    {
        _flightOrchestrator = flightOrchestrator;
        _autopilot = autopilot;
    }

    public async Task RefreshAsync()
    {
        StatusMessage = "Flight monitor active.";
    }
}
