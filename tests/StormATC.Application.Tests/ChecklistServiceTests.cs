using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using StormATC.Application.Abstractions;
using StormATC.Application.Checklists;
using StormATC.Domain.Checklists;
using Xunit;

namespace StormATC.Application.Tests;

/// <summary>Tests for checklist execution state handling.</summary>
public sealed class ChecklistServiceTests
{
    private readonly ChecklistService _service = new(
        new EmptyChecklistRepository(),
        NullLogger<ChecklistService>.Instance);

    [Fact]
    public void StartChecklistRejectsEmptyDefinition()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.StartChecklist(new ChecklistDefinition { Id = "x", Name = "x", Steps = [] }));
    }

    [Fact]
    public async Task AdvanceStepRequiresCompletingRequiredSteps()
    {
        string executionId = _service.StartChecklist(Definition("cl", Steps(
            Step("1", required: true),
            Step("2", required: true))));

        // Step 1 is required and incomplete — advancing must be refused.
        Assert.False(_service.AdvanceStep(executionId));

        // Completing step 1 allows the advance.
        Assert.True(await _service.ExecuteStepAsync(executionId, "1"));
        Assert.True(_service.AdvanceStep(executionId));
        Assert.Equal("2", _service.GetCurrentStep(executionId)?.Id);
    }

    [Fact]
    public async Task SkippedOptionalStepsCountTowardProgress()
    {
        string executionId = _service.StartChecklist(Definition("cl", Steps(
            Step("1", required: true),
            Step("2", required: false),
            Step("3", required: true))));

        Assert.True(await _service.ExecuteStepAsync(executionId, "1"));
        Assert.True(_service.AdvanceStep(executionId));   // -> optional step 2
        Assert.True(_service.AdvanceStep(executionId));   // skip optional step 2 -> step 3
        Assert.True(await _service.ExecuteStepAsync(executionId, "3"));
        Assert.True(_service.AdvanceStep(executionId));

        ChecklistExecutionState? state = _service.GetExecutionState(executionId);
        Assert.NotNull(state);
        Assert.True(state!.IsCompleted);
        Assert.Equal(100.0, state.ProgressPercent);
    }

    [Fact]
    public async Task AdvanceStepOnCompletedExecutionIsSafe()
    {
        string executionId = _service.StartChecklist(Definition("cl", Steps(Step("1"))));
        Assert.True(await _service.ExecuteStepAsync(executionId, "1"));
        Assert.True(_service.AdvanceStep(executionId));

        // Completed execution: no throw, no further advance.
        Assert.False(_service.AdvanceStep(executionId));
        Assert.Null(_service.GetCurrentStep(executionId));
    }

    [Fact]
    public async Task ExecuteStepIsScopedToItsExecution()
    {
        string first = _service.StartChecklist(Definition("a", Steps(Step("1"))));
        string second = _service.StartChecklist(Definition("b", Steps(Step("1"))));

        Assert.True(await _service.ExecuteStepAsync(first, "1"));
        // The same step id in the second execution is untouched.
        Assert.Empty(_service.GetExecutionState(second)!.CompletedSteps);
        Assert.Single(_service.GetExecutionState(first)!.CompletedSteps);
    }

    [Fact]
    public async Task UnknownExecutionIdIsHandled()
    {
        Assert.False(_service.AdvanceStep("missing"));
        Assert.Null(_service.GetCurrentStep("missing"));
        Assert.Null(_service.GetExecutionState("missing"));
        Assert.False(await _service.ExecuteStepAsync("missing", "1"));
    }

    private static ChecklistDefinition Definition(string id, ChecklistStep[] steps) => new()
    {
        Id = id,
        Name = id,
        Steps = steps,
    };

    private static ChecklistStep[] Steps(params ChecklistStep[] steps) => steps;

    private static ChecklistStep Step(string id, bool required = true) => new()
    {
        Id = id,
        Title = $"Step {id}",
        IsOptional = !required,
    };

    private sealed class EmptyChecklistRepository : IChecklistRepository
    {
        public Task<IReadOnlyList<ChecklistDefinition>> LoadAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ChecklistDefinition>>([]);

        public Task<ChecklistDefinition?> LoadByIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult<ChecklistDefinition?>(null);
    }
}
