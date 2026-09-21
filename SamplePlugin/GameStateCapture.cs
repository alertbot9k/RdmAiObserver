using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;

namespace SamplePlugin;

public static class GameStateCapture
{
    public static GameState Capture()
    {
        var state = new GameState
        {
            LoggedIn = Plugin.ClientState.IsLoggedIn,
            PvpUiActive = Plugin.Condition[ConditionFlag.PvPDisplayActive],
            TerritoryId = Plugin.ClientState.TerritoryType
        };

        if (!state.LoggedIn)
            return state;

        var player = Plugin.ObjectTable.LocalPlayer;

        if (player == null)
            return state;

        var playerState = Plugin.PlayerState;
        var position = player.Position;

        state.Player = new PlayerSnapshot
        {
            ObjectId = player.EntityId,
            Name = player.Name.ToString(),
            Job = playerState.IsLoaded && playerState.ClassJob.IsValid
                ? playerState.ClassJob.Value.Name.ToString()
                : "Unknown",
            Hp = player.CurrentHp,
            MaxHp = player.MaxHp,
            Mp = player.CurrentMp,
            MaxMp = player.MaxMp,
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            Statuses = CaptureStatuses(player),
            Cast = CaptureCast(player),
            Actions = ActionCooldownTracker.Capture()
        };

        foreach (var member in Plugin.PartyList)
        {
            state.Party.Add(new PartyMemberSnapshot
            {
                ObjectId = member.EntityId,
                Name = member.Name.ToString(),
                Job = member.ClassJob.IsValid
                    ? member.ClassJob.Value.Name.ToString()
                    : "Unknown",
                Hp = member.CurrentHP,
                MaxHp = member.MaxHP,
                Distance = System.Numerics.Vector3.Distance(position, member.Position)
            });
        }

        // PlayerObjects contains battle characters only. Keep the capture local
        // so normal cities do not produce an oversized JSON snapshot.
        foreach (var character in Plugin.ObjectTable.PlayerObjects)
        {
            if (ReferenceEquals(character, player))
                continue;

            var distance = System.Numerics.Vector3.Distance(position, character.Position);
            if (distance > 60f)
                continue;

            state.NearbyCharacters.Add(new NearbyCharacterSnapshot
            {
                ObjectId = character.EntityId,
                Name = character.Name.ToString(),
                Job = character.ClassJob.IsValid
                    ? character.ClassJob.Value.Name.ToString()
                    : "Unknown",
                Kind = character.ObjectKind.ToString(),
                Distance = distance,
                Hp = character.CurrentHp,
                MaxHp = character.MaxHp,
                IsCasting = character.IsCasting,
                Cast = CaptureCast(character),
                Statuses = CaptureStatuses(character)
            });
        }

        var target = Plugin.TargetManager.Target;

        if (target != null)
        {
            state.Target = new TargetSnapshot
            {
                ObjectId = target.EntityId,
                Name = target.Name.ToString(),
                Kind = target.ObjectKind.ToString(),
                Distance = System.Numerics.Vector3.Distance(
                    position, target.Position)
            };

            // Objects such as doors do not have combat HP or status lists.
            if (target is ICharacter character)
            {
                state.Target.Hp = character.CurrentHp;
                state.Target.MaxHp = character.MaxHp;
            }

            if (target is IBattleChara battleTarget)
            {
                state.Target.Statuses = CaptureStatuses(battleTarget);
                state.Target.Cast = CaptureCast(battleTarget);
            }
        }

        return state;
    }

    private static List<StatusSnapshot> CaptureStatuses(IBattleChara character)
    {
        var result = new List<StatusSnapshot>();

        foreach (var status in character.StatusList)
        {
            if (status == null || status.StatusId == 0)
                continue;

            var data = status.GameData;
            var remaining = status.RemainingTime;

            result.Add(new StatusSnapshot
            {
                Id = status.StatusId,
                Name = data.IsValid ? data.Value.Name.ToString() : "Unknown",
                // Preserve the API value; zero is not automatically an expired status.
                RemainingSeconds = float.IsFinite(remaining) ? remaining : null,
                // Raw parameter: its meaning varies by status, so do not assume stacks.
                Param = status.Param,
                SourceId = status.SourceId
            });
        }

        return result;
    }

    private static CastSnapshot? CaptureCast(IBattleChara character)
    {
        if (!character.IsCasting)
            return null;

        return new CastSnapshot
        {
            ActionId = character.CastActionId,
            ActionType = character.CastActionType,
            CurrentSeconds = character.CurrentCastTime,
            TotalSeconds = character.TotalCastTime,
            IsInterruptible = character.IsCastInterruptible
        };
    }
}

