using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture that loads fml.toml as Forge from Minecraft 1.13.2 to 1.19.4 does: with NightConfig's autosave and
/// autoreload, whose file watcher reloads the config while FML corrects it on a first start.
/// </summary>
public sealed class ConfigurationWatchTests
{
    private const string MainClass = "net.minecraftforge.fml.loading.FMLConfig";
    private const string Sources = "Fixtures/ConfigWatch/**/*.java";

    /// <summary>
    /// Without the agent the watcher's reload lands while the correction walks the config, which throws ConcurrentModificationException; with it nothing reloads
    /// and every value is corrected. These games run on Java 8 to 17, so the fixture is compiled for each.
    /// </summary>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentCorrectsTheConfigurationWithoutAWatcherReloadingIt(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "LegacyForgePatcher");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "CRASHED java.util.ConcurrentModificationException", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarCorrectsTheConfigurationWithoutAWatcherReloadingIt(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "LegacyForgePatcher");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    private static void AssertPatched(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(
            expectedSubstring: "LegacyForgePatcher: patched net/minecraftforge/fml/loading/FMLConfig (loadFrom(Ljava/nio/file/Path;)V: autosave and autoreload left off (2 changes))",
            patched.Output,
            StringComparison.Ordinal
        );
        // Every outdated value is corrected, and no watcher reloads the file meanwhile; loadFrom saves it once at the end.
        Assert.Contains(expectedSubstring: "CORRECTED 3 values, RELOADED 0 times", patched.Output, StringComparison.Ordinal);
    }

    // The patched method goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
