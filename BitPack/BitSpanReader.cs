using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace BitPack;

public ref struct BitSpanReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _bitPosition;

    public BitSpanReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _bitPosition = 0;
    }

    internal BitSpanReader(ReadOnlySpan<byte> buffer, int bitPosition)
    {
        _buffer = buffer;
        _bitPosition = bitPosition;
    }

    internal int BitPosition => _bitPosition;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;
        var value = (_buffer[byteIndex] & (1 << bitOffset)) != 0;
        _bitPosition++;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt(int bitCount)
    {
        return (int)ReadLong(bitCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLong(int bitCount)
    {
        if (bitCount <= 0) return 0;

        ulong value;
        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;
        var totalBits = bitOffset + bitCount;

        if (totalBits <= 8 && byteIndex + 1 <= _buffer.Length)
        {
            var mask = (1 << bitCount) - 1;
            value = (uint)((_buffer[byteIndex] >> bitOffset) & mask);
        }
        else if (totalBits <= 16 && byteIndex + 2 <= _buffer.Length)
        {
            var current = MemoryMarshal.Read<ushort>(_buffer.Slice(byteIndex));
            var mask = (1 << bitCount) - 1;
            value = (uint)((current >> bitOffset) & mask);
        }
        else if (totalBits <= 32 && byteIndex + 4 <= _buffer.Length)
        {
            var current = MemoryMarshal.Read<uint>(_buffer.Slice(byteIndex));
            var mask = uint.MaxValue >> (32 - bitCount);
            value = (current >> bitOffset) & mask;
        }
        else if (byteIndex + 9 <= _buffer.Length)
        {
            var currentWord = MemoryMarshal.Read<ulong>(_buffer.Slice(byteIndex));
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
                value |= (ulong)byteBits << shift;
                shift += bitsToRead;
                currentBitPos += bitsToRead;
                bitsRemaining -= bitsToRead;
            }
        }

        _bitPosition += bitCount;
        return (long)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadULong(int bitCount)
    {
        return (ulong)ReadLong(bitCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat()
    {
        return BitConverter.Int32BitsToSingle(ReadInt(32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        return BitConverter.Int64BitsToDouble(ReadLong(64));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime ReadDateTime()
    {
        return new DateTime(ReadLong(64), DateTimeKind.Utc);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal ReadDecimal()
    {
        var lo = ReadInt(32);
        var mid = ReadInt(32);
        var hi = ReadInt(32);
        var flags = ReadInt(32);
        return new decimal(lo, mid, hi, (flags & 0x80000000) != 0, (byte)((flags >> 16) & 0x7F));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString(int maxLength)
    {
        return ReadString(maxLength, BitWriter.CalculateBitsNeeded(maxLength * 4));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString(int maxLength, int lengthBits)
    {
        var length = ReadInt(lengthBits);
        if (length == 0) return string.Empty;

        return ReadStringLarge(length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string ReadStringLarge(int length)
    {
        var pooled = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            ReadBytes(pooled.AsSpan(0, length));
            return Encoding.UTF8.GetString(pooled.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooled);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadBytes(Span<byte> destination)
    {
        if (destination.IsEmpty) return;

        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;

        if (bitOffset == 0)
        {
            _buffer.Slice(byteIndex, destination.Length).CopyTo(destination);
            _bitPosition += destination.Length * 8;
        }
        else
        {
            var invShift = 8 - bitOffset;
            var mask = (1 << bitOffset) - 1;
            var i = 0;

            for (; i + 1 < destination.Length; i += 2)
            {
                var window = (uint)(_buffer[byteIndex] |
                                    (_buffer[byteIndex + 1] << 8) |
                                    (_buffer[byteIndex + 2] << 16));
                destination[i] = (byte)(window >> bitOffset);
                destination[i + 1] = (byte)(window >> (8 + bitOffset));
                byteIndex += 2;
            }

            for (; i < destination.Length; i++)
            {
                var b0 = _buffer[byteIndex] >> bitOffset;
                var b1 = (_buffer[byteIndex + 1] & mask) << invShift;
                destination[i] = (byte)(b0 | b1);
                byteIndex++;
            }

            _bitPosition += destination.Length * 8;
        }
    }

    public void Reset()
    {
        _bitPosition = 0;
    }
}