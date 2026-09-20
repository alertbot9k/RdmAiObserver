using System;
using System.Collections.Generic;
using SamplePlugin.Windows;

namespace SamplePlugin;

/// <summary>
/// Re-evaluates a saved match with the current rules so recommendation changes
/// can be tested without entering another live match.
/// </summary>
public static class ReplayAnalyzer
{
    public static ReplayAnalysis Analyze(IReadOnlyList<RecordedGameState> snapshots)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var actionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        string? previousRecommendation = null;
        var changes = 0;
        var activeSnapshots = 0;
        var inferredActions = 0;
        var evaluatedActions = 0;
        var matchingActions = 0;
        DecisionRecommendation? previousAdvice = null;

        foreach (var snapshot in snapshots)
        {
            var recommendation = DecisionEngine.Evaluate(snapshot.State);
            counts.TryGetValue(recommendation.Recommendation, out var count);
            counts[recommendation.Recommendation] = count + 1;

            if (snapshot.State.Player is { Hp: > 0 })
            {
                activeSnapshots++;
                if (previousRecommendation != null &&
                    !string.Equals(previousRecommendation, recommendation.Recommendation, StringComparison.Ordinal))
                {
                    changes++;
                }

                previousRecommendation = recommendation.Recommendation;
            }
            else
            {
                // A respawn begins a new decision sequence. Do not count the
                // gap between lives as recommendation churn.
                previousRecommendation = null;
            }

            var reliableActionWindow = false;
            var windowMatched = false;
            foreach (var action in snapshot.InferredActions)
            {
                if (snapshot.State.Player is not { Hp: > 0 } || !IsReliable(action))
                    continue;

                inferredActions++;
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
        }

        return new ReplayAnalysis(
            snapshots.Count,
            activeSnapshots,
            changes,
            inferredActions,
            evaluatedActions,
            matchingActions,
            FindMostCommon(counts),
            FindMostCommon(actionCounts));
    }

    private static bool IsReliable(InferredActionUse action)
    {
        // Older recordings captured Recuperate's one-second shared cooldown
        // as an action use. Real tracked cooldowns and proc uses remain valid.
        return !string.Equals(action.Name, "Recuperate", StringComparison.OrdinalIgnoreCase) ||
               action.CooldownRemainingSeconds > 2.5f;
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

public sealed record ReplayAnalysis(
    int SnapshotCount,
    int ActiveSnapshotCount,
    int RecommendationChanges,
    int InferredActionCount,
    int EvaluatedActionCount,
    int MatchingActionCount,
    string MostCommonRecommendation,
    string MostCommonAction)
{
    public float ChangesPerMinute => ActiveSnapshotCount <= 1
        ? 0f
        : RecommendationChanges / ((ActiveSnapshotCount - 1) * 2f / 60f);

    public float MatchPercent => EvaluatedActionCount == 0
        ? 0f
        : MatchingActionCount * 100f / EvaluatedActionCount;
}
