using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Making EventBus build a missing listener list without holding the lock on its map, in each shape EventBus had before 0.10.5.</summary>
public sealed class EventListenerPatchTests
{
    private const byte AconstNull = 0x01;
    private const byte Areturn = 0xb0;
    private const string ClassName = "net/minecraftforge/eventbus/api/EventListenerHelper";
    private const string ComputeIfAbsent = "computeIfAbsent";
    private const string ComputeIfAbsentDescriptor = "(Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;";
    private const string ComputeListenerList = "computeListenerList";
    private const string Helper = "legacyforgepatcher/EventListeners";
    private const byte IconstZero = 0x03;
    private const byte InvokeInterface = 0xb9;
    private const byte InvokeStatic = 0xb8;
    private const string ListDescriptor = "(Ljava/lang/Class;Z)Lnet/minecraftforge/eventbus/ListenerList;";
    private const string Map = "java/util/Map";
    private const string Method = "getListenerListInternal";
    private const byte Pop = 0x57;

    /// <summary>The class the agent writes has computeListenerList's descriptor and hands the call to the helper, with itself as the class of EventBus's loader.</summary>
    [Fact]
    public void EventListenerListsHandsTheCallToTheHelper()
    {
        byte[] bytes = EventListenerLists.Definition.Bytes.ToArray();

        Assert.Equal(EventListenerLists.Name, JavaClassFile.Read(bytes).Name);
        Assert.NotNull(JavaClassFile.Read(bytes).FindMethod(ComputeListenerList, ListDescriptor));
        ClassFileAssert.Calls(bytes, ComputeListenerList, Helper, ComputeListenerList, descriptor: "(Ljava/lang/Class;Ljava/lang/Class;Z)Ljava/lang/Object;");
    }

    /// <summary>The Java helper declares the methods the redirects call, in a class that Java 8 loads.</summary>
    [Fact]
    public void HelperDeclaresTheMethodsTheRedirectsCall()
    {
        JavaClassFile helper = JavaClassFile.Read(JavaClasses.Legacyforgepatcher.EventListeners.Bytes.Span);

        Assert.Equal(Helper, helper.Name);
        Assert.Equal(expected: 52, helper.MajorVersion);
        Assert.NotNull(
            helper.FindMethod(ComputeIfAbsent, descriptor: "(Ljava/util/Map;Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;")
        );
        Assert.NotNull(helper.FindMethod(ComputeListenerList, descriptor: "(Ljava/lang/Class;Ljava/lang/Class;Z)Ljava/lang/Object;"));
    }

    /// <summary>EventBus from 0.10.5 builds the list outside the lock and stores it with putIfAbsent, so it stays as it is: the helper lets go of locks the caller holds.</summary>
    [Fact]
    public void PatchLeavesTheFixedEventBusAlone()
    {
        ClassPatchResult result = EventListenerLists.Patch.Apply(CreateLockedClassFile(store: "putIfAbsent"));

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "nothing to change in getListenerListInternal", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>EventBus 0.10.2 to 0.10.4 build it under the write lock and put it in the map, so building it goes to the class the agent writes.</summary>
    [Fact]
    public void PatchRedirectsTheBuildUnderTheWriteLock()
    {
        ClassPatchResult result = EventListenerLists.Patch.Apply(CreateLockedClassFile(store: "put"));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.AppliedEdits);
        ClassFileAssert.Calls(patched, Method, EventListenerLists.Name, ComputeListenerList, ListDescriptor);
        ClassFileAssert.DoesNotCall(patched, Method, ClassName, ComputeListenerList, ListDescriptor);
    }

    /// <summary>EventBus 0.9.3 to 0.10.1 build the list in a synchronized map's computeIfAbsent, which goes to the helper.</summary>
    [Fact]
    public void PatchRedirectsTheSynchronizedMapsComputeIfAbsent()
    {
        ClassFileWriter writer = new(ClassName);
        int computeIfAbsent = writer.AddInterfaceMethodReference(Map, ComputeIfAbsent, ComputeIfAbsentDescriptor);

        writer = writer.AddMethod(
            Method,
            ListDescriptor,
            maxStack: 3,
            maxLocals: 2,
            [AconstNull, AconstNull, AconstNull, .. Interface(computeIfAbsent, arguments: 3), Areturn]
        );

        ClassPatchResult result = EventListenerLists.Patch.Apply(writer.ToArray());
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.AppliedEdits);
        ClassFileAssert.Calls(
            patched,
            Method,
            Helper,
            ComputeIfAbsent,
            descriptor: "(Ljava/util/Map;Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;"
        );
        ClassFileAssert.DoesNotCall(patched, Method, Map, ComputeIfAbsent, ComputeIfAbsentDescriptor);
    }

    // A getListenerListInternal that takes the write lock, builds the list and stores it in the map with the given method.
    private static byte[] CreateLockedClassFile(string store)
    {
        ClassFileWriter writer = new(ClassName);
        int writeLock = writer.AddInterfaceMethodReference(owner: "java/util/concurrent/locks/ReadWriteLock", name: "writeLock", descriptor: "()Ljava/util/concurrent/locks/Lock;");
        int build = writer.AddMethodReference(ClassName, ComputeListenerList, ListDescriptor);
        int put = writer.AddInterfaceMethodReference(Map, store, descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;");

        byte[] code =
        [
            AconstNull, .. Interface(writeLock, arguments: 1), Pop,
            AconstNull, IconstZero, InvokeStatic, .. Index(build), Pop,
            AconstNull, AconstNull, AconstNull, .. Interface(put, arguments: 3), Pop,
            AconstNull, Areturn,
        ];

        writer = writer.AddMethod(Method, ListDescriptor, maxStack: 3, maxLocals: 2, code);

        return writer.ToArray();
    }

    private static byte[] Index(int index)
    {
        return [checked((byte)(index >> 8)), checked((byte)(index & 0xff))];
    }

    private static byte[] Interface(int method, byte arguments)
    {
        return [InvokeInterface, .. Index(method), arguments, 0];
    }
}
