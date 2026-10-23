using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture of Minecraft 1.7's network tick that runs between the channel initializer adding a new connection and
/// channelActive giving it its channel, as a server thread now and then does.
/// </summary>
public sealed class PendingConnectionTests
{
    private const string MainClass = "net.minecraft.network.NetworkSystem";
    private const string Patched =
        "ModLoaderFixes: patched net/minecraft/network/NetworkSystem (func_151269_c()V: 1 invocation of net/minecraft/network/NetworkManager.func_150724_d redirected; "
        + "func_151269_c()V: 1 invocation of net/minecraft/network/NetworkManager.func_74428_b redirected)";
    private const string Sources = "Fixtures/PendingConnection/**/*.java";

    /// <summary>
    /// Without the agent that tick drops the new connection, whose login start then goes unanswered; with it the connection waits for a later tick, which answers
    /// it, and a connection whose channel closed is still dropped. Forge for Minecraft 1.7 runs on Java 8.
    /// </summary>
    [Theory]
    [JdkData(8)]
    public async Task AgentKeepsAConnectionTheTickSeesBeforeItHasAChannel(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "ModLoaderFixes");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, cancellationToken: cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "joining dropped, answered []", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarKeepsAConnectionTheTickSeesBeforeItHasAChannel(Jdk jdk)
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
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "joining kept, answered [login start]; leaving dropped", patched.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "FAILED", patched.Output, StringComparison.Ordinal);
    }

    // The patched tick goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
