using System.Globalization;

using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Every patch of the agent against the real jars of the Forge, NeoForge, FML and EventBus versions at the edges of what each fix covers.</summary>
public sealed class RealJarTests
{
    private const string EventListenerHelper = "net/minecraftforge/eventbus/api/EventListenerHelper";
    private const string Forge = "https://maven.minecraftforge.net";

    private const string ForgeConfiguration = "net/minecraftforge/fml/loading/FMLConfig";
    private const string NeoForgeConfiguration = "net/neoforged/fml/loading/FMLConfig";
    private const string NeoForged = "https://maven.neoforged.net/releases";
    private const string ObjectHolderRegistry = "net/minecraftforge/registries/ObjectHolderRegistry";

    /// <summary>
    /// Each pinned jar with what the patches change in it: one entry per class a patch changes, with the number of members that patch adds, so that a class two patches
    /// change, or one that two versions change with different patches, reads differently.
    /// </summary>
    public static TheoryData<string, string, string, string[]> Jars { get; } = new()
    {
        // FML's registry rebuilds on the network thread, which Forge for 1.12.2 moved to the client thread itself.
        {
            Forge, "net.minecraftforge:forge:1.12.1-14.22.1.2478:universal", "80dffcf8b6330d4002589ae92463f51c7823ad24a4a1f3a2c331e9743f97d922",
            ["net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4 +0", "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7 +0"]
        },
        { Forge, "net.minecraftforge:forge:1.12.2-14.23.5.2859:universal", "bd8314729d787e35164476d75afb337afb9783917e4f6fa1f77f049adba22f2d", [] },
        // The handshake handler under each of its names, and the object holder registry, which keeps its HashSet up to 1.21.4.
        {
            Forge, "net.minecraftforge:forge:1.13.2-25.0.223:universal", "2a346876d4ea5dbfec7ae7fb88ac69fded58a2c504baa7a63156fe1fc3253786",
            ["net/minecraftforge/fml/network/FMLHandshakeHandler +0", ObjectHolderRegistry + " +0"]
        },
        {
            Forge, "net.minecraftforge:forge:1.17.1-37.1.1:universal", "2cc032ae95d60f03e62be4929f3ecd66354e6ed135fa3cf676ced2b2f7910a6d",
            ["net/minecraftforge/fmllegacy/network/FMLHandshakeHandler +0", ObjectHolderRegistry + " +0"]
        },
        {
            Forge, "net.minecraftforge:forge:1.20.1-47.4.28:universal", "9a6e4341cb5fc92ac6efdea4a9bc7e83cdec75c0f996f9af376c58af7ea7bf97",
            ["net/minecraftforge/network/HandshakeHandler +0", ObjectHolderRegistry + " +0"]
        },
        {
            NeoForged, "net.neoforged:forge:1.20.1-47.1.106:universal", "af67e430f39dc742713c94975166fac29c1a49f5ba7c31095a77289931399dc1",
            ["net/minecraftforge/network/HandshakeHandler +0", ObjectHolderRegistry + " +0"]
        },
        { Forge, "net.minecraftforge:forge:1.21.4-54.1.0:universal", "d5537ad2a67aaee88b1d84b58817f62e49d64f0e0b3ea51b8b03b95e17a7dad0", [ObjectHolderRegistry + " +0"] },
        // FML's config: loaded once, with its getter and invoker, and up to 1.19.4 built without autosave and autoreload; NeoForge's FML 4 changed configData's type.
        { Forge, "net.minecraftforge:fmlloader:1.19.4-45.3.0", "1a5469a8646c0b83561fedd828a7605ede8812d329434042489571883e8f89a5", [ForgeConfiguration + " +0", ForgeConfiguration + " +2"] },
        { Forge, "net.minecraftforge:fmlloader:1.20.1-47.4.28", "9c26db7ee5ae50e0d3d493fe2f07dd4a10d61c1e678203af97986e45c269f414", [ForgeConfiguration + " +2"] },
        { NeoForged, "net.neoforged.fancymodloader:loader:2.0.17", "fdb7c4ffd3b03ce5dba32d453a5335630416b4c820ce54eada5cba96e36be625", [NeoForgeConfiguration + " +2"] },
        { NeoForged, "net.neoforged.fancymodloader:loader:4.0.39", "2c5b1386bba0e77c769e95d53eb2edb00773ef3bded5196a44277ade8f112534", [NeoForgeConfiguration + " +2"] },
        // EventBus's listener lists: built in computeIfAbsent up to 0.10.1, under the write lock with the helper's accessors in 0.10.2 to 0.10.4, and fixed from 0.10.5.
        { Forge, "net.minecraftforge:eventbus:0.10.1", "00315a766018bc6f0c4a2e1eb465c866ac194d7a29d820283ba3db57ba763915", [EventListenerHelper + " +0"] },
        { Forge, "net.minecraftforge:eventbus:0.10.3", "eee16845dafb8d3032b246b82b9a542860dc09c067182c02fcb31038a741d028", [EventListenerHelper + " +3"] },
        { Forge, "net.minecraftforge:eventbus:0.10.5", "061c8c32b827425c4d38d18629c4a45e3d4230b2228fb93d15d9ec28e141a262", [] },
    };

    /// <summary>The patches change exactly the classes each fix was written for in that version, and leave every other class of the jar as it is.</summary>
    /// <param name="repository">The Maven repository.</param>
    /// <param name="coordinates">The artifact.</param>
    /// <param name="sha256">Its pinned checksum.</param>
    /// <param name="expected">What the patches change, as <c>class +added members</c>.</param>
    [Theory]
    [MemberData(nameof(Jars))]
    public async Task PatchesChangeWhatEachFixCoversInARealJar(string repository, string coordinates, string sha256, string[] expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        string jar = await new MavenArtifact(repository, coordinates, sha256).GetPathAsync(TestContext.Current.CancellationToken);

        List<string> changed =
        [
            .. PatcherAgent.Patches.SelectMany(patch => patch.ApplyToJar(jar))
                .Where(static result => result.Changed)
                .Select(static result => result.ClassName + " +" + result.AddedMembers.ToString(CultureInfo.InvariantCulture))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), changed);
    }
}
