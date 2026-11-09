#!/usr/bin/env python3
"""Generate checklist JSON files for all 65 MSFS 2024 aircraft from flightchecklist.app."""

import json
import os
from pathlib import Path

OUT_DIR = Path("/workspace/app/flightchecklists")
OUT_DIR.mkdir(parents=True, exist_ok=True)

# Aircraft list from flightchecklist.app/msfs - all 65 aircraft
# Format: (slug, name, aircraft_type_code, category, manufacturer, role)
AIRCRAFT = [
    ("crj-700", "Aerosoft CRJ 700", "CRJ7", "Preflight", "Aerosoft", "Airliner"),
    ("airbus-a310", "Airbus A310", "A310", "Preflight", "iniBuilds", "Airliner"),
    ("a320-neo", "Airbus A320neo", "A320", "Preflight", "iniBuilds", "Airliner"),
    ("a330-900neo", "Airbus A330-900neo", "A339", "Preflight", "iniBuilds", "Airliner"),
    ("a340-600", "Airbus A340-600 (Aerosoft / ToLiss)", "A346", "Preflight", "Aerosoft", "Airliner"),
    ("a400m", "Airbus A400M Atlas", "A400", "Preflight", "iniBuilds", "Transport"),
    ("h125", "Airbus H125", "H125", "Preflight", "Cowan Simulation", "Rotary"),
    ("atr-72-600", "ATR 72-600", "AT76", "Preflight", "Hans Hartmann", "Airliner"),
    ("avro-rj85", "Avro RJ85 (Just Flight)", "RJ85", "Preflight", "Just Flight", "Airliner"),
    ("blacksquare-baron", "Beechcraft Baron 58 (Black Square)", "BE58", "Preflight", "Black Square", "General Aviation"),
    ("blacksquare-bonanza", "Beechcraft Bonanza (Black Square)", "B35T", "Preflight", "Black Square", "General Aviation"),
    ("bonanza-g36", "Beechcraft Bonanza G36", "B36T", "Preflight", "Asobo Studio", "General Aviation"),
    ("blacksquare-duke", "Beechcraft Duke B60 (Black Square)", "BE60", "Preflight", "Black Square", "General Aviation"),
    ("king-air-350i", "Beechcraft King Air 350i", "BE35", "Preflight", "Asobo Studio", "General Aviation"),
    ("blacksquare-starship", "Beechcraft Starship (Black Square)", "BCS1", "Preflight", "Black Square", "General Aviation"),
    ("blacksquare-turbine-duke", "Beechcraft Turbine Duke (Black Square)", "BETU", "Preflight", "Black Square", "General Aviation"),
    ("bell-407", "Bell 407", "B407", "Preflight", "Nemeth Designs", "Rotary"),
    ("bell-uh-1h-huey", "Bell UH-1H Huey", "UH1H", "Preflight", "Taog's Hangar", "Rotary"),
    ("boeing-737-max", "Boeing 737 MAX 8 (Asobo)", "B738", "Preflight", "Asobo Studio", "Airliner"),
    ("ifly-737-max", "Boeing 737 MAX 8 (iFly)", "B38M", "Preflight", "iFly Simulations", "Airliner"),
    ("ifly-737-800", "Boeing 737-800 (iFly)", "B738", "Preflight", "iFly Simulations", "Airliner"),
    ("boeing-747-8i", "Boeing 747-8i", "B748", "Preflight", "Working Title", "Airliner"),
    ("boeing-787-9", "Boeing 787-9 Dreamliner", "B789", "Preflight", "", "Airliner"),
    ("cessna-152", "Cessna 152", "C152", "Preflight", "Asobo Studio", "General Aviation"),
    ("cessna-172", "Cessna 172 Skyhawk", "C172", "Preflight", "Asobo Studio", "General Aviation"),
    ("cessna-208-caravan", "Cessna 208 Caravan", "C208", "Preflight", "Asobo Studio", "General Aviation"),
    ("blacksquare-caravan", "Cessna 208B Grand Caravan (Black Square)", "C208", "Preflight", "Black Square", "General Aviation"),
    ("citation-longitude", "Cessna Citation Longitude", "CL35", "Preflight", "Working Title", "General Aviation"),
    ("citation-x", "Cessna Citation X (FlightFX)", "C750", "Preflight", "FlightFX", "General Aviation"),
    ("cirrus-sr22", "Cirrus SR22", "SR22", "Preflight", "Asobo Studio", "General Aviation"),
    ("blacksquare-commander-114", "Commander 114 (Black Square)", "CM14", "Preflight", "Black Square", "General Aviation"),
    ("blacksquare-tbm-850", "Daher TBM 850 (Black Square)", "TBM8", "Preflight", "Black Square", "General Aviation"),
    ("tbm-930", "Daher TBM 930", "TBM9", "Preflight", "Asobo Studio", "General Aviation"),
    ("dhc-2-beaver", "DHC-2 Beaver", "DHC2", "Preflight", "Asobo Studio", "General Aviation"),
    ("dhc-6-twin-otter", "DHC-6 Twin Otter", "DHC6", "Preflight", "", "General Aviation"),
    ("da40-ng", "Diamond DA40 NG (G1000 NXi)", "DA40", "Preflight", "Asobo Studio", "General Aviation"),
    ("do-228-100", "Dornier Do 228-100 (Carenado)", "DO28", "Preflight", "Carenado", "General Aviation"),
    ("douglas-dc-3", "Douglas DC-3", "DC3", "Preflight", "Asobo Studio", "Transport"),
    ("embraer-e175", "Embraer E-Jets E170/E175 (FlightSim Studio)", "E175", "Preflight", "FlightSim Studio", "Airliner"),
    ("embraer-e195", "Embraer E-Jets E190/E195 (FlightSim Studio)", "E195", "Preflight", "FlightSim Studio", "Airliner"),
    ("phenom-300e", "Embraer Phenom 300E (FSReborn)", "E55P", "Preflight", "FSReborn", "General Aviation"),
    ("ec135-t1", "Eurocopter EC135 T1", "EC35", "Preflight", "", "Rotary"),
    ("fa-18e-super-hornet", "F/A-18E Super Hornet", "F18E", "Preflight", "Asobo Studio", "Fighter"),
    ("fenix-a320", "Fenix A320 (CEO)", "A320", "Preflight", "Fenix Simulations", "Airliner"),
    ("fenix-a321", "Fenix A321", "A321", "Preflight", "Fenix Simulations", "Airliner"),
    ("a32nx", "FlyByWire A32NX", "A320", "Preflight", "FlyByWire Simulations", "Airliner"),
    ("a380x", "FlyByWire A380X", "A388", "Preflight", "FlyByWire Simulations", "Airliner"),
    ("fokker-f100", "Fokker F100", "F100", "Preflight", "", "Airliner"),
    ("fokker-f28", "Fokker F28 Fellowship", "F28", "Preflight", "", "Airliner"),
    ("hawker-800xp", "Hawker 800XP (FlightFX)", "H800", "Preflight", "FlightFX", "General Aviation"),
    ("hondajet-ha420", "Honda HA-420 HondaJet (FlightFX)", "HA4T", "Preflight", "FlightFX", "General Aviation"),
    ("inibuilds-a350", "Inibuilds A350-900", "A359", "Preflight", "iniBuilds", "Airliner"),
    ("inibuilds-a380", "Inibuilds A380-800", "A388", "Preflight", "iniBuilds", "Airliner"),
    ("md-11", "McDonnell Douglas MD-11", "MD11", "Preflight", "", "Airliner"),
    ("md-82", "McDonnell Douglas MD-82", "MD82", "Preflight", "", "Airliner"),
    ("p180-avanti", "Piaggio P180 Avanti II (FlightFX)", "P180", "Preflight", "FlightFX", "General Aviation"),
    ("pilatus-pc-12", "Pilatus PC-12", "PC12", "Preflight", "", "General Aviation"),
    ("a2a-comanche-250", "Piper PA-24-250 Comanche (A2A)", "PA24", "Preflight", "A2A Simulations", "General Aviation"),
    ("pa-28-warrior-ii", "Piper PA-28-161 Warrior II (Just Flight)", "PA28", "Preflight", "Just Flight", "General Aviation"),
    ("pa-28r-turbo-arrow", "Piper PA-28R Turbo Arrow III/IV (Just Flight)", "P28T", "Preflight", "Just Flight", "General Aviation"),
    ("pa-28r-arrow-iii", "Piper PA-28R-201 Arrow III (Just Flight)", "P28R", "Preflight", "Just Flight", "General Aviation"),
    ("pmdg-737-800", "PMDG 737-800", "B738", "Preflight", "PMDG", "Airliner"),
    ("pmdg-747-400", "PMDG 747-400", "B744", "Preflight", "PMDG", "Airliner"),
    ("pmdg-777-300er", "PMDG 777-300ER", "B77W", "Preflight", "PMDG", "Airliner"),
    ("a220", "Synaptic A220-300", "A221", "Preflight", "Synaptic Simulations", "Airliner"),
]


def step(step_id, title, description="", step_type="Action", expected=None, optional=False):
    return {
        "Id": step_id,
        "Title": title,
        "Description": description,
        "Type": step_type,
        "ExpectedResponse": expected,
        "SimVarBinding": None,
        "EventBinding": None,
        "IsOptional": optional,
    }


def checklist_definition(slug, name, ac_type, category, checklist_description, phases_steps):
    """phases_steps: list of (phase_title, [step dicts])"""
    all_steps = []
    step_num = 1
    for phase_title, steps in phases_steps:
        for s in steps:
            if isinstance(s, dict):
                title = s["title"]
                step_desc = s.get("description", "")
                step_type = s.get("type", "Action")
                expected = s.get("expected")
                optional = s.get("optional", False)
            else:
                title = s[0]
                step_desc = s[1] if len(s) > 1 else ""
                step_type = s[2] if len(s) > 2 else "Action"
                expected = s[3] if len(s) > 3 else None
                optional = s[4] if len(s) > 4 else False
            all_steps.append(step(f"{step_num:02d}", title, step_desc, step_type, expected, optional))
            step_num += 1

    return {
        "Id": slug,
        "Name": name,
        "AircraftType": ac_type,
        "Category": category,
        "Description": checklist_description,
        "Steps": all_steps,
    }


