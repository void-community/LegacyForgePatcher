using JvmBridge.Runtime;

namespace LegacyForgePatcher.ClassFiles;

/// <summary>Writes constant pool entries from an index on, adding each distinct entry once.</summary>
/// <param name="firstIndex">The index of the first entry it adds: 1 for a new class file, or the count of the pool it extends.</param>
internal sealed class ConstantPoolBuilder(int firstIndex)
{
    private readonly List<Action<Stream>> _entries = [];
    private readonly Dictionary<(ConstantTag Tag, string Value), ushort> _indices = [];

    /// <summary>The constant_pool_count of a pool that ends with the added entries.</summary>
    public ushort Count { get; private set; } = checked((ushort)firstIndex);

    public ushort Class(string name)
    {
        ushort nameIndex = Utf8(name);

        return Add(ConstantTag.Class, name, stream => stream.WriteU2(nameIndex));
    }

    public ushort InterfaceMethodReference(string owner, string name, string descriptor)
    {
        return MemberReference(ConstantTag.InterfaceMethodReference, owner, name, descriptor);
    }

    public ushort MethodReference(string owner, string name, string descriptor)
    {
        return MemberReference(ConstantTag.MethodReference, owner, name, descriptor);
    }

    public ushort Utf8(string value)
    {
        return Add(
            ConstantTag.Utf8,
            value,
            stream =>
            {
                // Class files store the bytes without the zero terminator JNI names take.
                ReadOnlySpan<byte> encoded = ModifiedUtf8.Encode(value).AsSpan(..^1);
                stream.WriteU2(checked((ushort)encoded.Length));
                stream.Write(encoded);
            }
        );
    }

    public void WriteTo(Stream stream)
    {
        foreach (Action<Stream> writeEntry in _entries)
            writeEntry(stream);
    }

    private ushort Add(ConstantTag tag, string value, Action<Stream> writeContents)
    {
        if (_indices.TryGetValue((tag, value), out ushort existing))
            return existing;

        ushort index = Count;
        Count = checked((ushort)(index + 1));
        _indices.Add((tag, value), index);

        _entries.Add(
            stream =>
            {
                stream.WriteByte((byte)tag);
                writeContents(stream);
            }
        );

        return index;
    }

    private ushort MemberReference(ConstantTag tag, string owner, string name, string descriptor)
    {
        ushort ownerIndex = Class(owner);
        ushort nameAndTypeIndex = NameAndType(name, descriptor);

        return Add(
            tag,
            $"{owner}.{name}:{descriptor}",
            stream =>
            {
                stream.WriteU2(ownerIndex);
                stream.WriteU2(nameAndTypeIndex);
            }
        );
    }

    private ushort NameAndType(string name, string descriptor)
    {
        ushort nameIndex = Utf8(name);
        ushort descriptorIndex = Utf8(descriptor);

        return Add(
            ConstantTag.NameAndType,
            $"{name}:{descriptor}",
            stream =>
            {
                stream.WriteU2(nameIndex);
                stream.WriteU2(descriptorIndex);
            }
        );
    }
}
