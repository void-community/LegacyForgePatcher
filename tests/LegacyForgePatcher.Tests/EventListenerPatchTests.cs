using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Making EventBus build a missing listener list without holding the lock on its map, in each shape EventBus had before 0.10.5.</summary>
public sealed class EventListenerPatchTests
{
    private const string ClassName = "net/minecraftforge/eventbus/api/EventListenerHelper";
    private const string ComputeIfAbsent = "computeIfAbsent";
    private const string ComputeIfAbsentDescriptor = "(Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;";
    private const string ComputeListenerList = "computeListenerList";
    private const string Helper = "legacyforgepatcher/EventListeners";
    private const string HelperComputeIfAbsentDescriptor = "(Ljava/util/Map;Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;";
    private const string ListDescriptor = "(Ljava/lang/Class;Z)Lnet/minecraftforge/eventbus/ListenerList;";
    private const string Map = "java/util/Map";
    private const string Method = "getListenerListInternal";

    /// <summary>The Java helper declares the methods the redirects call, computeListenerList with EventBus's own descriptor, in a class that Java 8 loads.</summary>
    [Fact]
    public void HelperDeclaresTheMethodsTheRedirectsCall()
    {
        JavaClassFile helper = JavaClassFile.Read(JavaClasses.Legacyforgepatcher.EventListeners.Bytes.Span);

        Assert.Equal(Helper, helper.Name);
        Assert.Equal(expected: 52, helper.MajorVersion);
        Assert.NotNull(helper.FindMethod(ComputeIfAbsent, HelperComputeIfAbsentDescriptor));
        Assert.NotNull(helper.FindMethod(ComputeListenerList, ListDescriptor));
    }

    /// <summary>EventBus from 0.10.5 builds the list outside the lock and stores it with putIfAbsent, so it stays as it is: the helper lets go of locks the caller holds.</summary>
    [Fact]
    public void PatchLeavesTheFixedEventBusAlone()
    {
        ClassPatchResult result = EventListenerLists.Patch.Apply(CreateLockedClassFile(store: "putIfAbsent"));

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "nothing to change in getListenerListInternal", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// EventBus 0.10.2 to 0.10.4 build it under the write lock and put it in the map; the helper that builds it then takes the lock, the map and computeListenerList,
    /// so a class that has the shape but not the lock and the map is left alone. The redirect itself runs against the fixtures of <c>EventListenerTests</c>, whose
    /// EventListenerHelper declares them.
    /// </summary>
    [Fact]
    public void PatchLeavesTheWriteLockShapeWithoutTheLockAndTheMap()
    {
        ClassPatchResult result = EventListenerLists.Patch.Apply(CreateLockedClassFile(store: "put"));

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "nothing to change in getListenerListInternal", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>EventBus 0.9.3 to 0.10.1 build the list in a synchronized map's computeIfAbsent, which goes to the helper.</summary>
    [Fact]
    public void PatchRedirectsTheSynchronizedMapsComputeIfAbsent()
    {
        JavaMethod computeIfAbsent = JavaMethod.Interface(Map, ComputeIfAbsent, ComputeIfAbsentDescriptor);

        byte[] classFile = new ClassFileWriter(ClassName)
            .AddMethod(Method, ListDescriptor, code => code.AConstNull().AConstNull().AConstNull().Invoke(computeIfAbsent).AReturn())
            .ToArray();

        ClassPatchResult result = EventListenerLists.Patch.Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.AppliedEdits);
        ClassFileAssert.Calls(patched, Method, Helper, ComputeIfAbsent, HelperComputeIfAbsentDescriptor);
        ClassFileAssert.DoesNotCall(patched, Method, Map, ComputeIfAbsent, ComputeIfAbsentDescriptor);
    }

    // A getListenerListInternal that takes the write lock, builds the list with the private computeListenerList and stores it in the map with the given method;
    // ClassFileWriter declares no fields, so the class has no lock or map of its own.
    private static byte[] CreateLockedClassFile(string store)
    {
        JavaMethod writeLock = JavaMethod.Interface(owner: "java/util/concurrent/locks/ReadWriteLock", name: "writeLock", descriptor: "()Ljava/util/concurrent/locks/Lock;");
        JavaMethod build = JavaMethod.Static(ClassName, ComputeListenerList, ListDescriptor);
        JavaMethod put = JavaMethod.Interface(Map, store, descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;");

        return new ClassFileWriter(ClassName)
            .AddMethod(
                Method,
                ListDescriptor,
                code => code.AConstNull()
                    .Invoke(writeLock)
                    .Pop()
                    .AConstNull()
                    .IConst(value: 0)
                    .Invoke(build)
                    .Pop()
                    .AConstNull()
                    .AConstNull()
                    .AConstNull()
                    .Invoke(put)
                    .Pop()
                    .AConstNull()
                    .AReturn()
            )
            .AddMethod(ComputeListenerList, ListDescriptor, static code => code.AConstNull().AReturn(), accessFlags: 0x000a)
            .ToArray();
    }
}
