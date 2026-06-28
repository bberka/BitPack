using System;
using System.Runtime.CompilerServices;

namespace BitPack;

public class BitWriter
{
    private readonly byte[] _buffer;
    private int _bitPosition = 0;

    public BitWriter(byte[] buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    /// <summary>
    /// Writes a single boolean value as 1 bit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(bool value)
    {
        if (value)
        {
            var byteIndex = _bitPosition >> 3;
            var bitOffset = _bitPosition & 7;
            _buffer[byteIndex] |= (byte)(1 << bitOffset);
        }
        _bitPosition++;
    }

    /// <summary>
    /// Writes an integer value using the specified number of bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt(int value, int bitCount)
    {
        WriteLong(value, bitCount);
    }

    /// <summary>
    /// Writes a signed 64-bit integer using the specified number of bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLong(long value, int bitCount)
    {
        if (bitCount <= 0) return;

        // Mask the value to the specified bitCount to avoid writing garbage bits
        if (bitCount < 64)
        {
            value &= (1L << bitCount) - 1;
        }

        var bitsRemaining = bitCount;
        var currentVal = value;
        var currentBitPos = _bitPosition;

        while (bitsRemaining > 0)
        {
            var bIndex = currentBitPos >> 3;
            var bOffset = currentBitPos & 7;
            var bitsToWrite = Math.Min(bitsRemaining, 8 - bOffset);

            var mask = (1 << bitsToWrite) - 1;
            var byteBits = (int)(currentVal & mask);

            _buffer[bIndex] = (byte)((_buffer[bIndex] & ~(mask << bOffset)) | (byteBits << bOffset));

            currentVal >>= bitsToWrite;
            currentBitPos += bitsToWrite;
            bitsRemaining -= bitsToWrite;
        }

        _bitPosition += bitCount;
    }

    /// <summary>
    /// Writes an unsigned 64-bit integer using the specified number of bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteULong(ulong value, int bitCount)
    {
        WriteLong((long)value, bitCount);
    }

    /// <summary>
    /// Writes a 32-bit single-precision floating point number (4 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat(float value)
    {
        WriteInt(BitConverter.SingleToInt32Bits(value), 32);
    }

    /// <summary>
    /// Writes a 64-bit double-precision floating point number (8 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        WriteLong(BitConverter.DoubleToInt64Bits(value), 64);
    }

    /// <summary>
    /// Writes a DateTime value as its 64-bit Tick representation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateTime(DateTime value)
    {
        WriteLong(value.ToUniversalTime().Ticks, 64);
    }

    /// <summary>
    /// Writes a 128-bit high-precision decimal value (16 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDecimal(decimal value)
    {
        var bits = decimal.GetBits(value);
        WriteInt(bits[0], 32);
        WriteInt(bits[1], 32);
        WriteInt(bits[2], 32);
        WriteInt(bits[3], 32);
    }

    /// <summary>
    /// Writes a string using UTF-8 encoding, packing its length and byte data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(string value, int maxLength)
    {
        var text = value ?? string.Empty;
        if (text.Length > maxLength)
        {
            text = text.Substring(0, maxLength);
        }

        var maxBytes = maxLength * 4;
        Span<byte> tempBytes = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];

        var bytesWritten = System.Text.Encoding.UTF8.GetBytes(text.AsSpan(), tempBytes);
        var lenBits = CalculateBitsNeeded(maxBytes);
        WriteInt(bytesWritten, lenBits);

        for (var i = 0; i < bytesWritten; i++)
        {
            WriteInt(tempBytes[i], 8);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateBitsNeeded(int maxValue)
    {
        if (maxValue <= 0) return 0;
        return (int)Math.Ceiling(Math.Log(maxValue + 1, 2));
    }

    /// <summary>
    /// Total number of bytes written to the buffer.
    /// </summary>
    public int BytesWritten => (_bitPosition + 7) / 8;

    /// <summary>
    /// Reset the writer state to start writing from the beginning.
    /// </summary>
    public void Reset()
    {
        _bitPosition = 0;
        Array.Clear(_buffer, 0, _buffer.Length);
    }
}
