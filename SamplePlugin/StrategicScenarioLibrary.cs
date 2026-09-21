namespace SamplePlugin;

public sealed record StrategicScenario(string Name, GameState State, MovementTrend? Movement, CrystalStrategy Expected);

/// <summary>Synthetic CC positions used to exercise strategy without launching the game.</summary>
public static class StrategicScenarioLibrary
{
    public static IReadOnlyList<StrategicScenario> All { get; } =
    [
        new("outside CC", State(territory: 0, objectiveDistance: null), null, CrystalStrategy.Observe),
        new("approach unoccupied crystal", State(objectiveDistance: 35), null, CrystalStrategy.Approach),
        new("contest occupied crystal", State(objectiveDistance: 5, allies: 1, enemies: 1, objectiveEnemies: 1), null, CrystalStrategy.Contest),
        new("escort moving crystal", State(objectiveDistance: 12, allies: 1), new(2, 3, 2, -1, true, true), CrystalStrategy.Escort),
        new("retreat at critical HP", State(objectiveDistance: 5, playerHp: 15000, enemies: 1), null, CrystalStrategy.Retreat),
        new("regroup while isolated", State(objectiveDistance: 35, allies: 0, enemies: 2), null, CrystalStrategy.Regroup)
    ];

    private static GameState State(uint territory = 1116, float? objectiveDistance = 10, uint playerHp = 50000,
        int allies = 0, int enemies = 0, int objectiveEnemies = 0)
    {
        var state = new GameState
        {
            LoggedIn = true,
            TerritoryId = territory,
            NearbyScanRadius = 60,
            NearbyScanComplete = true,
            Player = new PlayerSnapshot { ObjectId = 1, Hp = playerHp, MaxHp = 58500 }
        };
        if (objectiveDistance.HasValue)
            state.Objective = new ObjectiveSnapshot { BaseId = ObjectiveEvaluator.TacticalCrystalBaseId,
                DistanceToPlayer = objectiveDistance.Value, EnemiesWithin10Yalms = objectiveEnemies };
        for (var i = 0; i < allies; i++)
            state.Party.Add(new PartyMemberSnapshot { ObjectId = (ulong)(10 + i), Hp = 50000, MaxHp = 50000, Distance = 8, X = 5 + i });
        for (var i = 0; i < enemies; i++)
            state.NearbyCharacters.Add(new NearbyCharacterSnapshot { ObjectId = (ulong)(20 + i), Relation = CombatRelation.Hostile,
                Hp = 50000, MaxHp = 50000, Distance = 8 + i, X = -5 - i });
        return state;
    }
}
