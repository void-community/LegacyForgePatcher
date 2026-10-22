using JvmBridge.Agents;
using JvmBridge.ClassFiles;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Redirecting FML's <c>Collections.sort</c> call in CoreModManager class files.</summary>
public sealed class TweakSortPatchTests
{
    private const byte AconstNull = 0x01;
    private const string ClassName = "cpw/mods/fml/relauncher/CoreModManager";
    private const byte InvokeStatic = 0xb8;
    private const byte Return = 0xb1;
    private const string SortDescriptor = "(Ljava/util/List;Ljava/util/Comparator;)V";
    private const string SortTweakList = "sortTweakList";

    /// <summary>The compiled helper declares the static method the patch calls, in a class that Java 8 loads.</summary>
    [Fact]
    public void HelperDeclaresTheMethodThePatchCalls()
    {
        JavaClassFile helper = JavaClassFile.Read(JavaClasses.Legacyforgepatcher.Collections.Bytes.Span);

        Assert.Equal(JavaClasses.Legacyforgepatcher.Collections.Name, helper.Name);
        Assert.Equal(expected: 52, helper.MajorVersion);
        Assert.NotNull(helper.FindMethod(name: "sort", SortDescriptor));
    }

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

        MemberReference invoked = GetInvokedMethod(patched, method: "other");

        Assert.Equal(expected: "java/util/Collections", invoked.Owner);
    }

    /// <summary>The call in <c>sortTweakList</c> now goes to the helper, and the result says it was the only one redirected.</summary>
    [Fact]
    public void PatchRedirectsTheSortInSortTweakList()
    {
        ClassPatchResult result = PatcherAgent.TweakSort.Apply(CreateClassFile((SortTweakList, CallsSort: true)));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        MemberReference invoked = GetInvokedMethod(patched, SortTweakList);

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        Assert.Equal(JavaClasses.Legacyforgepatcher.Collections.Name, invoked.Owner);
        Assert.Equal(expected: "sort", invoked.Name);
        Assert.Equal(SortDescriptor, invoked.Descriptor);
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
        ClassFileWriter writer = new(ClassName);
        int sort = writer.AddMethodReference(owner: "java/util/Collections", name: "sort", SortDescriptor);

        foreach ((string name, bool callsSort) in methods)
        {
            byte[] code = callsSort ? [AconstNull, AconstNull, InvokeStatic, checked((byte)(sort >> 8)), checked((byte)(sort & 0xff)), Return] : [Return];

            writer = writer.AddMethod(name, descriptor: "()V", maxStack: 2, maxLocals: 0, code);
        }

        return writer.ToArray();
    }

    private static MemberReference GetInvokedMethod(byte[] classFile, string method)
    {
        JavaClassFile file = JavaClassFile.Read(classFile);
        CodeAttribute code = file.FindMethod(method)?.Code ?? throw new InvalidOperationException($"{method} has no code.");
        Instruction invocation = Assert.Single(code.Instructions, static instruction => instruction.Opcode is InvokeStatic);

        return file.GetMemberReference(invocation.ConstantIndex ?? throw new InvalidOperationException(message: "The invocation has no constant."));
    }
}
