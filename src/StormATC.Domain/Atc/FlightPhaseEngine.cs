using StormATC.Domain.Airports;
using StormATC.Domain.State;
using StormATC.Domain.ValueObjects;

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
    private const double ClimbToCruiseToleranceFeet = 200;
    private const double InitialClimbAglFeet = 1000;
    private const double FallbackClimbAglFeet = 3000;
    private const double DescentBufferNm = 2;
    private const double ApproachDistanceNm = 10;
    private const double FinalApproachDistanceNm = 4;
    private const double HoldingShortAirportDistanceNm = 3;
    private const double LineUpRunwayDistanceNm = 1;
    private const double LineUpHeadingToleranceDegrees = 20;
    private const double TakeoffRollSpeedKnots = 40;
    private const double RolloutTaxiInSpeedKnots = 5;
    private const double ShutdownSpeedKnots = 1;
    private const double GoAroundVerticalSpeedFpm = 800;
    private const double GoAroundClimbAglFeet = 1000;

    public PhaseDecision Evaluate(PhaseEvaluationContext context)
    {
        // Guard 1: Emergency/abnormal handling. Once abnormal, stay abnormal until a future
        // recovery path is defined rather than re-transitioning every tick.
        if (context.CurrentPhase == State.FlightPhase.Abnormal)
        {
            return PhaseDecision.Silent();
        }

        // Dispatch to phase handler for pilot/ATC-driven transitions.
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

        // Becoming airborne from any ground phase -> InitialClimb.
        if (!context.AircraftState.IsOnGround && IsGroundPhase(context.CurrentPhase))
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.InitialClimb);
        }

        // Touchdown from any airborne phase -> Rollout.
        if (context.AircraftState.IsOnGround && IsAirbornePhase(context.CurrentPhase))
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Rollout);
        }

        // Engines started on the ground -> Startup.
        if (context.CurrentPhase == State.FlightPhase.Preflight
            && context.AircraftState.EnginesRunning
            && context.AircraftState.IsOnGround)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Startup);
        }

        // Taxiing after startup -> TaxiOut.
        if (context.CurrentPhase == State.FlightPhase.Startup
            && context.AircraftState.IsOnGround
            && context.AircraftState.EnginesRunning
            && (context.AircraftState.IsTaxiing || context.AircraftState.GroundSpeedKnots > 1))
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.TaxiOut);
        }

        // Stopped near the departure aerodrome after taxi-out -> HoldingShort.
        if (context.CurrentPhase == State.FlightPhase.TaxiOut
            && context.AircraftState.IsOnGround
            && context.AircraftState.EnginesRunning
            && context.AircraftState.GroundSpeedKnots < ShutdownSpeedKnots
            && context.DepartureAirport is not null
            && context.AircraftState.Position.DistanceNauticalMilesTo(
                context.DepartureAirport.ReferencePosition) <= HoldingShortAirportDistanceNm)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.HoldingShort);
        }

        // Aligned with the departure runway threshold while holding -> LineUp.
        if (context.CurrentPhase == State.FlightPhase.HoldingShort
            && context.AircraftState.IsOnGround
            && context.AircraftState.GroundSpeedKnots < ShutdownSpeedKnots
            && IsAlignedWithDepartureRunway(context))
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.LineUp);
        }

        // Takeoff roll -> Takeoff.
        if (context.CurrentPhase == State.FlightPhase.LineUp
            && context.AircraftState.IsOnGround
            && context.AircraftState.GroundSpeedKnots >= TakeoffRollSpeedKnots)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Takeoff);
        }

        // Stable climb through 1000 ft AGL -> Climb.
        if (context.CurrentPhase == State.FlightPhase.InitialClimb
            && context.AircraftState.AltitudeAboveGroundFeet > InitialClimbAglFeet)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Climb);
        }

        // Climb -> Cruise at the planned or terrain-required altitude.
        if (context.CurrentPhase == State.FlightPhase.Climb
            && TryGetClimbOutAltitude(context) is double climbOutAltitude
            && context.AircraftState.IndicatedAltitudeFeet >= climbOutAltitude - ClimbToCruiseToleranceFeet)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Cruise);
        }

        // Fallback: with no usable cruise target, treat a stable high climb as cruise.
        if (context.CurrentPhase == State.FlightPhase.Climb
            && TryGetClimbOutAltitude(context) is null
            && context.AircraftState.AltitudeAboveGroundFeet > FallbackClimbAglFeet)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Cruise);
        }

        // Cruise -> Descent at the computed top of descent.
        if (context.CurrentPhase == State.FlightPhase.Cruise
            && TryGetTopOfDescent(context) is double topOfDescentNm
            && topOfDescentNm > 0
            && DistanceToDestinationNm(context) is double remainingNm
            && remainingNm <= topOfDescentNm + DescentBufferNm)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Descent);
        }

        // Descent -> Approach inside the approach gate.
        if (context.CurrentPhase == State.FlightPhase.Descent
            && DistanceToDestinationNm(context) is double descentRemainingNm
            && descentRemainingNm <= ApproachDistanceNm)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Approach);
        }

        // Approach -> FinalApproach close to the runway.
        if (context.CurrentPhase == State.FlightPhase.Approach
            && DistanceToDestinationNm(context) is double approachRemainingNm
            && approachRemainingNm <= FinalApproachDistanceNm)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.FinalApproach);
        }

        // Go-around: a strong climb-out during approach returns to Climb once established.
        if (context.CurrentPhase == State.FlightPhase.FinalApproach
            && !context.AircraftState.IsOnGround
            && context.AircraftState.VerticalSpeedFeetPerMinute > GoAroundVerticalSpeedFpm)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.GoAround);
        }

        if (context.CurrentPhase == State.FlightPhase.GoAround
            && context.AircraftState.AltitudeAboveGroundFeet > GoAroundClimbAglFeet)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Climb);
        }

        // Rollout -> TaxiIn below taxi speed.
        if (context.CurrentPhase == State.FlightPhase.Rollout
            && context.AircraftState.IsOnGround
            && context.AircraftState.GroundSpeedKnots < RolloutTaxiInSpeedKnots)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.TaxiIn);
        }

        // TaxiIn -> Shutdown at the stand with parking brake set.
        if (context.CurrentPhase == State.FlightPhase.TaxiIn
            && context.AircraftState.IsOnGround
            && context.AircraftState.ParkingBrakeSet
            && context.AircraftState.GroundSpeedKnots < ShutdownSpeedKnots)
        {
            return PhaseDecision.TransitionTo(State.FlightPhase.Shutdown);
        }

        return PhaseDecision.Silent();
    }

    private static bool IsGroundPhase(State.FlightPhase phase) => phase is
        State.FlightPhase.Preflight or State.FlightPhase.Startup or State.FlightPhase.TaxiOut
        or State.FlightPhase.HoldingShort or State.FlightPhase.LineUp or State.FlightPhase.Takeoff;

    private static bool IsAirbornePhase(State.FlightPhase phase) => phase is
        State.FlightPhase.InitialClimb or State.FlightPhase.Climb or State.FlightPhase.Cruise
        or State.FlightPhase.Descent or State.FlightPhase.Approach or State.FlightPhase.FinalApproach
        or State.FlightPhase.GoAround;

    private static double? TryGetClimbOutAltitude(PhaseEvaluationContext context)
    {
        if (context.FlightPlan?.CruiseAltitude is { } cruiseAltitude)
        {
            return cruiseAltitude.Feet;
        }

        if (context.TerrainProfile is { MaxRequiredAltitudeFt: > 0 } terrain)
        {
            return terrain.MaxRequiredAltitudeFt;
        }

        return null;
    }

    private static double? DistanceToDestinationNm(PhaseEvaluationContext context)
    {
        GeoPosition? destination = context.DestinationAirport?.ReferencePosition
            ?? LastRouteLegPosition(context.FlightPlan);

        return destination is GeoPosition position
            ? context.AircraftState.Position.DistanceNauticalMilesTo(position)
            : null;
    }

    private static GeoPosition? LastRouteLegPosition(Domain.FlightPlanning.FlightPlan? plan)
    {
        if (plan?.RouteLegs is not { Count: > 0 } legs)
        {
            return null;
        }

        for (int i = legs.Count - 1; i >= 0; i--)
        {
            if (legs[i].Position is GeoPosition position)
            {
                return position;
            }
        }

        return null;
    }

    private static double? TryGetTopOfDescent(PhaseEvaluationContext context)
    {
        if (DistanceToDestinationNm(context) is not double remainingNm || remainingNm <= 0)
        {
            return null;
        }

        double destinationElevation = context.DestinationAirport?.ElevationFeet ?? 0;
        double altitudeToLose = context.AircraftState.IndicatedAltitudeFeet - destinationElevation;
        if (altitudeToLose <= 0)
        {
            return 0;
        }

        double descentRate = context.DescentRateFpm > 0 ? context.DescentRateFpm : 1500;
        double speed = context.CruiseSpeedKnots > 0
            ? context.CruiseSpeedKnots
            : Math.Max(context.AircraftState.GroundSpeedKnots, 1);

        // NM = (ft / fpm) minutes * knots / 60.
        return speed * altitudeToLose / (descentRate * 60);
    }

    private static bool IsAlignedWithDepartureRunway(PhaseEvaluationContext context)
    {
        if (context.DepartureAirport is not AirportData airport
            || context.FlightPlan?.DepartureRunway is not RunwayIdentifier runwayId)
        {
            return false;
        }

        RunwayData? runway = airport.FindRunway(runwayId);
        if (runway is null)
        {
            return false;
        }

        double distanceNm = context.AircraftState.Position
            .DistanceNauticalMilesTo(runway.ThresholdPosition);
        if (distanceNm > LineUpRunwayDistanceNm)
        {
            return false;
        }

        double runwayHeading = runway.CenterlineBearing.Degrees;
        double aircraftHeading = NormalizeDegrees(context.AircraftState.MagneticHeadingDegrees);
        double difference = Math.Abs(NormalizeDegrees(aircraftHeading - runwayHeading + 180) - 180);

        return difference <= LineUpHeadingToleranceDegrees;
    }

    private static double NormalizeDegrees(double degrees)
    {
        double normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static PhaseDecision HandlePreflight(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

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
        PhaseDecision.Silent();

    private static PhaseDecision HandleRollout(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleTaxiIn(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();

    private static PhaseDecision HandleShutdown(PhaseEvaluationContext context) =>
        PhaseDecision.Silent();
}
