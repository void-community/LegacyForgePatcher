using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Making the list of Forge's handshake messages that wait for a reply a CopyOnWriteArrayList, in the handler's constructor.</summary>
public sealed class SentMessagesPatchTests
{
    private const byte Aload0 = 0x2a;
    private const string ArrayList = "java/util/ArrayList";
    private const string Constructor = "<init>";
    private const string CopyOnWriteArrayList = "java/util/concurrent/CopyOnWriteArrayList";
    private const byte Dup = 0x59;
    private const byte InvokeSpecial = 0xb7;
    private const string ListDescriptor = "Ljava/util/List;";
    private const byte New = 0xbb;
    private const byte PutField = 0xb5;
    private const byte Return = 0xb1;

    /// <summary>The handler's names in Forge 1.13 to 1.16, in 1.17 and from 1.18.</summary>
    public static TheoryData<string> HandlerNames { get; } =
    [
        "net/minecraftforge/fml/network/FMLHandshakeHandler",
        "net/minecraftforge/fmllegacy/network/FMLHandshakeHandler",
        "net/minecraftforge/network/HandshakeHandler",
    ];

    /// <summary>The list stored in <c>sentMessages</c> becomes a CopyOnWriteArrayList, and the other list the constructor creates stays an ArrayList.</summary>
    /// <param name="className">The handler's name.</param>
    [Theory]
    [MemberData(nameof(HandlerNames))]
    public void PatchCreatesSentMessagesAsACopyOnWriteArrayList(string className)
    {
        ClassPatchResult result = Apply(CreateClassFile(className, firstField: "sentMessages"));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.AppliedEdits);
        Assert.Equal([CopyOnWriteArrayList, ArrayList], CreatedClasses(patched));
        ClassFileAssert.Calls(patched, Constructor, CopyOnWriteArrayList, Constructor, descriptor: "()V");
        ClassFileAssert.Calls(patched, Constructor, ArrayList, Constructor, descriptor: "()V");
    }

    /// <summary>A constructor that creates its lists for other fields isn't the handler this fix was written for, so the class is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesConstructorsThatDontCreateSentMessages()
    {
        ClassPatchResult result = Apply(CreateClassFile(className: "net/minecraftforge/network/HandshakeHandler", firstField: "pendingMessages"));

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "nothing to change in <init>", result.SkippedReason, StringComparison.Ordinal);
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        string className = JavaClassFile.Read(classFile).Name;

        return HandshakeSentMessages.Patches.Single(patch => patch.ClassName == className).Apply(classFile);
    }

    // A constructor that stores a new ArrayList in the given field and then another one in packetBacklog, as Forge's handler from 1.18 does.
    private static byte[] CreateClassFile(string className, string firstField)
    {
        ClassFileWriter writer = new(className);
        int arrayList = writer.AddClass(ArrayList);
        int constructor = writer.AddMethodReference(ArrayList, Constructor, descriptor: "()V");
        byte[] first = StoreNewList(arrayList, constructor, writer.AddFieldReference(className, firstField, ListDescriptor));
        byte[] second = StoreNewList(arrayList, constructor, writer.AddFieldReference(className, name: "packetBacklog", ListDescriptor));

        writer = writer.AddMethod(Constructor, descriptor: "()V", maxStack: 3, maxLocals: 1, [.. first, .. second, Return], accessFlags: 0x0001);

        return writer.ToArray();
    }

    private static List<string> CreatedClasses(byte[] classFile)
    {
        JavaClassFile model = JavaClassFile.Read(classFile);
        CodeAttribute code = model.FindMethod(Constructor)?.Code ?? throw new InvalidOperationException(message: "The class has no constructor.");

        return [.. code.Instructions.Where(static instruction => instruction.Opcode == New).Select(instruction => model.GetClassName(instruction.ConstantIndex ?? 0))];
    }

    private static byte[] Index(int index)
    {
        return [checked((byte)(index >> 8)), checked((byte)(index & 0xff))];
    }

    private static byte[] StoreNewList(int listClass, int listConstructor, int field)
    {
        return [Aload0, New, .. Index(listClass), Dup, InvokeSpecial, .. Index(listConstructor), PutField, .. Index(field)];
    }
}
