using System.Globalization;
using System.Runtime.CompilerServices;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>Real Forge servers, as itzg installs them, with the agent loaded.</summary>
public sealed class ForgeServerTests
{
    private const string AgentDirectory = "/patcher/";
    // Renovate updates the image through the custom manager in .github/renovate.json.
    private const string Image = "itzg/minecraft-server:2026.10.0-java8";
    private const string LoaderDirectory = "/loader/";
    private const string ReadyLine = "Done (";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(minutes: 10);

    /// <summary>The Forge 1.12 builds that drop players at login, the newest stable ones itzg picks, with the protocol version of their Minecraft.</summary>
    public static TheoryData<string, string, int> Forge112Builds { get; } = new()
    {
        { "1.12", "14.21.1.2387", 335 },
        { "1.12.1", "14.22.1.2478", 338 },
        { "1.12.2", "14.23.5.2859", 340 },
    };

    /// <summary>With the jar loaded, a Forge 1.12 server lets every player of a burst of logins reach the play state; without it, such a burst drops many of them.</summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="forgeVersion">The Forge build.</param>
    /// <param name="protocol">The protocol version of that Minecraft.</param>
    [Theory]
    [MemberData(nameof(Forge112Builds))]
    public async Task Forge112ServerKeepsPlayersThatLogInUnderLoad(string version, string forgeVersion, int protocol)
    {
        FileInfo agent = new(PublishedAgent.FindJar(name: "LegacyForgePatcher").FilePath);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(StartTimeout);

        // The logins run inside the server's container, so they load its event loops without depending on the host's port forwarding.
        string loader = await new Jdk(major: 8).CompileAsync(sources: "Fixtures/LoginLoad/**/*.java", cancellationToken: timeout.Token);

        await using IContainer server = CreateServer(version, memory: "2G", option: "-javaagent:", agent)
            .WithEnvironment(name: "FORGE_VERSION", forgeVersion)
            .WithEnvironment(name: "ONLINE_MODE", value: "FALSE")
            .WithEnvironment(name: "MAX_PLAYERS", value: "1000")
            .WithResourceMapping(new DirectoryInfo(loader), LoaderDirectory)
            .Build();

        string logs = await StartAsync(server, timeout.Token);

        Assert.Contains(ReadyLine, logs, StringComparison.Ordinal);

        // Many logins at once keep the event loops busy, so the login packets are still queued when FML switches the state.
        ExecResult logins = await server.ExecAsync(
            ["java", "-cp", LoaderDirectory, "loginload.LoginLoad", "127.0.0.1", "25565", protocol.ToString(CultureInfo.InvariantCulture), "200", "40"],
            timeout.Token
        );

        logs = await ReadLogsAsync(server, timeout.Token);

        Assert.Contains(
            expectedSubstring: "LegacyForgePatcher: patched net/minecraftforge/fml/common/network/handshake/NetworkDispatcher",
            logs,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(expectedSubstring: "Can't serialize unregistered packet", logs, StringComparison.Ordinal);
        Assert.True(logins.ExitCode is 0, logins.Stdout + logins.Stderr);
        Assert.Contains(expectedSubstring: "LOGINS {logged in=200}", logins.Stdout, StringComparison.Ordinal);
    }

    /// <summary>Forge for Minecraft 1.7.2 sorts its tweakers without crashing and the server becomes ready with the native library loaded by <c>-agentpath</c>.</summary>
    [Fact]
    public async Task Forge172ServerStartsWithAgentPath()
    {
        await AssertForge172StartsAsync(option: "-agentpath:", new FileInfo(PublishedAgent.Find(name: "LegacyForgePatcher").FilePath));
    }

    /// <summary>The same server becomes ready with the jar loaded by <c>-javaagent</c>.</summary>
    [Fact]
    public async Task Forge172ServerStartsWithJavaAgentJar()
    {
        await AssertForge172StartsAsync(option: "-javaagent:", new FileInfo(PublishedAgent.FindJar(name: "LegacyForgePatcher").FilePath));
    }

    private static async Task AssertForge172StartsAsync(string option, FileInfo agent)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(StartTimeout);

        IContainer server = CreateServer(version: "1.7.2", memory: "1G", option, agent).Build();

        await using ConfiguredAsyncDisposable disposal = server.ConfigureAwait(continueOnCapturedContext: false);

        string logs = await StartAsync(server, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);

        Assert.Contains(expectedSubstring: "LegacyForgePatcher: patched cpw/mods/fml/relauncher/CoreModManager", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(expectedSubstring: "ConcurrentModificationException", logs, StringComparison.Ordinal);
        Assert.Contains(ReadyLine, logs, StringComparison.Ordinal);
    }

    private static ContainerBuilder CreateServer(string version, string memory, string option, FileInfo agent)
    {
        return new ContainerBuilder(Image)
            .WithEnvironment(name: "EULA", value: "TRUE")
            .WithEnvironment(name: "TYPE", value: "FORGE")
            .WithEnvironment(name: "VERSION", version)
            .WithEnvironment(name: "MEMORY", memory)
            .WithEnvironment(name: "JVM_OPTS", option + AgentDirectory + agent.Name)
            .WithResourceMapping(agent, AgentDirectory);
    }

    private static async Task<string> ReadLogsAsync(IContainer container, CancellationToken cancellationToken)
    {
        (string standardOutput, string standardError) = await container.GetLogsAsync(ct: cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        return standardOutput + standardError;
    }

    // itzg installs Forge first, then the server either becomes ready or exits.
    private static async Task<string> StartAsync(IContainer server, CancellationToken cancellationToken)
    {
        await server.StartAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        Task<long> exit = server.GetExitCodeAsync(cancellationToken);
        string logs = await ReadLogsAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

        while (!logs.Contains(ReadyLine, StringComparison.Ordinal) && !exit.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds: 2), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
            logs = await ReadLogsAsync(server, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }

        return logs;
    }
}
