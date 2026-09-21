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
        if (result.Count == 0)
            result.Add(new("complete", DiagnosticSeverity.Info, 0, "No recording completeness problems were detected."));
        return result;
    }

    private static void Add(List<RecordingDiagnostic> result, string code, DiagnosticSeverity severity, int count, string message)
    {
        if (count > 0) result.Add(new(code, severity, count, message));
    }
}
