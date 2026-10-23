using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Builds FML's config without NightConfig's autosave and autoreload, as Forge for Minecraft 1.20.1 does. Forge from 1.13.2 to 1.19.4 builds it with both, so on a
/// first start each key that <c>ConfigSpec.correct</c> adds to the default <c>fml.toml</c> saves the file, and each save wakes NightConfig's file watcher, which
/// reloads the file into the config that <c>correct</c> still iterates: the game exits with a <c>ConcurrentModificationException</c> or a parse error, or keeps a
/// <c>fml.toml</c> with keys written twice. <c>loadFrom</c> saves the corrected config itself, and nothing else changes it on these versions.
/// </summary>
internal static class ModLoaderConfigurationWatch
{
    private const string Builder = "com/electronwill/nightconfig/core/file/GenericBuilder";
    private const string BuilderDescriptor = "()Lcom/electronwill/nightconfig/core/file/GenericBuilder;";

    /// <summary>Gets the patch of Forge's <c>FMLConfig</c>: both builder calls in <c>loadFrom</c> return the builder they are called on, so they go.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(className: "net/minecraftforge/fml/loading/FMLConfig")
        .Method(name: "loadFrom", descriptor: "(Ljava/nio/file/Path;)V")
        .RemoveInvocation(JavaMethod.Virtual(Builder, name: "autosave", BuilderDescriptor))
        .RemoveInvocation(JavaMethod.Virtual(Builder, name: "autoreload", BuilderDescriptor));
}
