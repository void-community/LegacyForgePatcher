using System.Buffers.Binary;

using LegacyForgePatcher.ClassFiles;

namespace LegacyForgePatcher;

/// <summary>
/// Points FML's <c>Collections.sort</c> calls at <see cref="SortClass"/>. FML sorts LaunchWrapper's tweakers while LaunchWrapper iterates
/// them, and Java from 8u20 counts a sort as a structural change, so the iteration throws <c>ConcurrentModificationException</c>.
/// </summary>
internal static class CoreModManagerPatch
{
    public const string ClassName = "cpw/mods/fml/relauncher/CoreModManager";

    /// <summary>Patches a CoreModManager class file, or returns <see langword="null"/> when it doesn't sort with <c>Collections.sort</c>.</summary>
    public static byte[]? Apply(ReadOnlySpan<byte> bytes)
    {
        byte[] original = bytes.ToArray();
        ConstantPool pool = ConstantPool.Read(original);
        int sort = pool.FindMethodReference(owner: "java/util/Collections", SortClass.MethodName, SortClass.MethodDescriptor);

        if (sort is 0)
            return null;

        ConstantPoolBuilder additions = new(pool.Count);
        ushort sortClass = additions.Class(SortClass.Name);

        using MemoryStream patched = new(original.Length + 64);

        patched.Write(original, offset: 0, ConstantPool.CountOffset);
        patched.WriteU2(additions.Count);
        patched.Write(original, ConstantPool.CountOffset + 2, pool.End - ConstantPool.CountOffset - 2);
        additions.WriteTo(patched);
        patched.Write(original, pool.End, original.Length - pool.End);

        // The existing entries keep their offsets, as the added ones follow them.
        byte[] result = patched.ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(pool.ClassIndexOffset(sort)), sortClass);

        return result;
    }
}
