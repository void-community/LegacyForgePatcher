using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Makes the class hierarchy cache of ModLauncher's <c>TransformerClassWriter</c> safe to share between threads, as ModLauncher 8 does. ModLauncher 2 to 5, which Forge
/// for Minecraft 1.13.2 to 1.15.2 ships, keeps it in three static <c>HashMap</c>s that every thread transforming a class reads and writes without a lock, and records
/// a class's parent before its hierarchy, so a thread that finds the parent first crashes the game with a <c>NullPointerException</c> in
/// <c>getCommonSuperClass</c>. The class initializer gets the maps from <c>modloaderfixes.ClassHierarchyCache</c> instead, whose maps are locked and count a class
/// as known only once all of it is recorded. ModLauncher 8 keeps <c>ConcurrentHashMap</c>s in other fields, so the patch leaves it alone.
/// </summary>
internal static class ModLauncherHierarchies
{
    private const string HashMap = "java/util/HashMap";
    private const string Helper = "modloaderfixes/ClassHierarchyCache";

    /// <summary>Gets the patch of <c>TransformerClassWriter</c>.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(className: "cpw/mods/modlauncher/TransformerClassWriter")
        .Method(name: "<clinit>", descriptor: "()V")
        .ReplaceConstruction(HashMap, Factory(name: "parents"), storedIn: "classParents")
        .ReplaceConstruction(HashMap, Factory(name: "hierarchies"), storedIn: "classHierarchies")
        .ReplaceConstruction(HashMap, Factory(name: "interfaces"), storedIn: "isInterface");

    private static JavaMethod Factory(string name)
    {
        return JavaMethod.Static(Helper, name, descriptor: "()Ljava/util/HashMap;");
    }
}
