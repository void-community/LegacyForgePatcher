using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture whose network thread drives FML's client handshake states as Forge for Minecraft 1.12 and 1.12.1 have them
/// while its main thread ticks as Minecraft's client thread does.
/// </summary>
public sealed class RegistryThreadTests
{
    private const string MainClass = "net.minecraftforge.fml.common.network.handshake.FMLHandshakeClientState";
    private const string Sources = "Fixtures/RegistryThread/**/*.java";

    /// <summary>Without the agent the network thread rebuilds the registries; with it the client thread does, in order, and the handshake still gets the injection's result.</summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentRebuildsRegistriesOnTheClientThread(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.True(unpatched.ExitCode is 0, unpatched.Output);
        Assert.Contains(expectedSubstring: "REVERT on Netty Client IO #1", unpatched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "INJECT {minecraft:block=server} on Netty Client IO #1", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> moves the rebuilds as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarRebuildsRegistriesOnTheClientThread(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent jar = PublishedAgent.FindJar(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    private static void AssertPatched(JavaRun patched)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(
            expectedSubstring: "ModLoaderFixes: patched net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: "ModLoaderFixes: patched net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7",
            patched.Output,
            StringComparison.Ordinal
        );
        // The revert is scheduled before the injection, so the client thread runs them in that order, and the network thread receives the injection's result.
        Assert.Contains(
            string.Join(Environment.NewLine, value: ["REVERT on main", "INJECT {minecraft:block=server} on main", "MISSING 1"]),
            patched.Output.ReplaceLineEndings(),
            StringComparison.Ordinal
        );
    }

    // The class the agent writes itself, and the classes it patches, go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
