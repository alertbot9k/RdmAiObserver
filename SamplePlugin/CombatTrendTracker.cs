using System;
using System.Collections.Generic;

namespace SamplePlugin;

public sealed record CombatTrend(float HpLostPercent, float WindowSeconds)
{
    public bool IsRapidDamage => HpLostPercent >= 20f && WindowSeconds <= 3.25f;
}

/// <summary>
/// Maintains a short rolling HP window. This lets the advisor react to burst
/// damage instead of relying only on the current HP percentage.
/// </summary>
public sealed class CombatTrendTracker
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MinimumSampleInterval = TimeSpan.FromMilliseconds(200);
    private readonly Queue<HpSample> samples = new();

    public CombatTrend? Update(GameState state, DateTime capturedAtUtc)
    {
        var player = state.Player;
        if (player == null || player.MaxHp == 0 || player.Hp == 0)
        {
            Reset();
            return null;
        }

        var hpPercent = player.Hp * 100f / player.MaxHp;
        if (samples.Count > 0 && capturedAtUtc - samples.Peek().CapturedAtUtc > TimeSpan.FromSeconds(10))
            Reset();

        if (samples.Count == 0 ||
            capturedAtUtc - GetNewest().CapturedAtUtc >= MinimumSampleInterval ||
            Math.Abs(GetNewest().HpPercent - hpPercent) >= 0.1f)
        {
            samples.Enqueue(new HpSample(capturedAtUtc, hpPercent));
        }

        var cutoff = capturedAtUtc - Window;
        while (samples.Count > 1 && samples.Peek().CapturedAtUtc < cutoff)
            samples.Dequeue();

        var peakHp = hpPercent;
        var oldestAt = capturedAtUtc;
        foreach (var sample in samples)
        {
            peakHp = Math.Max(peakHp, sample.HpPercent);
            oldestAt = sample.CapturedAtUtc < oldestAt ? sample.CapturedAtUtc : oldestAt;
        }

        return new CombatTrend(
            Math.Max(0f, peakHp - hpPercent),
            Math.Max(0f, (float)(capturedAtUtc - oldestAt).TotalSeconds));
    }

    public void Reset() => samples.Clear();

    private HpSample GetNewest()
    {
        HpSample newest = default;
        foreach (var sample in samples)
            newest = sample;
        return newest;
    }

    private readonly record struct HpSample(DateTime CapturedAtUtc, float HpPercent);
}
