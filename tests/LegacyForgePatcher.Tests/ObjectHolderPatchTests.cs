using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Making the set of object holder handlers that Forge's ObjectHolderRegistry keeps a concurrent one, in its static initializer.</summary>
public sealed class ObjectHolderPatchTests
{
    private const string ClassInitializer = "<clinit>";
    private const string ClassName = "net/minecraftforge/registries/ObjectHolderRegistry";
    private const string Constructor = "<init>";
    private const byte Dup = 0x59;
    private const string HashMap = "java/util/HashMap";
    private const string HashSet = "java/util/HashSet";
    private const byte InvokeSpecial = 0xb7;
    private const byte New = 0xbb;
    private const byte PutStatic = 0xb3;
    private const byte Return = 0xb1;

    /// <summary>The set stored in <c>objectHolders</c> comes from <c>ConcurrentHashMap.newKeySet()</c>, and the map the initializer creates next stays a HashMap.</summary>
    [Fact]
    public void PatchCreatesObjectHoldersAsAConcurrentKeySet()
    {
        ClassPatchResult result = ObjectHolderHandlers.Patch.Apply(CreateClassFile(setField: "objectHolders"));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.AppliedEdits);
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
        Assert.Contains(expectedSubstring: "nothing to change in <clinit>", result.SkippedReason, StringComparison.Ordinal);
    }

    // A static initializer that stores a new HashSet in the given field and a new HashMap in namedHandlers, as Forge's for Minecraft 1.20.1 does.
    private static byte[] CreateClassFile(string setField)
    {
        ClassFileWriter writer = new(ClassName);
        byte[] set = StoreNew(writer, HashSet, writer.AddFieldReference(ClassName, setField, descriptor: "Ljava/util/Set;"));
        byte[] map = StoreNew(writer, HashMap, writer.AddFieldReference(ClassName, name: "namedHandlers", descriptor: "Ljava/util/Map;"));

        writer = writer.AddMethod(ClassInitializer, descriptor: "()V", maxStack: 2, maxLocals: 0, [.. set, .. map, Return], accessFlags: 0x0008);

        return writer.ToArray();
    }

    private static byte[] Index(int index)
    {
        return [checked((byte)(index >> 8)), checked((byte)(index & 0xff))];
    }

    private static byte[] StoreNew(ClassFileWriter writer, string type, int field)
    {
        int created = writer.AddClass(type);
        int constructor = writer.AddMethodReference(type, Constructor, descriptor: "()V");

        return [New, .. Index(created), Dup, InvokeSpecial, .. Index(constructor), PutStatic, .. Index(field)];
    }
}
