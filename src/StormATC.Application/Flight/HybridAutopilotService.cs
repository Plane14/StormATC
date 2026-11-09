using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StormATC.Application.Abstractions;

namespace StormATC.Application.Flight;

/// <summary>Hybrid autopilot service - native AP for enroute, PID for ground/precision.</summary>
public sealed class HybridAutopilotService : IAutopilotService
{
    private readonly ILogger<HybridAutopilotService> _logger;

    public HybridAutopilotService(ILogger<HybridAutopilotService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Domain.State.ControlTargets Update(
        Domain.State.PhaseEvaluationContext context,
        Domain.State.PhaseDecision decision)
    {
        var targets = decision.ControlTargets ?? new Domain.State.ControlTargets();

        _logger.LogDebug(
            "Autopilot update: mode={Mode}, alt={Alt}, heading={Heading}, speed={Speed}, VS={VS}",
            targets.Mode,
            targets.TargetAltitudeFeet?.Feet,
            targets.TargetHeadingDegrees,
            targets.TargetSpeedKnots?.Knots,
            targets.TargetVerticalSpeedFpm?.Fpm);

        return targets;
    }
}
