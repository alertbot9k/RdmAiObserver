using System.Collections.Generic;

namespace SamplePlugin;

/// <summary>
/// Small, repeatable offline states for exercising the decision engine.
/// They never read from or write to the game.
/// </summary>
public static class ScenarioLibrary
{
    private static readonly Scenario[] Scenarios =
    {
        new(
            "No immediate priority",
            CreateState(playerHp: 58500, targetHp: null, nearbyEnemy: false)),
        new(
            "Low health under pressure",
            CreateState(playerHp: 18000, targetHp: 58500, nearbyEnemy: true)),
        new(
            "Low-health finish opportunity",
            CreateState(playerHp: 58500, targetHp: 12000, nearbyEnemy: true)),
        new(
            "Safe pressure",
            CreateState(playerHp: 58500, targetHp: 58500, nearbyEnemy: true)),
        new(
            "Survival overrides a finish",
            CreateState(playerHp: 16000, targetHp: 8000, nearbyEnemy: true))
    };

    public static int Count => Scenarios.Length;

    public static Scenario Get(int index)
    {
        var normalizedIndex = ((index % Count) + Count) % Count;
        return Scenarios[normalizedIndex];
    }

    private static GameState CreateState(uint playerHp, uint? targetHp, bool nearbyEnemy)
    {
        var state = new GameState
        {
            LoggedIn = true,
            TerritoryId = 1033,
            Player = new PlayerSnapshot
            {
                Name = "Blackjet Morgul",
                Job = "red mage",
                Hp = playerHp,
                MaxHp = 58500,
                Mp = 10000,
                MaxMp = 10000
            },
            Party = new List<PartyMemberSnapshot>
            {
                new() { Name = "Blackjet Morgul", Job = "red mage", Hp = playerHp, MaxHp = 58500 },
                new() { Name = "Ally One", Job = "warrior", Hp = 65000, MaxHp = 65000 }
            }
        };

        if (targetHp.HasValue)
        {
            state.Target = new TargetSnapshot
            {
                Name = "Enemy One",
                Kind = "Pc",
                Distance = 12,
                Hp = targetHp.Value,
                MaxHp = 58500
            };
        }

        if (nearbyEnemy)
        {
            state.NearbyCharacters.Add(new NearbyCharacterSnapshot
            {
                Name = "Enemy One",
                Kind = "Pc",
                Distance = 12,
                Hp = targetHp ?? 58500,
                MaxHp = 58500
            });
        }

        return state;
    }
}

public sealed record Scenario(string Name, GameState State);
