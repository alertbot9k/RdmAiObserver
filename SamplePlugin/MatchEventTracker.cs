using System;
using System.Linq;

namespace SamplePlugin;

public sealed record MatchEventSummary(
    int Deaths,
    int Respawns,
    int SpawnProtectionEntries,
    int SpawnProtectionExits,
    int TargetChanges,
    int EngagementStarts,
    int EngagementEnds);

public static class MatchEventTracker
{
    public static MatchEventSummary Analyze(IReadOnlyList<RecordedGameState> snapshots)
    {
        var deaths = 0; var respawns = 0; var protectionIn = 0; var protectionOut = 0;
        var targetChanges = 0; var engagementStarts = 0; var engagementEnds = 0;
        bool? alive = null; bool? protectedState = null; bool? engaged = null;
        ulong targetId = 0; string? targetName = null;
        foreach (var frame in snapshots.OrderBy(frame => frame.CapturedAtUtc))
        {
            var state = frame.State;
            var nowAlive = state.Player is { Hp: > 0 };
            var nowProtected = nowAlive && HasStatus(state.Player!.Statuses, "Invincibility");
            var nowEngaged = nowAlive && !nowProtected &&
                (state.Target?.Hp is > 0 || CombatProximity.CountEnemies(state, 25f) > 0);
            var newTargetId = state.Target?.ObjectId ?? 0;
            var newTargetName = state.Target?.Name;
            if (alive == true && !nowAlive) deaths++;
            if (alive == false && nowAlive) respawns++;
            if (protectedState == false && nowProtected) protectionIn++;
            if (protectedState == true && !nowProtected) protectionOut++;
            if (engaged == false && nowEngaged) engagementStarts++;
            if (engaged == true && !nowEngaged) engagementEnds++;
            if (HasTarget(targetId, targetName) && HasTarget(newTargetId, newTargetName) &&
                !SameTarget(targetId, targetName, newTargetId, newTargetName)) targetChanges++;
            alive = nowAlive; protectedState = nowProtected; engaged = nowEngaged;
            targetId = newTargetId; targetName = newTargetName;
        }
        return new(deaths, respawns, protectionIn, protectionOut, targetChanges, engagementStarts, engagementEnds);
    }

    private static bool HasStatus(IEnumerable<StatusSnapshot> statuses, string name) =>
        statuses.Any(status => string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase));
    private static bool HasTarget(ulong id, string? name) => id != 0 || !string.IsNullOrWhiteSpace(name);
    private static bool SameTarget(ulong leftId, string? leftName, ulong rightId, string? rightName) =>
        leftId != 0 && rightId != 0 ? leftId == rightId : string.Equals(leftName, rightName, StringComparison.Ordinal);
}
