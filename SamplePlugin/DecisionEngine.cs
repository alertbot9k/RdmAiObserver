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
    {
        var player = state.Player;
        if (player == null || player.MaxHp == 0)
            return Recommend(DecisionPriority.Wait, "Wait for player data", "The player snapshot is incomplete.");

        var hp = Percent(player.Hp, player.MaxHp);
        var targetHp = state.Target?.Hp is uint current && state.Target.MaxHp is uint maximum && maximum > 0
            ? Percent(current, maximum)
            : (float?)null;
        var nearbyEnemies = CountNearbyEnemies(state, 15f);
        var canSpendMp = player.Mp >= CommonActionMpCost;
        var crowdControl = FindStatus(player.Statuses, PurifiableStatuses);
        var selfGuarding = HasStatus(player.Statuses, "Guard");
        var targetGuarding = HasStatus(state.Target?.Statuses, "Guard");
        var targetHasMonomachy = HasStatus(state.Target?.Statuses, "Monomachy");
        var dualcastReady = HasStatus(player.Statuses, "Dualcast");
        var prefulgenceReady = HasStatus(player.Statuses, "Prefulgence Ready");
        var thornedFlourish = HasStatus(player.Statuses, "Thorned Flourish");

        if (selfGuarding)
            return Recommend(DecisionPriority.Defend, "Hold Guard", "Guard is active; avoid canceling its protection with another action.");

        if (crowdControl != null && canSpendMp)
            return Recommend(DecisionPriority.Purify, "Purify", $"{crowdControl} is active and at least {CommonActionMpCost:N0} MP is available.");

        if (hp <= 30f && canSpendMp)
            return Recommend(DecisionPriority.Recover, "Use Recuperate", $"HP is critical at {hp:F0}% and Recuperate MP is available.");

        if (hp <= 30f)
            return Recommend(DecisionPriority.Retreat, "Guard and disengage", $"HP is critical at {hp:F0}% but there is not enough MP for Recuperate.");

        if (hp <= 50f && nearbyEnemies >= 2)
            return Recommend(DecisionPriority.Defend, "Use Forte or Guard", $"HP is {hp:F0}% with {nearbyEnemies} opponents inside 15 yalms.");

        if (prefulgenceReady && state.Target != null)
            return Recommend(DecisionPriority.Burst, "Use Prefulgence", "Prefulgence Ready is active; use the instant damage and party healing before it expires.");

        if (thornedFlourish && state.Target != null)
            return Recommend(DecisionPriority.Control, "Use Vice of Thorns", "Thorned Flourish is active; Vice of Thorns adds damage and a stun.");

        if (state.Target == null)
            return Recommend(DecisionPriority.Observe, "Select a vulnerable target", "No target is selected.");

        if (targetGuarding && state.Target.Distance > 5f)
            return Recommend(DecisionPriority.Reposition, "Do not spend ranged burst", $"{state.Target.Name} is Guarding; reposition or pressure a different target.");

        if (targetHp <= 30f && hp >= 55f && nearbyEnemies <= 2)
        {
            var monomachyText = targetHasMonomachy ? " Monomachy is already active." : " Apply Monomachy with Corps-a-corps if available.";
            return Recommend(DecisionPriority.FinishTarget, "Commit to the finish", $"{state.Target.Name} is at {targetHp:F0}% HP and the local risk is acceptable.{monomachyText}");
        }

        if (dualcastReady && state.Target.Distance <= 25f)
            return Recommend(DecisionPriority.Pressure, "Use Grand Impact", "Dualcast is active and the target is in spell range.");

        if (state.Target.Distance <= 5f && hp >= 60f && nearbyEnemies <= 2)
            return Recommend(DecisionPriority.Burst, "Continue the melee combo", $"The target is in melee range with {nearbyEnemies} nearby opponent(s) and your HP is stable.");

        if (state.Target.Distance > 25f)
            return Recommend(DecisionPriority.Reposition, "Move into spell range", $"{state.Target.Name} is {state.Target.Distance:F0} yalms away.");

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

    private static int CountNearbyEnemies(GameState state, float range)
    {
        var partyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in state.Party)
            partyNames.Add(member.Name);

        var count = 0;
        foreach (var character in state.NearbyCharacters)
        {
            if (character.Distance <= range && !partyNames.Contains(character.Name))
                count++;
        }

        return count;
    }
}

public enum DecisionPriority
{
    Wait,
    Observe,
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
