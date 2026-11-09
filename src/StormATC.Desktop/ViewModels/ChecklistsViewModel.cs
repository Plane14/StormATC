using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StormATC.Application.Abstractions;
using StormATC.Domain.Checklists;

namespace StormATC.Desktop.ViewModels;

/// <summary>View model for the Checklists page.</summary>
public sealed partial class ChecklistsViewModel : ObservableObject, IRefreshablePage
{
    private readonly IChecklistService _checklistService;
    private readonly IChecklistRepository _repository;

    [ObservableProperty]
    private ObservableCollection<ChecklistDefinition> _checklists = [];

    [ObservableProperty]
    private ChecklistDefinition? _selectedChecklist;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isExecuting;

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
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load checklists: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExecuteSelectedChecklistAsync()
    {
        if (SelectedChecklist is null)
        {
            StatusMessage = "Select a checklist first.";
            return;
        }

        IsExecuting = true;
        try
        {
            string executionId = _checklistService.StartChecklist(SelectedChecklist);
            StatusMessage = $"Executing '{SelectedChecklist.Name}' — step 1 of {SelectedChecklist.Steps.Count}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start checklist: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAdvanceStep))]
    private void AdvanceStep()
    {
        StatusMessage = "Step advanced.";
    }

    private bool CanAdvanceStep() => IsExecuting;
}
