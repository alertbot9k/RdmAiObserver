using System;
using System.Collections.Generic;

namespace SamplePlugin;

/// <summary>
/// Re-evaluates a saved match with the current rules so recommendation changes
/// can be tested without entering another live match.
/// </summary>
public static class ReplayAnalyzer
{
    public static ReplayReport CreateReport(IReadOnlyList<RecordedGameState> snapshots, DateTime generatedAtUtc)
    {
        snapshots = OrderSnapshots(snapshots);
        var events = new List<ReplayTimelineEvent>();
        var trendTracker = new CombatTrendTracker();
        string? previousRecommendation = null;
        ulong previousTargetObjectId = 0;
        string? previousTargetName = null;
        GameState? previousState = null;

        foreach (var snapshot in snapshots)
        {
            var trend = trendTracker.Update(snapshot.State, snapshot.CapturedAtUtc);
            var recommendation = DecisionEngine.Evaluate(snapshot.State, trend);
            var observedActions = ActionInferenceEngine.Infer(
                previousState, snapshot.State, snapshot.CapturedAtUtc);
            previousState = snapshot.State;
            var player = snapshot.State.Player;
            var hpPercent = player is { MaxHp: > 0 }
                ? player.Hp * 100f / player.MaxHp
                : (float?)null;

            var targetObjectId = snapshot.State.Target?.ObjectId ?? 0;
            var targetName = snapshot.State.Target?.Name;
            var targetChanged = targetObjectId != 0 && previousTargetObjectId != 0
                ? targetObjectId != previousTargetObjectId
                : !string.Equals(targetName, previousTargetName, StringComparison.Ordinal);
            if (!string.Equals(previousRecommendation, recommendation.Recommendation, StringComparison.Ordinal) || targetChanged)
            {
                events.Add(new ReplayTimelineEvent(
                    snapshot.CapturedAtUtc,
                    "Recommendation",
                    recommendation.Recommendation,
                    recommendation.Reason,
                    recommendation.Priority.ToString(),
                    hpPercent,
                    snapshot.State.Target?.Name));
                previousRecommendation = recommendation.Recommendation;
            }
            previousTargetObjectId = targetObjectId;
            previousTargetName = targetName;

            if (player is not { Hp: > 0 })
                continue;

            foreach (var action in observedActions)
            {
                if (!IsReliable(action))
                    continue;

                events.Add(new ReplayTimelineEvent(
                    snapshot.CapturedAtUtc,
                    "ObservedAction",
                    action.Name,
                    action.Evidence,
                    null,
                    hpPercent,
                    snapshot.State.Target?.Name));
            }
        }

        return new ReplayReport(RecordingSchema.CurrentVersion, generatedAtUtc, Analyze(snapshots), events);
    }

