namespace StormATC.Domain.Checklists;

/// <summary>Category of a checklist.</summary>
public enum ChecklistCategory
{
    Preflight,
    Startup,
    BeforeTaxi,
    Takeoff,
    Climb,
    Cruise,
    Descent,
    Approach,
    Landing,
    Shutdown,
    Emergency,
}

/// <summary>Type of a checklist step.</summary>
public enum ChecklistStepType
{
    /// <summary>Challenge/response step - pilot reads challenge, controller or pilot responds.</summary>
    ChallengeResponse,
    /// <summary>Action step - pilot performs an action and ticks it off.</summary>
    Action,
    /// <summary>Verification step - pilot verifies a condition is met.</summary>
    Verification,
    /// <summary>Configuration step - pilot sets a SimVar or triggers a SimConnect event.</summary>
    Configuration,
}

/// <summary>A binding to a SimConnect variable for auto-checking a step.</summary>
public sealed record SimVarBinding
{
    public string SimVarName { get; init; } = string.Empty;
    public object ExpectedValue { get; init; } = string.Empty;
    public double Tolerance { get; init; } = 0.0;
}

/// <summary>A binding to a SimConnect event for executing a step.</summary>
public sealed record EventBinding
{
    public string EventName { get; init; } = string.Empty;
    public int EventValue { get; init; }
}

/// <summary>A single step within a checklist definition.</summary>
public sealed record ChecklistStep
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ChecklistStepType Type { get; init; } = ChecklistStepType.Action;
    public string? ExpectedResponse { get; init; }
    public SimVarBinding? SimVarBinding { get; init; }
    public EventBinding? EventBinding { get; init; }
    public bool IsOptional { get; init; }
}

/// <summary>A complete checklist definition.</summary>
public sealed record ChecklistDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public ChecklistCategory Category { get; init; } = ChecklistCategory.Preflight;
    public IReadOnlyList<ChecklistStep> Steps { get; init; } = new List<ChecklistStep>();
    public string? Description { get; init; }
}
