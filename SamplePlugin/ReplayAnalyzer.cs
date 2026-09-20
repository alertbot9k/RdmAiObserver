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

            foreach (var action in snapshot.InferredActions)
            {
                inferredActions++;
                actionCounts.TryGetValue(action.Name, out var actionCount);
                actionCounts[action.Name] = actionCount + 1;
            }
        }

        return new ReplayAnalysis(
            snapshots.Count,
            activeSnapshots,
            changes,
            inferredActions,
            FindMostCommon(counts),
            FindMostCommon(actionCounts));
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
    string MostCommonRecommendation,
    string MostCommonAction)
{
    public float ChangesPerMinute => ActiveSnapshotCount <= 1
        ? 0f
        : RecommendationChanges / ((ActiveSnapshotCount - 1) * 2f / 60f);
}
