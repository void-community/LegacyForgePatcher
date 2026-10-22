using LegacyForgePatcher.ClassFiles;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Redirecting FML's <c>Collections.sort</c> calls in CoreModManager class files.</summary>
public sealed class CoreModManagerPatchTests
{
    private const string EmptyListDescriptor = "()Ljava/util/List;";
    private const string SortDescriptor = "(Ljava/util/List;Ljava/util/Comparator;)V";

    /// <summary>The code after the constant pool still refers to every entry by its index, so the patch leaves it as it was.</summary>
    [Fact]
    public void ApplyKeepsTheBytesAfterThePool()
    {
        byte[] original = CreateClassFile(callsCollectionsSort: true).Bytes;
        byte[] patched = CoreModManagerPatch.Apply(original) ?? throw new InvalidOperationException(message: "The class file was left unchanged.");

        int originalEnd = ConstantPool.Read(original).End;
        int patchedEnd = ConstantPool.Read(patched).End;

        Assert.Equal(original[originalEnd..], patched[patchedEnd..]);
    }

    /// <summary>A CoreModManager that doesn't sort with <c>Collections.sort</c> needs no patch.</summary>
    [Fact]
    public void ApplyLeavesClassesThatDontCallCollectionsSort()
    {
        Assert.Null(CoreModManagerPatch.Apply(CreateClassFile(callsCollectionsSort: false).Bytes));
    }

    /// <summary>The sort's method reference keeps its index and now names the generated class, while other references stay as they were.</summary>
    [Fact]
    public void ApplyPointsCollectionsSortAtSortClass()
    {
        TestClassFile classFile = CreateClassFile(callsCollectionsSort: true);
        byte[] patched = CoreModManagerPatch.Apply(classFile.Bytes) ?? throw new InvalidOperationException(message: "The class file was left unchanged.");
        ConstantPool pool = ConstantPool.Read(patched);

        Assert.Equal(classFile.Sort, pool.FindMethodReference(SortClass.Name, SortClass.MethodName, SortDescriptor));
        Assert.Equal(expected: 0, pool.FindMethodReference(owner: "java/util/Collections", name: "sort", SortDescriptor));
        Assert.Equal(classFile.EmptyList, pool.FindMethodReference(owner: "java/util/Collections", name: "emptyList", EmptyListDescriptor));
    }

    /// <summary>Bytes without the class file magic number aren't patched.</summary>
    [Fact]
    public void ApplyRejectsBytesThatArentAClassFile()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(static () => CoreModManagerPatch.Apply(new byte[16]));

        Assert.Contains(expectedSubstring: "magic number", exception.Message, StringComparison.Ordinal);
    }

    // A class file with no members, whose pool holds references CoreModManager makes.
    private static TestClassFile CreateClassFile(bool callsCollectionsSort)
    {
        ConstantPoolBuilder pool = new(firstIndex: 1);
        ushort thisClass = pool.Class(CoreModManagerPatch.ClassName);
        ushort superClass = pool.Class(name: "java/lang/Object");
        ushort emptyList = pool.MethodReference(owner: "java/util/Collections", name: "emptyList", EmptyListDescriptor);
        ushort sort = callsCollectionsSort ? pool.MethodReference(owner: "java/util/Collections", name: "sort", SortDescriptor) : (ushort)0;

        using MemoryStream classFile = new();

        classFile.WriteU4(value: 0xcafebabe);
        classFile.WriteU2(value: 0);
        classFile.WriteU2(value: 50);
        classFile.WriteU2(pool.Count);
        pool.WriteTo(classFile);
        classFile.WriteU2(value: 0x0021);
        classFile.WriteU2(thisClass);
        classFile.WriteU2(superClass);

        for (int count = 0; count < 4; count++)
            classFile.WriteU2(value: 0);

        return new TestClassFile(classFile.ToArray(), emptyList, sort);
    }

    /// <param name="Bytes">The class file.</param>
    /// <param name="EmptyList">The index of the <c>Collections.emptyList</c> method reference.</param>
    /// <param name="Sort">The index of the <c>Collections.sort</c> method reference, or zero when the class file has none.</param>
    private sealed record TestClassFile(byte[] Bytes, int EmptyList, int Sort);
}
