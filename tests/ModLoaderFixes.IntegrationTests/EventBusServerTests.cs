using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>Real Forge 1.14.2 and 1.14.3 servers, as itzg installs them, whose EventBus builds its listener lists through the agent.</summary>
[Collection(ForgeServer.Collection)]
public sealed class EventBusServerTests
{
    // Renovate updates the image through the custom manager in .github/renovate.json.
    private const string Image = "itzg/minecraft-server:2026.10.0-java8";

    /// <summary>Forge 1.14.2 with EventBus 0.10.1, whose fix redirects its synchronized map's computeIfAbsent, and 1.14.3 with EventBus 0.10.3, whose fix redirects computeListenerList.</summary>
    public static TheoryData<string, string, string> Builds { get; } = new()
    {
        { "1.14.2", "26.0.63", "java/util/Map.computeIfAbsent" },
        { "1.14.3", "27.0.60", "net/minecraftforge/eventbus/api/EventListenerHelper.computeListenerList" },
    };

    /// <summary>
    /// The agent rewrites how EventBus builds listener lists, and the server loads its mods in parallel through it and becomes ready. Dedicated servers have no
    /// client thread to deadlock with, so the deadlock itself runs in <see cref="EventListenerTests"/>.
    /// </summary>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="forgeVersion">The Forge build.</param>
    /// <param name="redirected">The call the agent redirects in that EventBus.</param>
    [Theory]
    [MemberData(nameof(Builds))]
    public async Task ServerLoadsModsThroughThePatchedEventBus(string version, string forgeVersion, string redirected)
    {
        FileInfo agent = new(PublishedAgent.FindJar(name: "ModLoaderFixes").FilePath);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(ForgeServer.StartTimeout);

        await using IContainer server = ForgeServer.Create(Image, type: "FORGE", version, memory: "2G", option: "-javaagent:", agent)
            .WithEnvironment(name: "FORGE_VERSION", forgeVersion)
            .Build();

        string logs = await ForgeServer.StartAsync(server, timeout.Token);

        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);
        ForgeServer.AssertLogged(
            logs,
            "ModLoaderFixes: patched net/minecraftforge/eventbus/api/EventListenerHelper (getListenerListInternal(Ljava/lang/Class;Z)Lnet/minecraftforge/eventbus/ListenerList;: 1 invocation of " + redirected + " redirected"
        );
        // Forge 1.14 ships ModLauncher 2 and 3, whose class hierarchy cache the agent makes safe to share between threads.
        ForgeServer.AssertLogged(logs, ForgeServer.ModLauncherPatched);
        // Forge 1.14 builds its fml.toml with autosave and autoreload too, which the server corrects on its first start.
        ForgeServer.AssertLogged(
            logs,
            expected: "ModLoaderFixes: patched net/minecraftforge/fml/loading/FMLConfig (loadFrom(Ljava/nio/file/Path;)V: 1 invocation of com/electronwill/nightconfig/core/file/GenericBuilder.autosave removed; loadFrom(Ljava/nio/file/Path;)V: 1 invocation of com/electronwill/nightconfig/core/file/GenericBuilder.autoreload removed)"
        );
    }
}
