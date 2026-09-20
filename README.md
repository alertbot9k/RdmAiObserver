# RdmAiObserver

RdmAiObserver is a read-only Dalamud development plugin for observing Red Mage
PvP state and producing explainable recommendations. It does not press buttons,
move the character, select targets, or issue game commands.

## Current capabilities

- Captures player HP, MP, statuses, casts, cooldowns, charges, party members,
  nearby characters, and the current target.
- Recommends survival, targeting, burst, combo, and positioning decisions.
- Records one snapshot every two seconds for up to ten minutes.
- Infers reliable cooldown and proc-consumption events without executing actions.
- Replays saved states through the latest decision rules.
- Analyzes recommendation stability and advice/action-window agreement offline.
- Includes deterministic offline scenarios with expected PASS/FAIL results.

## Offline workflow

The observer window includes these development controls:

- **Previous/Next Scenario** selects a deterministic test state.
- **Run All Offline Checks** validates every expected recommendation at once.
- **Start/Stop Recording** captures a live test session.
- **Load Latest Recording** opens its final captured state.
- **Analyze Recording** re-evaluates the full saved match with the current rules.

Most rule changes can therefore be evaluated without playing another match.

## Build

Build `SamplePlugin.slnx` in Visual Studio with the .NET SDK and Dalamud dev
files installed. The output is under `SamplePlugin/bin/x64/Debug` by default.
Pushing to `master` also runs the repository's GitHub Actions build.

## Safety boundary

All game integration in this repository is observational. `ActionCooldownTracker`
reads action state through Dalamud/FFXIVClientStructs, while the decision engine
returns text recommendations only.
