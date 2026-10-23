using JvmBridge.Agents;
using JvmBridge.ClassFiles;

using Xunit;

namespace LegacyForgePatcher.Tests;

/// <summary>What the agent registers as a whole: its patches and the helpers they call.</summary>
public sealed class PatcherAgentTests
{
    /// <summary>Every helper is a class that Java 8 loads, the oldest Java the fixes need.</summary>
    [Fact]
    public void EveryHelperLoadsOnJava8()
    {
        foreach (JavaClassDefinition helper in PatcherAgent.Helpers)
            Assert.Equal(expected: 52, JavaClassFile.Read(helper.Bytes.Span).MajorVersion);
    }

    /// <summary>Every method a patch calls in a helper is one the helper declares, static and with that descriptor, so no patched class fails to link to it.</summary>
    [Fact]
    public void EveryPatchCallsMethodsItsHelpersDeclare()
    {
        foreach (ClassPatch patch in PatcherAgent.Patches)
            Assert.Empty(patch.FindMissingTargets(PatcherAgent.Helpers));
    }
}
