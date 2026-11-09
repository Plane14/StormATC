using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StormATC.Application.Abstractions;

namespace StormATC.Desktop.ViewModels;

/// <summary>View model for the Mission Builder page.</summary>
public sealed partial class MissionBuilderViewModel : ObservableObject
{
    private readonly IContinuousFlightService _continuousFlightService;

    [ObservableProperty]
    private double _minRangeNm = 50;

    [ObservableProperty]
    private double _maxRangeNm = 500;

    [ObservableProperty]
    private int _serviceTimeMinutes = 30;

    [ObservableProperty]
    private double _reserveFactor = 0.9;

    [ObservableProperty]
    private string _statusMessage = "Mission builder ready.";

    /// <summary>Creates a mission builder view model.</summary>
    public MissionBuilderViewModel(IContinuousFlightService continuousFlightService)
    {
        _continuousFlightService = continuousFlightService ?? throw new ArgumentNullException(nameof(continuousFlightService));
    }

    [RelayCommand]
    private async Task StartMissionAsync()
    {
        StatusMessage = "Mission started.";
        await _continuousFlightService.PlanNextLegAsync(CancellationToken.None);
    }

    [RelayCommand]
    private void StopMission()
    {
        StatusMessage = "Mission stopped.";
    }
}
