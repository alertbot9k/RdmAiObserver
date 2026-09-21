using System;
using System.Collections.Generic;

namespace SamplePlugin;

public enum PolicyPlanStatus { Planned, ObserveOnly, Recover }

public sealed record PolicyStep(
    string Action,
    string Purpose,
    bool Interruptible,
    string AbortIf,
    ControlCommandKind Kind = ControlCommandKind.Action,
    string? ActionName = null,
    ulong TargetObjectId = 0);

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

        if (recommendation.Recommendation == "Hold Guard")
            return new(PolicyPlanStatus.Recover, recommendation.Reason, [], recommendation);

        if (recommendation.Recommendation == "Finish Standard-issue Elixir")
            return new(PolicyPlanStatus.Recover, recommendation.Reason, [], recommendation);

        var steps = CreateSteps(recommendation, facts);
        if (steps.Count == 0)
            return new(PolicyPlanStatus.ObserveOnly,
                "The recommendation does not resolve to a concrete, typed command.", [], recommendation);

        if (recommendation.Recommendation is "Use Recuperate" or "Use Forte" or "Purify" or "Use Guard")
        {
            return new(PolicyPlanStatus.Planned, recommendation.Reason, steps, recommendation);
        }

        if (facts.TargetMitigation == MitigationState.Invulnerable)
            return new(PolicyPlanStatus.ObserveOnly, "Target is invulnerable; wait for a valid target state.", [], recommendation);

        return new(PolicyPlanStatus.Planned, recommendation.Reason, steps, recommendation);
    }

    private static List<PolicyStep> CreateSteps(DecisionRecommendation recommendation, ObservationFacts facts)
    {
        const string combatAbort = "Abort if target identity, range, or nearby threat evidence changes.";
        const string survivalAbort = "Abort if player protection, crowd control, or target threat facts change.";
        var text = recommendation.Recommendation;
        var purpose = recommendation.Priority is DecisionPriority.Defend or DecisionPriority.Recover or DecisionPriority.Purify
            ? "Stabilize survival state"
            : "Execute the current recommendation";
        var abortIf = purpose == "Stabilize survival state" ? survivalAbort : combatAbort;

        if (text == "Cancel Elixir and move")
        {
            return
            [
                new(text, "Cancel the unsafe recovery cast", true, survivalAbort, ControlCommandKind.CancelCast),
                new("Move away from nearby threats", "Create a safe recovery window", true, survivalAbort, ControlCommandKind.Move)
            ];
        }

        if (text is "Kite toward your team" or "Guard and disengage" or "Disengage immediately" or
            "Disengage toward your team" or "Close distance to continue the melee combo" or "Move into spell range")
            return [new(text, purpose, true, abortIf, ControlCommandKind.Move)];

        if (text.StartsWith("Target ", StringComparison.Ordinal) ||
            text.StartsWith("Switch to ", StringComparison.Ordinal) || text is "Find a live target" or "Find another target")
        {
            var target = TargetEvaluator.FindBest(facts.State);
            return target == null
                ? []
                : [new(text, purpose, true, abortIf, ControlCommandKind.SelectTarget, TargetObjectId: target.ObjectId)];
        }

        if (text == "Use Displacement, then Scorch")
            return [Action("Displacement", purpose, abortIf), Action("Scorch", purpose, abortIf)];
        if (text == "Corps-a-corps, then Enchanted Riposte")
            return [Action("Corps-a-corps", purpose, abortIf), Action("Enchanted Riposte", purpose, abortIf)];
        if (text == "Start Enchanted Riposte")
            return [Action("Enchanted Riposte", purpose, abortIf)];
        if (text == "Purify")
            return [Action("Purify", purpose, abortIf)];
        if (text.StartsWith("Use ", StringComparison.Ordinal))
            return [Action(text[4..], purpose, abortIf)];

        return [];
    }

    private static PolicyStep Action(string actionName, string purpose, string abortIf) =>
        new($"Use {actionName}", purpose, true, abortIf, ControlCommandKind.Action, actionName);
}
