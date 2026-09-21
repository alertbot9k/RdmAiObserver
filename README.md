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
- Stores stable object identities when the game exposes them, while retaining
  compatibility with older name-based recordings.
- Infers cooldown, proc-consumption, combo, and medium-confidence Recuperate
  events with evidence labels.
- Replays saved states through the latest decision and action-inference rules.
- Conservatively identifies confirmed Crystalline Conflict recordings from
  territory or live UI evidence without guessing the duty from status names.
- Classifies observed lifecycle as loading, active, protected, respawning, or
  unknown so downstream rules can abstain when the match state is uncertain.
- With an explicitly timestamped stale capture, the decision layer abstains and
  reports that a fresh observation is required.
- Automatically writes a compact `replay-analysis.json` timeline and summary.
- Analyzes deaths, HP pressure, isolation, spawn protection, target range,
  engaged-time targeting, and advice/action-window agreement offline.
- Tolerates legacy or incomplete replay frames with a missing timestamp during
  combat-trend analysis.
- Runs 57 deterministic scenario checks plus 99 automated tests covering decisions,
  normalization, and replay infrastructure in a standalone project, without
  launching FFXIV.
- Converts recommendations into typed shadow commands for actions, target
  selection, movement, and cast cancellation instead of treating every decision
  as an action press.
- Replays typed policy commands against recorded transitions, verifies every
  command in multi-step plans, and writes `policy-simulation.json` without
  sending game input.
- Separates verified, failed, and not-observable transitions so actions without
  a reliable next-snapshot effect are never reported as successful.
- Records bounded non-player world-object identities and positions during
  confirmed Crystalline Conflict sessions so the moving crystal can be
  identified from evidence rather than a localized name guess.
- Recognizes the confirmed Tactical Crystal BaseId and records its position,
  player distance, nearby ally/enemy counts, and contested state.
- Tracks PvP Sprint readiness and active status, with separate recommendations
  for safe crystal travel, regrouping, isolated disengagement, and holding it
  during immediate combat.

## Architecture

`SamplePlugin` owns Dalamud services, game-state capture, the recorder, and the
observer window. `ObservationFacts` is the platform-independent normalization
boundary: it turns a raw snapshot into explicit mode evidence, freshness,
threat, protection, and action-readiness facts. `RdmAiObserver.Core` compiles
the game-state models, normalized facts, combat decisions, target selection,
scenarios, action inference, and replay analysis without Dalamud references.
The plugin references the same core assembly that the offline checks use.
`RdmAiObserver.Tests` runs the scenario and infrastructure checks as a
command-line regression suite.

## Offline workflow

The observer window includes these development controls:

- **Previous/Next Scenario** selects a deterministic test state.
- **Run All Offline Checks** validates every expected recommendation at once.
- **Start/Stop Recording** captures a live test session.
- **Load Latest Recording** opens its final captured state.
- **Analyze Recording** re-evaluates the full saved match and writes a compact
  analysis report with the current rules.
- **Simulate Policy (No Input)** runs the typed policy and safety layers against
  the recording, shows acceptance and verification metrics, and writes
  `policy-simulation.json`. It never controls the game.

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

To analyze a saved match without launching FFXIV, run:

```sh
dotnet run --project RdmAiObserver.Analyzer -- recorded-states.json analysis-output
```

The command prints replay and shadow-policy metrics and optionally writes fresh
`replay-analysis.json` and `policy-simulation.json` reports to the output folder.

## Safety boundary

All game integration in this repository is observational. `ActionCooldownTracker`
reads action state through Dalamud/FFXIVClientStructs, while the decision engine
returns text recommendations only.
