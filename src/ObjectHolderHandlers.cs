using JvmBridge.Agents;
using JvmBridge.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Makes the set of object holder handlers that Forge's <c>ObjectHolderRegistry</c> keeps a concurrent one. <c>addHandler</c> adds to a plain static
/// <c>HashSet</c> without a lock, and mods register handlers from the mod-loading worker threads at once, which can break the set's hash map; a Forge 1.17.1
/// client crashed with a <c>ClassCastException</c> in <c>HashMap$TreeNode</c>. Forge creates the set in the class's static initializer from Minecraft 1.13.2 to
/// 1.21.4, and NeoForge for 1.20.1 does too.
/// </summary>
internal static class ObjectHolderHandlers
{
    private const string ClassInitializer = "<clinit>";
    private const byte Dup = 0x59;
    private const string Field = "objectHolders";
    private const string FieldDescriptor = "Ljava/util/Set;";
    private const string HashSet = "java/util/HashSet";
    private const byte InvokeSpecial = 0xb7;
    private const byte InvokeStatic = 0xb8;
    private const byte New = 0xbb;
    private const byte Nop = 0x00;
    private const byte PutStatic = 0xb3;

    /// <summary>Gets the patch of <c>ObjectHolderRegistry</c>.</summary>
    internal static ClassPatch Patch { get; } = ClassPatch.For(className: "net/minecraftforge/registries/ObjectHolderRegistry")
        .Method(ClassInitializer, descriptor: "()V")
        .Edit(description: "objectHolders created as a ConcurrentHashMap key set", UseConcurrentKeySet);

    private static IEnumerable<Instruction> FindCreations(JavaClassFile model, IReadOnlyList<Instruction> instructions)
    {
        for (int index = 0; index + 3 < instructions.Count; index++)
        {
            Instruction create = instructions[index];
            Instruction construct = instructions[index + 2];
            Instruction store = instructions[index + 3];

            bool creates = create is { Opcode: New, ConstantIndex: { } created } && model.GetClassName(created) == HashSet;
            bool constructs = construct is { Opcode: InvokeSpecial, ConstantIndex: { } called } && model.GetMemberReference(called) is { Owner: HashSet, Name: "<init>", Descriptor: "()V" };
            bool stores = store is { Opcode: PutStatic, ConstantIndex: { } field } && model.GetMemberReference(field) is { Name: Field, Descriptor: FieldDescriptor } reference && reference.Owner == model.Name;

            if (creates && instructions[index + 1].Opcode == Dup && constructs && stores)
                yield return create;
        }
    }

    private static byte[] Index(int index)
    {
        return [checked((byte)(index >> 8)), checked((byte)(index & 0xff))];
    }

    // Rewrites `new HashSet; dup; invokespecial HashSet.<init>()V` before `putstatic this.objectHolders:Set` to `invokestatic ConcurrentHashMap.newKeySet()` and four
    // nops, at the same length. Both leave one Set on the stack for the putstatic, and no stack map frame can fall between them in this straight run of code.
    private static int UseConcurrentKeySet(ClassEditContext context)
    {
        ClassFileEditor editor = context.Editor;
        JavaClassFile model = editor.Model;

        if (model.FindMethod(ClassInitializer, descriptor: "()V")?.Code is not { } code)
            return 0;

        List<Instruction> creations = [.. FindCreations(model, code.Instructions)];

        if (creations.Count == 0)
            return 0;

        int newKeySet = editor.AddMethodReference(
            owner: "java/util/concurrent/ConcurrentHashMap",
            name: "newKeySet",
            descriptor: "()Ljava/util/concurrent/ConcurrentHashMap$KeySetView;"
        );

        int changed = 0;

        foreach (Instruction create in creations)
        {
            int constructor = code.Instructions.Single(instruction => instruction.Offset == create.Offset + 4).ConstantIndex ?? 0;
            byte[] expected = [New, .. Index(create.ConstantIndex ?? 0), Dup, InvokeSpecial, .. Index(constructor)];
            byte[] replacement = [InvokeStatic, .. Index(newKeySet), Nop, Nop, Nop, Nop];

            if (editor.ReplaceCode(ClassInitializer, methodDescriptor: "()V", create.Offset, expected, replacement))
                changed++;
        }

        return changed;
    }
}
