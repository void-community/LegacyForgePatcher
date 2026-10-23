using JvmBridge.Agents;
using JvmBridge.ClassFiles;
using JvmBridge.Testing;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>Pointing FML's load of its config at the helper that skips a second load.</summary>
public sealed class ModLoaderConfigurationPatchTests
{
    private const string Helper = "legacyforgepatcher/ModLoaderConfigs";
    private const string HelperDescriptor = "(Ljava/lang/Object;Ljava/nio/file/Path;)V";
    private const string LoadFrom = "loadFrom";
    private const string LoadFromDescriptor = "(Ljava/nio/file/Path;)V";

    /// <summary>The names of FML's configuration class in Forge and in NeoForge.</summary>
    public static TheoryData<string> ConfigurationClassNames { get; } = ["net/minecraftforge/fml/loading/FMLConfig", "net/neoforged/fml/loading/FMLConfig"];

    /// <summary>The compiled helper declares the static method the patches call, in a class that Java 8 loads.</summary>
    [Fact]
    public void HelperDeclaresTheMethodThePatchesCall()
    {
        JavaClassFile helper = JavaClassFile.Read(JavaClasses.Legacyforgepatcher.ModLoaderConfigs.Bytes.Span);

        Assert.Equal(Helper, helper.Name);
        Assert.Equal(expected: 52, helper.MajorVersion);
        Assert.NotNull(helper.FindMethod(LoadFrom, HelperDescriptor));
    }

    /// <summary>A FMLConfig whose <c>load</c> doesn't call <c>loadFrom</c> isn't the code this fix was written for, so it is left alone with a reason.</summary>
    [Fact]
    public void PatchLeavesConfigsWhoseLoadDoesNotCallLoadFrom()
    {
        ClassPatchResult result = Apply(CreateClassFile(className: "net/neoforged/fml/loading/FMLConfig", caller: "reload"));

        Assert.False(result.Changed);
        Assert.Contains(expectedSubstring: "load()V", result.SkippedReason, StringComparison.Ordinal);
    }

    /// <summary><c>load</c>'s call of <c>loadFrom</c> goes to the helper, with the config as its first argument.</summary>
    /// <param name="className">FMLConfig's name.</param>
    [Theory]
    [MemberData(nameof(ConfigurationClassNames))]
    public void PatchRedirectsTheLoadInLoad(string className)
    {
        ClassPatchResult result = Apply(CreateClassFile(className, caller: "load"));
        byte[] patched = result.Bytes ?? throw new InvalidOperationException(result.ToString());

        Assert.Equal(expected: 1, result.RedirectedInvocations);
        ClassFileAssert.Calls(patched, method: "load", Helper, LoadFrom, HelperDescriptor);
        ClassFileAssert.DoesNotCall(patched, method: "load", className, LoadFrom, LoadFromDescriptor);
    }

    private static ClassPatchResult Apply(byte[] classFile)
    {
        string className = JavaClassFile.Read(classFile).Name;

        return ModLoaderConfigurationLoad.Patches.Single(patch => patch.ClassName == className).Apply(classFile);
    }

    // A class file whose load is empty unless it is the caller, and whose caller calls loadFrom on a null config with a null path.
    private static byte[] CreateClassFile(string className, string caller)
    {
        JavaMethod loadFrom = JavaMethod.Virtual(className, LoadFrom, LoadFromDescriptor);
        ClassFileWriter writer = new(className);

        if (caller != "load")
            writer = writer.AddMethod(name: "load", descriptor: "()V", static code => code.Return());

        return writer.AddMethod(caller, descriptor: "()V", code => code.AConstNull().AConstNull().Invoke(loadFrom).Return()).ToArray();
    }
}
