using System;
using System.Runtime.CompilerServices;

namespace BitPack;

public class BitReader
{
    private readonly ReadOnlyMemory<byte> _buffer;
    private int _bitPosition;

    public BitReader(byte[] buffer)
        : this(buffer == null ? throw new ArgumentNullException(nameof(buffer)) : new ReadOnlyMemory<byte>(buffer))
    {
    }

    public BitReader(ReadOnlyMemory<byte> buffer)
    {
        _buffer = buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        var reader = CreateSpanReader();
        var value = reader.ReadBool();
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt(int bitCount)
    {
        var reader = CreateSpanReader();
        var value = reader.ReadInt(bitCount);
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLong(int bitCount)
    {
        var reader = CreateSpanReader();
        var value = reader.ReadLong(bitCount);
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadULong(int bitCount)
    {
        var reader = CreateSpanReader();
        var value = reader.ReadULong(bitCount);
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat()
    {
        var reader = CreateSpanReader();
        var value = reader.ReadFloat();
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        var reader = CreateSpanReader();
        var value = reader.ReadDouble();
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime ReadDateTime()
    {
        var reader = CreateSpanReader();
        var value = reader.ReadDateTime();
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal ReadDecimal()
    {
        var reader = CreateSpanReader();
        var value = reader.ReadDecimal();
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString(int maxLength)
    {
        var reader = CreateSpanReader();
        var value = reader.ReadString(maxLength);
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString(int maxLength, int lengthBits)
    {
        var reader = CreateSpanReader();
        var value = reader.ReadString(maxLength, lengthBits);
        Commit(reader);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadBytes(Span<byte> destination)
    {
        var reader = CreateSpanReader();
        reader.ReadBytes(destination);
        Commit(reader);
    }

    public void Reset()
    {
        _bitPosition = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BitSpanReader CreateSpanReader()
    {
        return new BitSpanReader(_buffer.Span, _bitPosition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Commit(BitSpanReader reader)
    {
        _bitPosition = reader.BitPosition;
    }
}