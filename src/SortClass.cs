using LegacyForgePatcher.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Generates <c>legacyforgepatcher.Collections</c>, whose <c>sort(List, Comparator)</c> sorts a list as Java before 8u20 did: it sorts a copy
/// of the elements and sets them back, which doesn't count as a structural change to the list, so the list's iterators carry on.
/// </summary>
internal static class SortClass
{
    public const string MethodDescriptor = "(Ljava/util/List;Ljava/util/Comparator;)V";
    public const string MethodName = "sort";
    public const string Name = "legacyforgepatcher/Collections";

    private const byte Aload0 = 0x2a;
    private const byte Aload1 = 0x2b;
    private const byte Aload2 = 0x2c;
    private const byte Astore2 = 0x4d;
    private const byte InvokeInterface = 0xb9;
    private const byte InvokeStatic = 0xb8;

    // Straight-line code needs no stack map frames, so the class loads on Java 6 and later.
    private const ushort MajorVersion = 50;
    private const ushort PublicFinalSuper = 0x0031;
    private const ushort PublicStatic = 0x0009;
    private const byte Return = 0xb1;

    public static byte[] Generate()
    {
        ConstantPoolBuilder pool = new(firstIndex: 1);
        ushort thisClass = pool.Class(Name);
        ushort superClass = pool.Class(name: "java/lang/Object");
        ushort methodName = pool.Utf8(MethodName);
        ushort methodDescriptor = pool.Utf8(MethodDescriptor);
        ushort code = pool.Utf8(value: "Code");
        ushort toArray = pool.InterfaceMethodReference(owner: "java/util/List", name: "toArray", descriptor: "()[Ljava/lang/Object;");
        ushort sortArray = pool.MethodReference(owner: "java/util/Arrays", name: "sort", descriptor: "([Ljava/lang/Object;Ljava/util/Comparator;)V");
        ushort asList = pool.MethodReference(owner: "java/util/Arrays", name: "asList", descriptor: "([Ljava/lang/Object;)Ljava/util/List;");
        ushort copy = pool.MethodReference(owner: "java/util/Collections", name: "copy", descriptor: "(Ljava/util/List;Ljava/util/List;)V");

        // Object[] elements = list.toArray(); Arrays.sort(elements, comparator); Collections.copy(list, Arrays.asList(elements));
        using MemoryStream instructions = new();

        instructions.WriteByte(Aload0);
        instructions.WriteByte(InvokeInterface);
        instructions.WriteU2(toArray);
        instructions.WriteByte(value: 1);
        instructions.WriteByte(value: 0);
        instructions.WriteByte(Astore2);
        instructions.WriteByte(Aload2);
        instructions.WriteByte(Aload1);
        instructions.WriteByte(InvokeStatic);
        instructions.WriteU2(sortArray);
        instructions.WriteByte(Aload0);
        instructions.WriteByte(Aload2);
        instructions.WriteByte(InvokeStatic);
        instructions.WriteU2(asList);
        instructions.WriteByte(InvokeStatic);
        instructions.WriteU2(copy);
        instructions.WriteByte(Return);

        using MemoryStream classFile = new();

        // The magic number, minor and major versions, and the constant pool.
        classFile.WriteU4(value: 0xcafebabe);
        classFile.WriteU2(value: 0);
        classFile.WriteU2(MajorVersion);
        classFile.WriteU2(pool.Count);
        pool.WriteTo(classFile);

        // The class, its superclass, no interfaces and no fields.
        classFile.WriteU2(PublicFinalSuper);
        classFile.WriteU2(thisClass);
        classFile.WriteU2(superClass);
        classFile.WriteU2(value: 0);
        classFile.WriteU2(value: 0);

        // One method, whose Code attribute holds two stack slots, three locals, the instructions, no exception handlers and no attributes.
        classFile.WriteU2(value: 1);
        classFile.WriteU2(PublicStatic);
        classFile.WriteU2(methodName);
        classFile.WriteU2(methodDescriptor);
        classFile.WriteU2(value: 1);
        classFile.WriteU2(code);
        classFile.WriteU4(checked((uint)(12 + instructions.Length)));
        classFile.WriteU2(value: 2);
        classFile.WriteU2(value: 3);
        classFile.WriteU4(checked((uint)instructions.Length));
        instructions.WriteTo(classFile);
        classFile.WriteU2(value: 0);
        classFile.WriteU2(value: 0);

        // No class attributes.
        classFile.WriteU2(value: 0);

        return classFile.ToArray();
    }
}
