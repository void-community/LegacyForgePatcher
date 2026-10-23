using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Making EventBus build a missing listener list without holding the lock on its map, in each shape EventBus had before 0.10.5.</summary>
public sealed class EventListenerPatchTests
{
    private const string ClassName = "net/minecraftforge/eventbus/api/EventListenerHelper";
    private const string ComputeIfAbsent = "computeIfAbsent";
    private const string ComputeIfAbsentDescriptor = "(Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;";
    private const string ComputeListenerList = "computeListenerList";
    private const string Helper = "modloaderfixes/EventListeners";
    private const string ListDescriptor = "(Ljava/lang/Class;Z)Lnet/minecraftforge/eventbus/ListenerList;";
    private const string Map = "java/util/Map";
    private const string Method = "getListenerListInternal";

    private static ClassPatch SynchronizedMapPatch => EventListenerLists.Patches[index: 0];

    private static ClassPatch WriteLockPatch => EventListenerLists.Patches[index: 1];

    /// <summary>
    /// EventBus 0.10.2 to 0.10.4 build it under the write lock and put it in the map, so building it goes to the helper, and the patch adds the getters and the invoker
    /// through which the helper reaches the lock, the map and computeListenerList; the patch for the synchronized map leaves the class alone.
    /// </summary>
    [Fact]
    public void PatchRedirectsTheBuildUnderTheWriteLock()
    {
        byte[] classFile = CreateLockedClassFile(store: "put");
        ClassPatchResult result = WriteLockPatch.Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());
        JavaClassFile model = JavaClassFile.Read(patched);

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        Assert.Equal(expected: 3, result.AddedMembers);
        ClassFileAssert.Calls(patched, Method, Helper, ComputeListenerList, ListDescriptor);
        ClassFileAssert.DoesNotCall(patched, Method, ClassName, ComputeListenerList, ListDescriptor);
        Assert.NotNull(model.FindMethod(name: "modloaderfixes$lock", descriptor: "()Ljava/util/concurrent/locks/ReadWriteLock;"));
        Assert.NotNull(model.FindMethod(name: "modloaderfixes$listeners", descriptor: "()Ljava/util/Map;"));
        Assert.NotNull(model.FindMethod(name: "modloaderfixes$computeListenerList", ListDescriptor));
        Assert.False(SynchronizedMapPatch.Apply(classFile).Changed);
    }

    /// <summary>EventBus 0.9.3 to 0.10.1 build the list in a synchronized map's computeIfAbsent, which goes to the helper; the patch for the write lock leaves it alone.</summary>
    [Fact]
    public void PatchRedirectsTheSynchronizedMapsComputeIfAbsent()
    {
        JavaMethod computeIfAbsent = JavaMethod.Interface(Map, ComputeIfAbsent, ComputeIfAbsentDescriptor);

        byte[] classFile = new ClassFileWriter(ClassName)
            .AddMethod(Method, ListDescriptor, code => code.AConstNull().AConstNull().AConstNull().Invoke(computeIfAbsent).AReturn())
            .ToArray();

        ClassPatchResult result = SynchronizedMapPatch.Apply(classFile);
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(
            patched,
            Method,
            Helper,
            ComputeIfAbsent,
            descriptor: "(Ljava/util/Map;Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;"
        );
        ClassFileAssert.DoesNotCall(patched, Method, Map, ComputeIfAbsent, ComputeIfAbsentDescriptor);
        Assert.False(WriteLockPatch.Apply(classFile).Changed);
    }

    /// <summary>EventBus from 0.10.5 builds the list outside the lock and stores it with putIfAbsent, so both patches leave it alone and say why.</summary>
    [Fact]
    public void PatchesLeaveTheFixedEventBusAlone()
    {
        byte[] classFile = CreateLockedClassFile(store: "putIfAbsent");
        ClassPatchResult synchronizedMap = SynchronizedMapPatch.Apply(classFile);
        ClassPatchResult writeLock = WriteLockPatch.Apply(classFile);

        Assert.False(synchronizedMap.Changed);
        Assert.False(writeLock.Changed);
        Assert.Contains(expectedSubstring: "requires", synchronizedMap.SkippedReason, StringComparison.Ordinal);
        Assert.Contains(expectedSubstring: "!putIfAbsent()", writeLock.SkippedReason, StringComparison.Ordinal);
    }

    // An EventListenerHelper with EventBus 0.10.2's private lock, map and computeListenerList, whose getListenerListInternal takes the write lock, builds the list and
    // stores it in the map with the given method.
    private static byte[] CreateLockedClassFile(string store)
    {
        JavaMethod writeLock = JavaMethod.Interface(owner: "java/util/concurrent/locks/ReadWriteLock", name: "writeLock", descriptor: "()Ljava/util/concurrent/locks/Lock;");
        JavaMethod build = JavaMethod.Static(ClassName, ComputeListenerList, ListDescriptor);
        JavaMethod put = JavaMethod.Interface(Map, store, descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;");

        return new ClassFileWriter(ClassName)
            .AddField(name: "lock", descriptor: "Ljava/util/concurrent/locks/ReadWriteLock;", accessFlags: 0x001a)
            .AddField(name: "listeners", descriptor: "Ljava/util/Map;", accessFlags: 0x001a)
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
