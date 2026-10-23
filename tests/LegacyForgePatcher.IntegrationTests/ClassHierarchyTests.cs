using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>
/// The published agent in a real JVM, against a fixture of ModLauncher 2 to 5's class hierarchy cache, where one thread asks for a class's common superclass while
/// another has recorded the class's parent but not yet its hierarchy, as two threads transforming classes at once do.
/// </summary>
public sealed class ClassHierarchyTests
{
    private const string MainClass = "cpw.mods.modlauncher.TransformerClassWriter";
    private const string Patched =
        "LegacyForgePatcher: patched cpw/mods/modlauncher/TransformerClassWriter (<clinit>()V: 1 new java/util/HashMap() replaced by legacyforgepatcher/ClassHierarchyCache.parents() stored in classParents";
    private const string Sources = "Fixtures/ClassHierarchies/**/*.java";

    /// <summary>
    /// Without the agent the asking thread reads the half recorded hierarchy as null and throws, as Forge 1.13.2 did when it loaded a world; with it the asking thread
    /// records the class itself and gets its superclass. Forge for 1.13.2 to 1.15.2 runs on Java 8 and later, so the fixture is compiled for Java 8 and 17.
    /// </summary>
    [Theory]
    [JdkData(8, 17)]
    public async Task AgentKeepsAHalfRecordedClassFromBreakingTheCommonSuperclass(Jdk jdk)
    {
        ArgumentNullException.ThrowIfNull(jdk);

        PublishedAgent agent = PublishedAgent.Find(name: "LegacyForgePatcher");

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string classPath = await jdk.CompileAsync(Sources, jdk.Major, cancellationToken);
        JavaRun unpatched = await jdk.RunAsync(MainClass, classPath, options: Verified(), cancellationToken: cancellationToken);

        Assert.NotEqual(expected: 0, unpatched.ExitCode);
        Assert.Contains(expectedSubstring: "CRASHED java.lang.NullPointerException", unpatched.Output, StringComparison.Ordinal);

        JavaRun patched = await jdk.RunAsync(MainClass, classPath, agent, Verified(), cancellationToken);

        AssertPatched(patched);
    }

    /// <summary>The jar for <c>-javaagent</c> fixes the fixture as the library for <c>-agentpath</c> does.</summary>
    [Theory]
    [JdkData(8)]
    public async Task JavaAgentJarKeepsAHalfRecordedClassFromBreakingTheCommonSuperclass(Jdk jdk)
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
        Assert.Contains(Patched, patched.Output, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "COMMON net/minecraft/world/World", patched.Output, StringComparison.Ordinal);
    }

    // The patched class initializer goes through the full verifier.
    private static JavaRunOptions Verified()
    {
        JavaRunOptions options = new();

        options.JvmArguments.Add(item: "-Xverify:all");

        return options;
    }
}
