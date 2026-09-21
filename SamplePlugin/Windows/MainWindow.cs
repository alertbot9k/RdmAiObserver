using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private GameState? replayState;
    private int scenarioIndex = -1;
    private string replayMessage = "Live state";
    private readonly List<RecordedGameState> recordedStates = new();
    private bool recordingHistoryLoaded;
    private bool isRecording;
    private DateTime nextAutomaticCaptureUtc = DateTime.MinValue;
    private ReplayAnalysis? replayAnalysis;
    private ReplayReport? replayReport;
    private PolicySimulationResult? policySimulation;
    private readonly RecommendationStabilizer recommendationStabilizer = new();
    private readonly CombatTrendTracker liveTrendTracker = new();
    private readonly CombatTrendTracker recordingTrendTracker = new();

    public MainWindow()
        : base($"FFXIV Observer v{BuildInfo.Version}##ObserverMain")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        var validation = ScenarioLibrary.ValidateAll();
        replayMessage = validation.Failures.Count == 0
            ? $"Offline checks: {validation.Passed}/{validation.Total} passed."
            : $"Offline checks: {validation.Passed}/{validation.Total} passed. First failure: {validation.Failures[0]}";

        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        if (recordedStates.Count == 0)
            return;

        FlushRecording();
        try
        {
            SaveReplayReport(recordedStates);
        }
        catch (Exception exception)
        {
            Plugin.Log.Warning($"Could not save the final replay analysis during shutdown: {exception.Message}");
        }
    }

    public override void Draw()
    {
        var liveState = GameStateCapture.Capture();
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
                WriteJsonAtomically(ReplayFilePath, liveState);
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
                recommendationStabilizer.Reset();
                liveTrendTracker.Reset();
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

        ImGui.SameLine();
        if (ImGui.Button("Run All Offline Checks"))
        {
            var validation = ScenarioLibrary.ValidateAll();
            replayMessage = validation.Failures.Count == 0
                ? $"Offline checks: {validation.Passed}/{validation.Total} passed."
                : $"Offline checks: {validation.Passed}/{validation.Total} passed. First failure: {validation.Failures[0]}";
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
                recordingTrendTracker.Reset();
                FlushRecording();
            }

            replayMessage = isRecording
                ? "Recording a snapshot every two seconds."
                : "Recording stopped.";

            if (!isRecording)
            {
                FlushRecording();
                if (recordedStates.Count > 0)
                {
                    try
                    {
                        SaveReplayReport(recordedStates);
                        replayMessage = $"Recording stopped. Analysis saved with {recordedStates.Count} snapshots.";
                    }
                    catch (Exception exception)
                    {
                        replayMessage = $"Recording saved, but analysis failed: {exception.Message}";
                    }
                }
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Load Latest Recording"))
        {
            LoadLatestRecording();
        }

        ImGui.SameLine();
        if (ImGui.Button("Analyze Recording"))
        {
            AnalyzeLatestRecording();
        }

        ImGui.SameLine();
        if (ImGui.Button("Simulate Policy (No Input)"))
        {
            SimulateLatestRecording();
        }

        if (replayReport != null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Copy Analysis JSON"))
                ImGui.SetClipboardText(JsonSerializer.Serialize(replayReport, JsonOptions));
        }

        if (policySimulation != null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Copy Policy Simulation JSON"))
                ImGui.SetClipboardText(JsonSerializer.Serialize(policySimulation, JsonOptions));
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("FFXIV Observer - Read Only");
        ImGui.TextUnformatted($"Build {BuildInfo.Version} ({BuildInfo.Milestone})");
        var source = scenario != null
            ? $"offline scenario: {scenario.Name}"
            : replayState != null ? "saved replay state" : "live game state";
        var observedMode = PvpModeDetector.Detect(state);
        ImGui.TextUnformatted($"Source: {source}");
        ImGui.TextUnformatted(replayMessage);
        ImGui.TextUnformatted($"Detected mode: {observedMode}");

        if (replayAnalysis != null && ImGui.CollapsingHeader("Offline replay analysis"))
        {
            ImGui.TextUnformatted($"Detected mode: {replayAnalysis.Mode}");
            ImGui.TextWrapped(replayAnalysis.CalibrationNote);
            ImGui.TextUnformatted($"Duration: {replayAnalysis.DurationSeconds / 60f:F1} minutes");
            ImGui.TextUnformatted($"Snapshots: {replayAnalysis.SnapshotCount} ({replayAnalysis.ActiveSnapshotCount} active)");
            ImGui.TextUnformatted($"Deaths / respawns: {replayAnalysis.DeathCount} / {replayAnalysis.RespawnCount}");
            ImGui.TextUnformatted($"Lowest active HP: {replayAnalysis.LowestHpPercent:F0}%");
            ImGui.TextUnformatted($"Critical-HP snapshots: {replayAnalysis.LowHpSnapshotCount}");
            ImGui.TextUnformatted($"Rapid-damage snapshots: {replayAnalysis.RapidDamageSnapshotCount}");
            ImGui.TextUnformatted($"Defensive recommendations: {replayAnalysis.DefensiveRecommendationCount}");
            ImGui.TextUnformatted($"No-target time: {replayAnalysis.NoTargetPercent:F0}% of engaged snapshots ({replayAnalysis.EngagedSnapshotCount} evaluated)");
            ImGui.TextUnformatted($"Protected / isolated snapshots: {replayAnalysis.ProtectedSnapshotCount} / {replayAnalysis.IsolatedSnapshotCount}");
            ImGui.TextUnformatted($"Target beyond 25 yalms: {replayAnalysis.OutOfRangeTargetSnapshotCount} snapshots");
            ImGui.TextUnformatted($"Safe Elixir opportunities: {replayAnalysis.ElixirOpportunitySnapshotCount}");
            ImGui.TextUnformatted($"Target Guard snapshots: {replayAnalysis.GuardingTargetSnapshotCount}");
            ImGui.TextUnformatted($"Procs consumed / expired: {replayAnalysis.ConsumedProcCount} / {replayAnalysis.ExpiredProcCount}");
            ImGui.TextUnformatted($"Observed actions: {replayAnalysis.InferredActionCount}");
            ImGui.TextUnformatted($"Advice/action windows matched: {replayAnalysis.MatchingActionCount} / {replayAnalysis.EvaluatedActionCount} ({replayAnalysis.MatchPercent:F0}%)");
            ImGui.TextUnformatted($"Recommendation changes: {replayAnalysis.RecommendationChanges} ({replayAnalysis.ChangesPerMinute:F1}/minute)");
            ImGui.TextUnformatted($"Most common advice: {replayAnalysis.MostCommonRecommendation}");
            ImGui.TextUnformatted($"Most common action: {replayAnalysis.MostCommonAction}");
        }


        if (policySimulation != null && ImGui.CollapsingHeader("Policy shadow simulation"))
        {
            ImGui.TextWrapped("Dry-run only: no target, movement, action, or game input was sent.");
            ImGui.TextUnformatted($"Snapshots: {policySimulation.SnapshotCount}");
            ImGui.TextUnformatted($"Planned / observe-only: {policySimulation.PlannedSnapshots} / {policySimulation.ObserveOnlySnapshots}");
            ImGui.TextUnformatted($"Simulated / rejected commands: {policySimulation.SimulatedCommands} / {policySimulation.RejectedCommands}");
            ImGui.TextUnformatted($"Acceptance: {policySimulation.AcceptancePercent:F1}%");
            ImGui.TextUnformatted($"Recovery transitions / safety fallbacks: {policySimulation.RecoveryTransitions} / {policySimulation.SafetyFallbacks}");
            ImGui.TextUnformatted($"Verified / verification failures: {policySimulation.VerifiedCommands} / {policySimulation.VerificationFailures}");
            ImGui.TextUnformatted($"Not observable at snapshot interval: {policySimulation.UnverifiableCommands}");
            ImGui.TextUnformatted($"Timing / game rejection failures: {policySimulation.TimingFailures} / {policySimulation.GameRejectedCommands}");
            ImGui.TextUnformatted($"Maximum failure streak: {policySimulation.MaxFailureStreak}");
            ImGui.TextUnformatted($"Emergency stops: {policySimulation.EmergencyStops}");
        }

        ImGui.Separator();

        var liveTrend = scenario == null && replayState == null
            ? liveTrendTracker.Update(liveState, DateTime.UtcNow)
            : null;
        var decisionTrend = scenario?.Trend ?? liveTrend;
        var rawRecommendation = DecisionEngine.Evaluate(state, decisionTrend);
        var recommendation = scenario != null || replayState != null
            ? rawRecommendation
            : recommendationStabilizer.Select(rawRecommendation, DateTime.UtcNow);
        ImGui.TextUnformatted($"Recommendation: {recommendation.Recommendation}");
        ImGui.TextUnformatted($"Priority: {recommendation.Priority}");
        ImGui.TextWrapped($"Reason: {recommendation.Reason}");
        if (scenario != null)
        {
            var passed = string.Equals(
                recommendation.Recommendation,
                scenario.ExpectedRecommendation,
                StringComparison.Ordinal);
            ImGui.TextUnformatted($"Offline check: {(passed ? "PASS" : "FAIL")}");
            if (!passed)
                ImGui.TextUnformatted($"Expected: {scenario.ExpectedRecommendation}");
        }
        ImGui.Separator();

        ImGui.TextUnformatted(
            $"Logged in: {(state.LoggedIn ? "YES" : "NO")}");
        ImGui.TextUnformatted($"Territory ID: {state.TerritoryId}");
        ImGui.TextUnformatted($"Detected PvP mode: {observedMode}");

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
        if (decisionTrend is { HpLostPercent: > 0.5f })
            ImGui.TextUnformatted($"Recent HP loss: {decisionTrend.HpLostPercent:F0}% over {decisionTrend.WindowSeconds:F1}s");
        ImGui.TextUnformatted(
            $"Position: X {player.X:F2}  Y {player.Y:F2}  Z {player.Z:F2}");

        if (player.Actions.Count > 0 && ImGui.CollapsingHeader("Tracked cooldowns"))
        {
            foreach (var action in player.Actions)
            {
                var stateText = action.IsAvailable
                    ? $"ready ({action.CurrentCharges} charge(s))"
                    : $"{action.RemainingSeconds:F1}s";
                ImGui.TextUnformatted($"{action.Name}: {stateText}");
            }
        }

        if (player.Statuses.Count > 0 && ImGui.CollapsingHeader("Player statuses"))
        {
            foreach (var status in player.Statuses)
            {
                var remaining = status.RemainingSeconds is >= 0f
                    ? $" ({status.RemainingSeconds:F1}s)"
                    : "";
                ImGui.TextUnformatted($"{status.Name}{remaining}");
            }
        }

        if (state.NearbyWorldObjects.Count > 0 && ImGui.CollapsingHeader("Objective discovery objects"))
        {
            ImGui.TextWrapped("Observed non-player objects only; no object is assumed to be the crystal until its BaseId is confirmed from recordings.");
            foreach (var worldObject in state.NearbyWorldObjects.OrderBy(worldObject => worldObject.Distance))
            {
                ImGui.TextUnformatted(
                    $"{worldObject.Name} [{worldObject.Kind}/{worldObject.SubKind}] BaseId {worldObject.BaseId} at {worldObject.Distance:F1}y " +
                    $"({worldObject.X:F1}, {worldObject.Y:F1}, {worldObject.Z:F1})");
            }
        }

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
            if (target.Hp is uint targetHp && target.MaxHp is uint targetMaxHp && targetMaxHp > 0)
                ImGui.TextUnformatted($"Target HP: {targetHp:N0} / {targetMaxHp:N0} ({targetHp * 100f / targetMaxHp:F0}%)");

            if (target.Statuses is { Count: > 0 } && ImGui.CollapsingHeader("Target statuses"))
            {
                foreach (var status in target.Statuses)
                {
                    var remaining = status.RemainingSeconds is >= 0f
                        ? $" ({status.RemainingSeconds:F1}s)"
                        : "";
                    ImGui.TextUnformatted($"{status.Name}{remaining}");
                }
            }
        }
    }

    private static string ReplayFilePath => Path.Combine(
        Plugin.PluginInterface.ConfigDirectory.FullName,
        "replay-state.json");

    private static string RecordingFilePath => Path.Combine(
        Plugin.PluginInterface.ConfigDirectory.FullName,
        "recorded-states.json");

    private static string AnalysisFilePath => Path.Combine(
        Plugin.PluginInterface.ConfigDirectory.FullName,
        "replay-analysis.json");

    private static string PolicySimulationFilePath => Path.Combine(
        Plugin.PluginInterface.ConfigDirectory.FullName,
        "policy-simulation.json");

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

            var capturedAt = DateTime.UtcNow;
            var trend = recordingTrendTracker.Update(state, capturedAt);
            var previousState = recordedStates.Count > 0
                ? recordedStates[^1].State
                : null;
            var inferredActions = ActionInferenceEngine.Infer(previousState, state, capturedAt);

            recordedStates.Add(new RecordedGameState
            {
                FormatVersion = RecordingSchema.CurrentVersion,
                CapturedAtUtc = capturedAt,
                State = state,
                Recommendation = DecisionEngine.Evaluate(state, trend),
                InferredActions = inferredActions
            });

            // Keep the most recent 10 minutes at one snapshot every two seconds.
            if (recordedStates.Count > 300)
                recordedStates.RemoveAt(0);

            // Persist every 30 seconds; stopping the recorder flushes immediately.
            if (recordedStates.Count % 15 == 0)
                FlushRecording();

            replayMessage = inferredActions.Count == 0
                ? $"Recording: {recordedStates.Count} snapshots captured."
                : $"Observed action: {string.Join(", ", inferredActions.Select(action => action.Name))}";
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

        SaveAutomaticCapture(GameStateCapture.Capture());
        nextAutomaticCaptureUtc = DateTime.UtcNow.AddSeconds(2);
    }

    private void FlushRecording()
    {
        try
        {
            WriteJsonAtomically(RecordingFilePath, recordedStates);
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

    private void AnalyzeLatestRecording()
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

            SaveReplayReport(savedStates);
            replayMessage = $"Analyzed {savedStates.Count} snapshots and saved replay-analysis.json.";
        }
        catch (Exception exception)
        {
            replayMessage = $"Could not analyze recording: {exception.Message}";
        }
    }

    private void SimulateLatestRecording()
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

            var allowedActions = new HashSet<string>(StringComparer.Ordinal)
            {
                "Recuperate", "Forte", "Purify", "Guard", "Standard-issue Elixir",
                "Enchanted Riposte", "Enchanted Zwerchhau", "Enchanted Redoublement",
                "Corps-a-corps", "Displacement", "Scorch", "Prefulgence",
                "Vice of Thorns", "Embolden", "Resolution", "Grand Impact", "Jolt III"
            };
            policySimulation = PolicySimulator.Run(
                savedStates,
                new SafetyPolicy(allowedActions),
                savedStates[0].CapturedAtUtc);
            WriteJsonAtomically(PolicySimulationFilePath, policySimulation);
            replayMessage = $"Shadow-simulated {savedStates.Count} snapshots with no game input.";
        }

        catch (Exception exception)
        {
            replayMessage = $"Could not simulate policy: {exception.Message}";
        }
    }

    private void SaveReplayReport(IReadOnlyList<RecordedGameState> states)
    {
        var report = ReplayAnalyzer.CreateReport(states, DateTime.UtcNow);
        replayReport = report;
        replayAnalysis = report.Analysis;
        WriteJsonAtomically(AnalysisFilePath, report);
    }

    private static void WriteJsonAtomically<T>(string path, T value)
    {
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(temporaryPath, path, true);
    }
}
