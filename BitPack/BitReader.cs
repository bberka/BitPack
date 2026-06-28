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

        ulong value;
        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;

        if (byteIndex + 9 <= _buffer.Length)
        {
            var currentWord = Unsafe.ReadUnaligned<ulong>(ref _buffer[byteIndex]);
            // Branchless mask: works for bitCount 1-64
            var mask = ulong.MaxValue >> (64 - bitCount);
            value = (currentWord >> bitOffset) & mask;

            var bitsRead = 64 - bitOffset;
            if (bitsRead < bitCount)
            {
                var remBits = bitCount - bitsRead;
                var remMask = (1UL << remBits) - 1;
                ulong remValue = _buffer[byteIndex + 8];
                value |= (remValue & remMask) << bitsRead;
            }
        }
        else
        {
            value = 0;
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

                value |= ((ulong)byteBits << shift);

                shift += bitsToRead;
                currentBitPos += bitsToRead;
                bitsRemaining -= bitsToRead;
            }
        }

        _bitPosition += bitCount;
        return (long)value;
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
        ReadBytes(bytes);

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Reads a block of bytes from the bitstream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadBytes(Span<byte> destination)
    {
        if (destination.IsEmpty) return;

        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;

        if (bitOffset == 0)
        {
            _buffer.AsSpan(byteIndex, destination.Length).CopyTo(destination);
            _bitPosition += destination.Length * 8;
        }
        else
        {
            var invShift = 8 - bitOffset;
            var mask = (1 << bitOffset) - 1;
            for (var i = 0; i < destination.Length; i++)
            {
                var b0 = _buffer[byteIndex] >> bitOffset;
                var b1 = (_buffer[byteIndex + 1] & mask) << invShift;
                destination[i] = (byte)(b0 | b1);
                byteIndex++;
            }
            _bitPosition += destination.Length * 8;
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
    /// Reset the reader state to start reading from the beginning.
    /// </summary>
    public void Reset()
    {
        _bitPosition = 0;
    }
}
