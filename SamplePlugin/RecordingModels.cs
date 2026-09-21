using System;
using System.Collections.Generic;

namespace SamplePlugin;

public static class RecordingSchema
{
    public const int CurrentVersion = 5;
}

public sealed class RecordedGameState
{
    // Version 5 adds stable object identities and normalized lifecycle facts
    // while remaining backward compatible with older snapshots.
    public int FormatVersion { get; set; } = RecordingSchema.CurrentVersion;
    public DateTime CapturedAtUtc { get; set; }
    public GameState State { get; set; } = new();
    public DecisionRecommendation? Recommendation { get; set; }
    public List<InferredActionUse> InferredActions { get; set; } = new();
}

public sealed class InferredActionUse
{
    public string Name { get; set; } = "";
    public DateTime DetectedAtUtc { get; set; }
    public uint PreviousCharges { get; set; }
    public uint CurrentCharges { get; set; }
    public float CooldownRemainingSeconds { get; set; }
    public string Confidence { get; set; } = "Unknown";
    public string Evidence { get; set; } = "Legacy recording";
}
