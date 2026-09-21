# Latest available recording audit

Audited with the `0.4.0` offline logic on 2026-09-21. Only one raw recording was
present under the XIVLauncher plugin configuration when the repository and plugin
configuration trees were inventoried. Earlier attachments had been overwritten and
could not be replayed again.

## Recording coverage

- 87 snapshots over 173.2 seconds, identified as Crystalline Conflict.
- 1 death and no completed respawn before recording ended.
- 6 direct target changes and 3 engagement starts / 3 engagement ends.
- Crystal evidence exists in 4 snapshots; 83 snapshots predate continuous territory
  capture and are diagnosed as missing crystal evidence.
- Sprint status produced an estimated 77.5 seconds active and 5 likely combat
  cancellations. Sprint readiness was not captured in this older recording, so no
  recommended or missed Sprint opportunities can be scored reliably.

## Recommendation stability

The initial audit found apparent reversals around urgent Purify/Recuperate decisions,
target loss, Guard transitions, and the 25-yalm range boundary. Those are supported
state changes rather than contradictions and are now excluded from instability
classification. The remaining Resolution / reposition / Resolution sequence was
caused by target distance changing from 19.7 to 25.9 to 16.6 yalms, so it too is a
supported range-boundary transition. After contextual filtering, the recording has
no unresolved contradictory or unstable recommendations.

## Action agreement

The analyzer inferred 12 reliable action windows and found 1 direct text match
(8.3%). By action: Grand Impact 1/4 matched; Resolution 0/2; Recuperate 0/2; Guard,
Embolden, Corps-a-corps, and Enchanted Riposte 0/1 each. This is not an accuracy score:
the observer recommends one highest-priority action per two-second snapshot, while
multiple off-global-cooldown actions can occur inside a sampling interval. The low
agreement is retained as a calibration warning and should not be optimized blindly.

## Shadow-policy results

- 50 planned snapshots and 10 observe-only snapshots.
- 31 verified transitions, 4 failed transitions, and 15 transitions not observable
  at the recording interval.
- Three failures were unobserved requested-target changes; one was a movement request
  without player displacement in the next snapshot.
- No emergency stop was triggered.
