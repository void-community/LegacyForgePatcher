using System.Buffers.Binary;

using JvmBridge.Runtime;

namespace LegacyForgePatcher.ClassFiles;

/// <summary>The constant pool of a class file, read far enough to find the methods its code calls.</summary>
internal sealed class ConstantPool
{
    /// <summary>The offset of the constant_pool_count field, which the pool's entries follow.</summary>
    public const int CountOffset = 8;

    private readonly byte[] _classFile;
    private readonly int[] _entryOffsets;

    private ConstantPool(byte[] classFile, int[] entryOffsets, int end)
    {
        _classFile = classFile;
        _entryOffsets = entryOffsets;
        End = end;
    }

    /// <summary>The pool's constant_pool_count, one more than its last entry's index.</summary>
    public int Count => _entryOffsets.Length;

    /// <summary>The offset of the first class file byte after the pool.</summary>
    public int End { get; }

    public static ConstantPool Read(byte[] classFile)
    {
        if (BinaryPrimitives.ReadUInt32BigEndian(classFile) != 0xcafebabe)
            throw new InvalidDataException(message: "The class file has no magic number.");

        int[] entryOffsets = new int[ReadU2(classFile, CountOffset)];
        int offset = CountOffset + 2;

        for (int index = 1; index < entryOffsets.Length; index++)
        {
            entryOffsets[index] = offset;
            ConstantTag tag = (ConstantTag)classFile[offset];

            offset += 1 + tag switch
            {
                ConstantTag.Utf8 => 2 + ReadU2(classFile, offset + 1),
                ConstantTag.Integer or ConstantTag.Float or ConstantTag.FieldReference or ConstantTag.MethodReference or ConstantTag.InterfaceMethodReference or ConstantTag.NameAndType or ConstantTag.Dynamic or ConstantTag.InvokeDynamic => 4,
                ConstantTag.Long or ConstantTag.Double => 8,
                ConstantTag.Class or ConstantTag.String or ConstantTag.MethodType or ConstantTag.Module or ConstantTag.Package => 2,
                ConstantTag.MethodHandle => 3,
                _ => throw new InvalidDataException($"The constant pool has an entry of unknown tag {tag} at index {index}.")
            };

            // Eight-byte constants take two entries.
            if (tag is ConstantTag.Long or ConstantTag.Double)
                index++;
        }

        return new ConstantPool(classFile, entryOffsets, offset);
    }

    /// <summary>The offset of the class_index field of a method reference.</summary>
    public int ClassIndexOffset(int methodReference)
    {
        return Tag(methodReference) is not ConstantTag.MethodReference
            ? throw new ArgumentException($"Constant pool entry {methodReference} is not a method reference.", nameof(methodReference))
            : _entryOffsets[methodReference] + 1;
    }

    /// <summary>Finds the method reference of a class method, or returns zero when the class file doesn't call it.</summary>
    public int FindMethodReference(string owner, string name, string descriptor)
    {
        for (int index = 1; index < Count; index++)
        {
            if (Tag(index) is not ConstantTag.MethodReference)
                continue;

            int entry = _entryOffsets[index];
            int nameAndType = _entryOffsets[ReadU2(_classFile, entry + 3)];
            string? entryOwner = ClassName(ReadU2(_classFile, entry + 1));
            string? entryName = Utf8(ReadU2(_classFile, nameAndType + 1));
            string? entryDescriptor = Utf8(ReadU2(_classFile, nameAndType + 3));

            if (entryOwner == owner && entryName == name && entryDescriptor == descriptor)
                return index;
        }

        return 0;
    }

    private static ushort ReadU2(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset));
    }

    private string? ClassName(int index)
    {
        return Tag(index) is ConstantTag.Class ? Utf8(ReadU2(_classFile, _entryOffsets[index] + 1)) : null;
    }

    private ConstantTag Tag(int index)
    {
        return _entryOffsets[index] is 0 ? 0 : (ConstantTag)_classFile[_entryOffsets[index]];
    }

    private string? Utf8(int index)
    {
        if (Tag(index) is not ConstantTag.Utf8)
            return null;

        int offset = _entryOffsets[index];

        return ModifiedUtf8.Decode(_classFile.AsSpan(offset + 3, ReadU2(_classFile, offset + 1)));
    }
}
