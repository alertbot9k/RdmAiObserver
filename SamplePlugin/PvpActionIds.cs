namespace SamplePlugin;

/// <summary>
/// Raw action IDs used when a cast transition is the only reliable evidence.
/// Cooldown capture otherwise resolves actions dynamically by name.
/// </summary>
public static class PvpActionIds
{
    public const uint StandardIssueElixir = 29055;

    public static bool TryGetKnownName(uint actionId, out string name)
    {
        if (actionId == StandardIssueElixir)
        {
            name = "Standard-issue Elixir";
            return true;
        }

        name = string.Empty;
        return false;
    }
}