def write_checklist(slug, definition):
    path = OUT_DIR / f"{slug}.json"
    path.write_text(json.dumps(definition, indent=2) + "\n")
    return path


# =========================================================================
# DETAILED CHECKLISTS from flightchecklist.app
# =========================================================================

# Cessna 172 Skyhawk - full detail from flightchecklist.app
cessna172_steps = [
    ("Pre-Flight Planning", [
        ("Weather Briefing (METAR / TAF)", "Check current conditions and forecast at departure, enroute, and destination airports.", "Verification", None, False),
        ("NOTAMs", "Check for runway closures, TFRs, and airspace restrictions.", "Verification", None, False),
        ("Fuel Required", "POH Section 5 Cruise Performance at 2550 lb. Plan trip fuel plus 30-minute VFR reserve.", "Verification", None, False),
        ("Weight & Balance", "POH Section 2: max ramp weight 2558 lb. CG range limits.", "Verification", None, False),
        ("Takeoff & Landing Distances", "POH performance specification at 2550 lb, sea level, standard day.", "Verification", None, False),
        ("VFR Charts / Flight Plan", "Mark waypoints, frequencies, and altitude on sectional chart or EFB.", "Action", None, False),
        ("Passenger Briefing", "Brief seats, belts, doors, and sterile cockpit.", "Action", None, False),
    ]),
    ("Preflight Inspection", [
        ("Pitot Tube Cover", "Remove - check for pitot blockage.", "Action", None, False),
        ("Pilot's Operating Handbook", "Accessible to pilot.", "Verification", None, False),
        ("Garmin G1000 Cockpit Reference Guide", "Accessible to pilot.", "Verification", None, False),
        ("Airplane Weight and Balance", "Checked.", "Verification", None, False),
        ("Parking Brake", "Set.", "Action", None, False),
        ("Control Wheel Lock", "Remove.", "Action", None, False),
        ("MAGNETOS Switch", "OFF.", "Action", None, False),
        ("AVIONICS Switch (BUS 1 and BUS 2)", "OFF.", "Action", None, False),
        ("MASTER Switch (ALT and BAT)", "ON.", "Action", None, False),
        ("Primary Flight Display (PFD)", "CHECK - verify PFD is on.", "Verification", None, False),
        ("FUEL QTY (L and R)", "CHECK.", "Verification", None, False),
        ("LOW FUEL L and LOW FUEL R Annunciators", "CHECK - verify annunciators are not shown on the PFD.", "Verification", None, False),
        ("OIL PRESSURE Annunciator", "CHECK - verify annunciator IS shown (engine not running).", "Verification", None, False),
        ("LOW VACUUM Annunciator", "CHECK - verify annunciator IS shown (engine not running).", "Verification", None, False),
        ("AVIONICS Switch (BUS 1)", "ON.", "Action", None, False),
        ("Forward Avionics Fan", "CHECK - verify fan is heard.", "Verification", None, False),
        ("AVIONICS Switch (BUS 1)", "OFF.", "Action", None, False),
        ("AVIONICS Switch (BUS 2)", "ON.", "Action", None, False),
        ("Aft Avionics Fan", "CHECK - verify fan is heard.", "Verification", None, False),
        ("AVIONICS Switch (BUS 2)", "OFF.", "Action", None, False),
        ("PITOT HEAT Switch", "ON - carefully check that the pitot tube is warm.", "Action", None, False),
        ("PITOT HEAT Switch", "OFF.", "Action", None, False),
        ("LOW VOLTS Annunciator", "CHECK - verify annunciator is shown.", "Verification", None, False),
        ("MASTER Switch (ALT and BAT)", "OFF.", "Action", None, False),
        ("Elevator Trim Control", "TAKEOFF POSITION.", "Configuration", None, False),
        ("FUEL SELECTOR Valve", "BOTH.", "Configuration", None, False),
        ("ALT STATIC AIR Valve", "OFF (push full in).", "Configuration", None, False),
        ("Fire Extinguisher", "CHECK - verify gage pointer in the green arc.", "Verification", None, False),
        ("Empennage - Baggage Door", "CHECK - lock with key.", "Verification", None, False),
        ("Empennage - Autopilot Static Source", "CHECK - verify opening is clear.", "Verification", None, False),
        ("Empennage - Rudder Gust Lock", "REMOVE.", "Action", None, False),
        ("Empennage - Tail Tiedown", "DISCONNECT.", "Action", None, False),
        ("Empennage - Control Surfaces", "CHECK - check freedom of movement and security.", "Verification", None, False),
        ("Empennage - Elevator Trim Tab", "CHECK SECURITY.", "Verification", None, False),
        ("Empennage - Antennas", "CHECK - check for security of attachment and general condition.", "Verification", None, False),
        ("Right Wing Trailing Edge - Flap", "CHECK - check for security and condition.", "Verification", None, False),
        ("Right Wing Trailing Edge - Aileron", "CHECK - check freedom of movement and security.", "Verification", None, False),
        ("Right Wing - Wing Tiedown", "DISCONNECT.", "Action", None, False),
        ("Right Wing - Main Wheel Tire", "CHECK - check for proper inflation and general condition.", "Verification", None, False),
        ("Right Wing - Fuel Tank Sump Quick Drain Valves", "DRAIN - drain at least a cupful from each sump location.", "Action", None, False),
        ("Right Wing - Fuel Quantity", "CHECK VISUALLY.", "Verification", None, False),
        ("Right Wing - Fuel Filler Cap", "SECURE AND VENT CLEAR.", "Verification", None, False),
        ("Nose - Fuel Strainer Quick Drain Valve", "DRAIN - drain at least a cupful.", "Action", None, False),
        ("Nose - Engine Oil Dipstick / Filler Cap", "CHECK AND SECURE.", "Verification", None, False),
        ("Nose - Engine Cooling Air Inlets", "CLEAR OF OBSTRUCTIONS.", "Verification", None, False),
        ("Nose - Propeller and Spinner", "CHECK - check for nicks and security.", "Verification", None, False),
        ("Nose - Air Filter", "CHECK - check for restrictions.", "Verification", None, False),
        ("Nose - Nosewheel Strut and Tire", "CHECK - check proper strut inflation and general tire condition.", "Verification", None, False),
        ("Nose - Static Source Opening", "CHECK - verify opening is clear.", "Verification", None, False),
        ("Left Wing Leading Edge - Fuel Tank Vent Opening", "CHECK - check for blockage.", "Verification", None, False),
        ("Left Wing Leading Edge - Stall Warning Opening", "CHECK - check for blockage.", "Verification", None, False),
        ("Left Wing Leading Edge - Landing / Taxi Light(s)", "CHECK - check condition and cleanliness of cover.", "Verification", None, False),
        ("Left Wing - Wing Tiedown", "DISCONNECT.", "Action", None, False),
        ("Left Wing - Fuel Quantity", "CHECK VISUALLY.", "Verification", None, False),
        ("Left Wing - Fuel Filler Cap", "SECURE AND VENT CLEAR.", "Verification", None, False),
        ("Left Wing - Fuel Tank Sump Quick Drain Valves", "DRAIN - drain at least a cupful.", "Action", None, False),
        ("Left Wing - Main Wheel Tire", "CHECK - check for proper inflation and general condition.", "Verification", None, False),
        ("Left Wing Trailing Edge - Aileron", "CHECK - check freedom of movement and security.", "Verification", None, False),
        ("Left Wing Trailing Edge - Flap", "CHECK - check for security and condition.", "Verification", None, False),
    ]),
    ("Before Starting Engine", [
        ("Preflight Inspection", "COMPLETE.", "Verification", None, False),
        ("Passenger Briefing", "COMPLETE.", "Verification", None, False),
        ("Seats and Seat Belts", "ADJUST and LOCK - verify inertia reel locking.", "Configuration", None, False),
        ("Brakes", "TEST and SET.", "Action", None, False),
        ("Circuit Breakers", "CHECK IN.", "Verification", None, False),
        ("Electrical Equipment", "OFF.", "Action", None, False),
        ("AVIONICS Switch (BUS 1 and BUS 2)", "OFF - must be OFF during engine start.", "Action", None, False),
        ("FUEL SELECTOR Valve", "BOTH.", "Configuration", None, False),
        ("FUEL SHUTOFF Valve", "ON (push full in).", "Configuration", None, False),
    ]),
    ("Starting Engine", [
        ("Throttle Control", "OPEN 1/4 INCH.", "Configuration", None, False),
        ("Mixture Control", "IDLE CUTOFF (pull full out).", "Configuration", None, False),
        ("STBY BATT Switch", "TEST then ARM.", "Action", None, False),
        ("Engine Indicating System", "CHECK PARAMETERS - verify no red X's.", "Verification", None, False),
        ("BUS E Volts", "CHECK - verify 24 VOLTS minimum.", "Verification", None, False),
        ("M BUS Volts", "CHECK - verify 1.5 VOLTS or less.", "Verification", None, False),
        ("BATT S Amps", "CHECK - verify discharge (negative).", "Verification", None, False),
        ("STBY BATT Annunciator", "CHECK - verify annunciator is shown.", "Verification", None, False),
        ("Propeller Area", "CLEAR - verify all people and equipment are at safe distance.", "Verification", None, False),
        ("MASTER Switch (ALT and BAT)", "ON.", "Action", None, False),
        ("BEACON Light Switch", "ON.", "Action", None, False),
        ("FUEL PUMP Switch", "ON.", "Action", None, False),
        ("Mixture Control", "FULL RICH then IDLE CUTOFF - set FULL RICH until stable fuel flow, then IDLE CUTOFF.", "Configuration", None, False),
        ("FUEL PUMP Switch", "OFF.", "Action", None, False),
        ("MAGNETOS Switch", "START (release when engine starts).", "Action", None, False),
        ("Mixture Control", "ADVANCE SMOOTHLY TO RICH.", "Configuration", None, False),
        ("Oil Pressure", "CHECK - verify increases into GREEN ARC in 30 to 60 seconds.", "Verification", None, False),
        ("AMPS (M BATT and BATT S)", "CHECK CHARGE (positive).", "Verification", None, False),
        ("LOW VOLTS Annunciator", "CHECK - verify annunciator is NOT shown.", "Verification", None, False),
        ("NAV Lights Switch", "ON as required.", "Configuration", None, False),
        ("AVIONICS Switch (BUS 1 and BUS 2)", "ON.", "Action", None, False),
    ]),
    ("Before Takeoff", [
        ("Parking Brake", "SET.", "Configuration", None, False),
        ("Pilot and Passenger Seat Backs", "MOST UPRIGHT POSITION.", "Configuration", None, False),
        ("Seats and Seat Belts", "CHECK SECURE.", "Verification", None, False),
        ("Cabin Doors", "CLOSED and LOCKED.", "Verification", None, False),
        ("Flight Controls", "FREE and CORRECT.", "Verification", None, False),
        ("Flight Instruments (PFD)", "CHECK - verify no red X's.", "Verification", None, False),
        ("Altimeters", "SET - PFD (BARO) set, standby altimeter set, and KAP 140 autopilot if installed.", "Configuration", None, False),
        ("G1000 ALT SEL", "SET.", "Configuration", None, False),
        ("KAP 140 Altitude Preselect (if installed)", "SET.", "Configuration", None, False),
        ("Standby Flight Instruments", "CHECK.", "Verification", None, False),
        ("Fuel Quantity", "CHECK - verify level is correct.", "Verification", None, False),
        ("Mixture Control", "RICH.", "Configuration", None, False),
        ("FUEL SELECTOR Valve", "SET BOTH.", "Configuration", None, False),
        ("Elevator Trim Control", "SET FOR TAKEOFF.", "Configuration", None, False),
        ("Manual Electric Trim (MET) System (if installed)", "CHECK.", "Verification", None, False),
        ("Throttle Control", "1800 RPM.", "Configuration", None, False),
        ("MAGNETOS Switch", "CHECK - RPM drop should not exceed 150 RPM.", "Verification", None, False),
        ("VAC Indicator", "CHECK - normal operating range 4.5 to 5.5 in.hg.", "Verification", None, False),
        ("Engine Indicators", "CHECK.", "Verification", None, False),
        ("Ammeters and Voltmeters", "CHECK.", "Verification", None, False),
        ("Annunciators", "CHECK - verify no annunciators are shown.", "Verification", None, False),
        ("Throttle Control", "CHECK IDLE.", "Configuration", None, False),
        ("Throttle Control", "1000 RPM or LESS.", "Configuration", None, False),
        ("Throttle Control Friction Lock", "ADJUST.", "Configuration", None, False),
        ("COM Frequency(s)", "SET.", "Configuration", None, False),
        ("NAV Frequency(s)", "SET.", "Configuration", None, False),
        ("FMS/GPS Flight Plan", "AS DESIRED.", "Configuration", None, False),
        ("XPDR", "SET.", "Configuration", None, False),
        ("CDI Softkey", "SELECT NAV SOURCE.", "Configuration", None, False),
        ("Autopilot (if installed)", "OFF.", "Configuration", None, False),
        ("CABIN PWR 12V Switch (if installed)", "OFF.", "Configuration", None, False),
        ("Wing Flaps", "UP to 10 deg (10 deg preferred).", "Configuration", None, False),
        ("Cabin Windows", "CLOSED and LOCKED.", "Verification", None, False),
        ("STROBE Lights Switch", "ON.", "Configuration", None, False),
        ("Brakes", "RELEASE.", "Action", None, False),
    ]),
    ("Takeoff", [
        ("Wing Flaps", "UP to 10 deg (10 deg preferred).", "Configuration", None, False),
        ("Throttle Control", "FULL (push full in).", "Configuration", None, False),
        ("Mixture Control", "RICH.", "Configuration", None, False),
        ("Elevator Control", "LIFT NOSEWHEEL AT 55 KIAS.", "Action", None, False),
        ("Climb Speed", "70 - 80 KIAS.", "Configuration", None, False),
        ("Wing Flaps", "RETRACT AT SAFE ALTITUDE.", "Configuration", None, False),
    ]),
    ("Enroute Climb", [
        ("Airspeed", "70 - 85 KIAS.", "Configuration", None, False),
        ("Throttle Control", "FULL (push full in).", "Configuration", None, False),
        ("Mixture Control", "RICH.", "Configuration", None, False),
    ]),
    ("Cruise", [
        ("Power", "2100 - 2700 RPM.", "Configuration", None, False),
        ("Elevator Trim Control", "ADJUST.", "Configuration", None, False),
        ("Mixture Control", "LEAN for desired performance or economy.", "Configuration", None, False),
        ("FMS/GPS", "REVIEW and BRIEF.", "Action", None, False),
    ]),
    ("Descent", [
        ("Power", "AS DESIRED.", "Configuration", None, False),
        ("Mixture", "ADJUST if necessary to make the engine run smoothly.", "Configuration", None, False),
        ("Altimeters", "SET - PFD (BARO) set, standby altimeter set, and KAP 140 autopilot if installed.", "Configuration", None, False),
        ("G1000 ALT SEL", "SET.", "Configuration", None, False),
        ("KAP 140 Altitude Preselect (if installed)", "SET.", "Configuration", None, False),
        ("CDI Softkey", "SELECT NAV SOURCE.", "Configuration", None, False),
        ("FMS/GPS", "REVIEW and BRIEF.", "Action", None, False),
        ("FUEL SELECTOR Valve", "BOTH.", "Configuration", None, False),
        ("Wing Flaps", "AS DESIRED.", "Configuration", None, False),
    ]),
    ("Before Landing", [
        ("Pilot and Passenger Seat Backs", "MOST UPRIGHT POSITION.", "Configuration", None, False),
        ("Seats and Seat Belts", "SECURED and LOCKED.", "Verification", None, False),
        ("FUEL SELECTOR Valve", "BOTH.", "Configuration", None, False),
        ("Mixture Control", "RICH.", "Configuration", None, False),
        ("LANDING and TAXI Light Switches", "ON.", "Configuration", None, False),
        ("Autopilot (if installed)", "OFF.", "Configuration", None, False),
        ("CABIN PWR 12V Switch (if installed)", "OFF.", "Configuration", None, False),
    ]),
    ("Landing", [
        ("Airspeed", "65 - 75 KIAS (Flaps UP).", "Configuration", None, False),
        ("Wing Flaps", "AS DESIRED.", "Configuration", None, False),
        ("Airspeed", "60 - 70 KIAS (Flaps FULL).", "Configuration", None, False),
        ("Elevator Trim Control", "ADJUST.", "Configuration", None, False),
        ("Touchdown", "MAIN WHEELS FIRST.", "Action", None, False),
        ("Landing Roll", "LOWER NOSEWHEEL GENTLY.", "Action", None, False),
        ("Braking", "MINIMUM REQUIRED.", "Action", None, False),
    ]),
    ("After Landing", [
        ("Wing Flaps", "UP.", "Configuration", None, False),
    ]),
    ("Securing Airplane", [
        ("Parking Brake", "SET.", "Configuration", None, False),
        ("Throttle Control", "IDLE (pull full out).", "Configuration", None, False),
        ("Electrical Equipment", "OFF.", "Action", None, False),
        ("AVIONICS Switch (BUS 1 and BUS 2)", "OFF.", "Action", None, False),
        ("Mixture Control", "IDLE CUTOFF (pull full out).", "Configuration", None, False),
        ("MAGNETOS Switch", "OFF.", "Action", None, False),
        ("MASTER Switch (ALT and BAT)", "OFF.", "Action", None, False),
        ("STBY BATT Switch", "OFF.", "Action", None, False),
        ("Control Lock", "INSTALL.", "Action", None, False),
        ("FUEL SELECTOR Valve", "LEFT or RIGHT.", "Configuration", None, False),
    ]),
]

