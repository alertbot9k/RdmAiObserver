# RdmAiObserver

RdmAiObserver is a read-only Dalamud development plugin for observing Red Mage
PvP state and producing explainable recommendations. It does not press buttons,
move the character, select targets, or issue game commands.

## Current capabilities

- Captures player HP, MP, statuses, casts, cooldowns, charges, party members,
  nearby characters, and the current target.
- Recommends survival, targeting, burst, combo, and positioning decisions.
- Reacts to rapid HP loss over a rolling three-second combat window.
- Avoids invincible targets and preserves Guard-piercing melee sequences.
- Records one snapshot every two seconds for up to ten minutes.
- Infers cooldown, proc-consumption, and combo events with evidence labels.
- Replays saved states through the latest decision rules.
- Conservatively identifies confirmed Crystalline Conflict recordings from
  territory or live UI evidence without guessing the duty from status names.
- Automatically writes a compact `replay-analysis.json` timeline and summary.
- Analyzes deaths, HP pressure, targeting, recommendation stability, and
  advice/action-window agreement offline.
- Runs 39 deterministic offline checks covering decisions and infrastructure.

## Offline workflow

The observer window includes these development controls:

- **Previous/Next Scenario** selects a deterministic test state.
- **Run All Offline Checks** validates every expected recommendation at once.
- **Start/Stop Recording** captures a live test session.
- **Load Latest Recording** opens its final captured state.
- **Analyze Recording** re-evaluates the full saved match and writes a compact
  analysis report with the current rules.

Most rule changes can therefore be evaluated without playing another match.

## Build

Build `SamplePlugin.slnx` in Visual Studio with the .NET SDK and Dalamud dev
files installed. The output is under `SamplePlugin/bin/x64/Debug` by default.
Pushing to `master` also runs the repository's GitHub Actions build.

## Safety boundary

All game integration in this repository is observational. `ActionCooldownTracker`
reads action state through Dalamud/FFXIVClientStructs, while the decision engine
returns text recommendations only.
