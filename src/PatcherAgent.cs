using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Repairs Forge where its own code breaks: the tweaker sort that stops Forge for Minecraft 1.7.2 on Java 8u20 and later, the connection state switch that drops
/// players at login on Forge for Minecraft 1.12, the registry rebuilds that crash Forge for 1.12 and 1.12.1 clients when a proxy moves them to another server, and
/// the list of handshake messages that drops players at login on Forge for 1.13 to 1.20.1, and the second load of FML's config that crashes Forge and NeoForge
/// clients at startup from 1.20.1, the set of object holder handlers that mods fill from several threads in Forge from 1.13.2, and the chunk batch answer that drops
/// Minecraft clients from 1.20.5 when it arrives before play is set up, and the listener lists that EventBus builds under its lock and deadlocks Forge 1.14.2 and
/// 1.14.3 clients with, and the file watcher that reloads FML's config while Forge 1.13.2 to 1.19.4 still corrects it on a first start.
/// </summary>
public sealed class PatcherAgent : JavaAgent
{
    /// <summary>The call to NetworkManager.setConnectionState, by the name that FML's deobfuscation gives it before Forge's classes are defined.</summary>
    internal static readonly JavaMethod SetConnectionState = JavaMethod.Virtual(
        owner: "net/minecraft/network/NetworkManager",
        name: "func_150723_a",
        descriptor: "(Lnet/minecraft/network/EnumConnectionState;)V"
    );

    /// <summary>
    /// Points FML's <c>Collections.sort</c> call at <c>legacyforgepatcher.Collections</c>. FML sorts LaunchWrapper's tweakers while LaunchWrapper iterates
    /// them, and Java from 8u20 counts a sort as a structural change, so the iteration throws <c>ConcurrentModificationException</c>.
    /// </summary>
    internal static readonly ClassPatch TweakSort = ClassPatch.For(className: "cpw/mods/fml/relauncher/CoreModManager")
        .Method(name: "sortTweakList", descriptor: "()V")
        .RedirectInvocation(
            JavaMethod.Static(owner: "java/util/Collections", name: "sort", descriptor: "(Ljava/util/List;Ljava/util/Comparator;)V"),
            toOwner: "legacyforgepatcher/Collections"
        );

    /// <summary>
    /// Points the <c>setConnectionState(PLAY)</c> call in FML's <c>NetworkDispatcher.serverInitiateHandshake</c> at <c>legacyforgepatcher.ConnectionStates</c>, which makes the
    /// switch on the connection's event loop. FML makes it at once on the server thread while the login packets it just sent are still queued on that loop, so the
    /// loop encodes them in the play state, which has no id for them, and the player is dropped with <c>Can't serialize unregistered packet</c>.
    /// </summary>
    internal static readonly ClassPatch HandshakeState = ClassPatch.For(className: "net/minecraftforge/fml/common/network/handshake/NetworkDispatcher")
        .Method(name: "serverInitiateHandshake", descriptor: "()I")
        .RedirectInvocation(
            SetConnectionState,
            // The helper takes Object parameters because the bootstrap loader that defines it can't see Minecraft's classes.
            JavaMethod.Static(owner: "legacyforgepatcher/ConnectionStates", name: "setConnectionState", descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)V")
        );

    /// <summary>Forge's revert of the registries to their frozen state, which its client handshake calls when a proxy resets the handshake.</summary>
    internal static readonly JavaMethod RevertToFrozen = JavaMethod.Static(owner: "net/minecraftforge/registries/GameData", name: "revertToFrozen", descriptor: "()V");

    /// <summary>Forge's injection of a server's registry data, which its client handshake calls when the server sends it.</summary>
    internal static readonly JavaMethod InjectSnapshot = JavaMethod.Static(
        owner: "net/minecraftforge/registries/GameData",
        name: "injectSnapshot",
        descriptor: "(Ljava/util/Map;ZZ)Lcom/google/common/collect/Multimap;"
    );

