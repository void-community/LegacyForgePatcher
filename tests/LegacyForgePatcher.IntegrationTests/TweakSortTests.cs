using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>The published agent in a real JVM, against a fixture that sorts tweakers the way FML for Minecraft 1.7.2 does.</summary>
public sealed class TweakSortTests
{
    private const string MainClass = "cpw.mods.fml.relauncher.CoreModManager";

    /// <summary>The fixture throws without the agent and completes its iteration with it.</summary>
    [Fact]
    public async Task AgentLetsFixtureSortTweakersWhileIteratingThem()
    {
        string agent = NativeAgent.GetPath();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await Jdk.CompileFixturesAsync(cancellationToken);
        JdkResult unpatched = await Jdk.RunAsync(tool: "java", ["-cp", classPath, MainClass], cancellationToken);

        // The JVM must count a sort as a structural change, as Java from 8u20 does, for the agent to have anything to fix.
        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "java.util.ConcurrentModificationException", unpatched.Output, StringComparison.Ordinal);

        JdkResult patched = await Jdk.RunAsync(tool: "java", [$"-agentpath:{agent}", "-cp", classPath, MainClass], cancellationToken);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(
            expectedSubstring: "LegacyForgePatcher: patched cpw/mods/fml/relauncher/CoreModManager",
            patched.Output,
            StringComparison.Ordinal
        );
        // As on Java before 8u20, the sort moves tweakers under the iterator, which calls one twice and skips another, as FML did there.
        Assert.Contains(expectedSubstring: "CALLED [deobfuscation, deobfuscation, injection]", patched.Output, StringComparison.Ordinal);
    }
}
