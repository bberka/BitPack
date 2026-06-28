using System;
using System.Runtime.CompilerServices;

namespace BitPack;

public class BitReader
{
    private readonly byte[] _buffer;
    private int _bitPosition = 0;

    public BitReader(byte[] buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    /// <summary>
    /// Reads a single boolean value as 1 bit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;
        var value = (_buffer[byteIndex] & (1 << bitOffset)) != 0;
        _bitPosition++;
        return value;
    }

    /// <summary>
    /// Reads an integer value using the specified number of bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt(int bitCount)
    {
        return (int)ReadLong(bitCount);
    }

    /// <summary>
    /// Reads a signed 64-bit integer using the specified number of bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLong(int bitCount)
    {
        if (bitCount <= 0) return 0;

        long value = 0;
        var bitsRemaining = bitCount;
        var currentBitPos = _bitPosition;
        var shift = 0;

        while (bitsRemaining > 0)
        {
            var bIndex = currentBitPos >> 3;
            var bOffset = currentBitPos & 7;
            var bitsToRead = Math.Min(bitsRemaining, 8 - bOffset);

            var mask = (1 << bitsToRead) - 1;
            var byteBits = (_buffer[bIndex] >> bOffset) & mask;

            value |= ((long)byteBits << shift);

            shift += bitsToRead;
            currentBitPos += bitsToRead;
            bitsRemaining -= bitsToRead;
        }

        _bitPosition += bitCount;
        return value;
    }

    /// <summary>
    /// Reads an unsigned 64-bit integer using the specified number of bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadULong(int bitCount)
    {
        return (ulong)ReadLong(bitCount);
    }

    /// <summary>
    /// Reads a 32-bit single-precision floating point number (4 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat()
    {
        return BitConverter.Int32BitsToSingle(ReadInt(32));
    }

    /// <summary>
    /// Reads a 64-bit double-precision floating point number (8 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        return BitConverter.Int64BitsToDouble(ReadLong(64));
    }

    /// <summary>
    /// Reads a DateTime value from its 64-bit Tick representation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime ReadDateTime()
    {
        return new DateTime(ReadLong(64), DateTimeKind.Utc);
    }

    /// <summary>
    /// Reads a 128-bit high-precision decimal value (16 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal ReadDecimal()
    {
        var bits = new int[4];
        bits[0] = ReadInt(32);
        bits[1] = ReadInt(32);
        bits[2] = ReadInt(32);
        bits[3] = ReadInt(32);
        return new decimal(bits);
    }

    /// <summary>
    /// Reads and reconstructs a UTF-8 encoded string using its maxLength constraint.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString(int maxLength)
    {
        var maxBytes = maxLength * 4;
        var lenBits = CalculateBitsNeeded(maxBytes);
        var length = ReadInt(lenBits);

        if (length == 0) return string.Empty;

        Span<byte> bytes = length <= 256 ? stackalloc byte[length] : new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)ReadInt(8);
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateBitsNeeded(int maxValue)
    {
        if (maxValue <= 0) return 0;
        return (int)Math.Ceiling(Math.Log(maxValue + 1, 2));
    }

    /// <summary>
    /// Reset the reader state to start reading from the beginning.
    /// </summary>
    public void Reset()
    {
        _bitPosition = 0;
    }
}
