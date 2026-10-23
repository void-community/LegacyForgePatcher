using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace ModLoaderFixes.Tests;

/// <summary>Redirecting Minecraft's answer to a chunk batch to the helper that sends it from the client thread.</summary>
public sealed class ChunkBatchAnswerPatchTests
{
    private const string ClassName = "net/minecraft/client/multiplayer/ClientPacketListener";
    private const string HandleChunkBatchFinished = "handleChunkBatchFinished";
    private const string HandlerDescriptor = "(Lnet/minecraft/network/protocol/game/ClientboundChunkBatchFinishedPacket;)V";
    private const string Helper = "modloaderfixes/ClientPackets";
    private const string HelperDescriptor = "(Lnet/minecraft/client/multiplayer/ClientPacketListener;Lnet/minecraft/network/protocol/Packet;)V";
    private const string Send = "send";
    private const string SendDescriptor = "(Lnet/minecraft/network/protocol/Packet;)V";

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
        JavaMethod send = JavaMethod.Virtual(ClassName, Send, SendDescriptor);
        ClassFileWriter writer = new(ClassName);

        foreach ((string name, string descriptor) in methods)
            writer = writer.AddMethod(name, descriptor, code => code.AConstNull().AConstNull().Invoke(send).Return());

        return writer.ToArray();
    }
}
