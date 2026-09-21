using System;
using System.Collections.Generic;

namespace SamplePlugin;

/// <summary>Conservative enemy count from a recorded snapshot, including a selected target omitted from the nearby list.</summary>
public static class CombatProximity
{
    public static int CountEnemies(GameState state, float range)
    {
        var partyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in state.Party)
            partyNames.Add(member.Name);

        var count = 0;
        var target = state.Target;
        var selectedTargetSeen = false;
        foreach (var character in state.NearbyCharacters)
        {
            if (character.Hp > 0 && character.MaxHp > 0 && character.Distance <= range &&
                !partyNames.Contains(character.Name))
            {
                count++;
                if (target != null && ((target.ObjectId != 0 && character.ObjectId == target.ObjectId) ||
                    (target.ObjectId == 0 && string.Equals(character.Name, target.Name, StringComparison.Ordinal))))
                    selectedTargetSeen = true;
            }
        }

        if (target is { Hp: > 0, MaxHp: > 0 } && target.Distance <= range &&
            !partyNames.Contains(target.Name) && !selectedTargetSeen)
            count++;

        return count;
    }
}
