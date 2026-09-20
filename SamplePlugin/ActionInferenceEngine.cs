using System;
using System.Collections.Generic;

namespace SamplePlugin;

/// <summary>
/// Infers a conservative action timeline from cooldown and status transitions.
/// Ambiguous one-second shared recasts and status loss on death are ignored.
/// </summary>
public static class ActionInferenceEngine
{
    public static List<InferredActionUse> Infer(
        GameState? previous,
        GameState current,
        DateTime detectedAtUtc)
    {
        var result = new List<InferredActionUse>();
        if (previous?.Player == null || current.Player == null)
            return result;

        var previousActions = new Dictionary<string, ActionCooldownSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in previous.Player.Actions)
            previousActions[action.Name] = action;

        foreach (var action in current.Player.Actions)
        {
            if (!previousActions.TryGetValue(action.Name, out var oldAction))
                continue;

            var actionSpecificRecast = action.TotalSeconds > 2.5f;
            var chargeSpent = actionSpecificRecast &&
                              action.CurrentCharges < oldAction.CurrentCharges;
            var cooldownStarted = !oldAction.IsCoolingDown &&
                                  action.IsCoolingDown &&
                                  actionSpecificRecast &&
                                  action.RemainingSeconds > 2.5f;

            if (!chargeSpent && !cooldownStarted)
                continue;

            result.Add(new InferredActionUse
            {
                Name = action.Name,
                DetectedAtUtc = detectedAtUtc,
                PreviousCharges = oldAction.CurrentCharges,
                CurrentCharges = action.CurrentCharges,
                CooldownRemainingSeconds = action.RemainingSeconds,
                Confidence = "High",
                Evidence = chargeSpent ? "Action-specific charge spent" : "Action-specific cooldown started"
            });
        }

        if (current.Player.Hp > 0)
            InferProcConsumption(previous.Player.Statuses, current.Player.Statuses, detectedAtUtc, result);

        return result;
    }

    private static void InferProcConsumption(
        List<StatusSnapshot> previous,
        List<StatusSnapshot> current,
        DateTime detectedAtUtc,
        List<InferredActionUse> result)
    {
        AddConsumedProc(previous, current, "Prefulgence Ready", "Prefulgence", detectedAtUtc, result);
        AddConsumedProc(previous, current, "Thorned Flourish", "Vice of Thorns", detectedAtUtc, result);
        AddConsumedProc(previous, current, "Dualcast", "Grand Impact", detectedAtUtc, result);

        if (HasStatus(previous, "Enchanted Riposte") && HasStatus(current, "Enchanted Zwerchhau"))
            AddInferred("Enchanted Zwerchhau", detectedAtUtc, result, "Melee combo advanced");
        if (HasStatus(previous, "Enchanted Zwerchhau") && HasStatus(current, "Enchanted Redoublement"))
            AddInferred("Enchanted Redoublement", detectedAtUtc, result, "Melee combo advanced");
        if (WasConsumed(previous, current, "Enchanted Redoublement"))
            AddInferred("Scorch", detectedAtUtc, result, "Melee combo proc consumed");
    }

    private static void AddConsumedProc(
        List<StatusSnapshot> previous,
        List<StatusSnapshot> current,
        string statusName,
        string actionName,
        DateTime detectedAtUtc,
        List<InferredActionUse> result)
    {
        if (WasConsumed(previous, current, statusName))
            AddInferred(actionName, detectedAtUtc, result, $"{statusName} consumed");
    }

    private static bool WasConsumed(
        List<StatusSnapshot> previous,
        List<StatusSnapshot> current,
        string statusName)
    {
        StatusSnapshot? previousStatus = null;
        foreach (var status in previous)
        {
            if (string.Equals(status.Name, statusName, StringComparison.OrdinalIgnoreCase))
            {
                previousStatus = status;
                break;
            }
        }

        if (previousStatus == null || HasStatus(current, statusName))
            return false;

        return previousStatus.RemainingSeconds is null or > 2.5f;
    }

    private static bool HasStatus(List<StatusSnapshot> statuses, string name)
    {
        foreach (var status in statuses)
        {
            if (string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void AddInferred(
        string name,
        DateTime detectedAtUtc,
        List<InferredActionUse> result,
        string evidence)
    {
        foreach (var existing in result)
        {
            if (string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
                return;
        }

        result.Add(new InferredActionUse
        {
            Name = name,
            DetectedAtUtc = detectedAtUtc,
            Confidence = "High",
            Evidence = evidence
        });
    }
}
