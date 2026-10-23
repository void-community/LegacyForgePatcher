using JvmBridge.Agents;

namespace LegacyForgePatcher;

/// <summary>
/// Makes the list of login messages that Forge's handshake handler waits on a <c>CopyOnWriteArrayList</c>. The server thread adds to it while the network thread
/// removes the messages the client answers with <c>removeIf</c>, which throws <c>ConcurrentModificationException</c> on an <c>ArrayList</c> that changes meanwhile,
/// and the connection closes with it. Forge for Minecraft 1.13 to 1.20.1, and NeoForge for 1.20.1, create the list in the handler's constructor.
/// </summary>
internal static class HandshakeSentMessages
{
    /// <summary>Gets the patches, one for each name the handler has had: in <c>fml.network</c> up to 1.16, in <c>fmllegacy.network</c> in 1.17 and in <c>network</c> from 1.18.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        .. new[] { "net/minecraftforge/fml/network/FMLHandshakeHandler", "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler", "net/minecraftforge/network/HandshakeHandler" }.Select(
            // Every other ArrayList the constructor creates stays one.
            static className => ClassPatch.For(className)
                .Method(name: "<init>")
                .ReplaceConstruction(fromClass: "java/util/ArrayList", toClass: "java/util/concurrent/CopyOnWriteArrayList", storedIn: "sentMessages")
        ),
    ];
}
