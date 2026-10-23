using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Making the list of Forge's handshake messages that wait for a reply a CopyOnWriteArrayList, in the handler's constructor.</summary>
public sealed class SentMessagesPatchTests
{
    private const string ArrayList = "java/util/ArrayList";
    private const string Constructor = "<init>";
    private const string CopyOnWriteArrayList = "java/util/concurrent/CopyOnWriteArrayList";
    private const string ListDescriptor = "Ljava/util/List;";

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

        Assert.Equal(expected: 1, result.ReplacedConstructions);
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
        Assert.Contains("no construction of " + ArrayList, result.SkippedReason, StringComparison.Ordinal);
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        string className = JavaClassFile.Read(classFile).Name;

        return HandshakeSentMessages.Patches.Single(patch => patch.ClassName == className).Apply(classFile);
    }

    // A constructor that stores a new ArrayList in the given field and then another one in packetBacklog, as Forge's handler from 1.18 does.
    private static byte[] CreateClassFile(string className, string firstField)
    {
        CodeBuilder StoreNewList(CodeBuilder code, string field)
        {
            return code.ALoad(slot: 0).New(ArrayList).Dup().InvokeSpecial(ArrayList, Constructor, descriptor: "()V").PutField(className, field, ListDescriptor);
        }

        return new ClassFileWriter(className)
            .AddMethod(
                Constructor,
                descriptor: "()V",
                code => StoreNewList(StoreNewList(code, firstField), field: "packetBacklog").Return(),
                accessFlags: 0x0001
            )
            .ToArray();
    }

    private static List<string> CreatedClasses(byte[] classFile)
    {
        JavaClassFile model = JavaClassFile.Read(classFile);
        CodeAttribute code = model.FindMethod(Constructor)?.Code ?? throw new InvalidOperationException(message: "The class has no constructor.");

        return [.. code.Instructions.Where(static instruction => instruction.Opcode == Opcodes.New).Select(instruction => model.GetClassName(instruction.ConstantIndex ?? 0))];
    }
}
