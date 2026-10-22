using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Makes EventBus build a missing listener list without holding the lock on its map, as EventBus 0.10.5 does. <c>EventListenerHelper.getListenerListInternal</c>
/// builds it under the lock, and building it constructs the event, whose class initializer asks for a listener list too, so a mod-loading worker and a thread
/// that initializes the event class can wait for each other; Forge 1.14.2 and 1.14.3 clients deadlock at startup this way. EventBus 0.9.3 to 0.10.1 build it in a
/// synchronized map's <c>computeIfAbsent</c>, and 0.10.2 to 0.10.4 under the write lock of a read-write lock, so the edit redirects the call that builds it in each.
/// </summary>
internal static class EventListenerLists
{
    /// <summary>The internal name of the class the agent writes for EventBus 0.10.2 to 0.10.4, which Forge's EventBus calls by a descriptor of its own types.</summary>
    internal const string Name = "legacyforgepatcher/EventListenerLists";

    private const byte Aload0 = 0x2a;
    private const byte Areturn = 0xb0;
    private const byte Checkcast = 0xc0;
    private const string Helper = "legacyforgepatcher/EventListeners";
    private const string HelperClass = "net/minecraftforge/eventbus/api/EventListenerHelper";
    private const byte Iload1 = 0x1b;
    private const byte InvokeInterface = 0xb9;
    private const byte InvokeStatic = 0xb8;
    private const byte LdcWide = 0x13;
    private const string ListenerList = "net/minecraftforge/eventbus/ListenerList";
    private const string Method = "getListenerListInternal";
    private const string MethodDescriptor = "(Ljava/lang/Class;Z)Lnet/minecraftforge/eventbus/ListenerList;";

    private static readonly JavaMethod ComputeIfAbsent = JavaMethod.Interface(
        owner: "java/util/Map",
        name: "computeIfAbsent",
        descriptor: "(Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;"
    );

    private static readonly JavaMethod ComputeListenerList = JavaMethod.Static(HelperClass, name: "computeListenerList", MethodDescriptor);

    /// <summary>Gets the patch of <c>EventListenerHelper</c>.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(HelperClass)
        .Method(Method, MethodDescriptor)
        .Edit(description: "listener list built outside the lock", BuildOutsideTheLock);

    /// <summary>Gets the class the agent writes, made of one straight-line static method that needs no stack map frames.</summary>
    internal static JavaClassDefinition Definition { get; } = JavaClassDefinition.FromBytes(Create());

    // EventBus 0.10.5 and later build the list before they take the lock and put it in with putIfAbsent, and must stay as they are: the helper for 0.10.2 to
    // 0.10.4 lets go of locks the caller holds.
    private static int BuildOutsideTheLock(ClassEditContext context)
    {
        ClassFileEditor editor = context.Editor;
        List<MemberReference> calls = [.. Calls(editor.Model)];

        if (calls.Any(static call => IsMapCall(call, ComputeIfAbsent.Name)))
            return editor.ReplaceInvocation(ComputeIfAbsent, Helper, methodName: Method, methodDescriptor: MethodDescriptor);

        bool locks = calls.Any(static call => call is { Owner: "java/util/concurrent/locks/ReadWriteLock", Name: "writeLock" });
        bool puts = calls.Any(static call => IsMapCall(call, name: "put"));
        bool putsIfAbsent = calls.Any(static call => IsMapCall(call, name: "putIfAbsent"));

        return locks && puts && !putsIfAbsent ? editor.ReplaceInvocation(ComputeListenerList, Name, methodName: Method, methodDescriptor: MethodDescriptor) : 0;
    }

    private static IEnumerable<MemberReference> Calls(JavaClassFile model)
    {
        if (model.FindMethod(Method, MethodDescriptor)?.Code is not { } code)
            yield break;

        foreach (Instruction instruction in code.Instructions)
        {
            if (instruction is { Opcode: InvokeInterface or InvokeStatic or 0xb6 or 0xb7, ConstantIndex: { } index })
                yield return model.GetMemberReference(index);
        }
    }

    private static byte[] Create()
    {
        ClassFileWriter writer = new(Name);
        int self = writer.AddClass(Name);
        int listenerList = writer.AddClass(ListenerList);
        int computeListenerList = writer.AddMethodReference(Helper, ComputeListenerList.Name, descriptor: "(Ljava/lang/Class;Ljava/lang/Class;Z)Ljava/lang/Object;");

        // return (ListenerList) EventListeners.computeListenerList(EventListenerLists.class, eventClass, fromInstanceCall)
        writer = writer.AddMethod(
            ComputeListenerList.Name,
            MethodDescriptor,
            maxStack: 3,
            maxLocals: 2,
            [LdcWide, .. Index(self), Aload0, Iload1, InvokeStatic, .. Index(computeListenerList), Checkcast, .. Index(listenerList), Areturn]
        );

        return writer.ToArray();
    }

    private static byte[] Index(int index)
    {
        return [checked((byte)(index >> 8)), checked((byte)(index & 0xff))];
    }

    private static bool IsMapCall(MemberReference call, string name)
    {
        return call.Owner == "java/util/Map" && call.Name == name;
    }
}
