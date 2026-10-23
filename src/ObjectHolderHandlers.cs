using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Makes the set of object holder handlers that Forge's <c>ObjectHolderRegistry</c> keeps a concurrent one. <c>addHandler</c> adds to a plain static
/// <c>HashSet</c> without a lock, and mods register handlers from the mod-loading worker threads at once, which can break the set's hash map; a Forge 1.17.1
/// client crashed with a <c>ClassCastException</c> in <c>HashMap$TreeNode</c>. Forge creates the set in the class's static initializer from Minecraft 1.13.2 to
/// 1.21.4, and NeoForge for 1.20.1 does too.
/// </summary>
internal static class ObjectHolderHandlers
{
    /// <summary>Gets the patch of <c>ObjectHolderRegistry</c>.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(className: "net/minecraftforge/registries/ObjectHolderRegistry")
        .Method(name: "<clinit>", descriptor: "()V")
        .ReplaceConstruction(
            fromClass: "java/util/HashSet",
            JavaMethod.Static(
                owner: "java/util/concurrent/ConcurrentHashMap",
                name: "newKeySet",
                descriptor: "()Ljava/util/concurrent/ConcurrentHashMap$KeySetView;"
            ),
            storedIn: "objectHolders"
        );
}
