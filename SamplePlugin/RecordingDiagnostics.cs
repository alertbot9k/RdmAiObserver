using System;
using System.Linq;

namespace SamplePlugin;

public enum DiagnosticSeverity { Info, Warning, Error }
public sealed record RecordingDiagnostic(string Code, DiagnosticSeverity Severity, int AffectedSnapshots, string Message);

public static class RecordingDiagnostics
{
    public static List<RecordingDiagnostic> Analyze(IReadOnlyList<RecordedGameState> snapshots)
    {
        var result = new List<RecordingDiagnostic>();
        if (snapshots.Count == 0)
        {
            result.Add(new("empty", DiagnosticSeverity.Error, 0, "The recording contains no snapshots."));
            return result;
        }
        Add(result, "missing-timestamp", DiagnosticSeverity.Error, snapshots.Count(frame => frame.CapturedAtUtc == default), "Snapshots are missing timestamps.");
        Add(result, "legacy-schema", DiagnosticSeverity.Warning, snapshots.Count(frame => frame.FormatVersion < RecordingSchema.CurrentVersion), "Snapshots use an older recording schema.");
        Add(result, "missing-player", DiagnosticSeverity.Warning, snapshots.Count(frame => frame.State.Player == null), "Player state is unavailable.");
        Add(result, "incomplete-nearby-scan", DiagnosticSeverity.Warning, snapshots.Count(frame => !frame.State.NearbyScanComplete), "Nearby-player evidence is incomplete.");
        var cc = snapshots.Where(frame => PvpModeDetector.Detect(frame.State) == ObservedPvpMode.CrystallineConflict).ToArray();
        if (cc.Length == 0)
            result.Add(new("no-cc-evidence", DiagnosticSeverity.Warning, snapshots.Count, "No confirmed Crystalline Conflict evidence was recorded."));
        else
        {
            Add(result, "missing-crystal", DiagnosticSeverity.Warning, cc.Count(frame => frame.State.Objective == null), "Confirmed CC snapshots are missing crystal evidence.");
            Add(result, "missing-sprint", DiagnosticSeverity.Warning, cc.Count(frame => frame.State.Player != null &&
                !frame.State.Player.Actions.Any(action => string.Equals(action.Name, "Sprint", StringComparison.OrdinalIgnoreCase))), "Sprint readiness was not captured.");
        }
        var ordered = snapshots.OrderBy(frame => frame.CapturedAtUtc).ToArray();
        var gaps = ordered.Zip(ordered.Skip(1)).Count(pair => pair.Second.CapturedAtUtc - pair.First.CapturedAtUtc > TimeSpan.FromSeconds(5));
        Add(result, "capture-gap", DiagnosticSeverity.Warning, gaps, "Capture gaps longer than five seconds reduce transition confidence.");
        var frozenFrames = CountFrozenFrames(ordered);
        Add(result, "frozen-capture", DiagnosticSeverity.Warning, frozenFrames, "Consecutive snapshots are identical for at least eight seconds; the game connection or capture may have stalled.");
        if (result.Count == 0)
            result.Add(new("complete", DiagnosticSeverity.Info, 0, "No recording completeness problems were detected."));
        return result;
    }

    private static void Add(List<RecordingDiagnostic> result, string code, DiagnosticSeverity severity, int count, string message)
    {
        if (count > 0) result.Add(new(code, severity, count, message));
    }

    private static int CountFrozenFrames(IReadOnlyList<RecordedGameState> ordered)
    {
        var total = 0;
        var runStart = 0;
        for (var index = 1; index <= ordered.Count; index++)
        {
            var same = index < ordered.Count && IsActiveCombat(ordered[index - 1].State) && IsActiveCombat(ordered[index].State) &&
                SameDynamicState(ordered[index - 1].State, ordered[index].State);
            if (same) continue;
            if (index - runStart > 1 &&
                ordered[index - 1].CapturedAtUtc - ordered[runStart].CapturedAtUtc >= TimeSpan.FromSeconds(8))
                total += index - runStart;
            runStart = index;
        }
        return total;
    }

    private static bool IsActiveCombat(GameState state) =>
        PvpModeDetector.Detect(state) == ObservedPvpMode.CrystallineConflict &&
        state.Player is { Hp: > 0 } &&
        (state.Target?.Hp is > 0 || CombatProximity.CountEnemies(state, 25f) > 0);

    private static bool SameDynamicState(GameState left, GameState right)
    {
        var a = left.Player; var b = right.Player;
        if (a == null || b == null) return a == null && b == null && left.LoggedIn == right.LoggedIn;
        return left.LoggedIn == right.LoggedIn && left.TerritoryId == right.TerritoryId &&
            a.Hp == b.Hp && a.Mp == b.Mp && a.X == b.X && a.Y == b.Y && a.Z == b.Z &&
            left.Target?.ObjectId == right.Target?.ObjectId && left.Target?.Hp == right.Target?.Hp && left.Target?.Distance == right.Target?.Distance &&
            a.Statuses.Count == b.Statuses.Count && a.Statuses.Zip(b.Statuses).All(pair =>
                pair.First.Id == pair.Second.Id && pair.First.RemainingSeconds == pair.Second.RemainingSeconds) &&
            a.Actions.Count == b.Actions.Count && a.Actions.Zip(b.Actions).All(pair =>
                pair.First.Id == pair.Second.Id && pair.First.RemainingSeconds == pair.Second.RemainingSeconds && pair.First.CurrentCharges == pair.Second.CurrentCharges) &&
            left.Party.Count == right.Party.Count && left.Party.Zip(right.Party).All(pair =>
                pair.First.ObjectId == pair.Second.ObjectId && pair.First.Hp == pair.Second.Hp && pair.First.X == pair.Second.X && pair.First.Z == pair.Second.Z) &&
            left.NearbyCharacters.Count == right.NearbyCharacters.Count && left.NearbyCharacters.Zip(right.NearbyCharacters).All(pair =>
                pair.First.ObjectId == pair.Second.ObjectId && pair.First.Hp == pair.Second.Hp && pair.First.X == pair.Second.X && pair.First.Z == pair.Second.Z);
    }
}
