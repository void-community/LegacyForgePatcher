using DotNet.Testcontainers.Containers;

using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.IntegrationTests;

/// <summary>Real Forge and NeoForge servers from Minecraft 1.19.4, as itzg installs them, which load FML's config through the agent's helper.</summary>
[Collection(ForgeServer.Collection)]
public sealed class ModLoaderConfigurationServerTests
{
    /// <summary>
    /// Forge for 1.19.4, which loads the config once, and Forge and NeoForge builds that load it twice, the ones seen crashing among them, with the itzg image that
    /// runs them. Renovate updates the images through the custom manager in .github/renovate.json.
    /// </summary>
    public static TheoryData<string, string, string, string> Builds { get; } = new()
    {
        { "itzg/minecraft-server:2026.10.0-java17", "FORGE", "1.19.4", "45.3.0" },
        { "itzg/minecraft-server:2026.10.0-java17", "FORGE", "1.20.2", "48.1.0" },
        { "itzg/minecraft-server:2026.10.0-java21", "FORGE", "1.21.4", "54.1.0" },
        { "itzg/minecraft-server:2026.10.0-java17", "NEOFORGE", "1.20.4", "20.4.253" },
        { "itzg/minecraft-server:2026.10.0-java21", "NEOFORGE", "1.21.4", "21.4.161" },
    };

    /// <summary>
    /// The agent points FMLConfig's load at its helper, and the server becomes ready: the helper reaches FMLConfig inside FML's module and loads the config the
    /// first time. Dedicated servers have no early loading window, so the crash itself only shows on clients, and the fixture in <see cref="ModLoaderConfigurationTests"/>
    /// covers it.
    /// </summary>
    /// <param name="image">The itzg image.</param>
    /// <param name="type">itzg's TYPE.</param>
    /// <param name="version">The Minecraft version.</param>
    /// <param name="loaderVersion">The Forge or NeoForge build.</param>
    [Theory]
    [MemberData(nameof(Builds))]
    public async Task ServerLoadsItsConfigurationThroughTheAgent(string image, string type, string version, string loaderVersion)
    {
        FileInfo agent = new(PublishedAgent.FindJar(name: "ModLoaderFixes").FilePath);

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(ForgeServer.StartTimeout);

        await using IContainer server = ForgeServer.Create(image, type, version, memory: "2G", option: "-javaagent:", agent)
            .WithEnvironment(type == "NEOFORGE" ? "NEOFORGE_VERSION" : "FORGE_VERSION", loaderVersion)
            .Build();

        string logs = await ForgeServer.StartAsync(server, timeout.Token);
        string package = type == "NEOFORGE" ? "net/neoforged" : "net/minecraftforge";

        ForgeServer.AssertLogged(logs, ForgeServer.ReadyLine);
        ForgeServer.AssertLogged(
            logs,
            $"ModLoaderFixes: patched {package}/fml/loading/FMLConfig (load()V: 1 invocation of {package}/fml/loading/FMLConfig.loadFrom redirected)"
        );
        // The agent logs the members a patch adds on a line of their own, when it defines the class.
        ForgeServer.AssertLogged(
            logs,
            $"ModLoaderFixes: patched {package}/fml/loading/FMLConfig (modloaderfixes$configData()Ljava/lang/Object;: getter of field configData added; modloaderfixes$loadFrom(Ljava/nio/file/Path;)V: invoker of loadFrom(Ljava/nio/file/Path;)V added)"
        );
    }
}
