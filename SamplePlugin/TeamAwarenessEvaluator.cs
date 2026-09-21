using System;
using System.Linq;
using System.Numerics;

namespace SamplePlugin;

public enum EvidenceConfidence { Unknown, Low, Medium, High }

public sealed record TeamAwareness(
    int NearbyAllies,
    int NearbyEnemies,
    int NumericalAdvantage,
    bool IsIsolated,
    bool AlliesClustered,
    TargetCandidate? FocusTarget,
    string RetreatDirection,
    EvidenceConfidence Confidence);

public static class TeamAwarenessEvaluator
{
    public static TeamAwareness Evaluate(GameState state)
    {
        var allies = CombatProximity.CountAllies(state, 15f);
        var enemies = CombatProximity.CountEnemies(state, 15f);
        var focus = TargetEvaluator.FindBest(state);
        return new TeamAwareness(
            allies,
            enemies,
            allies + 1 - enemies,
            enemies >= 2 && allies == 0,
            allies >= 2,
            focus,
            FindRetreatDirection(state),
            state.NearbyScanComplete ? EvidenceConfidence.High :
                state.NearbyScanRadius > 0 ? EvidenceConfidence.Medium : EvidenceConfidence.Low);
    }

    private static string FindRetreatDirection(GameState state)
    {
        var player = state.Player;
        if (player == null) return "unknown";
        var livingAllies = state.Party.Where(member => member.Hp > 0 && member.ObjectId != player.ObjectId).ToArray();
        if (livingAllies.Length > 0)
        {
            var x = livingAllies.Average(member => member.X) - player.X;
            var z = livingAllies.Average(member => member.Z) - player.Z;
            return $"toward allies ({Cardinal((float)x, (float)z)})";
        }

        var enemies = state.NearbyCharacters.Where(character =>
            character.Relation == CombatRelation.Hostile && character.Hp > 0).ToArray();
        if (enemies.Length == 0) return "unknown";
        var awayX = player.X - enemies.Average(enemy => enemy.X);
        var awayZ = player.Z - enemies.Average(enemy => enemy.Z);
        return $"away from enemies ({Cardinal((float)awayX, (float)awayZ)})";
    }

    private static string Cardinal(float x, float z)
    {
        if (MathF.Abs(x) >= MathF.Abs(z)) return x >= 0 ? "east" : "west";
        return z >= 0 ? "south" : "north";
    }
}
