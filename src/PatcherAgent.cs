using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>Repairs legacy Forge where its own code breaks: the tweaker sort that stops Forge for Minecraft 1.7.2 on Java 8u20 and later, and the connection state switch that drops players at login on Forge for Minecraft 1.12.</summary>
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

    /// <inheritdoc/>
    public override void Configure(AgentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The bootstrap loader makes the helpers visible to whichever loader defines FML.
        context.DefineClass(JavaClasses.Legacyforgepatcher.Collections, ClassLoaderTarget.Bootstrap);
        context.DefineClass(JavaClasses.Legacyforgepatcher.ConnectionStates, ClassLoaderTarget.Bootstrap);
        context.Patch(TweakSort);
        context.Patch(HandshakeState);
    }
}
