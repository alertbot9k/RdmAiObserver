using System;
using System.Collections.Generic;
using System.Linq;

namespace SamplePlugin;

public sealed record CorpusSimulationMetrics(
    int RecordingCount,
    int SnapshotCount,
    int SimulatedCommands,
    int RejectedCommands,
    int RecoveryTransitions,
    int MaxFailureStreak,
    int EmergencyStops,
    int VerificationFailures)
{
    public float AcceptanceRate => SimulatedCommands + RejectedCommands == 0
        ? 0f : SimulatedCommands * 100f / (SimulatedCommands + RejectedCommands);
    public float RejectionRate => 100f - AcceptanceRate;
}

public sealed record CorpusEvaluation(
    CorpusSimulationMetrics Metrics,
    IReadOnlyDictionary<string, PolicySimulationResult> PerRecording,
    bool MeetsSafetyThresholds,
    string Summary);

public static class PolicyCorpusEvaluator
{
    public static CorpusEvaluation Evaluate(
        IReadOnlyDictionary<string, IReadOnlyList<RecordedGameState>> corpus,
        SafetyPolicy policy,
        float maximumRejectionRate = 25f,
        int maximumFailureStreak = 3)
    {
        var results = new Dictionary<string, PolicySimulationResult>(StringComparer.Ordinal);
        foreach (var pair in corpus)
            results[pair.Key] = PolicySimulator.Run(pair.Value, policy, pair.Value.FirstOrDefault()?.CapturedAtUtc);

        var metrics = new CorpusSimulationMetrics(
            results.Count,
            results.Values.Sum(result => result.SnapshotCount),
            results.Values.Sum(result => result.SimulatedCommands),
            results.Values.Sum(result => result.RejectedCommands),
            results.Values.Sum(result => result.RecoveryTransitions),
            results.Values.Count == 0 ? 0 : results.Values.Max(result => result.MaxFailureStreak),
            results.Values.Sum(result => result.EmergencyStops),
            results.Values.Sum(result => result.VerificationFailures));
        var safe = metrics.RejectionRate <= maximumRejectionRate &&
                   metrics.MaxFailureStreak <= maximumFailureStreak && metrics.EmergencyStops == 0;
        var summary = results.Count == 0
            ? "No recordings were available for corpus evaluation."
            : $"{metrics.RecordingCount} recordings; acceptance {metrics.AcceptanceRate:F1}%; rejection {metrics.RejectionRate:F1}%; max failure streak {metrics.MaxFailureStreak}; emergency stops {metrics.EmergencyStops}.";
        return new(metrics, results, safe, summary);
    }
}
