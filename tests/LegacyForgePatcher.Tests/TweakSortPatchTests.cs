using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Redirecting FML's <c>Collections.sort</c> call in CoreModManager class files.</summary>
public sealed class TweakSortPatchTests
{
    private const string ClassName = "cpw/mods/fml/relauncher/CoreModManager";
    private const string SortDescriptor = "(Ljava/util/List;Ljava/util/Comparator;)V";
    private const string SortTweakList = "sortTweakList";

    /// <summary>A CoreModManager whose <c>sortTweakList</c> doesn't sort with <c>Collections.sort</c> needs no patch.</summary>
    [Fact]
    public void PatchLeavesClassesThatDontCallCollectionsSort()
    {
        ClassPatchResult result = PatcherAgent.TweakSort.Apply(CreateClassFile((SortTweakList, CallsSort: false)));

        Assert.False(result.Changed);
        Assert.Null(result.Bytes);
        Assert.NotNull(result.SkippedReason);
    }

    /// <summary>A CoreModManager without <c>sortTweakList</c>, as in Forge versions that sort differently, is left alone.</summary>
    [Fact]
    public void PatchLeavesClassesWithoutSortTweakList()
    {
        ClassPatchResult result = PatcherAgent.TweakSort.Apply(CreateClassFile((Name: "other", CallsSort: true)));

        Assert.False(result.Changed);
        Assert.Contains(SortTweakList, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The same call in any other method is somebody else's sort, so it stays as it was.</summary>
    [Fact]
    public void PatchLeavesTheSortInOtherMethodsAlone()
    {
        byte[] patched = PatcherAgent.TweakSort.Apply(CreateClassFile((SortTweakList, CallsSort: true), (Name: "other", CallsSort: true))).Bytes ?? throw new InvalidOperationException(message: "The class file was left unchanged.");

        ClassFileAssert.Calls(patched, method: "other", owner: "java/util/Collections", name: "sort", SortDescriptor);
    }

    /// <summary>The call in <c>sortTweakList</c> now goes to the helper, and the result says it was the only one redirected.</summary>
    [Fact]
    public void PatchRedirectsTheSortInSortTweakList()
    {
        ClassPatchResult result = PatcherAgent.TweakSort.Apply(CreateClassFile((SortTweakList, CallsSort: true)));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, SortTweakList, JavaClasses.Legacyforgepatcher.Collections.Name, name: "sort", SortDescriptor);
        ClassFileAssert.DoesNotCall(patched, SortTweakList, owner: "java/util/Collections", name: "sort", SortDescriptor);
    }

    /// <summary>Bytes that aren't a class file are skipped with a reason and never throw into the JVM.</summary>
    [Fact]
    public void PatchSkipsBytesThatArentAClassFile()
    {
        ClassPatchResult result = PatcherAgent.TweakSort.Apply(new byte[16]);

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "not a valid class file", result.SkippedReason, StringComparison.Ordinal);
    }

    // A class file with straight-line methods, each of which calls Collections.sort or just returns.
    private static byte[] CreateClassFile(params (string Name, bool CallsSort)[] methods)
    {
        JavaMethod sort = JavaMethod.Static(owner: "java/util/Collections", name: "sort", SortDescriptor);
        ClassFileWriter writer = new(ClassName);

        foreach ((string name, bool callsSort) in methods)
            writer = writer.AddMethod(name, descriptor: "()V", code => (callsSort ? code.AConstNull().AConstNull().Invoke(sort) : code).Return());

        return writer.ToArray();
    }
}
