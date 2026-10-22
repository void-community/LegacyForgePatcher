using JvmBridge.Agents;
using JvmBridge.Runtime;

namespace LegacyForgePatcher;

/// <summary>Lets Forge whose FML sorts LaunchWrapper's tweakers while LaunchWrapper iterates them launch on Java 8u20 and later.</summary>
public sealed class PatcherAgent : JavaAgent
{
    /// <inheritdoc/>
    public override void OnVmInit(AgentContext context, JavaEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        // The bootstrap loader makes the class visible to whichever loader defines FML.
        using JavaLocalReference sortClass = environment.DefineClass(SortClass.Name, loader: 0, SortClass.Generate());
    }

    /// <inheritdoc/>
    public override byte[]? TransformClass(AgentContext context, ClassFile file)
    {
        if (file.Name != CoreModManagerPatch.ClassName || CoreModManagerPatch.Apply(file.Bytes) is not { } patched)
            return null;

        Console.Error.WriteLine($"LegacyForgePatcher: patched {file.Name} to sort tweakers without disturbing their iteration");

        return patched;
    }
}
