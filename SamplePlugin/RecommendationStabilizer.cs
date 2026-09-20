using System;

namespace SamplePlugin;

/// <summary>
/// Prevents transient frame-to-frame state changes from flashing different
/// advice. Survival recommendations are never delayed.
/// </summary>
public sealed class RecommendationStabilizer
{
    private static readonly TimeSpan ConfirmationTime = TimeSpan.FromMilliseconds(400);

    private DecisionRecommendation? current;
    private DecisionRecommendation? pending;
    private DateTime pendingSinceUtc;

    public DecisionRecommendation Select(DecisionRecommendation candidate, DateTime nowUtc)
    {
        if (current == null || IsUrgent(candidate.Priority))
        {
            current = candidate;
            pending = null;
            return candidate;
        }

        if (SameAdvice(current, candidate))
        {
            current = candidate;
            pending = null;
            return candidate;
        }

        if (pending == null || !SameAdvice(pending, candidate))
        {
            pending = candidate;
            pendingSinceUtc = nowUtc;
            return current;
        }

        if (nowUtc - pendingSinceUtc < ConfirmationTime)
            return current;

        current = candidate;
        pending = null;
        return candidate;
    }

    public void Reset()
    {
        current = null;
        pending = null;
    }

    private static bool SameAdvice(DecisionRecommendation left, DecisionRecommendation right) =>
        left.Priority == right.Priority &&
        string.Equals(left.Recommendation, right.Recommendation, StringComparison.Ordinal);

    private static bool IsUrgent(DecisionPriority priority) => priority is
        DecisionPriority.Wait or
        DecisionPriority.Purify or
        DecisionPriority.Recover or
        DecisionPriority.Retreat or
        DecisionPriority.Defend;
}
