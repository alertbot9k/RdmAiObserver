# Changelog

## Unreleased

- Count a living selected target as a nearby threat when it is missing from
  the nearby-character list, without counting the same observed target twice.
- Require observed Elixir readiness before suggesting a new cast.
- Keep replay trend analysis safe when an incomplete frame lacks a timestamp.
- Added offline regression coverage for these cases.

## 0.2.3

- Added safe Standard-issue Elixir recovery advice using the 25-yalm threat scan.
- Added direct Elixir cast inference for existing and future recordings.
- Added Elixir cooldown availability tracking and replay opportunity metrics.
- Added finish/cancel guidance when an Elixir cast is already in progress.
- Expanded automatic offline validation to 57 checks.

## 0.2.2

- Prevented out-of-range proc recommendations beyond the 25-yalm spell limit.
- Added anti-isolation advice when multiple opponents are nearby without allies.
- Added protected-respawn regroup advice and dead-target replacement.
- Excluded dead/noncombat characters from nearby-enemy pressure counts.
- Added protection, isolation, and target-range replay metrics.
- Documented an anonymized three-match CC calibration baseline.
- Expanded automatic offline validation to 51 checks.

## 0.2.1

- Prevented ranged procs from being recommended into Guard or Invincibility.
- Added evidence-labeled Recuperate inference from matching MP and HP changes.
- Recomputes action evidence from raw states when older recordings are analyzed.
- Restricted no-target analysis to snapshots with a nearby live opponent.
- Excluded invincibility/respawn protection from engaged-time targeting metrics.
- Expanded automatic offline validation to 42 checks.

## 0.2.0

- Added rolling three-second HP-loss detection and rapid-burst defense advice.
- Added conservative Crystalline Conflict detection from territory/UI evidence;
  combat status names are never used to infer the duty.
- Added invincibility-aware target selection.
- Added compact `replay-analysis.json` reports with recommendation/action events.
- Added death, respawn, HP, targeting, proc, and recommendation metrics.
- Added evidence and confidence fields to inferred action events.
- Extracted action inference and recording models from the UI layer.
- Expanded automatic offline validation to 39 checks.
- Preserved read-only operation; no action execution or game control was added.
