# AICopilotFS Features Integration Plan

## Goal
Integrate all AICopilotFS configuration features into StormATC as an opt-in feature group, activated via a UI toggle button in Settings.

## Summary of Changes

### New Option Classes (StormATC.Application.Options)
1. **FlightOptions** - Default cruise speed, climb/descent rates
2. **TerrainOptions** - Terrain analysis sampling, grid, look-ahead parameters
3. **ContinuousFlightOptions** - Continuous flight mission parameters (range, service time, reserve)
4. **ChecklistOptions** - Checklist system enablement and directory

### Settings UI Integration
- Add "AI Copilot Features" toggle button in Services tab
- When enabled, show new collapsible sections for each option group
- Follow existing StormATC patterns for settings persistence

### Dependency Injection
- Register new options in ServiceCollectionExtensions.AddOptions()
- No new service implementations needed (options are configuration-only)

## Implementation Tasks

### 1. Add New Option Classes
**File:** `src/StormATC.Application/Options/StormAtcOptions.cs` (append to file)

Create four new sealed classes following existing patterns:
- `FlightOptions` with SectionName = "Flight"
- `TerrainOptions` with SectionName = "Terrain"  
- `ContinuousFlightOptions` with SectionName = "ContinuousFlight"
- `ChecklistOptions` with SectionName = "Checklist"

Each with proper validation attributes and defaults matching AICopilotFS.Configuration.cs

### 2. Register Options in DI
**File:** `src/StormATC.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`

In `AddOptions()` method, add registrations for all four new option classes:
- Bind to configuration sections
- Add ValidateDataAnnotations()
- Add ValidateOnStart()

### 3. Extend SettingsViewModel
**File:** `src/StormATC.Desktop/ViewModels/SettingsViewModel.cs`

Add:
- `EnableAICopilotFeatures` property (bool, binds to new option)
- Properties for all new option fields (e.g., `DefaultCruiseSpeedKt`, `PreFlightSampleSpacingNm`, etc.)
- Load from IOptions<> in constructor
- Include in `SaveSettingsAsync()` command
- Add observable collections for any enum/combo selections

### 4. Extend SettingsView XAML
**File:** `src/StormATC.Desktop/Views/SettingsView.xaml`

In Services tab, after "Provider configuration" section:
- Add toggle button "Enable AI Copilot Features" bound to `EnableAICopilotFeatures`
- Add collapsible panel (Visibility bound to EnableAICopilotFeatures)
- Inside panel, add sections for:
  - Flight Performance (cruise speed, climb/descent rates)
  - Terrain Analysis (sampling, grid, look-ahead)
  - Continuous Flight (range, service time, reserve factor)
  - Checklists (enabled, directory)
- Follow existing card/style patterns

### 5. Add Option to UserSettingsService Persistence
The SaveSettingsAsync in SettingsViewModel already uses UserSettingsService.SaveManyAsync with section/key pairs - new options will be automatically persisted when added to the save list.

## Configuration Section Names
| Option Class | Section Name |
|--------------|--------------|
| FlightOptions | "Flight" |
| TerrainOptions | "Terrain" |
| ContinuousFlightOptions | "ContinuousFlight" |
| ChecklistOptions | "Checklist" |

## Default Values (from AICopilotFS)
| Option | Default |
|--------|---------|
| DefaultCruiseSpeedKt | 140 |
| DefaultClimbRateFpm | 1200 |
| DefaultDescentRateFpm | 1500 |
| PreFlightSampleSpacingNm | 8.0 |
| InFlightGridWidthPoints | 10 |
| InFlightGridHeightPoints | 5 |
| InFlightLookAheadNm | 15.0 |
| InFlightUpdateInterval | 30 seconds |
| MinRangeNm | 50 |
| MaxRangeNm | 500 |
| ServiceTimeMinutes | 30 |
| ReserveFactor | 0.9 |
| Checklist Enabled | true |
| ChecklistDirectory | "flightchecklists" |

## Validation Rules
- Flight speeds: positive values
- Terrain intervals: positive TimeSpan, reasonable ranges
- Continuous flight ranges: MinRange < MaxRange
- ReserveFactor: 0.0 to 1.0
- ChecklistDirectory: non-empty string

## Testing
- Verify new options appear in Settings when toggle is enabled
- Verify settings persist to usersettings.json
- Verify options load correctly on restart
- Verify validation works for invalid values
- Run existing test suite to ensure no regressions

## Out of Scope
- Actual implementation of flight/terrain/continuous flight logic (this is configuration only)
- Checklist execution engine (only configuration)
- Any changes to domain logic or simulator connectors