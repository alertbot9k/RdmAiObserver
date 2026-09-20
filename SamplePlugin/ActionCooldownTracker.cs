using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace SamplePlugin;

/// <summary>
/// Reads cooldown state only. It never requests or executes an action.
/// </summary>
public static class ActionCooldownTracker
{
    private static readonly string[] TrackedNames =
    {
        "Enchanted Riposte",
        "Resolution",
        "Embolden",
        "Corps-a-corps",
        "Displacement",
        "Forte",
        "Recuperate",
        "Purify",
        "Guard"
    };

    private static readonly Dictionary<string, uint> ActionIds = new(StringComparer.OrdinalIgnoreCase);
    private static bool initialized;

    public static unsafe List<ActionCooldownSnapshot> Capture()
    {
        EnsureActionIds();
        var result = new List<ActionCooldownSnapshot>(ActionIds.Count);
        var manager = ActionManager.Instance();

        if (manager == null)
            return result;

        foreach (var name in TrackedNames)
        {
            if (!ActionIds.TryGetValue(name, out var id))
                continue;

            var total = manager->GetRecastTime(ActionType.Action, id);
            var elapsed = manager->GetRecastTimeElapsed(ActionType.Action, id);
            var coolingDown = manager->IsRecastTimerActive(ActionType.Action, id);
            var charges = (uint)manager->GetCurrentCharges(id);
            var remaining = coolingDown ? Math.Max(0f, total - elapsed) : 0f;

            result.Add(new ActionCooldownSnapshot
            {
                Id = id,
                Name = name,
                TotalSeconds = total,
                ElapsedSeconds = elapsed,
                RemainingSeconds = remaining,
                CurrentCharges = charges,
                IsCoolingDown = coolingDown,
                // A short recast is the shared/global cooldown, not this
                // action's real resource cooldown. Keep showing the next
                // recommendation while that short recast finishes.
                IsAvailable = charges > 0 || !coolingDown || total <= 2.5f
            });
        }

        return result;
    }

    private static void EnsureActionIds()
    {
        if (initialized)
            return;

        initialized = true;
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();

        foreach (var action in sheet)
        {
            if (!action.IsPvP)
                continue;

            var name = action.Name.ToString();
            foreach (var trackedName in TrackedNames)
            {
                if (!ActionIds.ContainsKey(trackedName) &&
                    string.Equals(name, trackedName, StringComparison.OrdinalIgnoreCase))
                {
                    ActionIds[trackedName] = action.RowId;
                    break;
                }
            }
        }
    }
}
