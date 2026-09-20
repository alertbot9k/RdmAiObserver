using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;

namespace SamplePlugin;

public sealed class GameState
{
    public bool LoggedIn { get; set; }
    public bool PvpUiActive { get; set; }
    public uint TerritoryId { get; set; }
    public PlayerSnapshot? Player { get; set; }
    public TargetSnapshot? Target { get; set; }
    public List<PartyMemberSnapshot> Party { get; set; } = new();
    public List<NearbyCharacterSnapshot> NearbyCharacters { get; set; } = new();

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
                Name = character.Name.ToString(),
                Kind = character.ObjectKind.ToString(),
                Distance = distance,
                Hp = character.CurrentHp,
                MaxHp = character.MaxHp,
                IsCasting = character.IsCasting,
                Cast = CaptureCast(character)
            });
        }

        var target = Plugin.TargetManager.Target;

        if (target != null)
        {
            state.Target = new TargetSnapshot
            {
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

public sealed class PlayerSnapshot
{
    public string Name { get; set; } = "";
    public string Job { get; set; } = "";
    public uint Hp { get; set; }
    public uint MaxHp { get; set; }
    public uint Mp { get; set; }
    public uint MaxMp { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public List<StatusSnapshot> Statuses { get; set; } = new();
    public CastSnapshot? Cast { get; set; }
    public List<ActionCooldownSnapshot> Actions { get; set; } = new();
}

public sealed class TargetSnapshot
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public float Distance { get; set; }
    public uint? Hp { get; set; }
    public uint? MaxHp { get; set; }
    // Null means this object does not expose combat statuses; [] means none observed.
    public List<StatusSnapshot>? Statuses { get; set; }
    public CastSnapshot? Cast { get; set; }
}

public sealed class StatusSnapshot
{
    public uint Id { get; set; }
    public string Name { get; set; } = "";
    public float? RemainingSeconds { get; set; }
    public ushort Param { get; set; }
    public uint SourceId { get; set; }
}

public sealed class NearbyCharacterSnapshot
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public float Distance { get; set; }
    public uint Hp { get; set; }
    public uint MaxHp { get; set; }
    public bool IsCasting { get; set; }
    public CastSnapshot? Cast { get; set; }
}

public sealed class PartyMemberSnapshot
{
    public string Name { get; set; } = "";
    public string Job { get; set; } = "";
    public uint Hp { get; set; }
    public uint MaxHp { get; set; }
    public float Distance { get; set; }
}

public sealed class CastSnapshot
{
    public uint ActionId { get; set; }
    public byte ActionType { get; set; }
    public float CurrentSeconds { get; set; }
    public float TotalSeconds { get; set; }
    public bool IsInterruptible { get; set; }
}

public sealed class ActionCooldownSnapshot
{
    public uint Id { get; set; }
    public string Name { get; set; } = "";
    public float TotalSeconds { get; set; }
    public float ElapsedSeconds { get; set; }
    public float RemainingSeconds { get; set; }
    public uint CurrentCharges { get; set; }
    public bool IsCoolingDown { get; set; }
    public bool IsAvailable { get; set; }
}
