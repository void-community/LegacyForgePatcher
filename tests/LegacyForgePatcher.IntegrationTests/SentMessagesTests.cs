using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture whose network thread removes an answered handshake message while the server thread adds the next one,
/// as Forge's handshake handler does from Minecraft 1.13 to 1.20.1.
/// </summary>
public sealed class SentMessagesTests
{
    private const string MainClass = "net.minecraftforge.network.HandshakeHandler";
    private const string Sources = "Fixtures/SentMessages/**/*.java";

    /// <summary>Without the agent the removal throws ConcurrentModificationException; with it the list is a CopyOnWriteArrayList and both changes land. Forge's handler runs on Java 8 to 17, so the fixture is compiled for each.</summary>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentLetsBothThreadsChangeSentMessages(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "LegacyForgePatcher");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "DROPPED java.util.ConcurrentModificationException", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarLetsBothThreadsChangeSentMessages(Jdk jdk)
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
            expectedSubstring: "LegacyForgePatcher: patched net/minecraftforge/network/HandshakeHandler (<init>: 1 new java/util/ArrayList replaced by new java/util/concurrent/CopyOnWriteArrayList stored in sentMessages)",
            patched.Output,
            StringComparison.Ordinal
        );
        // The answered message is gone, the one added meanwhile is there, and the constructor's other list is still an ArrayList.
        Assert.Contains(expectedSubstring: "SENT CopyOnWriteArrayList [2, 3], BACKLOG ArrayList", patched.Output, StringComparison.Ordinal);
    }

    // The patched constructor goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
