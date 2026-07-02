using System;
using System.Runtime.CompilerServices;

namespace BitPack;

public class BitWriter
{
    private readonly Memory<byte> _buffer;
    private int _bitPosition;

    public BitWriter(byte[] buffer)
        : this(buffer == null ? throw new ArgumentNullException(nameof(buffer)) : buffer.AsMemory())
    {
    }

    public BitWriter(Memory<byte> buffer)
    {
        _buffer = buffer;
    }

    /// <summary>
    ///     Total number of bytes written to the buffer.
    /// </summary>
    public int BytesWritten => (_bitPosition + 7) / 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(bool value)
    {
        var writer = CreateSpanWriter();
        writer.WriteBool(value);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt(int value, int bitCount)
    {
        var writer = CreateSpanWriter();
        writer.WriteInt(value, bitCount);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLong(long value, int bitCount)
    {
        var writer = CreateSpanWriter();
        writer.WriteLong(value, bitCount);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteULong(ulong value, int bitCount)
    {
        var writer = CreateSpanWriter();
        writer.WriteULong(value, bitCount);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat(float value)
    {
        var writer = CreateSpanWriter();
        writer.WriteFloat(value);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        var writer = CreateSpanWriter();
        writer.WriteDouble(value);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateTime(DateTime value)
    {
        var writer = CreateSpanWriter();
        writer.WriteDateTime(value);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDecimal(decimal value)
    {
        var writer = CreateSpanWriter();
        writer.WriteDecimal(value);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(string value, int maxLength)
    {
        var writer = CreateSpanWriter();
        writer.WriteString(value, maxLength);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(string value, int maxLength, int lengthBits)
    {
        var writer = CreateSpanWriter();
        writer.WriteString(value, maxLength, lengthBits);
        Commit(writer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytes(ReadOnlySpan<byte> source)
    {
        var writer = CreateSpanWriter();
        writer.WriteBytes(source);
        Commit(writer);
    }

    /// <summary>
    ///     Calculates the minimum number of bits needed to represent values 0..maxValue.
    ///     Uses integer bit manipulation instead of floating-point Math.Log.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CalculateBitsNeeded(int maxValue)
    {
        if (maxValue <= 0) return 0;
        var bits = 1;
        var v = maxValue;
        while (v > 1)
        {
            bits++;
            v >>= 1;
        }

        return bits;
    }

    /// <summary>
    ///     Reset the writer state to start writing from the beginning.
    /// </summary>
    public void Reset()
    {
        _bitPosition = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BitSpanWriter CreateSpanWriter()
    {
        return new BitSpanWriter(_buffer.Span, _bitPosition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Commit(BitSpanWriter writer)
    {
        _bitPosition = writer.BitPosition;
    }
}