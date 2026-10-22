using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Redirecting Minecraft's answer to a chunk batch to the helper that sends it from the client thread.</summary>
public sealed class ChunkBatchAnswerPatchTests
{
    private const byte AconstNull = 0x01;
    private const string ClassName = "net/minecraft/client/multiplayer/ClientPacketListener";
    private const string HandleChunkBatchFinished = "handleChunkBatchFinished";
    private const string HandlerDescriptor = "(Lnet/minecraft/network/protocol/game/ClientboundChunkBatchFinishedPacket;)V";
    private const string Helper = "legacyforgepatcher/ClientPackets";
    private const string HelperDescriptor = "(Ljava/lang/Object;Ljava/lang/Object;)V";
    private const byte InvokeVirtual = 0xb6;
    private const byte Return = 0xb1;
    private const string Send = "send";
    private const string SendDescriptor = "(Lnet/minecraft/network/protocol/Packet;)V";

    /// <summary>The compiled helper declares the static method the patch calls, in a class that Java 8 loads.</summary>
    [Fact]
    public void HelperDeclaresTheMethodThePatchCalls()
    {
        JavaClassFile helper = JavaClassFile.Read(JavaClasses.Legacyforgepatcher.ClientPackets.Bytes.Span);

        Assert.Equal(Helper, helper.Name);
        Assert.Equal(expected: 52, helper.MajorVersion);
        Assert.NotNull(helper.FindMethod(Send, HelperDescriptor));
    }

    /// <summary>A listener without the handler, as in Minecraft before chunk batches, is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesListenersWithoutTheHandler()
    {
        ClassPatchResult result = PatcherAgent.ChunkBatchAnswer.Apply(CreateClassFile((Name: "handlePing", Descriptor: "(Ljava/lang/Object;)V")));

        Assert.False(result.Changed);
        Assert.Contains(HandleChunkBatchFinished, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The answer in <c>handleChunkBatchFinished</c> goes to the helper, with the listener and the packet as its arguments, and the one in another handler stays.</summary>
    [Fact]
    public void PatchRedirectsTheAnswerInHandleChunkBatchFinished()
    {
        ClassPatchResult result = PatcherAgent.ChunkBatchAnswer.Apply(CreateClassFile((HandleChunkBatchFinished, HandlerDescriptor), (Name: "handlePing", Descriptor: "(Ljava/lang/Object;)V")));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, HandleChunkBatchFinished, Helper, Send, HelperDescriptor);
        ClassFileAssert.DoesNotCall(patched, HandleChunkBatchFinished, ClassName, Send, SendDescriptor);
        ClassFileAssert.Calls(patched, method: "handlePing", ClassName, Send, SendDescriptor);
    }

    // A class file whose methods each send a null packet on a null listener.
    private static byte[] CreateClassFile(params (string Name, string Descriptor)[] methods)
    {
        ClassFileWriter writer = new(ClassName);
        int send = writer.AddMethodReference(ClassName, Send, SendDescriptor);

        foreach ((string name, string descriptor) in methods)
        {
            writer = writer.AddMethod(
                name,
                descriptor,
                maxStack: 2,
                maxLocals: 1,
                [AconstNull, AconstNull, InvokeVirtual, checked((byte)(send >> 8)), checked((byte)(send & 0xff)), Return]
            );
        }

        return writer.ToArray();
    }
}
