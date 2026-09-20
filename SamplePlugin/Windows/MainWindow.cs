using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private GameState? replayState;
    private int scenarioIndex = -1;
    private string replayMessage = "Live state";
    private readonly List<RecordedGameState> recordedStates = new();
    private bool recordingHistoryLoaded;
    private bool isRecording;
    private DateTime nextAutomaticCaptureUtc = DateTime.MinValue;

    public MainWindow(Plugin plugin, string goatImagePath)
        : base("FFXIV Observer##ObserverMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
    }

    public override void Draw()
    {
        var liveState = GameState.Capture();
        var scenario = scenarioIndex >= 0 ? ScenarioLibrary.Get(scenarioIndex) : null;
        var state = scenario?.State ?? replayState ?? liveState;

        if (ImGui.Button("Copy JSON"))
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            ImGui.SetClipboardText(json);
        }

        ImGui.SameLine();
        if (ImGui.Button("Save Current Snapshot"))
        {
            try
            {
                File.WriteAllText(ReplayFilePath, JsonSerializer.Serialize(liveState, JsonOptions));
                replayMessage = "Saved current snapshot.";
            }
            catch (Exception exception)
            {
                replayMessage = $"Could not save snapshot: {exception.Message}";
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Load Saved Snapshot"))
        {
            try
            {
                if (!File.Exists(ReplayFilePath))
                {
                    replayMessage = "No saved snapshot yet.";
                }
                else
                {
                    replayState = JsonSerializer.Deserialize<GameState>(File.ReadAllText(ReplayFilePath), JsonOptions);
                    scenarioIndex = -1;
                    replayMessage = replayState == null ? "Saved snapshot was empty." : "Replay state loaded.";
                }
            }
            catch (Exception exception)
            {
                replayMessage = $"Could not load snapshot: {exception.Message}";
            }
        }

        if (replayState != null || scenario != null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Use Live State"))
            {
                replayState = null;
                scenarioIndex = -1;
                replayMessage = "Live state";
            }
        }

        if (ImGui.Button("Previous Scenario"))
        {
            scenarioIndex = scenarioIndex <= 0 ? ScenarioLibrary.Count - 1 : scenarioIndex - 1;
            replayState = null;
            replayMessage = $"Scenario: {ScenarioLibrary.Get(scenarioIndex).Name}";
        }

        ImGui.SameLine();
        if (ImGui.Button("Next Scenario"))
        {
            scenarioIndex = (scenarioIndex + 1) % ScenarioLibrary.Count;
            replayState = null;
            replayMessage = $"Scenario: {ScenarioLibrary.Get(scenarioIndex).Name}";
        }

        if (ImGui.Button(isRecording ? "Stop Recording" : "Start Recording"))
        {
            isRecording = !isRecording;
            nextAutomaticCaptureUtc = DateTime.MinValue;

            if (isRecording)
            {
                // A recording is one test session. Do not mix previous matches
                // into a new export; the old file is replaced on the first flush.
                recordedStates.Clear();
                recordingHistoryLoaded = true;
                FlushRecording();
            }

            replayMessage = isRecording
                ? "Recording a snapshot every two seconds."
                : "Recording stopped.";

            if (!isRecording)
                FlushRecording();
        }

        ImGui.SameLine();
        if (ImGui.Button("Load Latest Recording"))
        {
            LoadLatestRecording();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("FFXIV Observer - Read Only");
        var source = scenario != null
            ? $"offline scenario: {scenario.Name}"
            : replayState != null ? "saved replay state" : "live game state";
        ImGui.TextUnformatted($"Source: {source}");
        ImGui.TextUnformatted(replayMessage);
        ImGui.Separator();

        var recommendation = DecisionEngine.Evaluate(state);
        ImGui.TextUnformatted($"Recommendation: {recommendation.Recommendation}");
        ImGui.TextUnformatted($"Priority: {recommendation.Priority}");
        ImGui.TextUnformatted($"Reason: {recommendation.Reason}");
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

    private static string ReplayFilePath => Path.Combine(
        Plugin.PluginInterface.ConfigDirectory.FullName,
        "replay-state.json");

    private static string RecordingFilePath => Path.Combine(
        Plugin.PluginInterface.ConfigDirectory.FullName,
        "recorded-states.json");

    private void SaveAutomaticCapture(GameState state)
    {
        try
        {
            if (!recordingHistoryLoaded)
            {
                recordingHistoryLoaded = true;
                if (File.Exists(RecordingFilePath))
                {
                    var existingStates = JsonSerializer.Deserialize<List<RecordedGameState>>(
                        File.ReadAllText(RecordingFilePath), JsonOptions);
                    if (existingStates != null)
                        recordedStates.AddRange(existingStates);
                }
            }

            recordedStates.Add(new RecordedGameState
            {
                CapturedAtUtc = DateTime.UtcNow,
                State = state
            });

            // Keep the most recent 10 minutes at one snapshot every two seconds.
            if (recordedStates.Count > 300)
                recordedStates.RemoveAt(0);

            // Persist every 30 seconds; stopping the recorder flushes immediately.
            if (recordedStates.Count % 15 == 0)
                FlushRecording();

            replayMessage = $"Recording: {recordedStates.Count} snapshots captured.";
        }
        catch (Exception exception)
        {
            isRecording = false;
            replayMessage = $"Recording stopped: {exception.Message}";
        }
    }

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        if (!isRecording || DateTime.UtcNow < nextAutomaticCaptureUtc)
            return;

        SaveAutomaticCapture(GameState.Capture());
        nextAutomaticCaptureUtc = DateTime.UtcNow.AddSeconds(2);
    }

    private void FlushRecording()
    {
        try
        {
            File.WriteAllText(
                RecordingFilePath,
                JsonSerializer.Serialize(recordedStates, JsonOptions));
        }
        catch (Exception exception)
        {
            isRecording = false;
            replayMessage = $"Recording stopped: {exception.Message}";
        }
    }

    private void LoadLatestRecording()
    {
        try
        {
            if (!File.Exists(RecordingFilePath))
            {
                replayMessage = "No automatic recordings yet.";
                return;
            }

            var savedStates = JsonSerializer.Deserialize<List<RecordedGameState>>(
                File.ReadAllText(RecordingFilePath), JsonOptions);

            if (savedStates == null || savedStates.Count == 0)
            {
                replayMessage = "No automatic recordings found.";
                return;
            }

            replayState = savedStates[^1].State;
            scenarioIndex = -1;
            replayMessage = $"Loaded recording from {savedStates[^1].CapturedAtUtc:HH:mm:ss} UTC.";
        }
        catch (Exception exception)
        {
            replayMessage = $"Could not load recording: {exception.Message}";
        }
    }
}

public sealed class RecordedGameState
{
    public DateTime CapturedAtUtc { get; set; }
    public GameState State { get; set; } = new();
}
