using System;
using System.Collections.Generic;

namespace SamplePlugin;

public static class RecordingSchema
{
    public const int CurrentVersion = 7;
}

public sealed class RecordedGameState
{
    // Version 7 adds the confirmed crystal snapshot and combatant world positions
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
