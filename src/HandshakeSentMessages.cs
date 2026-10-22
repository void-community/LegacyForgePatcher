using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Makes the list of login messages that Forge's handshake handler waits on a <c>CopyOnWriteArrayList</c>. The server thread adds to it while the network thread
/// removes the messages the client answers with <c>removeIf</c>, which throws <c>ConcurrentModificationException</c> on an <c>ArrayList</c> that changes meanwhile,
/// and the connection closes with it. Forge for Minecraft 1.13 to 1.20.1, and NeoForge for 1.20.1, create the list in the handler's constructor.
/// </summary>
internal static class HandshakeSentMessages
{
    private const string ArrayList = "java/util/ArrayList";
    private const string Constructor = "<init>";
    private const string CopyOnWriteArrayList = "java/util/concurrent/CopyOnWriteArrayList";
    private const byte Dup = 0x59;
    private const string Field = "sentMessages";
    private const string FieldDescriptor = "Ljava/util/List;";
    private const byte InvokeSpecial = 0xb7;
    private const byte New = 0xbb;
    private const byte PutField = 0xb5;

    /// <summary>Gets the patches, one for each name the handler has had: in <c>fml.network</c> up to 1.16, in <c>fmllegacy.network</c> in 1.17 and in <c>network</c> from 1.18.</summary>
    internal static IReadOnlyList<ClassPatch> Patches { get; } =
    [
        .. new[] { "net/minecraftforge/fml/network/FMLHandshakeHandler", "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler", "net/minecraftforge/network/HandshakeHandler" }.Select(
            static className => ClassPatch.For(className).Method(Constructor).Edit(description: "sentMessages created as a CopyOnWriteArrayList", UseCopyOnWriteArrayList)
        ),
    ];

    private static IEnumerable<Site> FindSites(JavaClassFile model, ClassMember method)
    {
        if (method.Code is not { } code)
            yield break;

        IReadOnlyList<Instruction> instructions = code.Instructions;

        for (int index = 0; index + 3 < instructions.Count; index++)
        {
            Instruction create = instructions[index];
            Instruction construct = instructions[index + 2];
            Instruction store = instructions[index + 3];

            bool creates = create is { Opcode: New, ConstantIndex: { } created } && model.GetClassName(created) == ArrayList;
            bool constructs = construct is { Opcode: InvokeSpecial, ConstantIndex: { } called } && model.GetMemberReference(called) is { Owner: ArrayList, Name: Constructor, Descriptor: "()V" };
            bool stores = store is { Opcode: PutField, ConstantIndex: { } field } && model.GetMemberReference(field) is { Name: Field, Descriptor: FieldDescriptor } reference && reference.Owner == model.Name;

            if (creates && instructions[index + 1].Opcode == Dup && constructs && stores)
                yield return new Site(method.Descriptor, create.Offset, construct.Offset);
        }
    }

    // Rewrites `new ArrayList; dup; invokespecial ArrayList.<init>()V; putfield this.sentMessages:List` to create a CopyOnWriteArrayList, at the same length. The
    // object goes straight into a field of type List, so no stack map frame names its type. Every other ArrayList the constructor creates stays one.
    private static int UseCopyOnWriteArrayList(ClassEditContext context)
    {
        ClassFileEditor editor = context.Editor;
        JavaClassFile model = editor.Model;
        List<Site> sites = [.. model.Methods.Where(static method => method.Name == Constructor).SelectMany(method => FindSites(model, method))];

        if (sites.Count == 0)
            return 0;

        int copyOnWriteArrayList = editor.AddClass(CopyOnWriteArrayList);
        int copyOnWriteConstructor = editor.AddMethodReference(CopyOnWriteArrayList, Constructor, descriptor: "()V");

        foreach (Site site in sites)
        {
            editor.RewriteConstantOperand(Constructor, site.Descriptor, site.NewOffset, copyOnWriteArrayList);
            editor.RewriteConstantOperand(Constructor, site.Descriptor, site.ConstructorOffset, copyOnWriteConstructor);
        }

        return sites.Count;
    }

    private sealed record Site(string Descriptor, int NewOffset, int ConstructorOffset);
}
