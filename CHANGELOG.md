# Changelog

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
