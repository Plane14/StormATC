using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StormATC.Application.Abstractions;

namespace StormATC.Application.Continuous;

/// <summary>Service for continuous flight planning (destination selection, service turnaround).</summary>
public sealed class ContinuousFlightService : IContinuousFlightService
{
    private readonly ILogger<ContinuousFlightService> _logger;

    public ContinuousFlightService(ILogger<ContinuousFlightService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task PlanNextLegAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Planning next continuous flight leg.");
        return Task.CompletedTask;
    }
}
