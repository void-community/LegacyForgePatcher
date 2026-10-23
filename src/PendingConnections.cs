using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Lets the network tick of Minecraft 1.7 leave a connection alone until it has a channel, as Minecraft 1.8 does. <c>NetworkSystem</c>'s channel initializer adds
/// each new <c>NetworkManager</c> to the list that the server thread ticks, and only <c>channelActive</c> gives it its channel afterwards. A tick in between takes the
/// manager for a closed connection: it drops it from the list and tells its handshake handler, which does nothing, so the login that the network thread goes on to
/// start is never answered, never times out and never notices the client leave. Forge for 1.7 runs Minecraft with FML's names, which these are.
/// </summary>
internal static class PendingConnections
{
    private const string Helper = "legacyforgepatcher/Connections";
    private const string Manager = "net/minecraft/network/NetworkManager";

    /// <summary>
    /// Gets the patch of <c>NetworkSystem.networkTick</c>: its check whether a manager's channel is open, and its processing of the manager's packets, go to
    /// <c>legacyforgepatcher.Connections</c>, which counts a manager without a channel as open and skips its processing until it has one. Reporting it open alone
    /// would not do, because the processing reads the channel at once.
    /// </summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(className: "net/minecraft/network/NetworkSystem")
        .Method(name: "func_151269_c", descriptor: "()V")
        .RedirectInvocation(JavaMethod.Virtual(Manager, name: "func_150724_d", descriptor: "()Z"), Helper, toName: "isOpenOrPending")
        .RedirectInvocation(JavaMethod.Virtual(Manager, name: "func_74428_b", descriptor: "()V"), Helper, toName: "processIfActive");
}