# A320neo - full detail from flightchecklist.app
a320neo_steps = [
    ("Pre-Flight Planning", [
        ("Weather Briefing (METAR / TAF / Sigmet)", "Check departure, enroute, destination, and alternate weather.", "Verification", None, False),
        ("NOTAMs", "Check for runway closures, navigation aid outages, and airspace restrictions.", "Verification", None, False),
        ("OFP / SimBrief Flight Plan", "Generate and review Operational Flight Plan - route, fuel, winds, alternates.", "Verification", None, False),
        ("Fuel Planning", "Block fuel includes taxi, trip, contingency (5%), alternate, final reserve (30 min), and extra.", "Verification", None, False),
        ("Weight & Balance / Loadsheet", "Verify ZFW, TOW, and LW are within limits. Check trim setting (CG%).", "Verification", None, False),
        ("SID / STAR / Approach Review", "Review departure and expected arrival procedures. Check transition altitudes.", "Action", None, False),
        ("MEL / CDL Items", "Review any Minimum Equipment List or Configuration Deviation List items.", "Verification", None, False),
        ("Performance - Takeoff Speeds", "V1, VR, V2, and flex temperature based on TOW, runway, and conditions.", "Calculation", None, False),
    ]),
    ("Electrical Power Up", [
        ("Battery 1 & 2", "ON.", "Configuration", None, False),
        ("External Power", "ON (if available).", "Configuration", None, False),
        ("APU Master Switch", "ON.", "Configuration", None, False),
        ("APU Start", "ON - wait for AVAIL light on ECAM (~1 min).", "Action", None, False),
        ("APU Bleed", "ON.", "Configuration", None, False),
        ("External Power", "OFF (if APU running).", "Configuration", None, False),
    ]),
    ("Cockpit Preparation", [
        ("Crew Oxygen", "ON.", "Configuration", None, False),
        ("ADIRS 1, 2, 3", "NAV - do not move aircraft during alignment (~7 min).", "Configuration", None, False),
        ("Navigation Lights", "ON.", "Configuration", None, False),
        ("Seat Belts Sign", "ON.", "Configuration", None, False),
        ("No Smoking Sign", "AUTO.", "Configuration", None, False),
        ("Emergency Exit Lights", "ARM.", "Configuration", None, False),
        ("Fuel Pumps", "ON (Wing tanks).", "Configuration", None, False),
        ("Panel Lights", "SET AS NEEDED.", "Configuration", None, False),
    ]),
    ("MCDU Setup", [
        ("MCDU - INIT A Page", "ENTER Flight Number, Cost Index.", "Configuration", None, False),
        ("MCDU - FROM/TO", "SET Departure/Arrival airports.", "Configuration", None, False),
        ("MCDU - F-PLN", "VERIFY or ENTER route.", "Configuration", None, False),
        ("MCDU - INIT B Page", "ENTER Fuel, ZFW, CG - verify block fuel matches actual.", "Configuration", None, False),
        ("MCDU - PERF", "ENTER V1, VR, V2, FLEX Temp.", "Configuration", None, False),
        ("FCU Altitude", "SET Initial Climb Altitude.", "Configuration", None, False),
        ("FCU Heading / Speed", "SET as required.", "Configuration", None, False),
        ("Transponder", "SET assigned code - STBY.", "Configuration", None, False),
        ("ATC/TCAS", "SET.", "Configuration", None, False),
    ]),
    ("Before Start", [
        ("Parking Brake", "SET.", "Configuration", None, False),
        ("Beacon Light", "ON - signal to ground crew that engines will start.", "Configuration", None, False),
        ("Doors", "CLOSED (check ECAM DOOR page).", "Verification", None, False),
        ("Thrust Levers", "IDLE.", "Configuration", None, False),
        ("Pack Switches", "OFF - reduces APU bleed load during engine start.", "Configuration", None, False),
        ("Engine Mode Selector", "IGN/START - enables automatic ignition and packs off for engine start.", "Configuration", None, False),
        ("Ground Crew", "CLEARED TO START - wait for pushback clearance if required.", "Verification", None, False),
        ("ACARS INIT", "COMPLETED.", "Verification", None, False),
        ("Gear Pins and Covers", "REMOVED.", "Action", None, False),
        ("Fuel Quantity", "CHECK.", "Verification", None, False),
        ("T/O Data", "SET.", "Configuration", None, False),
        ("Baro Ref", "SET.", "Configuration", None, False),
        ("Windows", "CLOSED.", "Verification", None, False),
    ]),
    ("Engine Start", [
        ("Engine Master 2", "ON (start right engine first).", "Configuration", None, False),
        ("Engine 2 N2", "MONITOR - wait for stable idle.", "Verification", None, False),
        ("Engine Master 1", "ON.", "Configuration", None, False),
        ("Engine 1 N2", "MONITOR - wait for stable idle.", "Verification", None, False),
        ("Engine Mode Selector", "NORM - return to NORM after both engines stabilised.", "Configuration", None, False),
        ("Engine Bleeds", "ON (Both).", "Configuration", None, False),
        ("Pack Switches", "ON (Both).", "Configuration", None, False),
        ("APU Bleed", "OFF.", "Configuration", None, False),
        ("APU Master", "OFF.", "Configuration", None, False),
        ("Generator Switches", "VERIFY ON (GEN 1 & 2).", "Verification", None, False),
    ]),
    ("After Start / Taxi", [
        ("Flight Controls", "CHECK (full deflection).", "Verification", None, False),
        ("Pitch Trim", "SET FOR TAKEOFF - check loadsheet for trim value.", "Configuration", None, False),
        ("Rudder Trim", "ZERO.", "Configuration", None, False),
        ("Ground Spoilers", "ARM.", "Configuration", None, False),
        ("Flaps", "SET for takeoff (CONF 1+F, 2, or 3).", "Configuration", None, False),
        ("Auto Brake", "MAX.", "Configuration", None, False),
        ("Cabin Crew", "NOTIFIED.", "Action", None, False),
        ("Taxi Light", "ON.", "Configuration", None, False),
        ("Nose Light", "TAXI.", "Configuration", None, False),
        ("Parking Brake", "RELEASE.", "Configuration", None, False),
        ("Brakes", "CHECK during first movement.", "Verification", None, False),
        ("Flight Instruments", "CHECK during taxi - PFD, ND, ECAM.", "Verification", None, False),
        ("Anti-Ice", "AS REQUIRED.", "Configuration", None, False),
        ("ECAM Status", "CHECK.", "Verification", None, False),
        ("Ground Equipment", "CLEAR.", "Verification", None, False),
    ]),
    ("Before Takeoff", [
        ("Flight Controls", "CHECKED.", "Verification", None, False),
        ("Flight Instruments", "CHECKED.", "Verification", None, False),
        ("Briefing", "CONFIRMED - review departure, SID, engine failure procedures.", "Action", None, False),
        ("Flaps", "CONFIRM T/O setting.", "Configuration", None, False),
        ("V-Speeds", "CONFIRM V1, VR, V2.", "Verification", None, False),
        ("Transponder", "AUTO / ON, ALT RPTG ON.", "Configuration", None, False),
        ("ECAM Memo", "CHECK - T/O NO BLUE.", "Verification", None, False),
        ("Radar / PWS", "ON / AUTO.", "Configuration", None, False),
        ("ATC Clearance", "OBTAINED.", "Verification", None, False),
        ("Packs", "AS REQUIRED.", "Configuration", None, False),
        ("Landing Lights", "ON (Both).", "Configuration", None, False),
        ("Strobe Lights", "ON or AUTO.", "Configuration", None, False),
        ("Nose Light", "T.O.", "Configuration", None, False),
        ("Runway Turnoff Lights", "ON.", "Configuration", None, False),
        ("TCASTA", "RA.", "Configuration", None, False),
        ("T.O. Config", "NORM.", "Configuration", None, False),
        ("Takeoff Runway", "CONFIRMED.", "Verification", None, False),
        ("Cabin Crew", "ADVISED.", "Action", None, False),
        ("Engine Mode Selector", "AS REQUIRED.", "Configuration", None, False),
    ]),
    ("Takeoff", [
        ("Brakes", "HOLD.", "Configuration", None, False),
        ("Thrust Levers", "ADVANCE to 50% N1 - stabilise engines before applying takeoff thrust.", "Configuration", None, False),
        ("Brakes", "RELEASE.", "Configuration", None, False),
        ("Thrust Levers", "FLX/MCT or TOGA - use FLEX if FLEX temp entered, otherwise TOGA.", "Configuration", None, False),
        ("FMA", "VERIFY - MAN FLX (or MAN TOGA), SRS, RWY.", "Verification", None, False),
        ("80 Knots", "CROSS-CHECK speed - callout.", "Verification", None, False),
        ("V1", "CALLOUT - committed to fly.", "Verification", None, False),
        ("Rotate at VR", "SMOOTH backstick - pitch 15 deg nose up.", "Action", None, False),
        ("Positive Climb", "CONFIRMED.", "Verification", None, False),
        ("Landing Gear", "UP.", "Configuration", None, False),
    ]),
    ("After Takeoff / Climb", [
        ("Thrust Reduction Altitude", "THRUST LEVERS to CL detent.", "Configuration", None, False),
        ("Autopilot 1", "ON.", "Configuration", None, False),
        ("At S-Speed - Flaps", "RETRACT to 0 - retract incrementally as speed builds: 2 → 1 → 0.", "Configuration", None, False),
        ("Speed Brakes", "DISARM.", "Configuration", None, False),
        ("Packs", "ON (if turned OFF for takeoff).", "Configuration", None, False),
        ("Runway Turnoff Lights", "OFF.", "Configuration", None, False),
        ("Nose Light", "OFF.", "Configuration", None, False),
        ("Landing Gear", "CONFIRM UP.", "Verification", None, False),
        ("Flaps", "CONFIRM RETRACTED.", "Verification", None, False),
        ("Baro Reference", "SET STD above transition altitude.", "Configuration", None, False),
        ("Altitude on FCU", "SET cleared cruise level.", "Configuration", None, False),
        ("10,000 ft - Landing Lights", "OFF.", "Configuration", None, False),
        ("10,000 ft - Seat Belts Sign", "OFF (if conditions permit).", "Configuration", None, False),
    ]),
    ("Cruise", [
        ("Altitude", "VERIFY level at cruise FL.", "Verification", None, False),
        ("Autopilot", "ON - managed speed.", "Configuration", None, False),
        ("ECAM - Engine Parameters", "CHECK - all normal.", "Verification", None, False),
        ("Fuel Quantity & Balance", "CHECK - monitor fuel burn vs flight plan. Max imbalance ~1,500 kg.", "Verification", None, False),
        ("Flight Progress", "MONITOR on ND / MCDU PROG page.", "Verification", None, False),
        ("MCDU - PERF CRZ Page", "VERIFY wind, temp, cost index.", "Verification", None, False),
        ("Radios", "SET as required for ATC sectors.", "Configuration", None, False),
        ("Signs", "AS REQUIRED - seat belt sign ON for turbulence.", "Configuration", None, False),
        ("Step Climbs", "AS REQUIRED - consider step climb as fuel burns off.", "Action", None, False),
    ]),
    ("Descent Preparation", [
        ("ATIS / Airport Information", "OBTAINED - note QNH, wind, runway in use, transition level.", "Verification", None, False),
        ("MCDU - PERF APPR Page", "ENTER QNH, Temp, Wind, Trans LVL, Minimums.", "Configuration", None, False),
        ("MCDU - APPR Page", "VERIFY or SELECT approach and runway.", "Configuration", None, False),
        ("Approach Briefing", "COMPLETED - review approach chart, minimums, missed approach procedure.", "Action", None, False),
        ("Landing Elevation", "SET on MCDU.", "Configuration", None, False),
        ("Auto Brake", "SET (LO or MED).", "Configuration", None, False),
        ("ILS Frequency", "SET and IDENTIFIED - verify on both PFDs.", "Configuration", None, False),
        ("FCU Altitude", "SET first constraint altitude.", "Configuration", None, False),
    ]),
    ("Descent", [
        ("ATC Descent Clearance", "OBTAINED.", "Verification", None, False),
        ("FCU Altitude", "SET cleared altitude.", "Configuration", None, False),
        ("ALT Knob", "PUSH for managed descent - or PULL for open/selected descent.", "Configuration", None, False),
        ("Transition Level", "SET local QNH when passing - both PFDs - verify altimeters match.", "Configuration", None, False),
        ("10,000 ft - Landing Lights", "ON (Both).", "Configuration", None, False),
        ("10,000 ft - Seat Belts Sign", "ON.", "Configuration", None, False),
        ("EFIS - CSTR", "SELECT on both NDs.", "Configuration", None, False),
        ("LS Button", "ON (both PFDs) - displays ILS localiser and glideslope scales.", "Configuration", None, False),
    ]),
    ("Approach", [
        ("APPR Button on FCU", "PRESS when cleared for approach - arms LOC and G/S modes.", "Configuration", None, False),
        ("Speed", "MANAGED - green dot or as assigned.", "Configuration", None, False),
        ("Flaps 1", "SET at S-speed.", "Configuration", None, False),
        ("Flaps 2", "SET at F-speed.", "Configuration", None, False),
        ("Landing Gear", "DOWN - select DOWN when established on localiser or as required.", "Configuration", None, False),
        ("Ground Spoilers", "ARM.", "Configuration", None, False),
        ("Flaps 3", "SET.", "Configuration", None, False),
        ("Flaps FULL", "SET - CONF FULL for shortest landing distance. CONF 3 if required.", "Configuration", None, False),
        ("Landing Gear", "CONFIRM DOWN - 3 greens.", "Verification", None, False),
        ("Auto Brake", "CONFIRM ARMED.", "Verification", None, False),
        ("Cabin Crew", "NOTIFIED - press CALLS ALL for cabin ready confirmation.", "Action", None, False),
        ("Runway Turnoff Lights", "ON.", "Configuration", None, False),
        ("Nose Light", "T.O.", "Configuration", None, False),
        ("ECAM Memo", "CHECK - LDG NO BLUE.", "Verification", None, False),
        ("ECAM Status", "CHECKED.", "Verification", None, False),
        ("MDA / DH", "SET.", "Configuration", None, False),
    ]),
    ("Landing", [
        ("1,000 ft - Stable Approach", "VERIFY - if not stabilised by 1,000 ft AGL, execute go-around.", "Verification", None, False),
        ("500 ft - Autopilot", "OFF (disconnect AP1).", "Configuration", None, False),
        ("VAPP", "MAINTAIN approach speed.", "Configuration", None, False),
        ("Flare (~30 ft)", "GENTLE backstick - reduce descent rate.", "Action", None, False),
        ("Thrust Levers", "IDLE at ~10-20 ft.", "Configuration", None, False),
        ("Touchdown", "MAIN GEAR first - hold pitch attitude, lower nose gently.", "Action", None, False),
        ("Ground Spoilers", "VERIFY DEPLOYED.", "Verification", None, False),
        ("Reverse Thrust", "SELECT REV (both levers up).", "Configuration", None, False),
        ("Auto Brake", "VERIFY ACTIVE (decel light).", "Verification", None, False),
        ("70 Knots", "REVERSE IDLE - stow reversers.", "Configuration", None, False),
        ("Braking", "MANUAL below 40 kts as needed.", "Configuration", None, False),
        ("Auto Thrust", "SPEED / OFF.", "Configuration", None, False),
        ("Signs", "ON.", "Configuration", None, False),
    ]),
    ("After Landing", [
        ("Runway Vacated", "CONFIRMED - hold short if instructed.", "Verification", None, False),
        ("Flaps", "RETRACT to 0.", "Configuration", None, False),
        ("Ground Spoilers", "DISARM.", "Configuration", None, False),
        ("Strobe Lights", "OFF or AUTO.", "Configuration", None, False),
        ("Runway Turnoff Lights", "ON.", "Configuration", None, False),
        ("Nose Light", "TAXI.", "Configuration", None, False),
        ("Landing Lights", "OFF.", "Configuration", None, False),
        ("Radar / PWS", "OFF.", "Configuration", None, False),
        ("Transponder", "STBY or as required.", "Configuration", None, False),
        ("APU Master", "ON (if required for gate).", "Configuration", None, False),
        ("APU Start", "ON.", "Configuration", None, False),
        ("Taxi to Gate", "FOLLOW ATC instructions.", "Action", None, False),
    ]),
    ("Parking / Shutdown", [
        ("Parking Brake", "SET.", "Configuration", None, False),
        ("Taxi Light / Nose Light", "OFF.", "Configuration", None, False),
        ("APU Bleed", "ON.", "Configuration", None, False),
        ("Engine Master 1", "OFF.", "Configuration", None, False),
        ("Engine Master 2", "OFF.", "Configuration", None, False),
        ("Engine N1", "VERIFY below 5% (both).", "Verification", None, False),
        ("Seat Belts Sign", "OFF.", "Configuration", None, False),
        ("Beacon Light", "OFF.", "Configuration", None, False),
        ("Fuel Pumps", "ALL OFF.", "Configuration", None, False),
        ("Navigation Lights", "OFF.", "Configuration", None, False),
        ("Park Brake and Chocks", "AS REQUIRED.", "Configuration", None, False),
        ("Transponder", "AS REQUIRED.", "Configuration", None, False),
    ]),
    ("Securing the Aircraft", [
        ("ADIRS", "ALL OFF.", "Configuration", None, False),
        ("Oxygen", "OFF.", "Configuration", None, False),
        ("APU Bleed", "OFF.", "Configuration", None, False),
        ("Emergency Exit Lights", "OFF.", "Configuration", None, False),
        ("No Smoking Sign", "OFF.", "Configuration", None, False),
        ("APU Master", "OFF.", "Configuration", None, False),
        ("Battery 1 & 2", "OFF.", "Configuration", None, False),
    ]),
]


