using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.ClassFiles.Code;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Redirecting the registry rebuilds of FML's client handshake states to the helper that runs them on the client thread.</summary>
public sealed class RegistryThreadPatchTests
{
    private const string Accept = "accept";
    private const string ClientThread = "legacyforgepatcher/ClientThread";
    private const string Lambda = "lambda$accept$0";

    /// <summary>The Java helper declares GameData's methods with GameData's descriptors, so the redirects keep them, in a class that Java 8 loads.</summary>
    [Fact]
    public void ClientThreadDeclaresGameDatasMethods()
    {
        JavaClassFile helper = JavaClassFile.Read(JavaClasses.Legacyforgepatcher.ClientThread.Bytes.Span);

        Assert.Equal(ClientThread, helper.Name);
        Assert.Equal(expected: 52, helper.MajorVersion);
        Assert.NotNull(helper.FindMethod(PatcherAgent.RevertToFrozen.Name, PatcherAgent.RevertToFrozen.Descriptor));
        Assert.NotNull(helper.FindMethod(PatcherAgent.InjectSnapshot.Name, PatcherAgent.InjectSnapshot.Descriptor));
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

    /// <summary>The injection in WAITINGSERVERCOMPLETE's <c>accept</c> now goes to the helper, which returns its result.</summary>
    [Fact]
    public void InjectionPatchRedirectsTheInjectionInAccept()
    {
        ClassPatchResult result = PatcherAgent.RegistryInjection.Apply(
            CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$4", PatcherAgent.InjectSnapshot, Accept)
        );

        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, Accept, ClientThread, PatcherAgent.InjectSnapshot.Name, PatcherAgent.InjectSnapshot.Descriptor);
        ClassFileAssert.DoesNotCall(patched, Accept, PatcherAgent.InjectSnapshot.Owner, PatcherAgent.InjectSnapshot.Name, PatcherAgent.InjectSnapshot.Descriptor);
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

    /// <summary>The revert in DONE's <c>accept</c> now goes to the helper.</summary>
    [Fact]
    public void RevertPatchRedirectsTheRevertInAccept()
    {
        ClassPatchResult result = PatcherAgent.RegistryRevert.Apply(
            CreateClassFile(className: "net/minecraftforge/fml/common/network/handshake/FMLHandshakeClientState$7", PatcherAgent.RevertToFrozen, Accept)
        );

        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, Accept, ClientThread, PatcherAgent.RevertToFrozen.Name, PatcherAgent.RevertToFrozen.Descriptor);
        ClassFileAssert.DoesNotCall(patched, Accept, PatcherAgent.RevertToFrozen.Owner, PatcherAgent.RevertToFrozen.Name, PatcherAgent.RevertToFrozen.Descriptor);
    }

    // A class file with an empty accept and a method that calls the given GameData method with null and false arguments and returns what it returns.
    private static byte[] CreateClassFile(string className, JavaMethod call, string caller)
    {
        bool returnsValue = !call.Descriptor.EndsWith(value: ")V", StringComparison.Ordinal);
        bool takesArguments = !call.Descriptor.StartsWith(value: "()", StringComparison.Ordinal);
        ClassFileWriter writer = new(className);

        if (caller != Accept)
            writer = writer.AddMethod(Accept, descriptor: "()V", static code => code.Return());

        return writer.AddMethod(
                caller,
                returnsValue ? "()Ljava/lang/Object;" : "()V",
                code =>
                {
                    CodeBuilder called = (takesArguments ? code.AConstNull().IConst(value: 0).IConst(value: 0) : code).Invoke(call);

                    return returnsValue ? called.AReturn() : called.Return();
                }
            )
            .ToArray();
    }
}
