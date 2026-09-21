namespace SamplePlugin;

public static class BuildInfo
{
    public static string Version => typeof(BuildInfo).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    public const string Milestone = "offline-strategy-and-diagnostics";
}
