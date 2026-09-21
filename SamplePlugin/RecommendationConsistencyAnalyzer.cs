using System;

namespace SamplePlugin;

public sealed record RecommendationConsistencyIssue(DateTime CapturedAtUtc, string Kind, string Previous, string Current, string Detail);

public static class RecommendationConsistencyAnalyzer
{
    public static List<RecommendationConsistencyIssue> Analyze(IReadOnlyList<RecordedGameState> snapshots)
    {
        var ordered = snapshots.OrderBy(frame => frame.CapturedAtUtc).ToArray();
        var issues = new List<RecommendationConsistencyIssue>();
        var advice = ordered.Select(frame => DecisionEngine.Evaluate(frame.State)).ToArray();
        for (var i = 1; i < ordered.Length; i++)
        {
            var seconds = (ordered[i].CapturedAtUtc - ordered[i - 1].CapturedAtUtc).TotalSeconds;
            if (seconds <= 3 && SameTarget(ordered[i - 1].State, ordered[i].State) &&
                IsOpposed(advice[i - 1].Recommendation, advice[i].Recommendation))
                issues.Add(new(ordered[i].CapturedAtUtc, "ContradictoryTransition", advice[i - 1].Recommendation, advice[i].Recommendation, $"Opposed advice changed within {seconds:F1}s."));
            if (i >= 2 && seconds <= 3 &&
                string.Equals(advice[i - 2].Recommendation, advice[i].Recommendation, StringComparison.Ordinal) &&
                !string.Equals(advice[i - 1].Recommendation, advice[i].Recommendation, StringComparison.Ordinal) &&
                !IsUrgent(advice[i - 1].Priority) &&
                SameTarget(ordered[i - 2].State, ordered[i - 1].State) &&
                SameTarget(ordered[i - 1].State, ordered[i].State) &&
                SameRangeBand(ordered[i - 2].State, ordered[i - 1].State) &&
                SameRangeBand(ordered[i - 1].State, ordered[i].State) &&
                SameMitigation(ordered[i - 2].State, ordered[i - 1].State) &&
                SameMitigation(ordered[i - 1].State, ordered[i].State))
                issues.Add(new(ordered[i].CapturedAtUtc, "RapidReversal", advice[i - 1].Recommendation, advice[i].Recommendation, "Advice returned to its prior value within two capture intervals for the same target."));
        }
        return issues;
    }

    private static bool IsOpposed(string left, string right) =>
        (IsAdvance(left) && IsRetreat(right)) || (IsRetreat(left) && IsAdvance(right));
    private static bool IsAdvance(string value) => value.Contains("Move into", StringComparison.OrdinalIgnoreCase) || value.Contains("Close distance", StringComparison.OrdinalIgnoreCase) || value.Contains("Commit", StringComparison.OrdinalIgnoreCase);
    private static bool IsRetreat(string value) => value.Contains("Disengage", StringComparison.OrdinalIgnoreCase) || value.Contains("Kite", StringComparison.OrdinalIgnoreCase) || value.Contains("Regroup", StringComparison.OrdinalIgnoreCase);
    private static bool IsUrgent(DecisionPriority priority) => priority is DecisionPriority.Wait or DecisionPriority.Purify or DecisionPriority.Recover or DecisionPriority.Defend or DecisionPriority.Retreat;
    private static bool SameMitigation(GameState left, GameState right) =>
        HasStatus(left.Target, "Guard") == HasStatus(right.Target, "Guard") &&
        HasStatus(left.Target, "Invincibility") == HasStatus(right.Target, "Invincibility");
    private static bool SameRangeBand(GameState left, GameState right) =>
        (left.Target?.Distance <= 25f) == (right.Target?.Distance <= 25f) &&
        (left.Target?.Distance <= 5f) == (right.Target?.Distance <= 5f);
    private static bool HasStatus(TargetSnapshot? target, string name) =>
        target?.Statuses?.Any(status => string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase)) == true;
    private static bool SameTarget(GameState left, GameState right)
    {
        var leftTarget = left.Target;
        var rightTarget = right.Target;
        return leftTarget is { ObjectId: not 0 } && rightTarget is { ObjectId: not 0 }
            ? leftTarget.ObjectId == rightTarget.ObjectId
            : string.Equals(leftTarget?.Name, rightTarget?.Name, StringComparison.Ordinal);
    }
}
