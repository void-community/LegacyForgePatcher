using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>Lets Forge whose FML sorts LaunchWrapper's tweakers while LaunchWrapper iterates them launch on Java 8u20 and later.</summary>
public sealed class PatcherAgent : JavaAgent
{
    /// <summary>
    /// Points FML's <c>Collections.sort</c> call at <c>legacyforgepatcher.Collections</c>. FML sorts LaunchWrapper's tweakers while LaunchWrapper iterates
    /// them, and Java from 8u20 counts a sort as a structural change, so the iteration throws <c>ConcurrentModificationException</c>.
    /// </summary>
    internal static readonly ClassPatch TweakSort = ClassPatch.For(className: "cpw/mods/fml/relauncher/CoreModManager")
        .Method(name: "sortTweakList", descriptor: "()V")
        .RedirectInvocation(
            JavaMethod.Static(owner: "java/util/Collections", name: "sort", descriptor: "(Ljava/util/List;Ljava/util/Comparator;)V"),
            toOwner: "legacyforgepatcher/Collections"
        );

    /// <inheritdoc/>
    public override void Configure(AgentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The bootstrap loader makes the helper visible to whichever loader defines FML.
        context.DefineClass(JavaClasses.Legacyforgepatcher.Collections, ClassLoaderTarget.Bootstrap);
        context.Patch(TweakSort);
    }
}
