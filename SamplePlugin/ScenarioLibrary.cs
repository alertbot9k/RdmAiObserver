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
            CreateState(playerHp: 16000, targetHp: 8000, nearbyEnemy: true)),
        new(
            "Critical HP with Recuperate",
            CreateState(playerHp: 12000, targetHp: 50000, nearbyEnemy: true, playerMp: 6000)),
        new(
            "Critical HP without MP",
            CreateState(playerHp: 12000, targetHp: 50000, nearbyEnemy: true, playerMp: 1000)),
        new(
            "Purifiable crowd control",
            CreateState(playerHp: 50000, targetHp: 50000, nearbyEnemy: true, playerStatus: "Stun")),
        new(
            "Target is guarding",
            CreateState(playerHp: 50000, targetHp: 20000, nearbyEnemy: true, targetStatus: "Guard")),
        new(
            "Dualcast pressure",
            CreateState(playerHp: 50000, targetHp: 50000, nearbyEnemy: true, playerStatus: "Dualcast")),
        new(
            "Prefulgence ready",
            CreateState(playerHp: 35000, targetHp: 40000, nearbyEnemy: true, playerStatus: "Prefulgence Ready")),
        new(
            "Continue melee combo",
            CreateState(playerHp: 50000, targetHp: 42000, nearbyEnemy: true, playerStatus: "Enchanted Riposte", targetDistance: 3f)),
        new(
            "Incapacitated",
            CreateState(playerHp: 0, targetHp: 30000, nearbyEnemy: true))
    };

    public static int Count => Scenarios.Length;

    public static Scenario Get(int index)
    {
        var normalizedIndex = ((index % Count) + Count) % Count;
        return Scenarios[normalizedIndex];
    }

    private static GameState CreateState(
        uint playerHp,
        uint? targetHp,
        bool nearbyEnemy,
        uint playerMp = 10000,
        string? playerStatus = null,
        string? targetStatus = null,
        float targetDistance = 12f)
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
                Mp = playerMp,
                MaxMp = 10000
            },
            Party = new List<PartyMemberSnapshot>
            {
                new() { Name = "Blackjet Morgul", Job = "red mage", Hp = playerHp, MaxHp = 58500 },
                new() { Name = "Ally One", Job = "warrior", Hp = 65000, MaxHp = 65000 }
            }
        };

        if (playerStatus != null)
            state.Player.Statuses.Add(new StatusSnapshot { Name = playerStatus, RemainingSeconds = 3f });

        if (targetHp.HasValue)
        {
            state.Target = new TargetSnapshot
            {
                Name = "Enemy One",
                Kind = "Pc",
                Distance = targetDistance,
                Hp = targetHp.Value,
                MaxHp = 58500
            };

            if (targetStatus != null)
            {
                state.Target.Statuses = new List<StatusSnapshot>
                {
                    new() { Name = targetStatus, RemainingSeconds = 3f }
                };
            }
        }

        if (nearbyEnemy)
        {
            state.NearbyCharacters.Add(new NearbyCharacterSnapshot
            {
                Name = "Enemy One",
                Kind = "Pc",
                Distance = targetDistance,
                Hp = targetHp ?? 58500,
                MaxHp = 58500
            });
        }

        return state;
    }
}

public sealed record Scenario(string Name, GameState State);
