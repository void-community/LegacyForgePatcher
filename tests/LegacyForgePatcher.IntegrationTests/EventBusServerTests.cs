using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.IntegrationTests;

/// <summary>Real Forge 1.14.2 and 1.14.3 servers, as itzg installs them, whose EventBus builds its listener lists through the agent.</summary>
[Collection(ForgeServer.Collection)]
public sealed class EventBusServerTests
{
    // Renovate updates the image through the custom manager in .github/renovate.json.
    private const string Image = "itzg/minecraft-server:2026.10.0-java8";

    /// <summary>Forge 1.14.2 with EventBus 0.10.1, whose fix calls the bootstrap helper, and 1.14.3 with EventBus 0.10.3, whose fix calls a class the agent writes into EventBus's loader.</summary>
    public static TheoryData<string, string> Builds { get; } = new()
    {
        { "1.14.2", "26.0.63" },
        { "1.14.3", "27.0.60" },
    };

    /// <summary>
    /// The agent rewrites how EventBus builds listener lists, and the server loads its mods in parallel through it and becomes ready. Dedicated servers have no
    /// client thread to deadlock with, so the deadlock itself runs in <see cref="EventListenerTests"/>.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="forgeVersion">The Forge build.</param>
    [Theory]
    [MemberData(nameof(Builds))]
    public async Task ServerLoadsModsThroughThePatchedEventBus(string version, string forgeVersion)
    {
        FileInfo agent = new(PublishedAgent.FindJar(name: "LegacyForgePatcher").FilePath);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(ForgeServer.StartTimeout);

        await using IContainer server = ForgeServer.Create(Image, type: "FORGE", version, memory: "2G", option: "-javaagent:", agent)
            .WithEnvironment(name: "FORGE_VERSION", forgeVersion)
            .Build();

        string logs = await ForgeServer.StartAsync(server, timeout.Token);

        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);
        ForgeServer.AssertLogged(
            logs,
            expected: "LegacyForgePatcher: patched net/minecraftforge/eventbus/api/EventListenerHelper (getListenerListInternal(Ljava/lang/Class;Z)Lnet/minecraftforge/eventbus/ListenerList;: listener list built outside the lock (1 change))"
        );
    }
}
