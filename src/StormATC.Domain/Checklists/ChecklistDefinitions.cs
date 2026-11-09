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
    /// <summary>Challenge/response step.</summary>
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
    /// <summary>SimConnect variable name.</summary>
    public string SimVarName { get; init; } = string.Empty;

    /// <summary>Expected value.</summary>
    public object ExpectedValue { get; init; } = string.Empty;

    /// <summary>Tolerance for comparison.</summary>
    public double Tolerance { get; init; }
}

/// <summary>A binding to a SimConnect event for executing a step.</summary>
public sealed record EventBinding
{
    /// <summary>SimConnect event name.</summary>
    public string EventName { get; init; } = string.Empty;

    /// <summary>Event value.</summary>
    public int EventValue { get; init; }
}

/// <summary>A single step within a checklist definition.</summary>
public sealed record ChecklistStep
{
    /// <summary>Step identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Step title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Step description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Step type.</summary>
    public ChecklistStepType Type { get; init; } = ChecklistStepType.Action;

    /// <summary>Expected response for challenge/response steps.</summary>
    public string? ExpectedResponse { get; init; }

    /// <summary>SimVar binding for auto-checking.</summary>
    public SimVarBinding? SimVarBinding { get; init; }

    /// <summary>Event binding for auto-execution.</summary>
    public EventBinding? EventBinding { get; init; }

    /// <summary>True when this step is optional.</summary>
    public bool IsOptional { get; init; }
}

/// <summary>A complete checklist definition.</summary>
public sealed record ChecklistDefinition
{
    /// <summary>Checklist identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Checklist name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Aircraft type filter (e.g., "C172", "A320").</summary>
    public string AircraftType { get; init; } = string.Empty;

    /// <summary>Checklist category.</summary>
    public ChecklistCategory Category { get; init; } = ChecklistCategory.Preflight;

    /// <summary>Ordered steps.</summary>
    public IReadOnlyList<ChecklistStep> Steps { get; init; } = new List<ChecklistStep>();

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }
}
