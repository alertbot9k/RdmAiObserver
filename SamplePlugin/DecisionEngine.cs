using System;
using System.Collections.Generic;

namespace SamplePlugin;

/// <summary>
/// Produces explainable, read-only PvP advice from an observed GameState.
/// It never issues game commands or controls the player.
/// </summary>
public static class DecisionEngine
{
    private const uint CommonActionMpCost = 2000;

    private static readonly HashSet<string> PurifiableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stun", "Heavy", "Bind", "Silence", "Deep Freeze", "Miracle of Nature"
    };

    public static DecisionRecommendation Evaluate(GameState state)
        => Evaluate(state, null);

    public static DecisionRecommendation Evaluate(GameState state, CombatTrend? trend)
    {
        var player = state.Player;
        if (player == null || player.MaxHp == 0)
            return Recommend(DecisionPriority.Wait, "Wait for player data", "The player snapshot is incomplete.");

        var hp = Percent(player.Hp, player.MaxHp);
        var targetHp = state.Target?.Hp is uint current && state.Target.MaxHp is uint maximum && maximum > 0
            ? Percent(current, maximum)
            : (float?)null;
        var nearbyEnemies = CountNearbyEnemies(state, 15f);
        var nearbyEnemiesInSpellRange = CountNearbyEnemies(state, 25f);
        var nearbyAllies = CountNearbyAllies(state, 15f);
        var canSpendMp = player.Mp >= CommonActionMpCost;
        var purifyReady = IsActionAvailable(player.Actions, "Purify");
        var guardReady = IsActionAvailable(player.Actions, "Guard");
        var forteReady = IsActionAvailable(player.Actions, "Forte");
        var corpsReady = IsActionAvailable(player.Actions, "Corps-a-corps");
        var displacementReady = IsActionAvailable(player.Actions, "Displacement");
        var elixirReady = IsActionAvailable(player.Actions, "Standard-issue Elixir");
        var riposteReady = IsActionAvailable(player.Actions, "Enchanted Riposte");
        var emboldenReady = IsActionAvailable(player.Actions, "Embolden");
        var resolutionReady = IsActionKnownAndAvailable(player.Actions, "Resolution");
        var bestTarget = TargetEvaluator.FindBest(state);
        var crowdControl = FindStatus(player.Statuses, PurifiableStatuses);
        var selfGuarding = HasStatus(player.Statuses, "Guard");
        var selfInvincible = HasStatus(player.Statuses, "Invincibility");
        var targetGuarding = HasStatus(state.Target?.Statuses, "Guard");
        var targetInvincible = HasStatus(state.Target?.Statuses, "Invincibility");
        var targetHasMonomachy = HasStatus(state.Target?.Statuses, "Monomachy");
        var dualcastReady = HasStatus(player.Statuses, "Dualcast");
        var prefulgenceReady = HasStatus(player.Statuses, "Prefulgence Ready");
        var thornedFlourish = HasStatus(player.Statuses, "Thorned Flourish");
        var enchantedRiposte = HasStatus(player.Statuses, "Enchanted Riposte");
        var enchantedZwerchhau = HasStatus(player.Statuses, "Enchanted Zwerchhau");
        var enchantedRedoublement = HasStatus(player.Statuses, "Enchanted Redoublement");

        if (player.Hp == 0)
            return Recommend(DecisionPriority.Wait, "Wait for respawn", "The player is incapacitated; combat recommendations are paused.");

        if (selfInvincible)
            return Recommend(DecisionPriority.Wait, "Regroup while protected", "Spawn protection is active; rejoin nearby allies before committing offensive resources.");

        if (selfGuarding)
            return Recommend(DecisionPriority.Defend, "Hold Guard", "Guard is active; avoid canceling its protection with another action.");

        if (crowdControl != null && canSpendMp && purifyReady)
            return Recommend(DecisionPriority.Purify, "Purify", $"{crowdControl} is active and at least {CommonActionMpCost:N0} MP is available.");

        if (player.Cast?.ActionId == PvpActionIds.StandardIssueElixir)
        {
            return nearbyEnemiesInSpellRange == 0
                ? Recommend(DecisionPriority.Recover, "Finish Standard-issue Elixir", "The recovery cast is already in progress and the 25-yalm threat scan remains clear.")
                : Recommend(DecisionPriority.Defend, "Cancel Elixir and move", $"{nearbyEnemiesInSpellRange} live opponent(s) entered 25 yalms during the interruptible recovery cast.");
        }

        if (elixirReady && nearbyEnemiesInSpellRange == 0 &&
            (hp <= 70f || player.Mp <= 4000))
        {
            return Recommend(
                DecisionPriority.Recover,
                "Use Standard-issue Elixir",
                $"No live opponent is within 25 yalms; safely restore from {hp:F0}% HP and {player.Mp:N0} MP before re-engaging.");
        }

        if (trend?.IsRapidDamage == true && hp <= 75f)
        {
            var action = forteReady
                ? "Use Forte"
                : canSpendMp
                    ? "Use Recuperate"
                    : guardReady ? "Use Guard" : "Kite toward your team";
            return Recommend(
                DecisionPriority.Defend,
                action,
                $"HP fell {trend.HpLostPercent:F0}% within {trend.WindowSeconds:F1} seconds; interrupt the offensive sequence and stabilize.");
        }

        if (hp <= 30f && canSpendMp)
            return Recommend(DecisionPriority.Recover, "Use Recuperate", $"HP is critical at {hp:F0}% and Recuperate MP is available.");

        if (hp <= 30f)
        {
            var action = guardReady ? "Guard and disengage" : "Disengage immediately";
            return Recommend(DecisionPriority.Retreat, action, $"HP is critical at {hp:F0}% but there is not enough MP for Recuperate.");
        }

        if (hp <= 50f && nearbyEnemies >= 2)
        {
            var action = forteReady
                ? "Use Forte"
                : canSpendMp
                    ? "Use Recuperate"
                    : guardReady ? "Use Guard" : "Kite toward your team";
            return Recommend(DecisionPriority.Defend, action, $"HP is {hp:F0}% with {nearbyEnemies} opponents inside 15 yalms.");
        }

        if (hp <= 55f && player.Mp >= 4000 && !prefulgenceReady)
            return Recommend(DecisionPriority.Recover, "Use Recuperate", $"HP is {hp:F0}% and enough MP remains for two Recuperates.");

        if (state.Target == null)
        {
            if (nearbyEnemies >= 2 && nearbyAllies == 0)
                return Recommend(DecisionPriority.Reposition, "Disengage toward your team", $"{nearbyEnemies} opponents are nearby and no living ally is inside 15 yalms.");

            return bestTarget == null
                ? Recommend(DecisionPriority.Observe, "Regroup and scan", "No opponent is currently inside 25 yalms.")
                : Recommend(DecisionPriority.Target, $"Target {bestTarget.Name}", DescribeTarget(bestTarget));
        }

        // Validate the target before recommending a proc. Prefulgence, Vice of
        // Thorns, and Grand Impact do not pierce Guard and should not be wasted.
        if (targetHp is null or <= 0f)
        {
            return bestTarget != null &&
                   !string.Equals(state.Target.Name, bestTarget.Name, StringComparison.Ordinal)
                ? Recommend(DecisionPriority.Target, $"Switch to {bestTarget.Name}", $"{state.Target.Name} is not a live combat target; {DescribeTarget(bestTarget)}")
                : Recommend(DecisionPriority.Target, "Find a live target", $"{state.Target.Name} cannot currently be pressured.");
        }

        if (targetInvincible)
        {
            return bestTarget != null &&
                   !string.Equals(state.Target.Name, bestTarget.Name, StringComparison.Ordinal)
                ? Recommend(DecisionPriority.Target, $"Switch to {bestTarget.Name}", $"{state.Target.Name} has Invincibility; {DescribeTarget(bestTarget)}")
                : Recommend(DecisionPriority.Target, "Find another target", $"{state.Target.Name} has Invincibility and cannot be pressured effectively.");
        }

        if (targetGuarding)
        {
            if (nearbyEnemies >= 2 && nearbyAllies == 0 &&
                !enchantedRiposte && !enchantedZwerchhau)
                return Recommend(DecisionPriority.Reposition, "Disengage toward your team", $"{state.Target.Name} is Guarding while {nearbyEnemies} opponents are nearby and no ally is in support range.");

            if (state.Target.Distance > 5f &&
                (enchantedRiposte || enchantedZwerchhau) && state.Target.Distance <= 25f)
                return Recommend(DecisionPriority.Reposition, "Close distance to continue the melee combo", "The active melee chain ignores Guard, but the target is outside its 5-yalm range.");

            if (state.Target.Distance > 5f && corpsReady && riposteReady &&
                hp >= 70f && nearbyEnemies <= 2)
                return Recommend(DecisionPriority.Burst, "Corps-a-corps, then Enchanted Riposte", "The melee chain ignores Guard, and Monomachy reduces this target's return damage.");

            if (state.Target.Distance <= 5f && riposteReady)
                return Recommend(DecisionPriority.Burst, "Use Enchanted Riposte", "The melee chain ignores Guard and is currently available.");

            if (bestTarget != null && !bestTarget.IsGuarding &&
                !string.Equals(state.Target.Name, bestTarget.Name, StringComparison.Ordinal))
                return Recommend(DecisionPriority.Target, $"Switch to {bestTarget.Name}", $"{state.Target.Name} is Guarding; {DescribeTarget(bestTarget)}");

            return Recommend(DecisionPriority.Reposition, "Do not spend ranged burst", $"{state.Target.Name} is Guarding and the Guard-piercing melee chain is unavailable; preserve procs or switch targets.");
        }

        if (state.Target.Distance > 25f)
            return Recommend(DecisionPriority.Reposition, "Move into spell range", $"{state.Target.Name} is {state.Target.Distance:F0} yalms away; preserve ready procs until the target is within 25 yalms.");

        if (prefulgenceReady && state.Target != null)
            return Recommend(DecisionPriority.Burst, "Use Prefulgence", "Prefulgence Ready is active; use the instant damage and party healing before it expires.");

        if (thornedFlourish && state.Target != null)
            return Recommend(DecisionPriority.Control, "Use Vice of Thorns", "Thorned Flourish is active; Vice of Thorns adds damage and a stun.");

        if (enchantedRedoublement && state.Target.Distance <= 25f)
        {
            if (displacementReady && state.Target.Distance <= 5f)
                return Recommend(DecisionPriority.Burst, "Use Displacement, then Scorch", "Displacement strengthens the next spell by 15%; spend that boost on Scorch.");

            return Recommend(DecisionPriority.Burst, "Use Scorch", "The recorded combo state shows Enchanted Redoublement completed; continue into Scorch.");
        }

        if (enchantedZwerchhau && state.Target.Distance <= 5f)
            return Recommend(DecisionPriority.Burst, "Use Enchanted Redoublement", "Continue the active melee combo before its state expires.");

        if (enchantedRiposte && state.Target.Distance <= 5f)
            return Recommend(DecisionPriority.Burst, "Use Enchanted Zwerchhau", "Continue the active melee combo before its state expires.");

        if ((enchantedRiposte || enchantedZwerchhau) && state.Target.Distance <= 25f)
            return Recommend(DecisionPriority.Reposition, "Close distance to continue the melee combo", $"The combo is active, but {state.Target.Name} is {state.Target.Distance:F0} yalms away.");

        if (bestTarget != null &&
            !string.Equals(state.Target.Name, bestTarget.Name, StringComparison.Ordinal) &&
            targetHp is > 55f && bestTarget.HpPercent + 15f < targetHp)
        {
            return Recommend(DecisionPriority.Target, $"Switch to {bestTarget.Name}", DescribeTarget(bestTarget));
        }

        if (nearbyEnemies >= 2 && nearbyAllies == 0)
            return Recommend(DecisionPriority.Reposition, "Disengage toward your team", $"{nearbyEnemies} opponents are inside 15 yalms and no living ally is nearby; avoid spending a new burst window alone.");

        var safeBurstWindow = hp >= 70f && player.Mp >= 4000 && nearbyEnemies <= 2 && nearbyAllies >= 1;

        if (emboldenReady && safeBurstWindow && !HasStatus(player.Statuses, "Embolden"))
            return Recommend(DecisionPriority.Burst, "Use Embolden", "A teammate is nearby and defensive resources are sufficient to begin the burst window.");

        if (resolutionReady && state.Target.Distance <= 25f)
            return Recommend(DecisionPriority.Control, "Use Resolution", "Resolution is ready; apply its line damage and Silence before committing to melee.");

        if (dualcastReady && state.Target.Distance <= 25f)
            return Recommend(DecisionPriority.Pressure, "Use Grand Impact", "Dualcast is active; spend Grand Impact before beginning the melee commitment.");

        if (targetHp <= 30f && hp >= 55f && nearbyEnemies <= 2)
        {
            var monomachyText = targetHasMonomachy
                ? " Monomachy is already active."
                : corpsReady ? " Corps-a-corps is ready to apply Monomachy." : " Corps-a-corps is unavailable; finish from range.";
            return Recommend(DecisionPriority.FinishTarget, "Commit to the finish", $"{state.Target.Name} is at {targetHp:F0}% HP and the local risk is acceptable.{monomachyText}");
        }

        if (targetHasMonomachy && riposteReady && state.Target.Distance <= 5f && hp >= 55f && nearbyEnemies <= 2)
            return Recommend(DecisionPriority.Burst, "Start Enchanted Riposte", "Monomachy is active, the melee chain is ready, and local risk is acceptable.");

        if (!targetHasMonomachy && corpsReady && riposteReady && state.Target.Distance <= 25f &&
            hp >= 70f && player.Mp >= 4000 && nearbyEnemies <= 2 && nearbyAllies >= 1)
        {
            return Recommend(
                DecisionPriority.Burst,
                "Corps-a-corps, then Enchanted Riposte",
                $"Burst resources are ready with {hp:F0}% HP, {player.Mp:N0} MP, and {nearbyAllies} nearby ally/allies.");
        }

        if (state.Target.Distance <= 5f && hp >= 60f && nearbyEnemies <= 2)
            return Recommend(DecisionPriority.Burst, "Continue the melee combo", $"The target is in melee range with {nearbyEnemies} nearby opponent(s) and your HP is stable.");

        if (nearbyEnemies >= 3)
            return Recommend(DecisionPriority.Reposition, "Kite toward your team", $"{nearbyEnemies} opponents are inside 15 yalms; avoid an isolated melee commitment.");

        return Recommend(DecisionPriority.Pressure, "Use Jolt III and assess", "Build Dualcast at range while preserving defensive options.");
    }

    private static float Percent(uint current, uint maximum) => current * 100f / maximum;

    private static DecisionRecommendation Recommend(DecisionPriority priority, string action, string reason) =>
        new(priority, action, reason);

    private static string? FindStatus(IEnumerable<StatusSnapshot>? statuses, HashSet<string> names)
    {
        if (statuses == null)
            return null;

        foreach (var status in statuses)
        {
            if (names.Contains(status.Name))
                return status.Name;
        }

        return null;
    }

    private static bool HasStatus(IEnumerable<StatusSnapshot>? statuses, string name)
    {
        if (statuses == null)
            return false;

        foreach (var status in statuses)
        {
            if (string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsActionAvailable(IEnumerable<ActionCooldownSnapshot> actions, string name)
    {
        var found = false;
        foreach (var action in actions)
        {
            if (!string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;

            found = true;
            if (action.IsAvailable)
                return true;
        }

        // Offline scenarios and old recordings have no cooldown list. Preserve
        // their prior behavior instead of treating every action as unavailable.
        return !found;
    }

    private static bool IsActionKnownAndAvailable(IEnumerable<ActionCooldownSnapshot> actions, string name)
    {
        foreach (var action in actions)
        {
            if (string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase))
                return action.IsAvailable;
        }

        return false;
    }

    private static int CountNearbyEnemies(GameState state, float range)
    {
        var partyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in state.Party)
            partyNames.Add(member.Name);

        var count = 0;
        foreach (var character in state.NearbyCharacters)
        {
            if (character.Distance <= range && character.Hp > 0 && character.MaxHp > 0 &&
                !partyNames.Contains(character.Name))
                count++;
        }

        return count;
    }

    private static int CountNearbyAllies(GameState state, float range)
    {
        var count = 0;
        foreach (var member in state.Party)
        {
            if (member.Distance <= range && member.Hp > 0 &&
                !string.Equals(member.Name, state.Player?.Name, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    private static string DescribeTarget(TargetCandidate target)
    {
        var guardText = target.IsGuarding ? ", Guarding" : "";
        var markText = target.HasMonomachy ? ", Monomachy active" : "";
        return $"{target.Job} at {target.HpPercent:F0}% HP and {target.Distance:F0} yalms{guardText}{markText}.";
    }
}

public enum DecisionPriority
{
    Wait,
    Observe,
    Target,
    Pressure,
    Reposition,
    Control,
    Burst,
    FinishTarget,
    Defend,
    Recover,
    Purify,
    Retreat
}

public sealed record DecisionRecommendation(DecisionPriority Priority, string Recommendation, string Reason);
