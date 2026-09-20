using System;
using System.Collections.Generic;

namespace SamplePlugin;

/// <summary>
/// Produces a human-readable recommendation from an observed GameState.
/// This class never issues game commands or controls the player.
/// </summary>
public static class DecisionEngine
{
    public static DecisionRecommendation Evaluate(GameState state)
    {
        var player = state.Player;
        if (player == null || player.MaxHp == 0)
        {
            return new DecisionRecommendation(
                DecisionPriority.Wait,
                "Wait for player data",
                "The observer does not yet have a complete player snapshot.");
        }

        var healthPercent = player.Hp * 100f / player.MaxHp;
        if (healthPercent <= 35f)
        {
            return new DecisionRecommendation(
                DecisionPriority.Retreat,
                "Retreat and recover",
                $"Your HP is {healthPercent:F0}%, below the 35% survival threshold.");
        }

        if (state.Target?.Hp is uint targetHp && state.Target.MaxHp is uint targetMaxHp && targetMaxHp > 0)
        {
            var targetHealthPercent = targetHp * 100f / targetMaxHp;
            if (targetHealthPercent <= 25f)
            {
                return new DecisionRecommendation(
                    DecisionPriority.FinishTarget,
                    "Prioritize the low-HP target",
                    $"{state.Target.Name} is at {targetHealthPercent:F0}% HP.");
            }
        }

        var partyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in state.Party)
            partyNames.Add(member.Name);

        var nearbyEnemies = 0;
        foreach (var character in state.NearbyCharacters)
        {
            if (!partyNames.Contains(character.Name))
                nearbyEnemies++;
        }

        if (state.Target != null && nearbyEnemies > 0)
        {
            return new DecisionRecommendation(
                DecisionPriority.Pressure,
                "Apply measured pressure",
                $"{nearbyEnemies} nearby opponent(s) detected; keep safe positioning while evaluating {state.Target.Name}.");
        }

        return new DecisionRecommendation(
            DecisionPriority.Observe,
            "Assess the battlefield",
            "No immediate survival or finish priority is visible in this snapshot.");
    }
}

public enum DecisionPriority
{
    Wait,
    Observe,
    Pressure,
    FinishTarget,
    Retreat
}

public sealed record DecisionRecommendation(
    DecisionPriority Priority,
    string Recommendation,
    string Reason);
