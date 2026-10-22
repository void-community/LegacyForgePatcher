using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>The published agent in a real JVM, against a fixture whose threads add object holder handlers at once, as mods do while Forge loads them.</summary>
public sealed class ObjectHolderTests
{
    private const string MainClass = "net.minecraftforge.registries.ObjectHolderRegistry";
    private const string Sources = "Fixtures/ObjectHolders/**/*.java";

    /// <summary>
    /// Without the agent the HashSet loses handlers, breaks with the ClassCastException Forge clients crash with, or loops forever; with it every handler is
    /// kept. Forge's registry runs on Java 8 to 17, so the fixture is compiled for each.
    /// </summary>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentKeepsEveryHandlerThatThreadsAddAtOnce(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "LegacyForgePatcher");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        // Which way the set breaks varies from run to run, but it breaks: in many runs on Java 8 and 25 every one did.
        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.DoesNotContain(expectedSubstring: "HANDLERS", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarKeepsEveryHandlerThatThreadsAddAtOnce(Jdk jdk)
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
            expectedSubstring: "LegacyForgePatcher: patched net/minecraftforge/registries/ObjectHolderRegistry (<clinit>()V: objectHolders created as a ConcurrentHashMap key set (1 change))",
            patched.Output,
            StringComparison.Ordinal
        );
        // Every handler is there, and the map the initializer creates next is still a HashMap.
        Assert.Contains(expectedSubstring: "HANDLERS 400000 in KeySetView, NAMED HashMap", patched.Output, StringComparison.Ordinal);
    }

    // The patched initializer goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
