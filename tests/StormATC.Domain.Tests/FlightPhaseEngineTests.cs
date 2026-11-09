using StormATC.Domain.Aircraft;
using StormATC.Domain.Airports;
using StormATC.Domain.Atc;
using StormATC.Domain.FlightPlanning;
using StormATC.Domain.State;
using StormATC.Domain.ValueObjects;
using Xunit;
using Phase = StormATC.Domain.State.FlightPhase;

namespace StormATC.Domain.Tests;

/// <summary>Tests for the deterministic flight phase state machine.</summary>
public sealed class FlightPhaseEngineTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FlightPhaseEngine _engine = new();

    [Fact]
    public void ClimbReachesPlannedCruiseAltitude()
    {
        PhaseEvaluationContext context = Context(
            Phase.Climb,
            State(isOnGround: false, indicatedAltitude: 8000, altitudeAboveGround: 4000),
            plan: Plan(cruiseAltitude: Altitude.FromFlightLevel(80)));

        PhaseDecision decision = _engine.EvaluateStateChange(context);

        Assert.Equal(Phase.Cruise, decision.NewPhase);
    }

    [Fact]
    public void ClimbUsesPlanAltitudeNotTerrainRequirement()
    {
        // Terrain asks for 20 000 ft but the plan says FL80 — the plan wins.
        PhaseEvaluationContext context = Context(
            Phase.Climb,
            State(isOnGround: false, indicatedAltitude: 8000, altitudeAboveGround: 4000),
            plan: Plan(cruiseAltitude: Altitude.FromFlightLevel(80)),
            terrain: new TerrainProfile { MaxRequiredAltitudeFt = 20_000 });

        PhaseDecision decision = _engine.EvaluateStateChange(context);

        Assert.Equal(Phase.Cruise, decision.NewPhase);
    }

    [Fact]
    public void ClimbBelowPlannedCruiseStays()
    {
        PhaseEvaluationContext context = Context(
            Phase.Climb,
            State(isOnGround: false, indicatedAltitude: 7000, altitudeAboveGround: 4000),
            plan: Plan(cruiseAltitude: Altitude.FromFlightLevel(80)));

        PhaseDecision decision = _engine.EvaluateStateChange(context);

        Assert.Null(decision.NewPhase);
    }

    [Fact]
    public void ClimbWithoutPlanOrTerrainUsesAglFallback()
    {
        PhaseEvaluationContext context = Context(
            Phase.Climb,
            State(isOnGround: false, indicatedAltitude: 5000, altitudeAboveGround: 4000));

        PhaseDecision decision = _engine.EvaluateStateChange(context);

        Assert.Equal(Phase.Cruise, decision.NewPhase);
    }

    [Fact]
    public void CruiseDoesNotDescendBeforeTopOfDescent()
    {
        AirportData destination = Airport("KDDD", GeoPosition.Create(40.0, -75.0), elevationFeet: 0);
        PhaseEvaluationContext context = Context(
            Phase.Cruise,
            // ~60 NM from the destination — far outside the TOD for an 8000 ft descent.
            State(isOnGround: false, position: GeoPosition.Create(40.0, -74.0), indicatedAltitude: 8000, altitudeAboveGround: 8000),
            plan: Plan(cruiseAltitude: Altitude.FromFlightLevel(80)),
            destination: destination);

        PhaseDecision decision = _engine.EvaluateStateChange(context);

        Assert.Null(decision.NewPhase);
    }

    [Fact]
    public void CruiseDescendsInsideTopOfDescent()
    {
        AirportData destination = Airport("KDDD", GeoPosition.Create(40.0, -75.0), elevationFeet: 0);
        PhaseEvaluationContext context = Context(
            Phase.Cruise,
            // ~5 NM out — inside the computed TOD.
            State(isOnGround: false, position: GeoPosition.Create(40.0, -74.92), indicatedAltitude: 8000, altitudeAboveGround: 8000),
            plan: Plan(cruiseAltitude: Altitude.FromFlightLevel(80)),
            destination: destination);

        PhaseDecision decision = _engine.EvaluateStateChange(context);

        Assert.Equal(Phase.Descent, decision.NewPhase);
    }

    [Fact]
    public void TouchdownFromApproachStartsRollout()
    {
        PhaseEvaluationContext context = Context(
            Phase.Approach,
            State(isOnGround: true, groundSpeed: 0));

        PhaseDecision decision = _engine.EvaluateStateChange(context);

        Assert.Equal(Phase.Rollout, decision.NewPhase);
    }

    [Fact]
    public void LiftoffFromGroundPhaseStartsInitialClimb()
    {
        PhaseEvaluationContext context = Context(
            Phase.TaxiOut,
            State(isOnGround: false, indicatedAltitude: 200, altitudeAboveGround: 200));

        PhaseDecision decision = _engine.EvaluateStateChange(context);

        Assert.Equal(Phase.InitialClimb, decision.NewPhase);
    }

    [Fact]
    public void PreflightWaitsForEngines()
    {
        PhaseEvaluationContext cold = Context(
            Phase.Preflight,
            State(enginesRunning: false));
        Assert.Null(_engine.EvaluateStateChange(cold).NewPhase);

        PhaseEvaluationContext running = Context(
            Phase.Preflight,
            State(enginesRunning: true));
        Assert.Equal(Phase.Startup, _engine.EvaluateStateChange(running).NewPhase);
    }

    [Fact]
    public void AbnormalPhaseIsSticky()
    {
        PhaseEvaluationContext context = Context(
            Phase.Abnormal,
            State(isOnGround: false, indicatedAltitude: 100, altitudeAboveGround: 100));

        PhaseDecision decision = _engine.Evaluate(context);

        Assert.Null(decision.NewPhase);
    }

    private static PhaseEvaluationContext Context(
        Phase phase,
        AircraftState aircraft,
        FlightPlan? plan = null,
        AirportData? departure = null,
        AirportData? destination = null,
        TerrainProfile? terrain = null) => new()
        {
            CurrentPhase = phase,
            AircraftState = aircraft,
            FlightPlan = plan,
            DepartureAirport = departure,
            DestinationAirport = destination,
            Weather = null,
            TerrainProfile = terrain,
            Traffic = [],
            Realism = RealismOptions.FromProfile(FlightProfile.Standard),
            CurrentTime = Now,
            DescentRateFpm = 1500,
            CruiseSpeedKnots = 140,
        };

    private static AircraftState State(
        GeoPosition? position = null,
        double indicatedAltitude = 0,
        double altitudeAboveGround = 0,
        double groundSpeed = 0,
        bool isOnGround = true,
        bool enginesRunning = true,
        bool parkingBrake = false) =>
        AircraftState.Unknown(Now) with
        {
            Position = position ?? GeoPosition.Create(40.0, -75.0),
            IndicatedAltitudeFeet = indicatedAltitude,
            PressureAltitudeFeet = indicatedAltitude,
            AltitudeAboveGroundFeet = altitudeAboveGround,
            GroundSpeedKnots = groundSpeed,
            IsOnGround = isOnGround,
            EnginesRunning = enginesRunning,
            ParkingBrakeSet = parkingBrake,
        };

    private static FlightPlan Plan(Altitude? cruiseAltitude = null) => new()
    {
        FlightPlanId = "TEST-PLAN",
        Callsign = Callsign.Create("TEST1"),
        DepartureIcao = "KAAA",
        DestinationIcao = "KDDD",
        CruiseAltitude = cruiseAltitude,
        SourceIdentifier = "Test",
    };

    private static AirportData Airport(string icao, GeoPosition position, int elevationFeet) => new()
    {
        Icao = icao,
        Name = icao,
        ReferencePosition = position,
        ElevationFeet = elevationFeet,
        Frequencies = new AirportFrequencies(),
    };
}