    /// <summary>
    /// Points the <c>GameData.revertToFrozen()</c> call of FML's <c>DONE</c> handshake state at <c>legacyforgepatcher.RegistrySnapshots</c>, which schedules it on the client
    /// thread. Forge for Minecraft 1.12 and 1.12.1 reverts the registries on the network thread when a proxy resets the handshake to move the player to another server,
    /// while the client thread ticks the world and the player, which read them, and the game can crash. Forge for 1.12.2 schedules it itself, so its class is skipped.
    /// </summary>
    internal static readonly ClassPatch RegistryRevert = ClassPatch.For(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7")
        .Method(name: "accept")
        .RedirectInvocation(RevertToFrozen, RegistrySnapshots.Name);

    /// <summary>
    /// Points the <c>GameData.injectSnapshot</c> call of FML's <c>WAITINGSERVERCOMPLETE</c> handshake state at <c>legacyforgepatcher.RegistrySnapshots</c>, which runs it on
    /// the client thread and returns its result, for the same reason as <see cref="RegistryRevert"/>. The client thread runs scheduled tasks in order, so a revert still
    /// comes before the injection that follows it.
    /// </summary>
    internal static readonly ClassPatch RegistryInjection = ClassPatch.For(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4")
        .Method(name: "accept")
        .RedirectInvocation(InjectSnapshot, RegistrySnapshots.Name);

    /// <summary>
    /// Points the <c>send</c> call in Minecraft's <c>ClientPacketListener.handleChunkBatchFinished</c> at <c>legacyforgepatcher.ClientPackets</c>, which sends from the client
    /// thread. The network thread answers a chunk batch at once, and from Minecraft 1.20.5 an answer that lands between the client thread sending the end of configuration
    /// and switching outgoing packets to play meets a pipeline with no outgoing protocol, and the client drops its connection. It applies where the game runs with
    /// Mojang's names: Forge from 1.20.6, NeoForge and Minecraft from 26.1.
    /// </summary>
    internal static readonly ClassPatch ChunkBatchAnswer = ClassPatch.For(className: "net/minecraft/client/multiplayer/ClientPacketListener")
        .Method(name: "handleChunkBatchFinished", descriptor: "(Lnet/minecraft/network/protocol/game/ClientboundChunkBatchFinishedPacket;)V")
        .RedirectInvocation(
            JavaMethod.Virtual(
                owner: "net/minecraft/client/multiplayer/ClientPacketListener",
                name: "send",
                descriptor: "(Lnet/minecraft/network/protocol/Packet;)V"
            ),
            // The helper takes Object parameters because the bootstrap loader that defines it can't see Minecraft's classes.
            JavaMethod.Static(owner: "legacyforgepatcher/ClientPackets", name: "send", descriptor: "(Ljava/lang/Object;Ljava/lang/Object;)V")
        );

    /// <inheritdoc/>
    public override void Configure(AgentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The bootstrap loader makes the helpers visible to whichever loader defines FML.
        context.DefineClass(JavaClasses.Legacyforgepatcher.Collections, ClassLoaderTarget.Bootstrap);
        context.DefineClass(JavaClasses.Legacyforgepatcher.ConnectionStates, ClassLoaderTarget.Bootstrap);
        context.DefineClass(JavaClasses.Legacyforgepatcher.ClientThread, ClassLoaderTarget.Bootstrap);
        context.DefineClass(JavaClasses.Legacyforgepatcher.ModLoaderConfigs, ClassLoaderTarget.Bootstrap);
        context.DefineClass(JavaClasses.Legacyforgepatcher.ClientPackets, ClassLoaderTarget.Bootstrap);
        context.DefineClass(JavaClasses.Legacyforgepatcher.EventListeners, ClassLoaderTarget.Bootstrap);
        context.DefineClass(JavaClasses.Legacyforgepatcher.Connections, ClassLoaderTarget.Bootstrap);
        // EventBus calls this one by the descriptor of its own ListenerList, which only its loader can resolve.
        context.DefineClass(EventListenerLists.Definition, ClassLoaderTarget.PatchedClassLoader);
        // Forge's handshake calls this one by the descriptor of a Guava type, which only Forge's loader can resolve.
        context.DefineClass(RegistrySnapshots.Definition, ClassLoaderTarget.PatchedClassLoader);
        context.Patch(TweakSort);
        context.Patch(HandshakeState);
        context.Patch(RegistryRevert);
        context.Patch(RegistryInjection);

        foreach (ClassPatch patch in HandshakeSentMessages.Patches)
            context.Patch(patch);

        foreach (ClassPatch patch in ModLoaderConfigurationLoad.Patches)
            context.Patch(patch);

        context.Patch(ObjectHolderHandlers.Patch);
        context.Patch(ChunkBatchAnswer);
        context.Patch(EventListenerLists.Patch);
        context.Patch(ModLoaderConfigurationWatch.Patch);
        context.Patch(PendingConnections.Patch);
    }
}
