using System;
using System.Collections.Generic;

namespace SamplePlugin;

public enum ObservedPvpMode
{
    Unknown,
    CrystallineConflict,
    Frontline,
    RivalWings
}

/// <summary>
/// Identifies modes only when the captured state contains a reliable marker.
/// It deliberately returns Unknown instead of guessing from party size alone.
/// </summary>
public static class PvpModeDetector
{
    public static ObservedPvpMode Detect(GameState state)
    {
        if (ContainsStatus(state, "Frontline March"))
            return ObservedPvpMode.Frontline;

        if (ContainsStatus(state, "Soaring"))
            return ObservedPvpMode.RivalWings;

        // PvPDisplayActive is observed consistently in Crystalline Conflict,
        // but can be transient during loading and post-match transitions.
        if (state.PvpUiActive && state.Party.Count <= 5)
            return ObservedPvpMode.CrystallineConflict;

        return ObservedPvpMode.Unknown;
    }

    public static ObservedPvpMode Detect(IEnumerable<GameState> states)
    {
        var sawCrystallineConflict = false;
        foreach (var state in states)
        {
            var mode = Detect(state);
            if (mode is ObservedPvpMode.Frontline or ObservedPvpMode.RivalWings)
                return mode;
            if (mode == ObservedPvpMode.CrystallineConflict)
                sawCrystallineConflict = true;
        }

        return sawCrystallineConflict
            ? ObservedPvpMode.CrystallineConflict
            : ObservedPvpMode.Unknown;
    }

    private static bool ContainsStatus(GameState state, string name)
    {
        if (HasStatus(state.Player?.Statuses, name) || HasStatus(state.Target?.Statuses, name))
            return true;

        foreach (var character in state.NearbyCharacters)
        {
            if (HasStatus(character.Statuses, name))
                return true;
        }

        return false;
    }

    private static bool HasStatus(IEnumerable<StatusSnapshot>? statuses, string name)
    {
        if (statuses == null)
            return false;

        foreach (var status in statuses)
        {
            if (string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
