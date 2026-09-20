using System.Collections.Generic;

namespace SamplePlugin;

public enum ObservedPvpMode
{
    Unknown,
    CrystallineConflict
}

/// <summary>
/// Identifies modes only from confirmed territory/UI evidence. Combat statuses
/// are deliberately ignored because status names do not identify the duty.
/// </summary>
public static class PvpModeDetector
{
    // Confirmed from a user-recorded Crystalline Conflict match. Add territory
    // IDs only after they have been verified from known-duty recordings.
    private const uint ConfirmedCrystallineConflictTerritory = 1293;

    public static ObservedPvpMode Detect(GameState state)
    {
        if (state.TerritoryId == ConfirmedCrystallineConflictTerritory)
            return ObservedPvpMode.CrystallineConflict;

        // The live CC party contains exactly five members. PvPUiActive can be
        // transient during loading, so recordings also retain territory proof.
        if (state.PvpUiActive && state.Party.Count == 5)
            return ObservedPvpMode.CrystallineConflict;

        return ObservedPvpMode.Unknown;
    }

    public static ObservedPvpMode Detect(IEnumerable<GameState> states)
    {
        foreach (var state in states)
        {
            if (Detect(state) == ObservedPvpMode.CrystallineConflict)
                return ObservedPvpMode.CrystallineConflict;
        }

        return ObservedPvpMode.Unknown;
    }
}