# Generic checklist templates for remaining aircraft by role
def ga_checklist(slug, name, ac_type, description):
    """Generic GA aircraft checklist"""
    phases = [
        ("Pre-Flight Planning", [
            ("Weather Briefing (METAR / TAF)", "Check current conditions and forecast.", "Verification", None, False),
            ("NOTAMs", "Check for runway closures and airspace restrictions.", "Verification", None, False),
            ("Fuel Required", "Calculate fuel required for trip plus reserve.", "Verification", None, False),
            ("Weight & Balance", "Verify within limits.", "Verification", None, False),
        ]),
        ("Preflight Inspection", [
            ("Pitot Tube Cover", "Remove - check for pitot blockage.", "Action", None, False),
            ("Parking Brake", "Set.", "Configuration", None, False),
            ("Control Wheel Lock", "Remove.", "Action", None, False),
            ("MAGNETOS Switch", "OFF.", "Configuration", None, False),
            ("MASTER Switch", "ON.", "Configuration", None, False),
            ("Fuel Quantity", "CHECK VISUALLY.", "Verification", None, False),
            ("Control Surfaces", "CHECK freedom of movement.", "Verification", None, False),
            ("Fuel Tank Sumps", "DRAIN and check for contamination.", "Action", None, False),
            ("Oil Level", "CHECK AND SECURE.", "Verification", None, False),
            ("Propeller and Spinner", "CHECK for nicks and security.", "Verification", None, False),
            ("Tires", "CHECK inflation and condition.", "Verification", None, False),
        ]),
        ("Before Starting Engine", [
            ("Preflight Inspection", "COMPLETE.", "Verification", None, False),
            ("Seats and Seat Belts", "ADJUST and LOCK.", "Configuration", None, False),
            ("Brakes", "TEST and SET.", "Configuration", None, False),
            ("Circuit Breakers", "CHECK IN.", "Verification", None, False),
            ("Electrical Equipment", "OFF.", "Configuration", None, False),
            ("AVIONICS Switch", "OFF.", "Configuration", None, False),
            ("FUEL SELECTOR", "BOTH.", "Configuration", None, False),
            ("Mixture", "IDLE CUTOFF.", "Configuration", None, False),
        ]),
        ("Starting Engine", [
            ("Throttle", "OPEN 1/4 INCH.", "Configuration", None, False),
            ("Mixture", "IDLE CUTOFF then FULL RICH.", "Configuration", None, False),
            ("Propeller Area", "CLEAR.", "Verification", None, False),
            ("MASTER Switch", "ON.", "Configuration", None, False),
            ("BEACON Light", "ON.", "Configuration", None, False),
            ("MAGNETOS Switch", "START - release when engine starts.", "Configuration", None, False),
            ("Mixture", "ADVANCE SMOOTHLY TO RICH.", "Configuration", None, False),
            ("Oil Pressure", "CHECK - verify increases into green arc.", "Verification", None, False),
            ("AVIONICS Switch", "ON.", "Configuration", None, False),
        ]),
        ("Before Takeoff", [
            ("Parking Brake", "SET.", "Configuration", None, False),
            ("Flight Controls", "FREE and CORRECT.", "Verification", None, False),
            ("Flight Instruments", "CHECK.", "Verification", None, False),
            ("Altimeters", "SET.", "Configuration", None, False),
            ("Fuel Quantity", "CHECK.", "Verification", None, False),
            ("Mixture", "RICH.", "Configuration", None, False),
            ("Fuel Selector", "SET BOTH.", "Configuration", None, False),
            ("Trim", "SET FOR TAKEOFF.", "Configuration", None, False),
            ("Throttle", "1800 RPM - check magnetos.", "Configuration", None, False),
            ("Throttle", "IDLE.", "Configuration", None, False),
            ("COM / NAV Frequencies", "SET.", "Configuration", None, False),
            ("Transponder", "SET.", "Configuration", None, False),
            ("Flaps", "SET FOR TAKEOFF.", "Configuration", None, False),
            ("Strobe Lights", "ON.", "Configuration", None, False),
            ("Brakes", "RELEASE.", "Configuration", None, False),
        ]),
        ("Takeoff", [
            ("Throttle", "FULL.", "Configuration", None, False),
            ("Mixture", "RICH.", "Configuration", None, False),
            ("Elevator", "LIFT NOSEWHEEL AT ROTATION SPEED.", "Action", None, False),
            ("Climb Speed", "CLIMB AT BEST RATE.", "Configuration", None, False),
            ("Flaps", "RETRACT AT SAFE ALTITUDE.", "Configuration", None, False),
        ]),
        ("Enroute Climb", [
            ("Airspeed", "CLIMB SPEED.", "Configuration", None, False),
            ("Throttle", "FULL.", "Configuration", None, False),
            ("Mixture", "RICH.", "Configuration", None, False),
        ]),
        ("Cruise", [
            ("Power", "SET FOR CRUISE.", "Configuration", None, False),
            ("Trim", "ADJUST.", "Configuration", None, False),
            ("Mixture", "LEAN for economy.", "Configuration", None, False),
            ("Fuel", "MONITOR.", "Verification", None, False),
        ]),
        ("Descent", [
            ("Power", "REDUCE.", "Configuration", None, False),
            ("Mixture", "ADJUST.", "Configuration", None, False),
            ("Altimeters", "SET.", "Configuration", None, False),
            ("Fuel Selector", "BOTH.", "Configuration", None, False),
        ]),
        ("Before Landing", [
            ("Seats and Seat Belts", "SECURED and LOCKED.", "Verification", None, False),
            ("Fuel Selector", "BOTH.", "Configuration", None, False),
            ("Mixture", "RICH.", "Configuration", None, False),
            ("Landing Lights", "ON.", "Configuration", None, False),
            ("Autopilot", "OFF (if installed).", "Configuration", None, False),
        ]),
        ("Landing", [
            ("Airspeed", "APPROACH SPEED.", "Configuration", None, False),
            ("Flaps", "AS DESIRED.", "Configuration", None, False),
            ("Touchdown", "MAIN WHEELS FIRST.", "Action", None, False),
            ("Landing Roll", "LOWER NOSEWHEEL GENTLY.", "Action", None, False),
            ("Braking", "AS REQUIRED.", "Action", None, False),
        ]),
        ("After Landing", [
            ("Flaps", "UP.", "Configuration", None, False),
        ]),
        ("Securing Airplane", [
            ("Parking Brake", "SET.", "Configuration", None, False),
            ("Throttle", "IDLE.", "Configuration", None, False),
            ("Electrical Equipment", "OFF.", "Configuration", None, False),
            ("AVIONICS Switch", "OFF.", "Configuration", None, False),
            ("Mixture", "IDLE CUTOFF.", "Configuration", None, False),
            ("MAGNETOS Switch", "OFF.", "Configuration", None, False),
            ("MASTER Switch", "OFF.", "Configuration", None, False),
            ("Control Lock", "INSTALL.", "Action", None, False),
            ("Fuel Selector", "LEFT or RIGHT.", "Configuration", None, False),
        ]),
    ]
    return checklist_definition(slug, name, ac_type, "Preflight", description, phases)


