using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Redirecting Minecraft 1.7's network tick to the helper that leaves a connection without a channel for a later tick.</summary>
public sealed class PendingConnectionsPatchTests
{
    private const string ClassName = "net/minecraft/network/NetworkSystem";
    private const string Helper = "legacyforgepatcher/Connections";
    private const string IsChannelOpen = "func_150724_d";
    private const string Manager = "net/minecraft/network/NetworkManager";
    private const string NetworkTick = "func_151269_c";
    private const string ProcessReceivedPackets = "func_74428_b";

    /// <summary>A NetworkSystem without the tick, as in Minecraft with other names, is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesSystemsWithoutTheTick()
    {
        ClassPatchResult result = PendingConnections.Patch.Apply(CreateClassFile(method: "networkTick"));

        Assert.False(result.Changed);
        Assert.Contains(NetworkTick, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The tick's check of the channel and its processing of packets both go to the helper, with the manager as their argument.</summary>
    [Fact]
    public void PatchRedirectsTheCheckAndTheProcessing()
    {
        ClassPatchResult result = PendingConnections.Patch.Apply(CreateClassFile(NetworkTick));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 2, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, NetworkTick, Helper, name: "isOpenOrPending", descriptor: "(Lnet/minecraft/network/NetworkManager;)Z");
        ClassFileAssert.Calls(patched, NetworkTick, Helper, name: "processIfActive", descriptor: "(Lnet/minecraft/network/NetworkManager;)V");
        ClassFileAssert.DoesNotCall(patched, NetworkTick, Manager, IsChannelOpen, descriptor: "()Z");
        ClassFileAssert.DoesNotCall(patched, NetworkTick, Manager, ProcessReceivedPackets, descriptor: "()V");
    }

    // A NetworkSystem whose method checks a null manager's channel and processes its packets when it is open, as the tick does for each manager.
    private static byte[] CreateClassFile(string method)
    {
        JavaMethod isChannelOpen = JavaMethod.Virtual(Manager, IsChannelOpen, descriptor: "()Z");
        JavaMethod processReceivedPackets = JavaMethod.Virtual(Manager, ProcessReceivedPackets, descriptor: "()V");

        // The branch needs a class of version 50, which the JVM verifies without stack map frames.
        return new ClassFileWriter(ClassName, majorVersion: 50)
            .AddMethod(
                method,
                descriptor: "()V",
                code =>
                {
                    CodeLabel done = code.NewLabel();

                    return code.AConstNull().Invoke(isChannelOpen).If(Opcodes.IfEq, done).AConstNull().Invoke(processReceivedPackets).Mark(done).Return();
                }
            )
            .ToArray();
    }
}
