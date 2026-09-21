using System.Text.Json;
using System.Text.Json.Serialization;
using SamplePlugin;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project RdmAiObserver.Analyzer -- <recorded-states.json> [output-directory]");
    return 2;
}

var inputPath = Path.GetFullPath(args[0]);
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Recording not found: {inputPath}");
    return 2;
}

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() }
};

List<RecordedGameState>? states;
try
{
    states = JsonSerializer.Deserialize<List<RecordedGameState>>(File.ReadAllText(inputPath), options);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Could not read recording: {exception.Message}");
    return 2;
}

if (states == null || states.Count == 0)
{
    Console.Error.WriteLine("The recording contains no snapshots.");
    return 2;
}

states.Sort((left, right) => left.CapturedAtUtc.CompareTo(right.CapturedAtUtc));
var replay = ReplayAnalyzer.CreateReport(states, DateTime.UtcNow);
var allowedActions = new HashSet<string>(StringComparer.Ordinal)
{
    "Recuperate", "Forte", "Purify", "Guard", "Standard-issue Elixir",
    "Enchanted Riposte", "Enchanted Zwerchhau", "Enchanted Redoublement",
    "Corps-a-corps", "Displacement", "Scorch", "Prefulgence",
    "Vice of Thorns", "Embolden", "Resolution", "Grand Impact", "Jolt III"
};
var simulation = PolicySimulator.Run(
    states,
    new SafetyPolicy(allowedActions),
    states[0].CapturedAtUtc);

Console.WriteLine($"Snapshots: {states.Count}");
Console.WriteLine($"Duration: {replay.Analysis.DurationSeconds / 60f:F2} minutes");
Console.WriteLine($"Mode: {replay.Analysis.Mode}");
Console.WriteLine($"Deaths / respawns: {replay.Analysis.DeathCount} / {replay.Analysis.RespawnCount}");
Console.WriteLine($"Advice/action agreement: {replay.Analysis.MatchingActionCount}/{replay.Analysis.EvaluatedActionCount} ({replay.Analysis.MatchPercent:F1}%)");
Console.WriteLine($"Planned / observe-only snapshots: {simulation.PlannedSnapshots} / {simulation.ObserveOnlySnapshots}");
Console.WriteLine($"Simulated / rejected commands: {simulation.SimulatedCommands} / {simulation.RejectedCommands}");
Console.WriteLine($"Verified / failed transitions: {simulation.VerifiedCommands} / {simulation.VerificationFailures}");
Console.WriteLine($"Not observable at snapshot interval: {simulation.UnverifiableCommands}");
Console.WriteLine($"Emergency stops: {simulation.EmergencyStops}");
foreach (var group in simulation.Verifications
             .Where(verification => verification.Result == CommandVerificationResult.Failed)
             .GroupBy(verification => verification.Reason)
             .OrderByDescending(group => group.Count()))
    Console.WriteLine($"Transition failure: {group.Count()} x {group.Key}");

if (args.Length == 2)
{
    var outputDirectory = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(outputDirectory);
    File.WriteAllText(Path.Combine(outputDirectory, "replay-analysis.json"), JsonSerializer.Serialize(replay, options));
    File.WriteAllText(Path.Combine(outputDirectory, "policy-simulation.json"), JsonSerializer.Serialize(simulation, options));
    Console.WriteLine($"Reports written to: {outputDirectory}");
}

return 0;