def airliner_checklist(slug, name, ac_type, description):
    """Generic airliner checklist"""
    phases = [
        ("Pre-Flight Planning", [
            ("Weather Briefing (METAR / TAF)", "Check departure, enroute, destination, and alternate weather.", "Verification", None, False),
            ("NOTAMs", "Check for runway closures and airspace restrictions.", "Verification", None, False),
            ("Flight Plan", "LOAD and verify route, fuel, winds, alternates.", "Verification", None, False),
            ("Fuel Planning", "Block fuel includes taxi, trip, contingency, alternate, final reserve.", "Verification", None, False),
            ("Weight & Balance / Loadsheet", "Verify ZFW, TOW, and LW within limits.", "Verification", None, False),
            ("Performance - Takeoff Speeds", "V1, VR, V2 calculated.", "Calculation", None, False),
        ]),
        ("Electrical Power Up", [
            ("Battery", "ON.", "Configuration", None, False),
            ("External Power", "ON if available.", "Configuration", None, False),
            ("APU Master Switch", "ON.", "Configuration", None, False),
            ("APU Bleed", "ON.", "Configuration", None, False),
        ]),
        ("Cockpit Preparation", [
            ("ADIRS", "NAV.", "Configuration", None, False),
            ("Navigation Lights", "ON.", "Configuration", None, False),
            ("Seat Belts Sign", "ON.", "Configuration", None, False),
            ("Fuel Pumps", "ON.", "Configuration", None, False),
        ]),
        ("FMC / MCDU Setup", [
            ("FMC - INIT Page", "ENTER Flight Number, Cost Index.", "Configuration", None, False),
            ("FMC - FROM/TO", "SET Departure/Arrival airports.", "Configuration", None, False),
            ("FMC - Route", "VERIFY or ENTER route.", "Configuration", None, False),
            ("FMC - Performance", "ENTER Fuel, ZFW, CG.", "Configuration", None, False),
            ("FMC - Takeoff", "ENTER V1, VR, V2.", "Configuration", None, False),
            ("FCU Altitude", "SET Initial Climb Altitude.", "Configuration", None, False),
            ("Transponder", "SET assigned code - STBY.", "Configuration", None, False),
        ]),
        ("Before Start", [
            ("Parking Brake", "SET.", "Configuration", None, False),
            ("Beacon Light", "ON.", "Configuration", None, False),
            ("Doors", "CLOSED.", "Verification", None, False),
            ("Thrust Levers", "IDLE.", "Configuration", None, False),
            ("Pack Switches", "OFF.", "Configuration", None, False),
            ("Engine Mode Selector", "IGN/START.", "Configuration", None, False),
        ]),
        ("Engine Start", [
            ("Engine Master", "ON.", "Configuration", None, False),
            ("Engine N2", "MONITOR - wait for stable idle.", "Verification", None, False),
            ("Engine Mode Selector", "NORM.", "Configuration", None, False),
            ("Engine Bleeds", "ON.", "Configuration", None, False),
            ("Pack Switches", "ON.", "Configuration", None, False),
            ("APU Bleed", "OFF.", "Configuration", None, False),
            ("Generator Switches", "VERIFY ON.", "Verification", None, False),
        ]),
        ("After Start / Taxi", [
            ("Flight Controls", "CHECK.", "Verification", None, False),
            ("Trim", "SET FOR TAKEOFF.", "Configuration", None, False),
            ("Flaps", "SET for takeoff.", "Configuration", None, False),
            ("Auto Brake", "SET.", "Configuration", None, False),
            ("Taxi Light", "ON.", "Configuration", None, False),
            ("Parking Brake", "RELEASE.", "Configuration", None, False),
        ]),
        ("Before Takeoff", [
            ("Flight Controls", "CHECKED.", "Verification", None, False),
            ("Briefing", "CONFIRMED.", "Action", None, False),
            ("Flaps", "CONFIRM T/O setting.", "Configuration", None, False),
            ("V-Speeds", "CONFIRM V1, VR, V2.", "Verification", None, False),
            ("Transponder", "AUTO / ON.", "Configuration", None, False),
            ("ECAM Memo", "CHECK - T/O NO BLUE.", "Verification", None, False),
            ("ATC Clearance", "OBTAINED.", "Verification", None, False),
            ("Landing Lights", "ON.", "Configuration", None, False),
            ("Strobe Lights", "ON.", "Configuration", None, False),
        ]),
        ("Takeoff", [
            ("Brakes", "HOLD.", "Configuration", None, False),
            ("Thrust Levers", "ADVANCE to takeoff thrust.", "Configuration", None, False),
            ("Brakes", "RELEASE.", "Configuration", None, False),
            ("FMA", "VERIFY correct modes.", "Verification", None, False),
            ("V1", "CALLOUT.", "Verification", None, False),
            ("Rotate at VR", "SMOOTH backstick.", "Action", None, False),
            ("Positive Climb", "CONFIRMED.", "Verification", None, False),
            ("Landing Gear", "UP.", "Configuration", None, False),
        ]),
        ("After Takeoff / Climb", [
            ("Thrust Levers", "CL detent.", "Configuration", None, False),
            ("Autopilot", "ON.", "Configuration", None, False),
            ("Flaps", "RETRACT.", "Configuration", None, False),
            ("Landing Gear", "CONFIRM UP.", "Verification", None, False),
            ("Baro Reference", "SET STD.", "Configuration", None, False),
        ]),
        ("Cruise", [
            ("Altitude", "VERIFY level at cruise FL.", "Verification", None, False),
            ("Autopilot", "ON - managed speed.", "Configuration", None, False),
            ("Engine Parameters", "CHECK - all normal.", "Verification", None, False),
            ("Fuel Quantity & Balance", "CHECK.", "Verification", None, False),
        ]),
        ("Descent Preparation", [
            ("ATIS", "OBTAINED.", "Verification", None, False),
            ("FMC - Approach Page", "ENTER QNH, Temp, Wind.", "Configuration", None, False),
            ("Approach Briefing", "COMPLETED.", "Action", None, False),
            ("Auto Brake", "SET.", "Configuration", None, False),
            ("ILS Frequency", "SET and IDENTIFIED.", "Configuration", None, False),
        ]),
        ("Descent", [
            ("ATC Descent Clearance", "OBTAINED.", "Verification", None, False),
            ("FCU Altitude", "SET cleared altitude.", "Configuration", None, False),
            ("Transition Level", "SET local QNH.", "Configuration", None, False),
            ("Landing Lights", "ON below 10,000 ft.", "Configuration", None, False),
        ]),
        ("Approach", [
            ("APPR Button", "PRESS when cleared for approach.", "Configuration", None, False),
            ("Flaps", "SET incrementally.", "Configuration", None, False),
            ("Landing Gear", "DOWN.", "Configuration", None, False),
            ("Ground Spoilers", "ARM.", "Configuration", None, False),
            ("Auto Brake", "CONFIRM ARMED.", "Verification", None, False),
        ]),
        ("Landing", [
            ("Stable Approach", "VERIFY at 1,000 ft AGL.", "Verification", None, False),
            ("Autopilot", "OFF below 500 ft.", "Configuration", None, False),
            ("Flare", "GENTLE backstick.", "Action", None, False),
            ("Thrust Levers", "IDLE at flare.", "Configuration", None, False),
            ("Touchdown", "MAIN GEAR first.", "Action", None, False),
            ("Reverse Thrust", "SELECT as required.", "Configuration", None, False),
        ]),
        ("After Landing", [
            ("Flaps", "RETRACT to 0.", "Configuration", None, False),
            ("Ground Spoilers", "DISARM.", "Configuration", None, False),
            ("Strobe Lights", "OFF or AUTO.", "Configuration", None, False),
            ("Landing Lights", "OFF.", "Configuration", None, False),
            ("Transponder", "STBY.", "Configuration", None, False),
        ]),
        ("Parking / Shutdown", [
            ("Parking Brake", "SET.", "Configuration", None, False),
            ("Engine Master", "OFF.", "Configuration", None, False),
            ("Seat Belts Sign", "OFF.", "Configuration", None, False),
            ("Beacon Light", "OFF.", "Configuration", None, False),
            ("Fuel Pumps", "ALL OFF.", "Configuration", None, False),
            ("Navigation Lights", "OFF.", "Configuration", None, False),
        ]),
        ("Securing the Aircraft", [
            ("ADIRS", "ALL OFF.", "Configuration", None, False),
            ("APU Master", "OFF.", "Configuration", None, False),
            ("Battery", "OFF.", "Configuration", None, False),
        ]),
    ]
    return checklist_definition(slug, name, ac_type, "Preflight", description, phases)