    public static ReplayAnalysis Analyze(IReadOnlyList<RecordedGameState> snapshots)
    {
        snapshots = OrderSnapshots(snapshots);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var actionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var observedStates = new List<GameState>(snapshots.Count);
        string? previousRecommendation = null;
        var changes = 0;
        var activeSnapshots = 0;
        var engagedSnapshots = 0;
        var inferredActions = 0;
        var evaluatedActions = 0;
        var matchingActions = 0;
        var deaths = 0;
        var respawns = 0;
        var lowHpSnapshots = 0;
        var noTargetSnapshots = 0;
        var protectedSnapshots = 0;
        var isolatedSnapshots = 0;
        var outOfRangeTargetSnapshots = 0;
        var elixirOpportunitySnapshots = 0;
        var guardingTargetSnapshots = 0;
        var consumedProcs = 0;
        var expiredProcs = 0;
        var rapidDamageSnapshots = 0;
        var defensiveRecommendations = 0;
        var freshEvidenceSnapshots = 0;
        var incompleteEvidenceSnapshots = 0;
        var lowestHpPercent = 100f;
        bool? wasAlive = null;
        GameState? previousState = null;
        DecisionRecommendation? previousAdvice = null;
        var trendTracker = new CombatTrendTracker();

        foreach (var snapshot in snapshots)
        {
            var trend = trendTracker.Update(snapshot.State, snapshot.CapturedAtUtc);
            var recommendation = DecisionEngine.Evaluate(snapshot.State, trend);
            var facts = ObservationFacts.From(snapshot.State, snapshot.CapturedAtUtc, snapshot.CapturedAtUtc);
            if (facts.IsFresh) freshEvidenceSnapshots++;
            if (!facts.HasPlayer || !facts.TargetPresent && facts.EnemiesWithin25Yalms == 0)
                incompleteEvidenceSnapshots++;
            var observedActions = ActionInferenceEngine.Infer(
                previousState, snapshot.State, snapshot.CapturedAtUtc);
            if (trend?.IsRapidDamage == true)
                rapidDamageSnapshots++;
            if (recommendation.Priority is DecisionPriority.Defend or DecisionPriority.Recover or
                DecisionPriority.Purify or DecisionPriority.Retreat)
            {
                defensiveRecommendations++;
            }
            observedStates.Add(snapshot.State);
            expiredProcs += CountExpiredProcs(previousState, snapshot.State);
            counts.TryGetValue(recommendation.Recommendation, out var count);
            counts[recommendation.Recommendation] = count + 1;

            if (snapshot.State.Player is { Hp: > 0 } player)
            {
                activeSnapshots++;
                var hpPercent = player.MaxHp == 0 ? 100f : player.Hp * 100f / player.MaxHp;
                lowestHpPercent = Math.Min(lowestHpPercent, hpPercent);
                if (hpPercent <= 30f)
                    lowHpSnapshots++;
                var isProtected = HasStatus(player.Statuses, "Invincibility");
                var nearbyEnemies = facts.EnemiesWithin15Yalms;
                var nearbyEnemiesInSpellRange = facts.EnemiesWithin25Yalms;
                var nearbyAllies = facts.AlliesWithin15Yalms;
                if (isProtected)
                    protectedSnapshots++;
                if (!isProtected && nearbyEnemies >= 2 && nearbyAllies == 0)
                    isolatedSnapshots++;
                if (!isProtected && snapshot.State.Target?.Distance > 25f)
                    outOfRangeTargetSnapshots++;
                if (!isProtected && nearbyEnemiesInSpellRange == 0 &&
                    (hpPercent <= 70f || player.Mp <= 4000))
                    elixirOpportunitySnapshots++;

                var engaged = !isProtected &&
                              (snapshot.State.Target?.Hp is > 0 || nearbyEnemiesInSpellRange > 0);
                if (engaged)
                {
                    engagedSnapshots++;
                    if (snapshot.State.Target == null)
                        noTargetSnapshots++;
                }
                if (HasStatus(snapshot.State.Target?.Statuses, "Guard"))
                    guardingTargetSnapshots++;
                if (wasAlive == false)
                    respawns++;

                if (previousRecommendation != null &&
                    !string.Equals(previousRecommendation, recommendation.Recommendation, StringComparison.Ordinal))
                {
                    changes++;
                }

                previousRecommendation = recommendation.Recommendation;
                wasAlive = true;
            }
            else
            {
                // A respawn begins a new decision sequence. Do not count the
                // gap between lives as recommendation churn.
                previousRecommendation = null;
                if (wasAlive == true)
                    deaths++;
                wasAlive = false;
            }

            var reliableActionWindow = false;
            var windowMatched = false;
            foreach (var action in observedActions)
            {
                if (snapshot.State.Player is not { Hp: > 0 } || !IsReliable(action))
                    continue;

                inferredActions++;
                if (action.Name is "Prefulgence" or "Vice of Thorns" or "Grand Impact")
                    consumedProcs++;
                actionCounts.TryGetValue(action.Name, out var actionCount);
                actionCounts[action.Name] = actionCount + 1;

                if (previousAdvice != null)
                {
                    reliableActionWindow = true;
                    if (MentionsAction(previousAdvice.Recommendation, action.Name))
                        windowMatched = true;
                }
            }

            // Several off-global-cooldown actions can occur between two-second
            // snapshots. One recommendation can only name the first choice,
            // so score the interval once rather than penalizing every extra
            // action in the same capture window.
            if (reliableActionWindow)
            {
                evaluatedActions++;
                if (windowMatched)
                    matchingActions++;
            }

            previousAdvice = snapshot.State.Player is { Hp: > 0 }
                ? recommendation
                : null;
            previousState = snapshot.State;
        }

        return new ReplayAnalysis(
            snapshots.Count,
            snapshots.Count > 1
                ? Math.Max(0f, (float)(snapshots[^1].CapturedAtUtc - snapshots[0].CapturedAtUtc).TotalSeconds)
                : 0f,
            activeSnapshots,
            engagedSnapshots,
            changes,
            inferredActions,
            evaluatedActions,
            matchingActions,
            PvpModeDetector.Detect(observedStates),
            deaths,
            respawns,
            lowHpSnapshots,
            noTargetSnapshots,
            protectedSnapshots,
            isolatedSnapshots,
            outOfRangeTargetSnapshots,
            elixirOpportunitySnapshots,
            guardingTargetSnapshots,
            consumedProcs,
            expiredProcs,
            rapidDamageSnapshots,
            defensiveRecommendations,
            activeSnapshots == 0 ? 0f : lowestHpPercent,
            FindMostCommon(counts),
            FindMostCommon(actionCounts),
            freshEvidenceSnapshots,
            incompleteEvidenceSnapshots);
    }

