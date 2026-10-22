namespace LegacyForgePatcher.ClassFiles;

/// <summary>The tags of constant pool entries, as in the JVM specification's section 4.4.</summary>
internal enum ConstantTag : byte
{
    Utf8 = 1,
    Integer = 3,
    Float = 4,
    Long = 5,
    Double = 6,
    Class = 7,
    String = 8,
    FieldReference = 9,
    MethodReference = 10,
    InterfaceMethodReference = 11,
    NameAndType = 12,
    MethodHandle = 15,
    MethodType = 16,
    Dynamic = 17,
    InvokeDynamic = 18,
    Module = 19,
    Package = 20
}
