using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Redirecting Minecraft 1.7's network tick to the helper that leaves a connection without a channel for a later tick.</summary>
public sealed class PendingConnectionsPatchTests
{
    private const byte AconstNull = 0x01;
    private const string ClassName = "net/minecraft/network/NetworkSystem";
    private const string Helper = "legacyforgepatcher/Connections";
    private const byte IfEq = 0x99;
    private const byte InvokeVirtual = 0xb6;
    private const string IsChannelOpen = "func_150724_d";
    private const string Manager = "net/minecraft/network/NetworkManager";
    private const string NetworkTick = "func_151269_c";
    private const string ProcessReceivedPackets = "func_74428_b";
    private const byte Return = 0xb1;

    /// <summary>The compiled helper declares both static methods the patch calls, in a class that Java 8 loads.</summary>
    [Fact]
    public void HelperDeclaresTheMethodsThePatchCalls()
    {
        JavaClassFile helper = JavaClassFile.Read(JavaClasses.Legacyforgepatcher.Connections.Bytes.Span);

        Assert.Equal(Helper, helper.Name);
        Assert.Equal(expected: 52, helper.MajorVersion);
        Assert.NotNull(helper.FindMethod(name: "isOpenOrPending", descriptor: "(Ljava/lang/Object;)Z"));
        Assert.NotNull(helper.FindMethod(name: "processIfActive", descriptor: "(Ljava/lang/Object;)V"));
    }

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
        ClassFileAssert.Calls(patched, NetworkTick, Helper, name: "isOpenOrPending", descriptor: "(Ljava/lang/Object;)Z");
        ClassFileAssert.Calls(patched, NetworkTick, Helper, name: "processIfActive", descriptor: "(Ljava/lang/Object;)V");
        ClassFileAssert.DoesNotCall(patched, NetworkTick, Manager, IsChannelOpen, descriptor: "()Z");
        ClassFileAssert.DoesNotCall(patched, NetworkTick, Manager, ProcessReceivedPackets, descriptor: "()V");
    }

    // A NetworkSystem whose method checks a null manager's channel and processes its packets when it is open, as the tick does for each manager.
    private static byte[] CreateClassFile(string method)
    {
        ClassFileWriter writer = new(ClassName);
        int isChannelOpen = writer.AddMethodReference(Manager, IsChannelOpen, descriptor: "()Z");
        int processReceivedPackets = writer.AddMethodReference(Manager, ProcessReceivedPackets, descriptor: "()V");

        // 0: aconst_null; 1: invokevirtual isChannelOpen; 4: ifeq +7 (to 11); 7: aconst_null; 8: invokevirtual processReceivedPackets; 11: return
        byte[] code =
        [
            AconstNull,
            InvokeVirtual,
            checked((byte)(isChannelOpen >> 8)),
            checked((byte)(isChannelOpen & 0xff)),
            IfEq,
            0x00,
            0x07,
            AconstNull,
            InvokeVirtual,
            checked((byte)(processReceivedPackets >> 8)),
            checked((byte)(processReceivedPackets & 0xff)),
            Return,
        ];

        return writer.AddMethod(method, descriptor: "()V", maxStack: 1, maxLocals: 1, code).ToArray();
    }
}