    private static IReadOnlyList<RecordedGameState> OrderSnapshots(IReadOnlyList<RecordedGameState> snapshots)
    {
        if (snapshots.Count < 2)
            return snapshots;

        return snapshots
            .Select((snapshot, index) => (snapshot, index))
            .OrderBy(item => item.snapshot.CapturedAtUtc)
            .ThenBy(item => item.index)
            .Select(item => item.snapshot)
            .ToArray();
    }

    private static bool HasStatus(IEnumerable<StatusSnapshot>? statuses, string name)
    {
        if (statuses == null)
            return false;

        foreach (var status in statuses)
        {
            if (string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static int CountExpiredProcs(GameState? previous, GameState current)
    {
        if (previous?.Player == null || current.Player is not { Hp: > 0 })
            return 0;

        var count = 0;
        foreach (var procName in new[] { "Prefulgence Ready", "Thorned Flourish", "Dualcast" })
        {
            StatusSnapshot? oldProc = null;
            foreach (var status in previous.Player.Statuses)
            {
                if (string.Equals(status.Name, procName, StringComparison.OrdinalIgnoreCase))
                {
                    oldProc = status;
                    break;
                }
            }

            if (oldProc?.RemainingSeconds is >= 0f and <= 2.5f &&
                !HasStatus(current.Player.Statuses, procName))
                count++;
        }

        return count;
    }

    private static bool IsReliable(InferredActionUse action)
    {
        // Older recordings captured Recuperate's one-second shared cooldown
        // as an action use. A cooldown alone remains insufficient, but the new
        // MP-plus-healing evidence is useful as a medium-confidence event.
        return !string.Equals(action.Name, "Recuperate", StringComparison.OrdinalIgnoreCase) ||
               action.CooldownRemainingSeconds > 2.5f ||
               action.Evidence.StartsWith("MP fell by", StringComparison.Ordinal);
    }

    private static bool MentionsAction(string recommendation, string action)
    {
        return recommendation.Contains(action, StringComparison.OrdinalIgnoreCase) ||
               (string.Equals(action, "Enchanted Zwerchhau", StringComparison.OrdinalIgnoreCase) &&
                recommendation.Contains("melee combo", StringComparison.OrdinalIgnoreCase)) ||
               (string.Equals(action, "Enchanted Redoublement", StringComparison.OrdinalIgnoreCase) &&
                recommendation.Contains("melee combo", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindMostCommon(Dictionary<string, int> counts)
    {
        var bestName = "None";
        var bestCount = 0;
        foreach (var pair in counts)
        {
            if (pair.Value <= bestCount)
                continue;

            bestName = pair.Key;
            bestCount = pair.Value;
        }

        return bestCount == 0 ? bestName : $"{bestName} ({bestCount})";
    }
}

public sealed record ReplayReport(
    int FormatVersion,
    DateTime GeneratedAtUtc,
    ReplayAnalysis Analysis,
    List<ReplayTimelineEvent> Timeline);

public sealed record ReplayTimelineEvent(
    DateTime CapturedAtUtc,
    string Kind,
    string Description,
    string? Detail,
    string? Priority,
    float? PlayerHpPercent,
    string? TargetName);

public sealed record ReplayAnalysis(
    int SnapshotCount,
    float DurationSeconds,
    int ActiveSnapshotCount,
    int EngagedSnapshotCount,
    int RecommendationChanges,
    int InferredActionCount,
    int EvaluatedActionCount,
    int MatchingActionCount,
    ObservedPvpMode Mode,
    int DeathCount,
    int RespawnCount,
    int LowHpSnapshotCount,
    int NoTargetSnapshotCount,
    int ProtectedSnapshotCount,
    int IsolatedSnapshotCount,
    int OutOfRangeTargetSnapshotCount,
    int ElixirOpportunitySnapshotCount,
    int GuardingTargetSnapshotCount,
    int ConsumedProcCount,
    int ExpiredProcCount,
    int RapidDamageSnapshotCount,
    int DefensiveRecommendationCount,
    float LowestHpPercent,
    string MostCommonRecommendation,
    string MostCommonAction,
    int FreshEvidenceSnapshotCount,
    int IncompleteEvidenceSnapshotCount)
{
    public float ChangesPerMinute => DurationSeconds <= 0f
        ? 0f
        : RecommendationChanges / (DurationSeconds / 60f);

    public float MatchPercent => EvaluatedActionCount == 0
        ? 0f
        : MatchingActionCount * 100f / EvaluatedActionCount;

    public float NoTargetPercent => EngagedSnapshotCount == 0
        ? 0f
        : NoTargetSnapshotCount * 100f / EngagedSnapshotCount;

    public string CalibrationNote => Mode switch
    {
        ObservedPvpMode.CrystallineConflict => "Suitable for Crystalline Conflict strategy calibration.",
        _ => "Mode was not identified reliably; treat strategy conclusions cautiously."
    };
}
