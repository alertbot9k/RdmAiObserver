using System.Collections.Generic;

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
    public string Job { get; set; } = "";
    public string Kind { get; set; } = "";
    public float Distance { get; set; }
    public uint Hp { get; set; }
    public uint MaxHp { get; set; }
    public bool IsCasting { get; set; }
    public CastSnapshot? Cast { get; set; }
    public List<StatusSnapshot> Statuses { get; set; } = new();
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
