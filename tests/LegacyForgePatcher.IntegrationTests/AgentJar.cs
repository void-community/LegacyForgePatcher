using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>The agent packed into the one jar that <c>java -javaagent</c> loads, beside the published library of the platform.</summary>
internal static class AgentJar
{
    /// <summary>Finds the jar next to the published library, and skips the test when it hasn't been packed.</summary>
    /// <returns>The path of the jar.</returns>
    public static string Find()
    {
        PublishedAgent agent = PublishedAgent.Find(name: "LegacyForgePatcher");
        string jar = Path.ChangeExtension(agent.FilePath, extension: ".jar");

        if (File.Exists(jar))
            return jar;

        Assert.Skip(
            $"The agent jar is not packed beside {agent.FilePath}. Pack it with: dotnet jvmbridge pack -o {jar} --agent <rid>={agent.FilePath}"
        );

        throw new InvalidOperationException(message: "Assert.Skip returned.");
    }
}
