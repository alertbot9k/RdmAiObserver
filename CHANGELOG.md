# Changelog

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
