using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Making the set of object holder handlers that Forge's ObjectHolderRegistry keeps a concurrent one, in its static initializer.</summary>
public sealed class ObjectHolderPatchTests
{
    private const string ClassInitializer = "<clinit>";
    private const string ClassName = "net/minecraftforge/registries/ObjectHolderRegistry";
    private const string Constructor = "<init>";
    private const string HashMap = "java/util/HashMap";
    private const string HashSet = "java/util/HashSet";

    /// <summary>The set stored in <c>objectHolders</c> comes from <c>ConcurrentHashMap.newKeySet()</c>, and the map the initializer creates next stays a HashMap.</summary>
    [Fact]
    public void PatchCreatesObjectHoldersAsAConcurrentKeySet()
    {
        ClassPatchResult result = ObjectHolderHandlers.Patch.Apply(CreateClassFile(setField: "objectHolders"));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.ReplacedConstructions);
        ClassFileAssert.Calls(
            patched,
            ClassInitializer,
            owner: "java/util/concurrent/ConcurrentHashMap",
            name: "newKeySet",
            descriptor: "()Ljava/util/concurrent/ConcurrentHashMap$KeySetView;"
        );
        ClassFileAssert.DoesNotCall(patched, ClassInitializer, HashSet, Constructor, descriptor: "()V");
        ClassFileAssert.Calls(patched, ClassInitializer, HashMap, Constructor, descriptor: "()V");
    }

    /// <summary>An initializer that stores its HashSet in another field isn't the code this fix was written for, so the class is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesInitializersThatDontCreateObjectHolders()
    {
        ClassPatchResult result = ObjectHolderHandlers.Patch.Apply(CreateClassFile(setField: "pendingHolders"));

        Assert.False(result.Changed);
        Assert.Contains("no construction of " + HashSet, result.SkippedReason, StringComparison.Ordinal);
    }

    // A static initializer that stores a new HashSet in the given field and a new HashMap in namedHandlers, as Forge's for Minecraft 1.20.1 does.
    private static byte[] CreateClassFile(string setField)
    {
        static CodeBuilder StoreNew(CodeBuilder code, string type, string field, string descriptor)
        {
            return code.New(type).Dup().InvokeSpecial(type, Constructor, descriptor: "()V").PutStatic(ClassName, field, descriptor);
        }

        return new ClassFileWriter(ClassName)
            .AddMethod(
                ClassInitializer,
                descriptor: "()V",
                code => StoreNew(StoreNew(code, HashSet, setField, descriptor: "Ljava/util/Set;"), HashMap, field: "namedHandlers", descriptor: "Ljava/util/Map;").Return(),
                accessFlags: 0x0008
            )
            .ToArray();
    }
}
