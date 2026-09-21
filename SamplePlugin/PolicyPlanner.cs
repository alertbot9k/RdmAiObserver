using System;
using System.Collections.Generic;

namespace SamplePlugin;

public enum PolicyPlanStatus { Planned, ObserveOnly, Recover }

public sealed record PolicyStep(string Action, string Purpose, bool Interruptible, string AbortIf);

public sealed record PolicyPlan(
    PolicyPlanStatus Status,
    string Reason,
    IReadOnlyList<PolicyStep> Steps,
    DecisionRecommendation Recommendation)
{
    public bool IsActionable => Status == PolicyPlanStatus.Planned && Steps.Count > 0;
}

/// <summary>Builds a short, explainable action sequence without executing it.</summary>
public static class PolicyPlanner
{
    public static PolicyPlan Plan(ObservationFacts facts, CombatTrend? trend = null)
    {
        var recommendation = DecisionEngine.Evaluate(facts, trend);
        if (!facts.CanRecommend || facts.NearbyCompleteness == ObservationCompleteness.Unknown)
            return new(PolicyPlanStatus.ObserveOnly, "Evidence is stale or incomplete; no action sequence is safe.", [], recommendation);

        if (facts.MatchPhase is ObservedMatchPhase.Loading or ObservedMatchPhase.Respawning)
            return new(PolicyPlanStatus.Recover, "The match is not in an actionable combat phase.", [], recommendation);

        if (facts.PlayerCrowdControlled || facts.PlayerMitigation == MitigationState.Invulnerable)
            return new(PolicyPlanStatus.Recover, "Resolve the player's current control or protection state first.", [], recommendation);

        var steps = new List<PolicyStep>();
        if (recommendation.Recommendation is "Use Recuperate" or "Use Forte" or "Purify" or "Hold Guard")
        {
            steps.Add(new PolicyStep(recommendation.Recommendation, "Stabilize survival state", true,
                "Abort if player protection, crowd control, or target threat facts change."));
            return new(PolicyPlanStatus.Planned, recommendation.Reason, steps, recommendation);
        }

        if (facts.TargetMitigation == MitigationState.Invulnerable)
            return new(PolicyPlanStatus.ObserveOnly, "Target is invulnerable; wait for a valid target state.", [], recommendation);

        steps.Add(new PolicyStep(recommendation.Recommendation, "Execute the current recommendation", true,
            "Abort if target identity, range, or nearby threat evidence changes."));
        if (facts.Combo is ComboEvidence.Started or ComboEvidence.MidChain)
            steps.Add(new PolicyStep("Continue observed melee combo", "Preserve the confirmed combo sequence", true,
                "Abort if target leaves range, becomes protected, or the combo status disappears."));
        else if (facts.TargetCastInterruptible)
            steps.Add(new PolicyStep("Interrupt target cast", "Use the observed interrupt window", true,
                "Abort if the cast ends or interruptibility is no longer confirmed."));

        return new(PolicyPlanStatus.Planned, recommendation.Reason, steps, recommendation);
    }
}
