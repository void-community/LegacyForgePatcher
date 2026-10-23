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
    private const string Helper = "legacyforgepatcher/EventListeners";
    private const string HelperClass = "net/minecraftforge/eventbus/api/EventListenerHelper";
    private const string Method = "getListenerListInternal";
    private const string MethodDescriptor = "(Ljava/lang/Class;Z)Lnet/minecraftforge/eventbus/ListenerList;";
    private const int PublicStatic = 0x0009;
    private const int PublicStaticFinal = 0x0019;

    private static readonly JavaMethod ComputeIfAbsent = MapCall(name: "computeIfAbsent", descriptor: "(Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;");
    private static readonly JavaMethod ComputeListenerList = JavaMethod.Static(HelperClass, name: "computeListenerList", MethodDescriptor);
    private static readonly JavaMethod Put = MapCall(name: "put", descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;");
    private static readonly JavaMethod PutIfAbsent = MapCall(name: "putIfAbsent", descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;");
    private static readonly JavaMethod WriteLock = JavaMethod.Interface(owner: "java/util/concurrent/locks/ReadWriteLock", name: "writeLock", descriptor: "()Ljava/util/concurrent/locks/Lock;");

    /// <summary>Gets the patch of <c>EventListenerHelper</c>.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(HelperClass)
        .Method(Method, MethodDescriptor)
        .Edit(description: "listener list built outside the lock", BuildOutsideTheLock);

    // EventBus 0.10.5 and later build the list before they take the lock and put it in with putIfAbsent, and must stay as they are: the helper for 0.10.2 to
    // 0.10.4 lets go of locks the caller holds.
    private static int BuildOutsideTheLock(ClassEditContext context)
    {
        ClassFileEditor editor = context.Editor;
        JavaClassFile model = editor.Model;
        IReadOnlyList<Instruction> code = model.FindMethod(Method, MethodDescriptor)?.Code?.Instructions ?? [];

        bool Calls(JavaMethod method)
        {
            return code.Any(instruction => model.Invokes(instruction, method));
        }

        if (Calls(ComputeIfAbsent))
            return editor.ReplaceInvocation(ComputeIfAbsent, Helper, methodName: Method, methodDescriptor: MethodDescriptor);

        if (!Calls(WriteLock) || !Calls(Put) || Calls(PutIfAbsent))
            return 0;

        // The helper takes and gives back the lock, reads the map and builds the list itself, all private here; they open to it and stay static and final. A
        // class without all three isn't the EventBus the helper was written for, and returning 0 leaves it unchanged.
        int opened = editor.SetFieldAccess(name: "lock", descriptor: "Ljava/util/concurrent/locks/ReadWriteLock;", PublicStaticFinal)
            + editor.SetFieldAccess(name: "listeners", descriptor: "Ljava/util/Map;", PublicStaticFinal)
            + editor.SetMethodAccess(ComputeListenerList.Name, MethodDescriptor, PublicStatic);

        return opened == 3 ? editor.ReplaceInvocation(ComputeListenerList, Helper, methodName: Method, methodDescriptor: MethodDescriptor) : 0;
    }

    private static JavaMethod MapCall(string name, string descriptor)
    {
        return JavaMethod.Interface(owner: "java/util/Map", name, descriptor);
    }
}
