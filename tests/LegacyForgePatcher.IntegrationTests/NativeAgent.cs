using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>The agent published for the runtime <c>TARGET_RID</c> names, into <c>artifacts/agent/&lt;RID&gt;</c> as CI publishes it.</summary>
internal static class NativeAgent
{
    public const string FileName = "LegacyForgePatcher.so";

    /// <summary>The path of the published agent, skipping the test when no runtime is named.</summary>
    public static string GetPath()
    {
        string runtimeIdentifier = Environment.GetEnvironmentVariable(variable: "TARGET_RID") ?? string.Empty;

        Assert.SkipWhen(runtimeIdentifier.Length is 0, reason: "Set TARGET_RID to test the agent published into artifacts/agent/<RID>.");

        string path = Path.Combine(paths: [FindRepositoryRoot(), "artifacts", "agent", runtimeIdentifier, FileName]);

        return !File.Exists(path)
            ? throw new FileNotFoundException($"Publish the agent for {runtimeIdentifier} before testing it.", path)
            : path;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, path2: "LegacyForgePatcher.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException(message: "The tests run outside the repository.");
    }
}
