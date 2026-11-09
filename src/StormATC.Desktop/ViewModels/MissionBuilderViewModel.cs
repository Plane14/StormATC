using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using StormATC.Application.Abstractions;
using StormATC.Application.Options;

namespace StormATC.Desktop.ViewModels;

/// <summary>View model for the Mission Builder page.</summary>
public sealed partial class MissionBuilderViewModel : ObservableObject
{
    private readonly IContinuousFlightService _continuousFlightService;
    private readonly ContinuousFlightOptions _options;

    [ObservableProperty]
    private double _minRangeNm = 50;

    [ObservableProperty]
    private double _maxRangeNm = 500;

    [ObservableProperty]
    private int _serviceTimeMinutes = 30;

    [ObservableProperty]
    private double _reserveFactor = 0.9;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _lastPlannedRoute = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Mission builder ready.";

    /// <summary>Creates a mission builder view model.</summary>
    public MissionBuilderViewModel(
        IContinuousFlightService continuousFlightService,
        IOptions<ContinuousFlightOptions> options)
    {
        _continuousFlightService = continuousFlightService ?? throw new ArgumentNullException(nameof(continuousFlightService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        MinRangeNm = _options.MinRangeNm;
        MaxRangeNm = _options.MaxRangeNm;
        ServiceTimeMinutes = _options.ServiceTimeMinutes;
        ReserveFactor = _options.ReserveFactor;
    }

    [RelayCommand]
    private async Task StartMissionAsync()
    {
        if (_continuousFlightService.IsRunning)
        {
            StatusMessage = "A mission is already running.";
            return;
        }

        if (!TryValidate(out string? error))
        {
            StatusMessage = error ?? "Invalid mission parameters.";
            return;
        }

        var request = new ContinuousFlightRequest
        {
            MinRangeNm = MinRangeNm,
            MaxRangeNm = MaxRangeNm,
            ServiceTimeMinutes = ServiceTimeMinutes,
            ReserveFactor = ReserveFactor,
        };

        try
        {
            await _continuousFlightService.StartMissionAsync(request, CancellationToken.None);
            IsRunning = _continuousFlightService.IsRunning;
            LastPlannedRoute = _continuousFlightService.LastPlannedRoute ?? string.Empty;
            StatusMessage = _continuousFlightService.LastPlannedRoute is { Length: > 0 } route
                ? $"Mission started. {route}"
                : "Mission started; no destination was available in range.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start mission: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopMissionAsync()
    {
        try
        {
            await _continuousFlightService.StopMissionAsync(CancellationToken.None);
            IsRunning = false;
            StatusMessage = "Mission stopped.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to stop mission: {ex.Message}";
        }
    }

    private bool TryValidate(out string? error)
    {
        if (!double.IsFinite(MinRangeNm) || MinRangeNm <= 0)
        {
            error = "Min range must be a positive number.";
            return false;
        }

        if (!double.IsFinite(MaxRangeNm) || MaxRangeNm < MinRangeNm)
        {
            error = "Max range must be at least the min range.";
            return false;
        }

        if (MaxRangeNm > 5000)
        {
            error = "Max range cannot exceed 5000 NM.";
            return false;
        }

        if (ServiceTimeMinutes is < 1 or > 480)
        {
            error = "Service time must be between 1 and 480 minutes.";
            return false;
        }

        if (!double.IsFinite(ReserveFactor) || ReserveFactor is < 0 or > 1)
        {
            error = "Reserve factor must be between 0 and 1.";
            return false;
        }

        error = null;
        return true;
    }
}
