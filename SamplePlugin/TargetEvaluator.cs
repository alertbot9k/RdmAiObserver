using System;
using System.Collections.Generic;

namespace SamplePlugin;

public static class TargetEvaluator
{
    public static TargetCandidate? FindBest(GameState state)
    {
        var partyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in state.Party)
            partyNames.Add(member.Name);

        TargetCandidate? best = null;
        foreach (var character in state.NearbyCharacters)
        {
            if (partyNames.Contains(character.Name) || character.Hp == 0 || character.MaxHp == 0 || character.Distance > 25f)
                continue;

            var hpPercent = character.Hp * 100f / character.MaxHp;
            var guarding = HasStatus(character.Statuses, "Guard");
            var monomachy = HasStatus(character.Statuses, "Monomachy");
            if (HasStatus(character.Statuses, "Invincibility"))
                continue;

            var score = (100f - hpPercent) * 1.5f - character.Distance * 1.2f;

            if (guarding)
                score -= 40f;
            if (monomachy)
                score += 25f;
            if (character.Distance <= 5f)
                score += 8f;

            var candidate = new TargetCandidate(
                character.Name,
                character.Job,
                hpPercent,
                character.Distance,
                guarding,
                monomachy,
                score);

            if (best == null || candidate.Score > best.Score)
                best = candidate;
        }

        return best;
    }

    private static bool HasStatus(IEnumerable<StatusSnapshot> statuses, string name)
    {
        foreach (var status in statuses)
        {
            if (string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public sealed record TargetCandidate(
    string Name,
    string Job,
    float HpPercent,
    float Distance,
    bool IsGuarding,
    bool HasMonomachy,
    float Score);
