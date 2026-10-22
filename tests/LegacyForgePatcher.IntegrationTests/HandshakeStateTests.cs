using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>The published agent in a real JVM, against a fixture that switches the connection state as FML for Minecraft 1.12 does, with the event loop held as a loaded server's is.</summary>
public sealed class HandshakeStateTests
{
    private const string MainClass = "net.minecraftforge.fml.common.network.handshake.NetworkDispatcher";
    private const string Sources = "Fixtures/HandshakeState/**/*.java";

    /// <summary>Without the agent the fixture encodes the queued login packets in the play state; with it they go out in the login state, before the handshake.</summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentKeepsQueuedLoginPacketsInTheLoginState(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "LegacyForgePatcher");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(
            expectedSubstring: "Can't serialize unregistered packet: [SPacketEnableCompression in PLAY, SPacketLoginSuccess in PLAY]",
            unpatched.Output,
            StringComparison.Ordinal
        );

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, cancellationToken: cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarKeepsQueuedLoginPacketsInTheLoginState(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "LegacyForgePatcher");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, cancellationToken: cancellationToken);

        AssertPatched(patched);
    }

    private static void AssertPatched(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(
            expectedSubstring: "LegacyForgePatcher: patched net/minecraftforge/fml/common/network/handshake/NetworkDispatcher",
            patched.Output,
            StringComparison.Ordinal
        );
        // The switch runs on the event loop after the queued login packets and before the handshake packet that FML sends next.
        Assert.Contains(
            expectedSubstring: "SENT [LOGIN SPacketEnableCompression, LOGIN SPacketLoginSuccess, PLAY FML|HS ServerHello]",
            patched.Output,
            StringComparison.Ordinal
        );
    }
}
