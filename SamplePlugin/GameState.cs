namespace SamplePlugin;

public sealed class GameState
{
    public bool LoggedIn { get; set; }
    public uint TerritoryId { get; set; }
    public PlayerSnapshot? Player { get; set; }
    public TargetSnapshot? Target { get; set; }

    public static GameState Capture()
    {
        var state = new GameState
        {
            LoggedIn = Plugin.ClientState.IsLoggedIn,
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
            Z = position.Z
        };

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
        }

        return state;
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
}

public sealed class TargetSnapshot
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public float Distance { get; set; }
}