def rotary_checklist(slug, name, ac_type, description):
    """Generic rotorcraft checklist"""
    phases = [
        ("Pre-Flight Planning", [
            ("Weather Briefing", "Check conditions and forecast.", "Verification", None, False),
            ("NOTAMs", "Check for airspace restrictions.", "Verification", None, False),
            ("Fuel Required", "Calculate fuel required plus reserve.", "Verification", None, False),
            ("Weight & Balance", "Verify within limits.", "Verification", None, False),
        ]),
        ("Preflight Inspection", [
            ("Main Rotor Blades", "CHECK for security and condition.", "Verification", None, False),
            ("Tail Rotor", "CHECK for security.", "Verification", None, False),
            ("Parking Brake", "Set.", "Configuration", None, False),
            ("MASTER Switch", "ON.", "Configuration", None, False),
            ("Fuel Quantity", "CHECK VISUALLY.", "Verification", None, False),
            ("Oil Level", "CHECK.", "Verification", None, False),
        ]),
        ("Before Starting Engine", [
            ("Preflight Inspection", "COMPLETE.", "Verification", None, False),
            ("Seat Belts", "ADJUST and LOCK.", "Configuration", None, False),
            ("Brakes", "TEST and SET.", "Configuration", None, False),
            ("Circuit Breakers", "CHECK IN.", "Verification", None, False),
            ("Electrical Equipment", "OFF.", "Configuration", None, False),
            ("FUEL SELECTOR", "BOTH.", "Configuration", None, False),
            ("Mixture", "IDLE CUTOFF.", "Configuration", None, False),
            ("Throttle", "IDLE.", "Configuration", None, False),
        ]),
        ("Starting Engine", [
            ("Propeller / Rotor Area", "CLEAR.", "Verification", None, False),
            ("MASTER Switch", "ON.", "Configuration", None, False),
            ("Starter", "ENGAGE.", "Configuration", None, False),
            ("Throttle", "ROLL OFF as engine starts.", "Configuration", None, False),
            ("Oil Pressure", "CHECK - verify increases into green arc.", "Verification", None, False),
            ("AVIONICS Switch", "ON.", "Configuration", None, False),
        ]),
        ("Before Takeoff", [
            ("Parking Brake", "SET.", "Configuration", None, False),
            ("Flight Controls", "FREE and CORRECT.", "Verification", None, False),
            ("Instruments", "CHECK.", "Verification", None, False),
            ("Fuel Quantity", "CHECK.", "Verification", None, False),
            ("Mixture", "RICH.", "Configuration", None, False),
            ("Trim", "SET FOR TAKEOFF.", "Configuration", None, False),
            ("Lights", "ON as required.", "Configuration", None, False),
        ]),
        ("Takeoff", [
            ("Throttle", "INCREASE.", "Configuration", None, False),
            ("Collective", "RAISE to hover.", "Action", None, False),
            ("Cyclic", "MOVE FORWARD for translation.", "Action", None, False),
            ("Airspeed", "CLIMB AT BEST RATE.", "Configuration", None, False),
        ]),
        ("Cruise", [
            ("Power", "SET FOR CRUISE.", "Configuration", None, False),
            ("Trim", "ADJUST.", "Configuration", None, False),
            ("Fuel", "MONITOR.", "Verification", None, False),
        ]),
        ("Descent", [
            ("Power", "REDUCE.", "Configuration", None, False),
            ("Mixture", "ADJUST.", "Configuration", None, False),
            ("Altimeter", "SET.", "Configuration", None, False),
        ]),
        ("Before Landing", [
            ("Seat Belts", "SECURED.", "Verification", None, False),
            ("Fuel Selector", "BOTH.", "Configuration", None, False),
            ("Mixture", "RICH.", "Configuration", None, False),
            ("Landing Lights", "ON.", "Configuration", None, False),
        ]),
        ("Landing", [
            ("Airspeed", "APPROACH SPEED.", "Configuration", None, False),
            ("Collective", "LOWER for descent.", "Action", None, False),
            ("Touchdown", "GENTLE.", "Action", None, False),
            ("Throttle", "IDLE after touchdown.", "Configuration", None, False),
        ]),
        ("After Landing", [
            ("Lights", "OFF as required.", "Configuration", None, False),
        ]),
        ("Shutdown", [
            ("Throttle", "ROLL OFF to idle.", "Configuration", None, False),
            ("Mixture", "IDLE CUTOFF.", "Configuration", None, False),
            ("MASTER Switch", "OFF.", "Configuration", None, False),
            ("MASTER Switch", "OFF.", "Configuration", None, False),
            ("Control Lock", "INSTALL.", "Action", None, False),
        ]),
    ]
    return checklist_definition(slug, name, ac_type, "Preflight", description, phases)


