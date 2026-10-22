using System.Runtime.CompilerServices;

using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>Real legacy Forge servers, as itzg installs them, with the agent loaded.</summary>
[Collection(ForgeServer.Collection)]
public sealed class ForgeServerTests
{
    // Renovate updates the image through the custom manager in .github/renovate.json.
    private const string Image = "itzg/minecraft-server:2026.10.0-java8";

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

        timeout.CancelAfter(ForgeServer.StartTimeout);

        string loginLoad = await ForgeServer.CompileLoginLoadAsync(timeout.Token);

        await using IContainer server = ForgeServer.Create(Image, type: "FORGE", version, memory: "2G", option: "-javaagent:", agent)
            .WithEnvironment(name: "FORGE_VERSION", forgeVersion)
            .WithLoginLoad(loginLoad)
            .Build();

        string logs = await ForgeServer.StartAsync(server, timeout.Token);

        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);

        // Many logins at once keep the event loops busy, so the login packets are still queued when FML switches the state.
        ExecResult logins = await ForgeServer.LogInPlayersAsync(server, protocol, fmlVersion: null, timeout.Token);

        logs = await ForgeServer.ReadLogsAsync(server, timeout.Token);

        ForgeServer.AssertLogged(logs, expected: "LegacyForgePatcher: patched net/minecraftforge/fml/common/network/handshake/NetworkDispatcher");
        ForgeServer.AssertNotLogged(logs, unexpected: "Can't serialize unregistered packet");
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

        timeout.CancelAfter(ForgeServer.StartTimeout);

        IContainer server = ForgeServer.Create(Image, type: "FORGE", version: "1.7.2", memory: "1G", option, agent).Build();

        await using ConfiguredAsyncDisposable disposal = server.ConfigureAwait(continueOnCapturedContext: false);

        string logs = await ForgeServer.StartAsync(server, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);

        ForgeServer.AssertLogged(logs, expected: "LegacyForgePatcher: patched cpw/mods/fml/relauncher/CoreModManager");
        ForgeServer.AssertNotLogged(logs, unexpected: "ConcurrentModificationException");
        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);
    }
}
