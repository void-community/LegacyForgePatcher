using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Leaving NightConfig's autosave and autoreload off FML's config in Forge's FMLConfig.loadFrom.</summary>
public sealed class ConfigurationWatchPatchTests
{
    private const string Builder = "com/electronwill/nightconfig/core/file/GenericBuilder";
    private const string BuilderDescriptor = "()Lcom/electronwill/nightconfig/core/file/GenericBuilder;";
    private const string ClassName = "net/minecraftforge/fml/loading/FMLConfig";
    private const string LoadFrom = "loadFrom";

    /// <summary>Forge from 1.20.1 builds the config without either, so its loadFrom is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesConfigsBuiltWithoutThem()
    {
        ClassPatchResult result = ModLoaderConfigurationWatch.Patch.Apply(CreateClassFile(["sync"]));

        Assert.False(result.Changed);
        Assert.Contains("no invocation of " + Builder, result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary>Both calls go, so the builder they returned stays on the stack for the call that follows, which is kept.</summary>
    [Fact]
    public void PatchLeavesOffAutosaveAndAutoreload()
    {
        ClassPatchResult result = ModLoaderConfigurationWatch.Patch.Apply(CreateClassFile(["autosave", "autoreload", "sync"]));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 2, result.RemovedInvocations);
        ClassFileAssert.DoesNotCall(patched, LoadFrom, Builder, name: "autosave", BuilderDescriptor);
        ClassFileAssert.DoesNotCall(patched, LoadFrom, Builder, name: "autoreload", BuilderDescriptor);
        ClassFileAssert.Calls(patched, LoadFrom, Builder, name: "sync", BuilderDescriptor);
    }

    // A loadFrom that calls the given builder methods one after another on a null builder.
    private static byte[] CreateClassFile(string[] builderCalls)
    {
        return new ClassFileWriter(ClassName)
            .AddMethod(
                LoadFrom,
                descriptor: "(Ljava/nio/file/Path;)V",
                code => builderCalls.Aggregate(code.AConstNull(), static (code, call) => code.Invoke(JavaMethod.Virtual(Builder, call, BuilderDescriptor))).Pop().Return()
            )
            .ToArray();
    }
}