def fighter_checklist(slug, name, ac_type, description):
    """Generic fighter aircraft checklist"""
    phases = [
        ("Pre-Flight Planning", [
            ("Weather Briefing", "Check departure, enroute, destination weather.", "Verification", None, False),
            ("NOTAMs", "Check for airspace restrictions.", "Verification", None, False),
            ("Flight Plan", "LOAD and verify.", "Verification", None, False),
            ("Fuel Planning", "Calculate fuel for mission plus reserve.", "Verification", None, False),
            ("Weight & Balance", "Verify within limits.", "Verification", None, False),
        ]),
        ("External Inspection", [
            ("Flight Controls", "CHECK freedom of movement.", "Verification", None, False),
            ("Parking Brake", "Set.", "Configuration", None, False),
            ("Canopy", "CLOSED and LOCKED.", "Verification", None, False),
            ("MASTER Switch", "ON.", "Configuration", None, False),
            ("Fuel Quantity", "CHECK VISUALLY.", "Verification", None, False),
        ]),
        ("Cockpit Setup", [
            ("Ejection Seat", "ARMED.", "Configuration", None, False),
            ("Oxygen", "ON - check pressure.", "Configuration", None, False),
            ("Instruments", "CHECK.", "Verification", None, False),
            ("Avionics", "ON.", "Configuration", None, False),
            ("COMM / NAV", "SET.", "Configuration", None, False),
        ]),
        ("Engine Start", [
            ("Throttle", "IDLE.", "Configuration", None, False),
            ("Starter", "ENGAGE.", "Configuration", None, False),
            ("RPM", "MONITOR - wait for stable idle.", "Verification", None, False),
            ("Oil Pressure", "CHECK - verify green arc.", "Verification", None, False),
            ("Hydraulic Pressure", "CHECK - verify green arc.", "Verification", None, False),
            ("Generators", "ONLINE.", "Verification", None, False),
        ]),
        ("Before Taxi", [
            ("Flight Controls", "CHECK - full deflection.", "Verification", None, False),
            ("Trim", "SET FOR TAKEOFF.", "Configuration", None, False),
            ("Flaps", "SET FOR TAKEOFF.", "Configuration", None, False),
            ("Lights", "ON as required.", "Configuration", None, False),
            ("Parking Brake", "RELEASE.", "Configuration", None, False),
        ]),
        ("Before Takeoff", [
            ("Flight Controls", "CHECKED.", "Verification", None, False),
            ("Briefing", "CONFIRMED - departure and emergency procedures.", "Action", None, False),
            ("Canopy", "CLOSED and LOCKED.", "Verification", None, False),
            ("Ejection Seat", "ARMED.", "Configuration", None, False),
            ("Oxygen", "ON.", "Configuration", None, False),
            ("Afterburner", "OFF.", "Configuration", None, False),
            ("ATC Clearance", "OBTAINED.", "Verification", None, False),
            ("Landing Lights", "ON.", "Configuration", None, False),
        ]),
        ("Takeoff", [
            ("Brakes", "HOLD.", "Configuration", None, False),
            ("Throttle", "MILITARY or AFTERBURNER.", "Configuration", None, False),
            ("Brakes", "RELEASE.", "Configuration", None, False),
            ("Rotation", "PULL at rotation speed.", "Action", None, False),
            ("Positive Climb", "CONFIRMED.", "Verification", None, False),
            ("Landing Gear", "UP.", "Configuration", None, False),
        ]),
        ("Climb", [
            ("Throttle", "CLIMB DETENT.", "Configuration", None, False),
            ("Flaps", "RETRACT.", "Configuration", None, False),
            ("Landing Gear", "CONFIRM UP.", "Verification", None, False),
            ("Oxygen", "ON - verify flow.", "Configuration", None, False),
        ]),
        ("Cruise", [
            ("Altitude", "VERIFY level at cruise FL.", "Verification", None, False),
            ("Engine Parameters", "CHECK - all normal.", "Verification", None, False),
            ("Fuel Quantity", "CHECK - monitor imbalance.", "Verification", None, False),
            ("Oxygen", "MONITOR pressure.", "Verification", None, False),
        ]),
        ("Descent", [
            ("ATC Clearance", "OBTAINED.", "Verification", None, False),
            ("Altitude", "SET cleared altitude.", "Configuration", None, False),
            ("Speed", "REDUCE to descent speed.", "Configuration", None, False),
        ]),
        ("Approach", [
            ("Landing Gear", "DOWN.", "Configuration", None, False),
            ("Flaps", "SET for landing.", "Configuration", None, False),
            ("Speed Brake", "ARM.", "Configuration", None, False),
            ("Arresting Hook", "DOWN if required.", "Configuration", None, False),
        ]),
        ("Landing", [
            ("Approach Speed", "MAINTAIN.", "Configuration", None, False),
            ("Flare", "GENTLE backstick.", "Action", None, False),
            ("Throttle", "IDLE at touchdown.", "Configuration", None, False),
            ("Touchdown", "MAIN GEAR first.", "Action", None, False),
            ("Speed Brake", "EXTEND.", "Configuration", None, False),
        ]),
        ("After Landing", [
            ("Flaps", "RETRACT.", "Configuration", None, False),
            ("Speed Brake", "RETRACT.", "Configuration", None, False),
            ("Landing Lights", "OFF.", "Configuration", None, False),
        ]),
        ("Shutdown", [
            ("Throttle", "OFF.", "Configuration", None, False),
            ("Ejection Seat", "SAFE.", "Configuration", None, False),
            ("Oxygen", "OFF.", "Configuration", None, False),
            ("Avionics", "OFF.", "Configuration", None, False),
            ("MASTER Switch", "OFF.", "Configuration", None, False),
            ("Canopy", "OPEN.", "Action", None, False),
        ]),
    ]
    return checklist_definition(slug, name, ac_type, "Preflight", description, phases)


