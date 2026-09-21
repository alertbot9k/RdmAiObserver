using System;
using System.Linq;

namespace SamplePlugin;

public static class ObjectiveEvaluator
{
    public const uint TacticalCrystalBaseId = 14470;
    private const float ContestRadius = 10f;

    public static ObjectiveSnapshot? Find(GameState state)
    {
        var candidate = state.NearbyWorldObjects.FirstOrDefault(
            worldObject => worldObject.BaseId == TacticalCrystalBaseId);
        if (candidate == null)
            return null;

        var player = state.Player;
        var allies = state.Party.Count(member => member.Hp > 0 &&
            (player == null || member.ObjectId != player.ObjectId) &&
            Distance(member.X, member.Y, member.Z, candidate.X, candidate.Y, candidate.Z) <= ContestRadius);
        var enemies = state.NearbyCharacters.Count(character =>
            character.Relation == CombatRelation.Hostile && character.Hp > 0 &&
            Distance(character.X, character.Y, character.Z, candidate.X, candidate.Y, candidate.Z) <= ContestRadius);

        return new ObjectiveSnapshot
        {
            GameObjectId = candidate.GameObjectId,
            BaseId = candidate.BaseId,
            X = candidate.X,
            Y = candidate.Y,
            Z = candidate.Z,
            DistanceToPlayer = candidate.Distance,
            AlliesWithin10Yalms = allies,
            EnemiesWithin10Yalms = enemies
        };
    }

    private static float Distance(float x1, float y1, float z1, float x2, float y2, float z2) =>
        MathF.Sqrt(MathF.Pow(x1 - x2, 2) + MathF.Pow(y1 - y2, 2) + MathF.Pow(z1 - z2, 2));
}
