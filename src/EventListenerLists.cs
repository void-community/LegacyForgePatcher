using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace ModLoaderFixes;

/// <summary>
/// Makes EventBus build a missing listener list without holding the lock on its map, as EventBus 0.10.5 does. <c>EventListenerHelper.getListenerListInternal</c>
/// builds it under the lock, and building it constructs the event, whose class initializer asks for a listener list too, so a mod-loading worker and a thread
/// that initializes the event class can wait for each other; Forge 1.14.2 and 1.14.3 clients deadlock at startup this way. EventBus 0.9.3 to 0.10.1 build it in a
/// synchronized map's <c>computeIfAbsent</c>, and 0.10.2 to 0.10.4 under the write lock of a read-write lock, so there is a patch for each, and each skips the
/// other's. EventBus 0.10.5 and later build the list before they take the lock and put it in with putIfAbsent, and must stay as they are: the helper for 0.10.2 to
/// 0.10.4 lets go of locks the caller holds.
/// </summary>
internal static class EventListenerLists
{
    private const string Helper = "modloaderfixes/EventListeners";
    private const string HelperClass = "net/minecraftforge/eventbus/api/EventListenerHelper";
    private const string Method = "getListenerListInternal";
    private const string MethodDescriptor = "(Ljava/lang/Class;Z)Lnet/minecraftforge/eventbus/ListenerList;";

    private static readonly JavaMethod ComputeIfAbsent = MapCall(name: "computeIfAbsent", descriptor: "(Ljava/lang/Object;Ljava/util/function/Function;)Ljava/lang/Object;");
    private static readonly JavaMethod ComputeListenerList = JavaMethod.Static(HelperClass, name: "computeListenerList", MethodDescriptor);
    private static readonly JavaMethod Put = MapCall(name: "put", descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;");
    private static readonly JavaMethod PutIfAbsent = MapCall(name: "putIfAbsent", descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;");
    private static readonly JavaMethod WriteLock = JavaMethod.Interface(owner: "java/util/concurrent/locks/ReadWriteLock", name: "writeLock", descriptor: "()Ljava/util/concurrent/locks/Lock;");

    /// <summary>Gets the patches of <c>EventListenerHelper</c>: for EventBus 0.9.3 to 0.10.1, and for 0.10.2 to 0.10.4.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        ClassPatch.For(HelperClass)
            .Method(Method, MethodDescriptor)
            .When(static body => body.Calls(ComputeIfAbsent))
            .RedirectInvocation(ComputeIfAbsent, Helper),
        // The helper takes and gives back the lock, reads the map and builds the list itself, all private here, through the methods this patch adds.
        ClassPatch.For(HelperClass)
            .AddFieldGetter(field: "lock", name: "modloaderfixes$lock")
            .AddFieldGetter(field: "listeners", name: "modloaderfixes$listeners")
            .AddInvoker(ComputeListenerList.Name, MethodDescriptor, name: "modloaderfixes$computeListenerList")
            .Method(Method, MethodDescriptor)
            .When(static body => body.Calls(WriteLock) && body.Calls(Put) && !body.Calls(PutIfAbsent))
            .RedirectInvocation(ComputeListenerList, Helper),
    ];

    private static JavaMethod MapCall(string name, string descriptor)
    {
        return JavaMethod.Interface(owner: "java/util/Map", name, descriptor);
    }
}
