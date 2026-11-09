using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StormATC.Application.Abstractions;

namespace StormATC.Desktop.ViewModels;

/// <summary>View model for the Flight Monitor page.</summary>
public sealed partial class FlightMonitorViewModel : ObservableObject, IRefreshablePage
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

    /// <summary>Creates a flight monitor view model.</summary>
    public FlightMonitorViewModel(IFlightOrchestrator flightOrchestrator, IAutopilotService autopilot)
    {
        _flightOrchestrator = flightOrchestrator ?? throw new ArgumentNullException(nameof(flightOrchestrator));
        _autopilot = autopilot ?? throw new ArgumentNullException(nameof(autopilot));
    }

    /// <inheritdoc />
    [RelayCommand]
    public Task RefreshAsync()
    {
        StatusMessage = "Flight monitor active.";
        return Task.CompletedTask;
    }
}
