using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Redirecting FML's switch to the play state in NetworkDispatcher class files.</summary>
public sealed class HandshakeStatePatchTests
{
    private const string ClassName = "net/minecraftforge/fml/common/network/handshake/NetworkDispatcher";
    private const string HelperDescriptor = "(Lnet/minecraft/network/NetworkManager;Lnet/minecraft/network/EnumConnectionState;)V";
    private const string HelperName = "setConnectionState";
    private const string ServerInitiateHandshake = "serverInitiateHandshake";

    /// <summary>A NetworkDispatcher whose <c>serverInitiateHandshake</c> doesn't switch the state, as in Forge versions that do it elsewhere, is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesClassesWhoseHandshakeDoesNotSwitch()
    {
        ClassPatchResult result = PatcherAgent.HandshakeState.Apply(CreateClassFile((ServerInitiateHandshake, Switches: 0)));

        Assert.False(result.Changed);
        Assert.Null(result.Bytes);
        Assert.Contains("no invocation of " + PatcherAgent.SetConnectionState.Owner, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>A NetworkDispatcher without <c>serverInitiateHandshake</c>, as in Forge versions that start the handshake differently, is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesClassesWithoutServerInitiateHandshake()
    {
        ClassPatchResult result = PatcherAgent.HandshakeState.Apply(CreateClassFile((Name: "clientListenForServerHandshake", Switches: 1)));

        Assert.False(result.Changed);
        Assert.Contains(ServerInitiateHandshake, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>NetworkDispatcher switches the state in another method too, on the client side, and that switch stays as it was.</summary>
    [Fact]
    public void PatchLeavesTheSwitchInOtherMethodsAlone()
    {
        byte[] patched = PatcherAgent.HandshakeState.Apply(CreateClassFile((ServerInitiateHandshake, Switches: 1), (Name: "clientListenForServerHandshake", Switches: 1))).Bytes
            ?? throw new InvalidOperationException(message: "The class file was left unchanged.");

        ClassFileAssert.Calls(
            patched,
            method: "clientListenForServerHandshake",
            PatcherAgent.SetConnectionState.Owner,
            PatcherAgent.SetConnectionState.Name,
            PatcherAgent.SetConnectionState.Descriptor
        );
    }

    /// <summary>The switch in <c>serverInitiateHandshake</c> now goes to the helper, with the manager and the state as its arguments, and the result says it was the only call redirected.</summary>
    [Fact]
    public void PatchRedirectsTheSwitchInServerInitiateHandshake()
    {
        ClassPatchResult result = PatcherAgent.HandshakeState.Apply(CreateClassFile((ServerInitiateHandshake, Switches: 1)));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, ServerInitiateHandshake, JavaClasses.Legacyforgepatcher.ConnectionStates.Name, HelperName, HelperDescriptor);
        ClassFileAssert.DoesNotCall(
            patched,
            ServerInitiateHandshake,
            PatcherAgent.SetConnectionState.Owner,
            PatcherAgent.SetConnectionState.Name,
            PatcherAgent.SetConnectionState.Descriptor
        );
    }

    // A class file with straight-line methods returning an int, each of which switches the connection state the given number of times.
    private static byte[] CreateClassFile(params (string Name, int Switches)[] methods)
    {
        ClassFileWriter writer = new(ClassName);

        foreach ((string name, int switches) in methods)
        {
            writer = writer.AddMethod(
                name,
                descriptor: "()I",
                code =>
                {
                    for (int index = 0; index < switches; index++)
                        code = code.AConstNull().AConstNull().Invoke(PatcherAgent.SetConnectionState);

                    return code.IConst(value: 0).IReturn();
                }
            );
        }

        return writer.ToArray();
    }
}
