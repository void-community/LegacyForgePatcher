using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture whose network thread answers a chunk batch while the client thread is between sending the end of
/// configuration and setting up play, as Minecraft clients from 1.20.5 can, and while a proxy's server switch takes the client from play back into configuration.
/// </summary>
public sealed class ChunkBatchAnswerTests
{
    private const string MainClass = "net.minecraft.client.multiplayer.ClientPacketListener";
    private const string ServerSwitchClass = "net.minecraft.client.multiplayer.ServerSwitch";
    private const string Sources = "Fixtures/ChunkBatch/**/*.java";

    /// <summary>
    /// A server switch that the client thread handles before it runs its tasks takes the connection into configuration, which has no chunk batch answer, as
    /// Minecraft from 26.1 does. Without the agent the network thread sent the answer in play already; with it the client thread drops the answer, which belongs to
    /// a play phase that has ended, instead of failing to encode it and dropping the connection.
    /// </summary>
    /// <param name="jdk">The Java the client runs on.</param>
    [Theory]
    [JdkData(21, 25)]
    public async Task AgentDropsTheAnswerOfAPlayPhaseThatAServerSwitchEnded(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(ServerSwitchClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.True(unpatched.ExitCode is 0, unpatched.Output);
        Assert.Contains(
            expectedSubstring: "SENT ServerboundChunkBatchReceivedPacket[desiredChunksPerTick=5.25] as play on Netty",
            unpatched.Output,
            StringComparison.Ordinal
        );

        JavaRun patched = await jdk.RunAsync(ServerSwitchClass, classPath, agent, Verified(), cancellationToken);

        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.DoesNotContain(expectedSubstring: "Sending unknown packet", patched.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "SENT", patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "SWITCHED to configuration", patched.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Without the agent the answer meets a pipeline with no outgoing protocol and the client drops its connection; with it the client thread sends the answer
    /// once play is set up. These clients run on Java 21 and 25, and Minecraft calls send as classes compiled for them do.
    /// </summary>
    [Theory]
    [JdkData(21, 25)]
    public async Task AgentSendsTheAnswerOnceThePipelinePlays(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(
            expectedSubstring: "DROPPED Pipeline has no outbound protocol configured, can't process packet ServerboundChunkBatchReceivedPacket",
            unpatched.Output,
            StringComparison.Ordinal
        );

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(21)]
    public async Task JavaAgentJarSendsTheAnswerOnceThePipelinePlays(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    private static void AssertPatched(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(
            expectedSubstring: "ModLoaderFixes: patched net/minecraft/client/multiplayer/ClientPacketListener (handleChunkBatchFinished(",
            patched.Output,
            StringComparison.Ordinal
        );
        // The client thread sent the answer, after play was set up.
        Assert.Contains(
            expectedSubstring: "SENT ServerboundChunkBatchReceivedPacket[desiredChunksPerTick=5.25] as play on main",
            patched.Output,
            StringComparison.Ordinal
        );
    }

    // The patched class goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
