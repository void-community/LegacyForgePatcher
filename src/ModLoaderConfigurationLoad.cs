using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Makes FML read <c>fml.toml</c> once. Forge for Minecraft 1.20.1 to 1.21.4 and NeoForge load the config twice while the game starts, and the early loading
/// window starts reading it between the two loads. <c>FMLConfig.loadFrom</c> stores a new, empty config before it reads the file into it, so a read on the
/// window's thread in between finds no value, and the client crashes unboxing it. The patches point <c>load</c>'s call of <c>loadFrom</c> at
/// <c>legacyforgepatcher.ModLoaderConfigs</c>, which skips it when the config is loaded already.
/// </summary>
internal static class ModLoaderConfigurationLoad
{
    private const string Helper = "legacyforgepatcher/ModLoaderConfigs";
    private const string LoadFrom = "loadFrom";

    /// <summary>Gets the patches: one for Forge's <c>FMLConfig</c>, one for NeoForge's.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        .. new[] { "net/minecraftforge/fml/loading/FMLConfig", "net/neoforged/fml/loading/FMLConfig" }.Select(
            static className => ClassPatch.For(className)
                .Method(name: "load", descriptor: "()V")
                .RedirectInvocation(
                    JavaMethod.Virtual(className, LoadFrom, descriptor: "(Ljava/nio/file/Path;)V"),
                    // The helper takes the config as an Object because the bootstrap loader that defines it can't see FML's classes.
                    JavaMethod.Static(Helper, LoadFrom, descriptor: "(Ljava/lang/Object;Ljava/nio/file/Path;)V")
                )
        ),
    ];
}
