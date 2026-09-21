using SamplePlugin;
using Xunit;

namespace RdmAiObserver.Tests;

public sealed class PolicyCorpusEvaluatorTests
{
    [Fact]
    public void Empty_corpus_is_explicitly_reported()
    {
        var evaluation = PolicyCorpusEvaluator.Evaluate(new Dictionary<string, IReadOnlyList<RecordedGameState>>(),
            new SafetyPolicy(new HashSet<string>()));

        Assert.Equal(0, evaluation.Metrics.RecordingCount);
        Assert.False(evaluation.MeetsSafetyThresholds);
        Assert.Contains("No recordings", evaluation.Summary);
    }

    [Fact]
    public void Corpus_metrics_aggregate_each_recording()
    {
        var state = new GameState { TerritoryId = 1293, NearbyScanRadius = 60f, NearbyScanComplete = true,
            Player = new PlayerSnapshot { Hp = 50000, MaxHp = 58500 } };
        var corpus = new Dictionary<string, IReadOnlyList<RecordedGameState>>
        {
            ["sample"] = new[] { new RecordedGameState { CapturedAtUtc = DateTime.UnixEpoch, State = state } }
        };

        var evaluation = PolicyCorpusEvaluator.Evaluate(corpus, new SafetyPolicy(new HashSet<string> { "Target Enemy" }));

        Assert.Equal(1, evaluation.Metrics.RecordingCount);
        Assert.Equal(1, evaluation.Metrics.SnapshotCount);
        Assert.Contains("1 recordings", evaluation.Summary);
    }
}
