using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against fixtures of EventBus's listener lists in the two shapes Forge 1.14.2 and 1.14.3 ship, whose mod-loading worker
/// builds a list while the client thread initializes the event class it needs.
/// </summary>
public sealed class EventListenerTests
{
    private const string ComputeListenerList = "net/minecraftforge/eventbus/api/EventListenerHelper.computeListenerList";

    private const string MainClass = "net.minecraftforge.eventbus.api.Startup";

    /// <summary>The fixtures, with the call the agent redirects in each: EventBus 0.9.3 to 0.10.1's synchronized map, and 0.10.2 to 0.10.4's read-write lock.</summary>
    public static TheoryData<string, string> Shapes { get; } = new()
    {
        { "Fixtures/EventBus0101/**/*.java", "java/util/Map.computeIfAbsent" },
        { "Fixtures/EventBus0103/**/*.java", ComputeListenerList },
    };

    /// <summary>Without the agent the worker and the client thread wait for each other forever; with it both finish, and the event keeps one listener list. These clients run on Java 8.</summary>
    /// <param name="sources">The fixture's sources.</param>
    /// <param name="redirected">The call the agent redirects in that shape.</param>
    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task AgentBuildsListenerListsOutsideTheLock(string sources, string redirected)
    {
        Jdk jdk = new(major: 8);
        PublishedAgent agent = PublishedAgent.Find(name: "LegacyForgePatcher");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(sources, cancellationToken: cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "DEADLOCK between the mod-loading worker and the client thread", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched, redirected);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture whose fix needs a class in EventBus's own loader as the library for <c>-agentpath</c> does.</summary>
    [Fact]
    public async Task JavaAgentJarBuildsListenerListsOutsideTheLock()
    {
        Jdk jdk = new(major: 8);
        PublishedAgent jar = PublishedAgent.FindJar(name: "LegacyForgePatcher");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(sources: "Fixtures/EventBus0103/**/*.java", cancellationToken: cancellationToken);
        JavaRun patched = await jdk.RunAsync(MainClass, classPath, jar, Verified(), cancellationToken);

        AssertPatched(patched, ComputeListenerList);
    }

    private static void AssertPatched(JavaRun patched, string redirected)
    {
        Assert.True(patched.ExitCode is 0, patched.Output);
        Assert.Contains(
            "LegacyForgePatcher: patched net/minecraftforge/eventbus/api/EventListenerHelper (getListenerListInternal(Ljava/lang/Class;Z)Lnet/minecraftforge/eventbus/ListenerList;: 1 invocation of " + redirected + " redirected",
            patched.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(expectedSubstring: "STARTED with one TickEvent listener list", patched.Output, StringComparison.Ordinal);
    }

    // The patched class, and the class the agent writes into EventBus's loader, go through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
