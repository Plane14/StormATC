using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StormATC.Application.Abstractions;
using StormATC.Domain.Checklists;

namespace StormATC.Application.Checklists;

/// <summary>Service for checklist execution.</summary>
public sealed class ChecklistService : IChecklistService
{
    private readonly IChecklistRepository _repository;
    private readonly ILogger<ChecklistService> _logger;
    private readonly Dictionary<string, ChecklistExecutionState> _executions = new(StringComparer.Ordinal);

    /// <summary>Creates a checklist service.</summary>
    public ChecklistService(IChecklistRepository repository, ILogger<ChecklistService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Executes a checklist step.</summary>
    public Task ExecuteStepAsync(string stepId, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing checklist step: {StepId}", stepId);

        foreach (var execution in _executions.Values)
        {
            if (execution.CurrentStep?.Id == stepId && !execution.CompletedSteps.Contains(stepId))
            {
                execution.CompletedSteps.Add(stepId);
                _logger.LogInformation("Step {StepId} marked complete in checklist {ChecklistId}", stepId, execution.ChecklistId);
                break;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Starts executing a checklist definition.</summary>
    public string StartChecklist(ChecklistDefinition definition)
    {
        var executionId = Guid.NewGuid().ToString("N");
        var state = new ChecklistExecutionState
        {
            ExecutionId = executionId,
            ChecklistId = definition.Id,
            ChecklistName = definition.Name,
            Steps = definition.Steps,
            CurrentStepIndex = 0,
            CompletedSteps = new HashSet<string>(StringComparer.Ordinal),
            StartTime = DateTimeOffset.UtcNow,
        };

        _executions[executionId] = state;
        _logger.LogInformation("Started checklist {ChecklistId} (execution {ExecutionId})", definition.Id, executionId);

        return executionId;
    }

    /// <summary>Gets the current step of an execution.</summary>
    public ChecklistStep? GetCurrentStep(string executionId)
    {
        if (_executions.TryGetValue(executionId, out var state))
        {
            if (state.CurrentStepIndex >= 0 && state.CurrentStepIndex < state.Steps.Count)
            {
                return state.Steps[state.CurrentStepIndex];
            }
        }
        return null;
    }

    /// <summary>Advances to the next step.</summary>
    public bool AdvanceStep(string executionId)
    {
        if (!_executions.TryGetValue(executionId, out var state))
        {
            return false;
        }

        var current = state.Steps[state.CurrentStepIndex];
        if (!current.IsOptional && !state.CompletedSteps.Contains(current.Id))
        {
            return false; // Cannot skip a required incomplete step
        }

        state.CurrentStepIndex++;
        if (state.CurrentStepIndex >= state.Steps.Count)
        {
            state.CompletedAt = DateTimeOffset.UtcNow;
        }

        return true;
    }

    /// <summary>Gets all active executions.</summary>
    public IReadOnlyList<ChecklistExecutionState> GetActiveExecutions() =>
        _executions.Values.ToList().AsReadOnly();
}

/// <summary>Tracks the execution state of a checklist.</summary>
public sealed record ChecklistExecutionState
{
    /// <summary>Execution identifier.</summary>
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>Checklist identifier.</summary>
    public string ChecklistId { get; init; } = string.Empty;

    /// <summary>Checklist name.</summary>
    public string ChecklistName { get; init; } = string.Empty;

    /// <summary>Checklist steps.</summary>
    public IReadOnlyList<ChecklistStep> Steps { get; init; } = new List<ChecklistStep>();

    /// <summary>Current step index.</summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>Completed step IDs.</summary>
    public HashSet<string> CompletedSteps { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Start time.</summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Completion time, null when still running.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Current step, null when completed.</summary>
    public ChecklistStep? CurrentStep =>
        CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

    /// <summary>True when all steps are completed.</summary>
    public bool IsCompleted => CompletedAt is not null;

    /// <summary>Progress percentage (0-100).</summary>
    public double ProgressPercent =>
        Steps.Count == 0 ? 100.0 : (double)CompletedSteps.Count / Steps.Count * 100.0;
}
