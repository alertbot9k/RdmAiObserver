using System.Collections.Generic;

namespace SamplePlugin;

/// <summary>
/// Small, repeatable offline states for exercising the decision engine.
/// They never read from or write to the game.
/// </summary>
public static class ScenarioLibrary
{
    private static readonly string[] KnownActions =
    {
        "Enchanted Riposte", "Resolution", "Embolden", "Corps-a-corps",
        "Displacement", "Forte", "Recuperate", "Purify", "Guard"
    };

    private static readonly Scenario[] Scenarios =
    {
        new(
            "No immediate priority",
            CreateState(playerHp: 58500, targetHp: null, nearbyEnemyCount: 0),
            "Regroup and scan"),
        new(
            "Select nearby target",
            CreateState(playerHp: 58500, targetHp: null, nearbyEnemyCount: 1),
            "Target Enemy One"),
        new(
            "Critical HP with Recuperate",
            CreateState(playerHp: 12000, targetHp: 50000, playerMp: 6000),
            "Use Recuperate"),
        new(
            "Critical HP without MP",
            CreateState(playerHp: 12000, targetHp: 50000, playerMp: 1000,
                readyActions: new[] { "Guard" }),
            "Guard and disengage"),
        new(
            "Forte under heavy pressure",
            CreateState(playerHp: 26000, targetHp: 50000, nearbyEnemyCount: 3,
                readyActions: new[] { "Forte", "Guard" }),
            "Use Forte"),
        new(
            "Recover after Forte",
            CreateState(playerHp: 26000, targetHp: 50000, nearbyEnemyCount: 3,
                playerMp: 6000, readyActions: new[] { "Guard" }),
            "Use Recuperate"),
        new(
            "Moderate HP recovery",
            CreateState(playerHp: 29000, targetHp: 50000, playerMp: 6000),
            "Use Recuperate"),
        new(
            "Low-health finish opportunity",
            CreateState(playerHp: 58500, targetHp: 12000),
            "Commit to the finish"),
        new(
            "Safe pressure",
            CreateState(playerHp: 58500, targetHp: 58500),
            "Use Jolt III and assess"),
        new(
            "Survival overrides a finish",
            CreateState(playerHp: 16000, targetHp: 8000, playerMp: 6000),
            "Use Recuperate"),
        new(
            "Purifiable crowd control",
            CreateState(playerHp: 50000, targetHp: 50000, playerStatus: "Stun",
                readyActions: new[] { "Purify" }),
            "Purify"),
        new(
            "Guarding target at range",
            CreateState(playerHp: 50000, targetHp: 20000, targetStatus: "Guard"),
            "Do not spend ranged burst"),
        new(
            "Guard-piercing melee",
            CreateState(playerHp: 50000, targetHp: 20000, targetStatus: "Guard",
                targetDistance: 3f, readyActions: new[] { "Enchanted Riposte" }),
            "Use Enchanted Riposte"),
        new(
            "Dualcast pressure",
            CreateState(playerHp: 50000, targetHp: 50000, playerStatus: "Dualcast"),
            "Use Grand Impact"),
        new(
            "Prefulgence ready",
            CreateState(playerHp: 35000, targetHp: 40000, playerStatus: "Prefulgence Ready"),
            "Use Prefulgence"),
        new(
            "Continue melee combo",
            CreateState(playerHp: 50000, targetHp: 42000,
                playerStatus: "Enchanted Riposte", targetDistance: 3f),
            "Use Enchanted Zwerchhau"),
        new(
            "Recover melee combo range",
            CreateState(playerHp: 50000, targetHp: 42000,
                playerStatus: "Enchanted Riposte", targetDistance: 12f),
            "Close distance to continue the melee combo"),
        new(
            "Recover combo range through Guard",
            CreateState(playerHp: 50000, targetHp: 42000,
                playerStatus: "Enchanted Riposte", targetStatus: "Guard",
                targetDistance: 12f),
            "Close distance to continue the melee combo"),
        new(
            "Boost Scorch with Displacement",
            CreateState(playerHp: 50000, targetHp: 42000,
                playerStatus: "Enchanted Redoublement", targetDistance: 3f,
                readyActions: new[] { "Displacement" }),
            "Use Displacement, then Scorch"),
        new(
            "Begin safe burst",
            CreateState(playerHp: 50000, targetHp: 50000,
                readyActions: new[] { "Embolden" }),
            "Use Embolden"),
        new(
            "Resolution before engagement",
            CreateState(playerHp: 50000, targetHp: 50000,
                readyActions: new[] { "Resolution", "Corps-a-corps", "Enchanted Riposte" }),
            "Use Resolution"),
        new(
            "Safe melee commitment",
            CreateState(playerHp: 44000, targetHp: 50000,
                readyActions: new[] { "Corps-a-corps", "Enchanted Riposte" }),
            "Corps-a-corps, then Enchanted Riposte"),
        new(
            "Incapacitated",
            CreateState(playerHp: 0, targetHp: 30000),
            "Wait for respawn")
    };

    public static int Count => Scenarios.Length;

    public static Scenario Get(int index)
    {
        var normalizedIndex = ((index % Count) + Count) % Count;
        return Scenarios[normalizedIndex];
    }

    public static ScenarioValidation ValidateAll()
    {
        var failures = new List<string>();
        foreach (var scenario in Scenarios)
        {
            var actual = DecisionEngine.Evaluate(scenario.State).Recommendation;
            if (!string.Equals(actual, scenario.ExpectedRecommendation, System.StringComparison.Ordinal))
                failures.Add($"{scenario.Name}: expected '{scenario.ExpectedRecommendation}', got '{actual}'");
        }

        return new ScenarioValidation(Scenarios.Length, failures);
    }

    private static GameState CreateState(
        uint playerHp,
        uint? targetHp,
        uint playerMp = 10000,
        string? playerStatus = null,
        string? targetStatus = null,
        float targetDistance = 12f,
        int nearbyEnemyCount = 1,
        string[]? readyActions = null)
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
                new() { Name = "Ally One", Job = "warrior", Hp = 65000, MaxHp = 65000, Distance = 8f }
            }
        };

        foreach (var actionName in KnownActions)
        {
            state.Player.Actions.Add(new ActionCooldownSnapshot
            {
                Name = actionName,
                CurrentCharges = IsReady(actionName, readyActions) ? 1u : 0u,
                IsCoolingDown = !IsReady(actionName, readyActions),
                IsAvailable = IsReady(actionName, readyActions)
            });
        }

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

        for (var enemyIndex = 0; enemyIndex < nearbyEnemyCount; enemyIndex++)
        {
            state.NearbyCharacters.Add(new NearbyCharacterSnapshot
            {
                Name = enemyIndex == 0 ? "Enemy One" : $"Enemy {enemyIndex + 1}",
                Kind = "Pc",
                Distance = targetDistance + enemyIndex,
                Hp = enemyIndex == 0 ? targetHp ?? 58500 : 58500,
                MaxHp = 58500
            });
        }

        return state;
    }

    private static bool IsReady(string actionName, string[]? readyActions)
    {
        if (readyActions == null)
            return false;

        foreach (var readyAction in readyActions)
        {
            if (string.Equals(actionName, readyAction, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public sealed record Scenario(string Name, GameState State, string ExpectedRecommendation);

public sealed record ScenarioValidation(int Total, List<string> Failures)
{
    public int Passed => Total - Failures.Count;
}
