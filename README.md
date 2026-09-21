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
- Preserves ranged procs outside 25 yalms and avoids opening new burst windows
  while isolated against multiple opponents.
- Recommends Standard-issue Elixir only in clear recovery windows and recognizes
  observed Elixir casts in old and new recordings.
- Treats a living selected target as a nearby threat even when that target is
  absent from the nearby-character list, and requires observed Elixir readiness
  before recommending a new cast.
- Records one snapshot every two seconds for up to ten minutes.
- Infers cooldown, proc-consumption, combo, and medium-confidence Recuperate
  events with evidence labels.
- Replays saved states through the latest decision and action-inference rules.
- Conservatively identifies confirmed Crystalline Conflict recordings from
  territory or live UI evidence without guessing the duty from status names.
- Automatically writes a compact `replay-analysis.json` timeline and summary.
- Analyzes deaths, HP pressure, isolation, spawn protection, target range,
  engaged-time targeting, and advice/action-window agreement offline.
- Tolerates legacy or incomplete replay frames with a missing timestamp during
  combat-trend analysis.
- Runs 57 deterministic offline checks covering decisions and infrastructure in
  a standalone project, without launching FFXIV.

## Architecture

`SamplePlugin` owns Dalamud services, game-state capture, the recorder, and the
observer window. `RdmAiObserver.Core` compiles the game-state models, combat
decisions, target selection, scenarios, action inference, and replay analysis
without Dalamud references. The plugin references the same core assembly that
the offline checks use. `RdmAiObserver.Tests` runs the existing scenario and
infrastructure checks as a command-line regression suite.

## Offline workflow

The observer window includes these development controls:

- **Previous/Next Scenario** selects a deterministic test state.
- **Run All Offline Checks** validates every expected recommendation at once.
- **Start/Stop Recording** captures a live test session.
- **Load Latest Recording** opens its final captured state.
- **Analyze Recording** re-evaluates the full saved match and writes a compact
  analysis report with the current rules.

Most rule changes can therefore be evaluated without playing another match.
The current data-derived thresholds are documented in
[`docs/cc-recording-baseline.md`](docs/cc-recording-baseline.md).

## Build and test

With the .NET 10 SDK on Linux, macOS, or Windows, run the same portable build
and offline checks used by GitHub Actions:

```sh
dotnet build RdmAiObserver.Core.slnx --configuration Release
dotnet test RdmAiObserver.Core.slnx --configuration Release --no-build
```

The test runner reports each regression case and returns a nonzero exit status
on failure. To build the plugin on Windows, install the Dalamud developer files
or set `DALAMUD_HOME` to their directory, then run:

```sh
dotnet build SamplePlugin.slnx --configuration Release
```

The output is under `SamplePlugin/bin/x64/Release`. GitHub Actions runs the
portable checks on Linux and the plugin build on Windows. The downloaded
Dalamud distribution and the plugin still need Windows and in-game validation
after game or Dalamud API updates.

## Safety boundary

All game integration in this repository is observational. `ActionCooldownTracker`
reads action state through Dalamud/FFXIVClientStructs, while the decision engine
returns text recommendations only.
