using System.Runtime.CompilerServices;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>A real Forge server, as itzg installs it, with the agent loaded.</summary>
public sealed class ForgeServerTests
{
    private const string AgentDirectory = "/patcher/";
    // Renovate updates the image through the custom manager in .github/renovate.json.
    private const string Image = "itzg/minecraft-server:2026.10.0-java8";
    private const string ReadyLine = "Done (";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(minutes: 10);

    /// <summary>Forge for Minecraft 1.7.2 sorts its tweakers without crashing and the server becomes ready with the native library loaded by <c>-agentpath</c>.</summary>
    [Fact]
    public async Task Forge172ServerStartsWithAgentPath()
    {
        await AssertServerStartsAsync(option: "-agentpath:", new FileInfo(PublishedAgent.Find(name: "LegacyForgePatcher").FilePath));
    }

    /// <summary>The same server becomes ready with the jar loaded by <c>-javaagent</c>.</summary>
    [Fact]
    public async Task Forge172ServerStartsWithJavaAgentJar()
    {
        await AssertServerStartsAsync(option: "-javaagent:", new FileInfo(AgentJar.Find()));
    }

    private static async Task AssertServerStartsAsync(string option, FileInfo agent)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(StartTimeout);

        IContainer server = new ContainerBuilder(Image)
            .WithEnvironment(name: "EULA", value: "TRUE")
            .WithEnvironment(name: "TYPE", value: "FORGE")
            .WithEnvironment(name: "VERSION", value: "1.7.2")
            .WithEnvironment(name: "MEMORY", value: "1G")
            .WithEnvironment(name: "JVM_OPTS", option + AgentDirectory + agent.Name)
            .WithResourceMapping(agent, AgentDirectory)
            .Build();

        await using ConfiguredAsyncDisposable disposal = server.ConfigureAwait(continueOnCapturedContext: false);

        await server.StartAsync(timeout.Token).ConfigureAwait(continueOnCapturedContext: false);

        // itzg installs Forge first, then the server either becomes ready or exits.
        Task<long> exit = server.GetExitCodeAsync(timeout.Token);
        string logs = await ReadLogsAsync(server, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);

        while (!logs.Contains(ReadyLine, StringComparison.Ordinal) && !exit.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds: 2), timeout.Token).ConfigureAwait(continueOnCapturedContext: false);
            logs = await ReadLogsAsync(server, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);
        }

        Assert.Contains(expectedSubstring: "LegacyForgePatcher: patched cpw/mods/fml/relauncher/CoreModManager", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "ConcurrentModificationException", logs, StringComparison.Ordinal);
        Assert.Contains(ReadyLine, logs, StringComparison.Ordinal);
    }

    private static async Task<string> ReadLogsAsync(IContainer container, CancellationToken cancellationToken)
    {
        (string standardOutput, string standardError) = await container.GetLogsAsync(ct: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return standardOutput + standardError;
    }
}
