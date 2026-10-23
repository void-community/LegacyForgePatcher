using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Getting the maps of ModLauncher's class hierarchy cache from the helper that makes them safe to share, in TransformerClassWriter's class initializer.</summary>
public sealed class ModLauncherHierarchyPatchTests
{
    private const string ClassInitializer = "<clinit>";
    private const string ClassName = "cpw/mods/modlauncher/TransformerClassWriter";
    private const string FactoryDescriptor = "()Ljava/util/HashMap;";
    private const string HashMap = "java/util/HashMap";
    private const string Helper = "modloaderfixes/ClassHierarchyCache";

    /// <summary>Each of the three maps ModLauncher 2 to 5 create comes from the helper's factory for it, and the class creates no HashMap of its own any more.</summary>
    [Fact]
    public void PatchGetsTheThreeMapsFromTheHelper()
    {
        ClassPatchResult result = ModLauncherHierarchies.Patch.Apply(CreateClassFile(HashMap, fieldDescriptor: "Ljava/util/HashMap;", fields: ["classParents", "classHierarchies", "isInterface"]));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 3, result.ReplacedConstructions);
        ClassFileAssert.Calls(patched, ClassInitializer, Helper, name: "parents", FactoryDescriptor);
        ClassFileAssert.Calls(patched, ClassInitializer, Helper, name: "hierarchies", FactoryDescriptor);
        ClassFileAssert.Calls(patched, ClassInitializer, Helper, name: "interfaces", FactoryDescriptor);
        ClassFileAssert.DoesNotCall(patched, ClassInitializer, HashMap, name: "<init>", descriptor: "()V");
    }

    /// <summary>ModLauncher 8 keeps ConcurrentHashMaps in fields of other names, so its class is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesModLauncher8Alone()
    {
        ClassPatchResult result = ModLauncherHierarchies.Patch.Apply(
            CreateClassFile(
                className: "java/util/concurrent/ConcurrentHashMap",
                fieldDescriptor: "Ljava/util/Map;",
                fields: ["CLASS_PARENTS", "CLASS_HIERARCHIES", "IS_INTERFACE"]
            )
        );

        Assert.False(result.Changed);
        Assert.Contains("no construction of " + HashMap, result.SkippedReason, StringComparison.Ordinal);
    }

    // A static initializer that stores a new map of the given class in each of the given fields, as TransformerClassWriter's does.
    private static byte[] CreateClassFile(string className, string fieldDescriptor, params string[] fields)
    {
        CodeBuilder StoreNew(CodeBuilder code, string field)
        {
            return code.New(className).Dup().InvokeSpecial(className, name: "<init>", descriptor: "()V").PutStatic(ClassName, field, fieldDescriptor);
        }

        return new ClassFileWriter(ClassName)
            .AddMethod(ClassInitializer, descriptor: "()V", code => fields.Aggregate(code, StoreNew).Return(), accessFlags: 0x0008)
            .ToArray();
    }
}
