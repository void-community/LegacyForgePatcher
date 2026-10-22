using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Redirecting the registry rebuilds of FML's client handshake states to the class that runs them on the client thread.</summary>
public sealed class RegistryThreadPatchTests
{
    private const string Accept = "accept";
    private const byte AconstNull = 0x01;
    private const byte Areturn = 0xb0;
    private const string ClientThread = "legacyforgepatcher/ClientThread";
    private const string ClientThreadInjectDescriptor = "(Ljava/lang/Class;Ljava/util/Map;ZZ)Ljava/lang/Object;";
    private const string ClientThreadRevertDescriptor = "(Ljava/lang/Class;)V";
    private const byte IconstZero = 0x03;
    private const byte InvokeStatic = 0xb8;
    private const string Lambda = "lambda$accept$0";
    private const byte Return = 0xb1;

    /// <summary>The Java helper declares the methods the generated class calls, in a class that Java 8 loads.</summary>
    [Fact]
    public void ClientThreadDeclaresTheMethodsTheGeneratedClassCalls()
    {
        JavaClassFile helper = JavaClassFile.Read(JavaClasses.Legacyforgepatcher.ClientThread.Bytes.Span);

        Assert.Equal(ClientThread, helper.Name);
        Assert.Equal(expected: 52, helper.MajorVersion);
        Assert.NotNull(helper.FindMethod(PatcherAgent.RevertToFrozen.Name, ClientThreadRevertDescriptor));
        Assert.NotNull(helper.FindMethod(PatcherAgent.InjectSnapshot.Name, ClientThreadInjectDescriptor));
    }

    /// <summary>Forge for Minecraft 1.12.2 already runs the injection from a lambda on the client thread, so a WAITINGSERVERCOMPLETE that only calls it there is left alone.</summary>
    [Fact]
    public void InjectionPatchLeavesAnInjectionThatForgeAlreadyScheduled()
    {
        ClassPatchResult result = PatcherAgent.RegistryInjection.Apply(
            CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4", PatcherAgent.InjectSnapshot, Lambda)
        );

        Assert.False(result.Changed);
        Assert.Contains("no invocation of " + PatcherAgent.InjectSnapshot.Owner, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The injection in WAITINGSERVERCOMPLETE's <c>accept</c> now goes to the generated class, which returns its result.</summary>
    [Fact]
    public void InjectionPatchRedirectsTheInjectionInAccept()
    {
        ClassPatchResult result = PatcherAgent.RegistryInjection.Apply(
            CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4", PatcherAgent.InjectSnapshot, Accept)
        );

        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, Accept, RegistrySnapshots.Name, PatcherAgent.InjectSnapshot.Name, PatcherAgent.InjectSnapshot.Descriptor);
        ClassFileAssert.DoesNotCall(patched, Accept, PatcherAgent.InjectSnapshot.Owner, PatcherAgent.InjectSnapshot.Name, PatcherAgent.InjectSnapshot.Descriptor);
    }

    /// <summary>The generated class has GameData's methods with GameData's descriptors, and hands each call to the helper.</summary>
    [Fact]
    public void RegistrySnapshotsHandsGameDataCallsToTheHelper()
    {
        byte[] bytes = RegistrySnapshots.Definition.Bytes.ToArray();
        JavaClassFile generated = JavaClassFile.Read(bytes);

        Assert.Equal(RegistrySnapshots.Name, generated.Name);
        Assert.Equal(expected: 52, generated.MajorVersion);
        Assert.NotNull(generated.FindMethod(PatcherAgent.RevertToFrozen.Name, PatcherAgent.RevertToFrozen.Descriptor));
        Assert.NotNull(generated.FindMethod(PatcherAgent.InjectSnapshot.Name, PatcherAgent.InjectSnapshot.Descriptor));
        ClassFileAssert.Calls(bytes, PatcherAgent.RevertToFrozen.Name, ClientThread, PatcherAgent.RevertToFrozen.Name, ClientThreadRevertDescriptor);
        ClassFileAssert.Calls(bytes, PatcherAgent.InjectSnapshot.Name, ClientThread, PatcherAgent.InjectSnapshot.Name, ClientThreadInjectDescriptor);
    }

    /// <summary>Forge for Minecraft 1.12.2 already runs the revert from a lambda on the client thread, so a DONE that only calls it there is left alone.</summary>
    [Fact]
    public void RevertPatchLeavesARevertThatForgeAlreadyScheduled()
    {
        ClassPatchResult result = PatcherAgent.RegistryRevert.Apply(
            CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7", PatcherAgent.RevertToFrozen, Lambda)
        );

        Assert.False(result.Changed);
        Assert.Contains("no invocation of " + PatcherAgent.RevertToFrozen.Owner, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>The revert in DONE's <c>accept</c> now goes to the generated class.</summary>
    [Fact]
    public void RevertPatchRedirectsTheRevertInAccept()
    {
        ClassPatchResult result = PatcherAgent.RegistryRevert.Apply(
            CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7", PatcherAgent.RevertToFrozen, Accept)
        );

        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, Accept, RegistrySnapshots.Name, PatcherAgent.RevertToFrozen.Name, PatcherAgent.RevertToFrozen.Descriptor);
        ClassFileAssert.DoesNotCall(patched, Accept, PatcherAgent.RevertToFrozen.Owner, PatcherAgent.RevertToFrozen.Name, PatcherAgent.RevertToFrozen.Descriptor);
    }

    // A class file with an empty accept and a method that calls the given GameData method with null and false arguments and returns what it returns.
    private static byte[] CreateClassFile(string className, JavaMethod call, string caller)
    {
        ClassFileWriter writer = new(className);
        int reference = writer.AddMethodReference(call.Owner, call.Name, call.Descriptor);
        bool returnsValue = !call.Descriptor.EndsWith(value: ")V", StringComparison.Ordinal);
        byte[] arguments = call.Descriptor.StartsWith(value: "()", StringComparison.Ordinal) ? [] : [AconstNull, IconstZero, IconstZero];
        byte[] code = [.. arguments, InvokeStatic, checked((byte)(reference >> 8)), checked((byte)(reference & 0xff)), returnsValue ? Areturn : Return];
        string descriptor = returnsValue ? "()Ljava/lang/Object;" : "()V";

        if (caller != Accept)
            writer = writer.AddMethod(Accept, descriptor: "()V", maxStack: 0, maxLocals: 0, [Return]);

        writer = writer.AddMethod(caller, descriptor, maxStack: 3, maxLocals: 0, code);

        return writer.ToArray();
    }
}
