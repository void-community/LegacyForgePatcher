using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// The class Forge 1.12's client handshake calls instead of GameData. Each method has the descriptor of the call it replaces and hands it to the bootstrap helper
/// <c>legacyforgepatcher.ClientThread</c>, passing this class, which is defined in Forge's loader, so the helper can reach Forge and Minecraft through it. It is written
/// here instead of in Java because <c>injectSnapshot</c> returns Guava's Multimap, which javac would need to compile against.
/// </summary>
internal static class RegistrySnapshots
{
    /// <summary>The internal name of the class.</summary>
    internal const string Name = "legacyforgepatcher/RegistrySnapshots";

    private const byte Aload0 = 0x2a;
    private const byte Areturn = 0xb0;
    private const byte Checkcast = 0xc0;
    private const string ClientThread = "legacyforgepatcher/ClientThread";
    private const byte Iload1 = 0x1b;
    private const byte Iload2 = 0x1c;
    private const byte InvokeStatic = 0xb8;
    private const byte LdcWide = 0x13;
    private const byte Return = 0xb1;

    /// <summary>Gets the class, made of straight-line static methods that need no stack map frames.</summary>
    internal static JavaClassDefinition Definition { get; } = JavaClassDefinition.FromBytes(Create());

    private static byte[] Create()
    {
        ClassFileWriter writer = new(Name);
        int self = writer.AddClass(Name);
        int multimap = writer.AddClass(name: "com/google/common/collect/Multimap");
        int revertToFrozen = writer.AddMethodReference(ClientThread, name: "revertToFrozen", descriptor: "(Ljava/lang/Class;)V");
        int injectSnapshot = writer.AddMethodReference(ClientThread, name: "injectSnapshot", descriptor: "(Ljava/lang/Class;Ljava/util/Map;ZZ)Ljava/lang/Object;");

        // ClientThread.revertToFrozen(RegistrySnapshots.class)
        writer = writer.AddMethod(
            PatcherAgent.RevertToFrozen.Name,
            PatcherAgent.RevertToFrozen.Descriptor,
            maxStack: 1,
            maxLocals: 0,
            [LdcWide, .. Index(self), InvokeStatic, .. Index(revertToFrozen), Return]
        );

        // return (Multimap) ClientThread.injectSnapshot(RegistrySnapshots.class, snapshot, injectFrozenData, isLocalWorld)
        writer = writer.AddMethod(
            PatcherAgent.InjectSnapshot.Name,
            PatcherAgent.InjectSnapshot.Descriptor,
            maxStack: 4,
            maxLocals: 3,
            [LdcWide, .. Index(self), Aload0, Iload1, Iload2, InvokeStatic, .. Index(injectSnapshot), Checkcast, .. Index(multimap), Areturn]
        );

        return writer.ToArray();
    }

    private static byte[] Index(int index)
    {
        return [checked((byte)(index >> 8)), checked((byte)(index & 0xff))];
    }
}
