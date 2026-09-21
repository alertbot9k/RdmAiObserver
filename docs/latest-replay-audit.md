# Latest available recording audit

Audited with the `0.4.2` offline logic on 2026-09-21.

## Recording coverage

- 225 snapshots over 7.51 minutes, starting outside the duty and ending after
  returning outside it.
- 213/213 arena snapshots contain Tactical Crystal evidence; its observed movement
  span is 76.9 yalms.
- Sprint readiness is present in all 225 snapshots.
- Lifecycle: `OutsideCc -> Countdown -> Active -> DeadRespawning -> Active -> Results -> Exited -> OutsideCc`.
- 1 death, 1 respawn, 25 direct target changes, and 7 engagement starts / 7 ends.
- Recording diagnostics report no completeness problems.

## Recommendation stability

No unresolved contradictory transitions or rapid reversals remain after accounting
for target identity, range thresholds, mitigation changes, deaths, and urgent
survival responses.

## Sprint

- 20 distinct recommended-use opportunities; 12 were not followed by observed Sprint.
- Estimated Sprint-active time: 126.8 seconds.
- 12 likely combat cancellations.

## Action agreement

The analyzer inferred 39 reliable action windows and found 7 direct recommendation
matches (17.9%). Stronger individual agreement appeared for Standard-issue Elixir
(1/1), Enchanted Zwerchhau (1/1), Prefulgence (2/3), and Embolden (2/4).

This is a conservative text/action agreement measure, not an accuracy score: the
observer emits one highest-priority recommendation while multiple actions can occur
within each two-second sampling interval.

## Shadow-policy results

- 143 planned and 26 observe-only snapshots.
- 145 simulated commands, 55 verified transitions, 17 failed transitions, and 73
  transitions whose effect was not observable at the snapshot interval.
- No emergency stop was triggered.
