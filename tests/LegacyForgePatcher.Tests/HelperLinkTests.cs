using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>
/// The typed helpers against the real classes they call, as the agent's patches leave them: every class, field and method they name exists with the descriptor they
/// were compiled against, which their stubs only claim. The helpers for Forge 1.7 and 1.12 and for Mojang's names call classes that no Maven repository holds by
/// those names, so their fixtures and servers cover them instead.
/// </summary>
public sealed class HelperLinkTests
{
    private const string Forge = "https://maven.minecraftforge.net";
    private const string NeoForged = "https://maven.neoforged.net/releases";

    /// <summary>EventBus 0.10.2 to 0.10.4, whose EventListenerHelper the helper reaches through the methods the patch adds.</summary>
    public static TheoryData<string, string> EventBuses { get; } = new()
    {
        { "net.minecraftforge:eventbus:0.10.2", "e313a118c8b1042a2d8e931f5e627696f22b842f174e8fe4422f1bec5aa8f9b2" },
        { "net.minecraftforge:eventbus:0.10.3", "eee16845dafb8d3032b246b82b9a542860dc09c067182c02fcb31038a741d028" },
        { "net.minecraftforge:eventbus:0.10.4", "59f8a341f2016a0c607406028a306a25358dcd0928f5141bfe351b6dcd2e8391" },
    };

    /// <summary>Forge's FML and NeoForge's, with configData of each type NeoForge's FML has given it.</summary>
    public static TheoryData<string, string, string, string> Loaders { get; } = new()
    {
        {
            "net.minecraftforge:fmlloader:1.20.1-47.4.28", "9c26db7ee5ae50e0d3d493fe2f07dd4a10d61c1e678203af97986e45c269f414",
            "net.neoforged.fancymodloader:loader:2.0.17", "fdb7c4ffd3b03ce5dba32d453a5335630416b4c820ce54eada5cba96e36be625"
        },
        {
            "net.minecraftforge:fmlloader:1.19.4-45.3.0", "1a5469a8646c0b83561fedd828a7605ede8812d329434042489571883e8f89a5",
            "net.neoforged.fancymodloader:loader:4.0.39", "2c5b1386bba0e77c769e95d53eb2edb00773ef3bded5196a44277ade8f112534"
        },
    };

    /// <summary>The FML config helper, which serves Forge and NeoForge, links against both loaders that the patches changed.</summary>
    /// <param name="forgeCoordinates">Forge's FML loader.</param>
    /// <param name="forgeSha256">Its pinned checksum.</param>
    /// <param name="neoForgeCoordinates">NeoForge's FML loader.</param>
    /// <param name="neoForgeSha256">Its pinned checksum.</param>
    [Theory]
    [MemberData(nameof(Loaders))]
    public async Task ConfigurationHelperLinks(string forgeCoordinates, string forgeSha256, string neoForgeCoordinates, string neoForgeSha256)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string forge = await new MavenArtifact(Forge, forgeCoordinates, forgeSha256).GetPathAsync(cancellationToken);
        string neoForge = await new MavenArtifact(NeoForged, neoForgeCoordinates, neoForgeSha256).GetPathAsync(cancellationToken);

        await ClassFileAssert.LinksAsync(new Jdk(major: 8), JavaClasses.Legacyforgepatcher.ModLoaderConfigs, [forge, neoForge], PatcherAgent.Patches, cancellationToken);
    }

    /// <summary>The EventBus helper links against a real EventBus that the patches changed.</summary>
    /// <param name="coordinates">The EventBus artifact.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    [Theory]
    [MemberData(nameof(EventBuses))]
    public async Task EventBusHelperLinks(string coordinates, string sha256)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string eventBus = await new MavenArtifact(Forge, coordinates, sha256).GetPathAsync(cancellationToken);

        await ClassFileAssert.LinksAsync(new Jdk(major: 8), JavaClasses.Legacyforgepatcher.EventListeners, [eventBus], PatcherAgent.Patches, cancellationToken);
    }
}