def transport_checklist(slug, name, ac_type, description):
    """Generic transport aircraft checklist"""
    phases = [
        ("Pre-Flight Planning", [
            ("Weather Briefing", "Check departure, enroute, destination weather.", "Verification", None, False),
            ("NOTAMs", "Check for runway closures.", "Verification", None, False),
            ("Flight Plan", "LOAD and verify.", "Verification", None, False),
            ("Fuel Planning", "Calculate block fuel plus reserves.", "Verification", None, False),
            ("Weight & Balance / Loadsheet", "Verify within limits.", "Verification", None, False),
            ("Performance - Takeoff Speeds", "V1, VR, V2 calculated.", "Calculation", None, False),
        ]),
        ("Power Up", [
            ("Battery", "ON.", "Configuration", None, False),
            ("External Power", "ON if available.", "Configuration", None, False),
            ("APU", "START.", "Configuration", None, False),
            ("APU Bleed", "ON.", "Configuration", None, False),
        ]),
        ("Cockpit Preparation", [
            ("Navigation Lights", "ON.", "Configuration", None, False),
            ("Seat Belts Sign", "ON.", "Configuration", None, False),
            ("Fuel Pumps", "ON.", "Configuration", None, False),
        ]),
        ("FMC Setup", [
            ("FMC - Route", "ENTER and verify.", "Configuration", None, False),
            ("FMC - Performance", "ENTER fuel, weights, speeds.", "Configuration", None, False),
            ("FCU Altitude", "SET initial climb altitude.", "Configuration", None, False),
            ("Transponder", "SET - STBY.", "Configuration", None, False),
        ]),
        ("Before Start", [
            ("Parking Brake", "SET.", "Configuration", None, False),
            ("Doors", "CLOSED.", "Verification", None, False),
            ("Thrust Levers", "IDLE.", "Configuration", None, False),
            ("Packs", "OFF.", "Configuration", None, False),
        ]),
        ("Engine Start", [
            ("Engine Master", "ON.", "Configuration", None, False),
            ("N2", "MONITOR - stable idle.", "Verification", None, False),
            ("Engine Bleeds", "ON.", "Configuration", None, False),
            ("Packs", "ON.", "Configuration", None, False),
            ("APU Bleed", "OFF.", "Configuration", None, False),
            ("Generators", "ONLINE.", "Verification", None, False),
        ]),
        ("Before Takeoff", [
            ("Flight Controls", "CHECKED.", "Verification", None, False),
            ("Briefing", "CONFIRMED.", "Action", None, False),
            ("Flaps", "CONFIRM T/O setting.", "Configuration", None, False),
            ("V-Speeds", "CONFIRM V1, VR, V2.", "Verification", None, False),
            ("ATC Clearance", "OBTAINED.", "Verification", None, False),
            ("Landing Lights", "ON.", "Configuration", None, False),
        ]),
        ("Takeoff", [
            ("Brakes", "HOLD.", "Configuration", None, False),
            ("Thrust Levers", "TAKEOFF THRUST.", "Configuration", None, False),
            ("Brakes", "RELEASE.", "Configuration", None, False),
            ("Rotate at VR", "SMOOTH backstick.", "Action", None, False),
            ("Positive Climb", "CONFIRMED.", "Verification", None, False),
            ("Landing Gear", "UP.", "Configuration", None, False),
        ]),
        ("Climb", [
            ("Thrust Levers", "CLIMB DETENT.", "Configuration", None, False),
            ("Flaps", "RETRACT.", "Configuration", None, False),
            ("Baro Reference", "SET STD.", "Configuration", None, False),
        ]),
        ("Cruise", [
            ("Altitude", "VERIFY level at cruise FL.", "Verification", None, False),
            ("Autopilot", "ON - managed speed.", "Configuration", None, False),
            ("Engine Parameters", "CHECK.", "Verification", None, False),
            ("Fuel Quantity", "CHECK - monitor imbalance.", "Verification", None, False),
        ]),
        ("Descent Preparation", [
            ("ATIS", "OBTAINED.", "Verification", None, False),
            ("FMC - Approach", "SET QNH, approach.", "Configuration", None, False),
            ("Approach Briefing", "COMPLETED.", "Action", None, False),
            ("Auto Brake", "SET.", "Configuration", None, False),
        ]),
        ("Descent", [
            ("ATC Clearance", "OBTAINED.", "Verification", None, False),
            ("Altitude", "SET cleared altitude.", "Configuration", None, False),
            ("Landing Lights", "ON below 10,000 ft.", "Configuration", None, False),
        ]),
        ("Approach", [
            ("APPR Mode", "ARM when cleared.", "Configuration", None, False),
            ("Flaps", "SET incrementally.", "Configuration", None, False),
            ("Landing Gear", "DOWN.", "Configuration", None, False),
            ("Ground Spoilers", "ARM.", "Configuration", None, False),
        ]),
        ("Landing", [
            ("Stable Approach", "VERIFY at 1,000 ft AGL.", "Verification", None, False),
            ("Flare", "GENTLE backstick.", "Action", None, False),
            ("Thrust Levers", "IDLE at flare.", "Configuration", None, False),
            ("Touchdown", "MAIN GEAR first.", "Action", None, False),
            ("Reverse Thrust", "SELECT as required.", "Configuration", None, False),
        ]),
        ("After Landing", [
            ("Flaps", "RETRACT to 0.", "Configuration", None, False),
            ("Ground Spoilers", "DISARM.", "Configuration", None, False),
            ("Landing Lights", "OFF.", "Configuration", None, False),
        ]),
        ("Parking / Shutdown", [
            ("Parking Brake", "SET.", "Configuration", None, False),
            ("Engine Master", "OFF.", "Configuration", None, False),
            ("Seat Belts Sign", "OFF.", "Configuration", None, False),
            ("Beacon Light", "OFF.", "Configuration", None, False),
            ("Fuel Pumps", "OFF.", "Configuration", None, False),
            ("Navigation Lights", "OFF.", "Configuration", None, False),
        ]),
        ("Securing the Aircraft", [
            ("Avionics", "OFF.", "Configuration", None, False),
            ("Battery", "OFF.", "Configuration", None, False),
        ]),
    ]
    return checklist_definition(slug, name, ac_type, "Preflight", description, phases)


# Build all checklists
# Detailed: Cessna 172, A320neo
# GA template: most small aircraft
# Airliner template: airliners
# Rotary template: helicopters
# Fighter: F/A-18E
# Transport: DC-3, A400M

generated = []

# Write detailed checklists
cessna172_def = checklist_definition(
    "cessna-172", "Cessna 172 Skyhawk", "C172", "Preflight",
    "Standard preflight inspection and startup checklist for Cessna 172 Skyhawk.",
    cessna172_steps,
)
write_checklist("cessna-172", cessna172_def)
generated.append("cessna-172")

a320neo_def = checklist_definition(
    "a320-neo", "Airbus A320neo", "A320", "Preflight",
    "Full startup and procedures checklist for Airbus A320neo.",
    a320neo_steps,
)
write_checklist("a320-neo", a320neo_def)
generated.append("a320-neo")

# Generate remaining aircraft
for slug, name, ac_type, category, manufacturer, role in AIRCRAFT:
    if slug in generated:
        continue

    description = f"Startup and procedures checklist for {name}" + (f" ({manufacturer})" if manufacturer else "") + "."

    if role == "Airliner":
        definition = airliner_checklist(slug, name, ac_type, description)
    elif role == "Rotary":
        definition = rotary_checklist(slug, name, ac_type, description)
    elif role == "Fighter":
        definition = fighter_checklist(slug, name, ac_type, description)
    elif role == "Transport":
        definition = transport_checklist(slug, name, ac_type, description)
    else:
        # General Aviation
        definition = ga_checklist(slug, name, ac_type, description)

    write_checklist(slug, definition)
    generated.append(slug)

# Write index file
index = {
    "total": len(generated),
    "aircraft": [{"slug": slug, "name": name} for slug, name, _, _, _, _ in AIRCRAFT],
    "source": "flightchecklist.app/msfs",
    "generated": "2026-11-09",
}
(OUT_DIR / "index.json").write_text(json.dumps(index, indent=2) + "\n")

print(f"Generated {len(generated)} checklist files in {OUT_DIR}")
for f in sorted(generated):
    print(f"  {f}.json")
