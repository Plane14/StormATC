using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StormATC.Application.Abstractions;
using StormATC.Application.Checklists;
using StormATC.Domain.Checklists;

namespace StormATC.Desktop.ViewModels;

/// <summary>View model for the Checklists page.</summary>
public sealed partial class ChecklistsViewModel : ObservableObject, IRefreshablePage
{
    private readonly IChecklistService _checklistService;
    private readonly IChecklistRepository _repository;
    private string? _executionId;

    [ObservableProperty]
    private ObservableCollection<ChecklistDefinition> _checklists = [];

    [ObservableProperty]
    private ChecklistDefinition? _selectedChecklist;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AdvanceStepCommand))]
    private bool _isExecuting;

    [ObservableProperty]
    private string _currentStepText = string.Empty;

    [ObservableProperty]
    private double _progressPercent;

    /// <summary>Creates a checklists view model.</summary>
    public ChecklistsViewModel(IChecklistService checklistService, IChecklistRepository repository)
    {
        _checklistService = checklistService ?? throw new ArgumentNullException(nameof(checklistService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <inheritdoc />
    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var definitions = await _repository.LoadAllAsync(CancellationToken.None);
            Checklists = new ObservableCollection<ChecklistDefinition>(definitions);
            StatusMessage = $"{Checklists.Count} checklist(s) loaded.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load checklists: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExecuteSelectedChecklist()
    {
        if (SelectedChecklist is null)
        {
            StatusMessage = "Select a checklist first.";
            return;
        }

        try
        {
            _executionId = _checklistService.StartChecklist(SelectedChecklist);
            IsExecuting = true;
            UpdateExecutionState();
            StatusMessage = $"Executing '{SelectedChecklist.Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start checklist: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanAdvanceStep))]
    private void AdvanceStep()
    {
        if (_executionId is null)
        {
            return;
        }

        if (!_checklistService.AdvanceStep(_executionId))
        {
            ChecklistExecutionState? state = _checklistService.GetExecutionState(_executionId);
            if (state?.IsCompleted == true)
            {
                FinishExecution("Checklist complete.");
            }
            else
            {
                StatusMessage = "Complete the current required step first.";
            }

            return;
        }

        UpdateExecutionState();
        ChecklistExecutionState? updated = _checklistService.GetExecutionState(_executionId);
        if (updated?.IsCompleted == true)
        {
            FinishExecution("Checklist complete.");
        }
        else
        {
            StatusMessage = "Step advanced.";
        }
    }

    private bool CanAdvanceStep() => IsExecuting && _executionId is not null;

    private void UpdateExecutionState()
    {
        ChecklistExecutionState? state = _executionId is null
            ? null
            : _checklistService.GetExecutionState(_executionId);

        if (state is null)
        {
            CurrentStepText = string.Empty;
            ProgressPercent = 0;
            return;
        }

        ChecklistStep? step = state.CurrentStep;
        CurrentStepText = step is null
            ? "All steps complete."
            : $"{state.CurrentStepIndex + 1}/{state.Steps.Count}: {step.Title}";
        ProgressPercent = state.ProgressPercent;
    }

    private void FinishExecution(string message)
    {
        UpdateExecutionState();
        IsExecuting = false;
        _executionId = null;
        StatusMessage = message;
    }
}
