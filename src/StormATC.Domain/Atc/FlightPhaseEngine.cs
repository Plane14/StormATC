using StormATC.Domain.State;

namespace StormATC.Domain.Atc;

/// <summary>Deterministic flight phase state machine (separate from <see cref="IAtcStateEngine"/>).</summary>
public interface IFlightPhaseEngine
{
    /// <summary>Evaluates a phase context and returns a phase decision.</summary>
    State.PhaseDecision Evaluate(State.PhaseEvaluationContext context);

    /// <summary>Evaluates state changes without pilot input (autonomous transitions).</summary>
    State.PhaseDecision EvaluateStateChange(State.PhaseEvaluationContext context);
}

/// <summary>Default implementation of the flight phase engine (deterministic state machine).</summary>
/// <remarks>
/// This engine manages physical flight phases for autopilot control and terrain awareness.
/// It is separate from <see cref="StormAtcStateEngine"/> which handles ATC jurisdiction and
/// clearance decisions. The two engines operate on different concerns and can run concurrently.
/// </remarks>
public sealed class FlightPhaseEngine : IFlightPhaseEngine
{
    public PhaseDecision Evaluate(PhaseEvaluationContext context)
    {
        // Guard 1: Emergency/abnormal handling
        if (context.CurrentPhase == State.FlightPhase.Abnormal)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Abnormal);
        }

        // Guard 2: Airborne detection from ground phases
        if (!context.AircraftState.IsOnGround &&
            (context.CurrentPhase == State.FlightPhase.Preflight ||
             context.CurrentPhase == State.FlightPhase.Startup ||
             context.CurrentPhase == State.FlightPhase.TaxiOut))
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.InitialClimb);
        }

        // Dispatch to phase handler
        return context.CurrentPhase switch
        {
            State.FlightPhase.Preflight => HandlePreflight(context),
            State.FlightPhase.Startup => HandleStartup(context),
            State.FlightPhase.TaxiOut => HandleTaxiOut(context),
            State.FlightPhase.HoldingShort => HandleHoldingShort(context),
            State.FlightPhase.LineUp => HandleLineUp(context),
            State.FlightPhase.Takeoff => HandleTakeoff(context),
            State.FlightPhase.InitialClimb => HandleInitialClimb(context),
            State.FlightPhase.Climb => HandleClimb(context),
            State.FlightPhase.Cruise => HandleCruise(context),
            State.FlightPhase.Descent => HandleDescent(context),
            State.FlightPhase.Approach => HandleApproach(context),
            State.FlightPhase.FinalApproach => HandleFinalApproach(context),
            State.FlightPhase.GoAround => HandleGoAround(context),
            State.FlightPhase.Rollout => HandleRollout(context),
            State.FlightPhase.TaxiIn => HandleTaxiIn(context),
            State.FlightPhase.Shutdown => HandleShutdown(context),
            _ => PhaseDecision.Silent(),
        };
    }

    public PhaseDecision EvaluateStateChange(PhaseEvaluationContext context)
    {
        // Physical state triggers (no pilot input required)

        // Becoming airborne -> InitialClimb
        if (!context.AircraftState.IsOnGround &&
            (context.CurrentPhase == State.FlightPhase.Takeoff ||
             context.CurrentPhase == State.FlightPhase.LineUp ||
             context.CurrentPhase == State.FlightPhase.HoldingShort))
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.InitialClimb);
        }

        // Stable climb through 1000 ft AGL -> Climb
        if (context.CurrentPhase == State.FlightPhase.InitialClimb &&
            context.AircraftState.AltitudeAboveGroundFeet > 1000)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Climb);
        }

        // Climb -> Cruise (at or near target altitude, considering terrain)
        if (context.CurrentPhase == State.FlightPhase.Climb &&
            context.AircraftState.IndicatedAltitudeFeet >=
            (context.TerrainProfile?.MaxRequiredAltitudeFt ?? 0) - 200)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Cruise);
        }

        // Cruise -> Descent (when terrain profile indicates top of descent)
        if (context.CurrentPhase == State.FlightPhase.Cruise && context.TerrainProfile != null)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Descent);
        }

        // Touchdown -> Rollout
        if (context.AircraftState.IsOnGround &&
            (context.CurrentPhase == State.FlightPhase.FinalApproach ||
             context.CurrentPhase == State.FlightPhase.Approach))
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Rollout);
        }

        // Rollout -> TaxiIn (below 5 knots)
        if (context.CurrentPhase == State.FlightPhase.Rollout &&
            context.AircraftState.GroundSpeedKnots < 5)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.TaxiIn);
        }

        // TaxiIn -> Shutdown (at stand with parking brake set)
        if (context.CurrentPhase == State.FlightPhase.TaxiIn &&
            context.AircraftState.IsOnGround &&
            context.AircraftState.ParkingBrakeSet &&
            context.AircraftState.GroundSpeedKnots < 1)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Shutdown);
        }

        return PhaseDecision.Silent();
    }

    private static PhaseDecision HandlePreflight(PhaseEvaluationContext context) =>
        PhaseDecision.TransitionTo(State.FlightPhase.Startup);

    private static PhaseDecision HandleStartup(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleTaxiOut(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleHoldingShort(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleLineUp(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleTakeoff(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleInitialClimb(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleClimb(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleCruise(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleDescent(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleApproach(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleFinalApproach(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleGoAround(PhaseEvaluationContext context) =>
        PhaseDecision.TransitionTo(State.FlightPhase.InitialClimb);

    private static PhaseDecision HandleRollout(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleTaxiIn(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleShutdown(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();
}
