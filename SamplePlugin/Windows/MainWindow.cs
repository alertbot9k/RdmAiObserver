using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("FFXIV Observer##ObserverMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var state = GameState.Capture();

        ImGui.TextUnformatted("FFXIV Observer - Read Only");
        ImGui.TextUnformatted("Source: structured GameState snapshot");
        ImGui.Separator();

        ImGui.TextUnformatted(
            $"Logged in: {(state.LoggedIn ? "YES" : "NO")}");
        ImGui.TextUnformatted($"Territory ID: {state.TerritoryId}");

        var player = state.Player;

        if (player == null)
        {
            ImGui.TextUnformatted("Waiting for player data.");
            return;
        }

        ImGui.Spacing();
        ImGui.TextUnformatted($"Player: {player.Name}");
        ImGui.TextUnformatted($"Job: {player.Job}");
        ImGui.TextUnformatted($"HP: {player.Hp:N0} / {player.MaxHp:N0}");
        ImGui.TextUnformatted($"MP: {player.Mp:N0} / {player.MaxMp:N0}");
        ImGui.TextUnformatted(
            $"Position: X {player.X:F2}  Y {player.Y:F2}  Z {player.Z:F2}");

        ImGui.Spacing();
        ImGui.Separator();

        var target = state.Target;

        if (target == null)
        {
            ImGui.TextUnformatted("Target: None");
        }
        else
        {
            ImGui.TextUnformatted($"Target: {target.Name}");
            ImGui.TextUnformatted($"Target type: {target.Kind}");
            ImGui.TextUnformatted($"Distance: {target.Distance:F1} units");
        }
    }
}
