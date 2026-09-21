using System;
using System.Linq;

namespace SamplePlugin;

public enum CcLifecycleState
{
    OutsideCc,
    Loading,
    Countdown,
    Active,
    DeadRespawning,
    Results,
    Exited
}

public sealed record CcLifecycleObservation(CcLifecycleState State, string Reason);

/// <summary>
/// Stateful interpretation of conservative, observed CC transitions. It does
/// not control the game and does not infer queue state from missing evidence.
/// </summary>
public sealed class MatchLifecycleTracker
{
    private bool wasInCc;
    private bool becameActive;
    private bool awaitingRespawnRelease;

    public CcLifecycleObservation Update(GameState state)
    {
        var inCc = PvpModeDetector.Detect(state) == ObservedPvpMode.CrystallineConflict;
        if (!inCc)
        {
            if (wasInCc)
            {
                ResetMatch();
                wasInCc = false;
                return new(CcLifecycleState.Exited, "The previous snapshot was confirmed CC and the current snapshot is outside it.");
            }

            return new(CcLifecycleState.OutsideCc, "No confirmed Crystalline Conflict evidence is present.");
        }

        wasInCc = true;
        var player = state.Player;
        if (player == null)
            return new(CcLifecycleState.Loading, "The CC context is known, but the local player is not available yet.");

        var protectedState = HasStatus(player, "Invincibility");

        // Both supplied territory-1116 matches expose this flag only at the
        // post-match boundary. Require prior active play so it cannot turn an
        // initial loading/countdown frame into Results.
        if (becameActive && state.PvpUiActive)
        {
            awaitingRespawnRelease = false;
            return new(CcLifecycleState.Results, "The PvP results display became active after confirmed match activity.");
        }

        if (!becameActive)
        {
            if (protectedState)
                return new(CcLifecycleState.Countdown, "Initial spawn protection is active before observed combat activity.");

            if (player.Hp == 0)
                return new(CcLifecycleState.Loading, "The initial CC player snapshot is not yet alive.");

            becameActive = true;
            return new(CcLifecycleState.Active, "A living, unprotected player is present in confirmed CC.");
        }

        if (player.Hp == 0)
        {
            awaitingRespawnRelease = true;
            return new(CcLifecycleState.DeadRespawning, "The player is incapacitated during the active match.");
        }

        if (awaitingRespawnRelease)
        {
            if (protectedState)
                return new(CcLifecycleState.DeadRespawning, "The player has respawned but remains under spawn protection.");

            awaitingRespawnRelease = false;
        }

        return new(CcLifecycleState.Active, "The player is alive in an active confirmed CC match.");
    }

    public void Reset()
    {
        wasInCc = false;
        ResetMatch();
    }

    private void ResetMatch()
    {
        becameActive = false;
        awaitingRespawnRelease = false;
    }

    private static bool HasStatus(PlayerSnapshot player, string name) =>
        player.Statuses.Any(status => string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase));
}
