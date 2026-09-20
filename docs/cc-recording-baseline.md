# Crystalline Conflict recording baseline

This baseline summarizes the three confirmed Crystalline Conflict recordings
used to calibrate the observer. Raw player names and state files are not stored
in the repository.

| Recording | CC snapshots | Deaths | Protected | Isolated | Target >25y | Engaged no-target | Probable Recuperate | Safe Elixir snapshots |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 147 | 2 | 18 | 7 | 19 | 5 / 104 (5%) | 9 | 0 |
| 2 | 121 | 2 | 34 | 10 | 8 | 2 / 64 (3%) | 6 | 2 |
| 3 | 185 | 4 | 18 | 12 | 39 | 1 / 99 (1%) | 7 | 0 |

Definitions:

- **Protected**: player alive with the `Invincibility` spawn-protection status.
- **Isolated**: at least two live opponents and no living ally inside 15 yalms.
- **Target >25y**: a selected target beyond Red Mage spell range while the
  player is not spawn-protected.
- **Engaged no-target**: no selected target while a live opponent is within 25
  yalms, excluding spawn protection.
- **Probable Recuperate**: at least 1,500 net MP loss and 6,000 HP recovery
  between two-second samples, excluding spawn protection and observed Purify.
- **Safe Elixir snapshot**: no live opponent within 25 yalms while HP is at
  most 70% or MP is at most 4,000. Recording 2 contains two consecutive samples
  from one observed Standard-issue Elixir cast (action ID `29055`).

Calibration conclusions:

1. Missing targets during actual engagements are uncommon; earlier totals were
   inflated by spawn and respawn downtime.
2. Out-of-range recommendations are a meaningful failure mode and must be
   checked before consuming Prefulgence, Vice of Thorns, or Grand Impact.
3. Isolation occurs often enough to require an explicit anti-commit rule before
   opening Embolden, Resolution, or a fresh melee burst.
4. Territory `1293` is confirmed Crystalline Conflict. Combat status names,
   including `Frontline March`, are not duty identifiers.
5. The observed Elixir casts support recommending the 4.5-second recovery only
   when the full 25-yalm threat scan is clear.
