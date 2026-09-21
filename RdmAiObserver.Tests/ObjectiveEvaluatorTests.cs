using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class ObjectiveEvaluatorTests
{
    [Fact]
    public void Confirmed_crystal_base_id_produces_position_and_contest_counts()
    {
        var state = new GameState
        {
            Player = new PlayerSnapshot { ObjectId = 1, Hp = 50000, MaxHp = 58500 }
        };
        state.NearbyWorldObjects.Add(new WorldObjectSnapshot
            { GameObjectId = 50, BaseId = ObjectiveEvaluator.TacticalCrystalBaseId, Distance = 8f, X = 10f, Y = 1f, Z = 20f });
        state.Party.Add(new PartyMemberSnapshot
            { ObjectId = 1, Hp = 50000, MaxHp = 58500, X = 0f, Z = 0f });
        state.Party.Add(new PartyMemberSnapshot
            { ObjectId = 2, Hp = 50000, MaxHp = 58500, X = 12f, Y = 1f, Z = 20f });
        state.NearbyCharacters.Add(new NearbyCharacterSnapshot
            { ObjectId = 3, Relation = CombatRelation.Hostile, Hp = 50000, MaxHp = 58500, X = 15f, Y = 1f, Z = 20f });

        var objective = ObjectiveEvaluator.Find(state);

        Assert.NotNull(objective);
        Assert.Equal(10f, objective.X);
        Assert.Equal(20f, objective.Z);
        Assert.Equal(1, objective.AlliesWithin10Yalms);
        Assert.Equal(1, objective.EnemiesWithin10Yalms);
        Assert.True(objective.IsContested);
    }
}
