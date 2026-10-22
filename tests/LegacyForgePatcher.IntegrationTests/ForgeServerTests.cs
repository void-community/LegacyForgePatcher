using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

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

    /// <summary>Forge for Minecraft 1.7.2 sorts its tweakers without crashing and the server becomes ready.</summary>
    [Fact]
    public async Task Forge172ServerStartsWithAgent()
    {
        string agent = NativeAgent.GetPath();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(StartTimeout);

        await using IContainer server = new ContainerBuilder(Image)
            .WithEnvironment(name: "EULA", value: "TRUE")
            .WithEnvironment(name: "TYPE", value: "FORGE")
            .WithEnvironment(name: "VERSION", value: "1.7.2")
            .WithEnvironment(name: "MEMORY", value: "1G")
            .WithEnvironment(name: "JVM_OPTS", $"-agentpath:{AgentDirectory}{NativeAgent.FileName}")
            .WithResourceMapping(new FileInfo(agent), AgentDirectory)
            .Build();

        await server.StartAsync(timeout.Token);

        // itzg installs Forge first, then the server either becomes ready or exits.
        Task<long> exit = server.GetExitCodeAsync(timeout.Token);
        string logs = await ReadLogsAsync(server, timeout.Token);

        while (!logs.Contains(ReadyLine, StringComparison.Ordinal) && !exit.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds: 2), timeout.Token);
            logs = await ReadLogsAsync(server, timeout.Token);
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
