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
        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;
        var mask = (byte)(1 << bitOffset);
        if (value)
            _buffer[byteIndex] |= mask;
        else
            _buffer[byteIndex] &= (byte)~mask;
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

        // Branchless mask: works for bitCount 1-64
        // For bitCount=64: MaxValue >> 0 = MaxValue
        // For bitCount=1:  MaxValue >> 63 = 1
        var mask = ulong.MaxValue >> (64 - bitCount);
        var uValue = (ulong)value & mask;

        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;

        if (byteIndex + 9 <= _buffer.Length)
        {
            var currentWord = Unsafe.ReadUnaligned<ulong>(ref _buffer[byteIndex]);

            var shiftedValue = uValue << bitOffset;
            var targetMask = mask << bitOffset;

            currentWord = (currentWord & ~targetMask) | shiftedValue;
            Unsafe.WriteUnaligned(ref _buffer[byteIndex], currentWord);

            var bitsWritten = 64 - bitOffset;
            if (bitsWritten < bitCount)
            {
                var remBits = bitCount - bitsWritten;
                var remValue = (int)(uValue >> bitsWritten);
                var remMask = (1 << remBits) - 1;
                ref var remByte = ref _buffer[byteIndex + 8];
                remByte = (byte)((remByte & ~remMask) | remValue);
            }
        }
        else
        {
            var bitsRemaining = bitCount;
            var currentVal = uValue;
            var currentBitPos = _bitPosition;

            while (bitsRemaining > 0)
            {
                var bIndex = currentBitPos >> 3;
                var bOffset = currentBitPos & 7;
                var bitsToWrite = Math.Min(bitsRemaining, 8 - bOffset);

                var byteMask = (1 << bitsToWrite) - 1;
                var byteBits = (int)(currentVal & (ulong)byteMask);

                _buffer[bIndex] = (byte)((_buffer[bIndex] & ~(byteMask << bOffset)) | (byteBits << bOffset));

                currentVal >>= bitsToWrite;
                currentBitPos += bitsToWrite;
                bitsRemaining -= bitsToWrite;
            }
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

        WriteBytes(tempBytes.Slice(0, bytesWritten));
    }

    /// <summary>
    /// Writes a block of bytes to the bitstream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytes(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty) return;

        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;

        if (bitOffset == 0)
        {
            source.CopyTo(_buffer.AsSpan(byteIndex, source.Length));
            _bitPosition += source.Length * 8;
        }
        else
        {
            var invShift = 8 - bitOffset;
            for (var i = 0; i < source.Length; i++)
            {
                byte b = source[i];
                _buffer[byteIndex] = (byte)((_buffer[byteIndex] & ((1 << bitOffset) - 1)) | (b << bitOffset));
                _buffer[byteIndex + 1] = (byte)((_buffer[byteIndex + 1] & ~((1 << bitOffset) - 1)) | (b >> invShift));
                byteIndex++;
            }
            _bitPosition += source.Length * 8;
        }
    }

    /// <summary>
    /// Calculates the minimum number of bits needed to represent values 0..maxValue.
    /// Uses integer bit manipulation instead of floating-point Math.Log.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateBitsNeeded(int maxValue)
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
    /// Total number of bytes written to the buffer.
    /// </summary>
    public int BytesWritten => (_bitPosition + 7) / 8;

    /// <summary>
    /// Reset the writer state to start writing from the beginning.
    /// No buffer clear is needed because WriteLong and WriteBool explicitly
    /// mask and overwrite all target bits during each write operation.
    /// </summary>
    public void Reset()
    {
        _bitPosition = 0;
    }
}
