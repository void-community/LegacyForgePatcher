using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Builds FML's config without NightConfig's autosave and autoreload, as Forge for Minecraft 1.20.1 does. Forge from 1.13.2 to 1.19.4 builds it with both, so on a
/// first start each key that <c>ConfigSpec.correct</c> adds to the default <c>fml.toml</c> saves the file, and each save wakes NightConfig's file watcher, which
/// reloads the file into the config that <c>correct</c> still iterates: the game exits with a <c>ConcurrentModificationException</c> or a parse error, or keeps a
/// <c>fml.toml</c> with keys written twice. <c>loadFrom</c> saves the corrected config itself, and nothing else changes it on these versions.
/// </summary>
internal static class ModLoaderConfigurationWatch
{
    private const string Builder = "com/electronwill/nightconfig/core/file/GenericBuilder";
    private const string BuilderDescriptor = "()Lcom/electronwill/nightconfig/core/file/GenericBuilder;";
    private const byte InvokeVirtual = 0xb6;
    private const string Method = "loadFrom";
    private const string MethodDescriptor = "(Ljava/nio/file/Path;)V";
    private const byte Nop = 0x00;

    /// <summary>Gets the patch of Forge's <c>FMLConfig</c>.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(className: "net/minecraftforge/fml/loading/FMLConfig")
        .Method(Method, MethodDescriptor)
        .Edit(description: "autosave and autoreload left off", LeaveOffAutosaveAndAutoreload);

    // builder.autosave() and builder.autoreload() set a flag and return the builder they are called on, so three nops in place of each call leave the same builder,
    // of the same type, on the stack for the call that follows.
    private static int LeaveOffAutosaveAndAutoreload(ClassEditContext context)
    {
        ClassFileEditor editor = context.Editor;
        JavaClassFile model = editor.Model;

        if (model.FindMethod(Method, MethodDescriptor)?.Code is not { } code)
            return 0;

        Instruction[] calls =
        [
            .. code.Instructions.Where(
                instruction =>
                instruction is { Opcode: InvokeVirtual, ConstantIndex: { } index }
                && model.GetMemberReference(index) is { Owner: Builder, Name: "autosave" or "autoreload", Descriptor: BuilderDescriptor }
            ),
        ];

        int changed = 0;

        foreach (Instruction call in calls)
        {
            int index = call.ConstantIndex ?? 0;
            byte[] expected = [InvokeVirtual, checked((byte)(index >> 8)), checked((byte)(index & 0xff))];
            bool replaced = editor.ReplaceCode(Method, MethodDescriptor, call.Offset, expected, [Nop, Nop, Nop]);

            if (replaced)
                changed++;
        }

        return changed;
    }
}
