using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace BitPack;

public ref struct BitSpanWriter
{
    private Span<byte> _buffer;
    private int _bitPosition;

    public BitSpanWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _bitPosition = 0;
    }

    internal BitSpanWriter(Span<byte> buffer, int bitPosition)
    {
        _buffer = buffer;
        _bitPosition = bitPosition;
    }

    internal int BitPosition => _bitPosition;

    public int BytesWritten => (_bitPosition + 7) / 8;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt(int value, int bitCount)
    {
        WriteLong(value, bitCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLong(long value, int bitCount)
    {
        if (bitCount <= 0) return;

        var uValue = (ulong)value;
        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;
        var totalBits = bitOffset + bitCount;

        if (totalBits <= 8 && byteIndex + 1 <= _buffer.Length)
        {
            var mask = (byte)((1 << bitCount) - 1);
            var val = (byte)(uValue & mask);
            ref var b = ref _buffer[byteIndex];
            b = (byte)((b & ~(mask << bitOffset)) | (val << bitOffset));
        }
        else if (totalBits <= 16 && byteIndex + 2 <= _buffer.Length)
        {
            var mask = (1 << bitCount) - 1;
            var val = (ushort)(uValue & (ulong)mask);
            var targetMask = (ushort)(mask << bitOffset);
            var shiftedVal = (ushort)(val << bitOffset);
            var current = Unsafe.ReadUnaligned<ushort>(ref _buffer[byteIndex]);
            current = (ushort)((current & ~targetMask) | shiftedVal);
            Unsafe.WriteUnaligned(ref _buffer[byteIndex], current);
        }
        else if (totalBits <= 32 && byteIndex + 4 <= _buffer.Length)
        {
            var mask = uint.MaxValue >> (32 - bitCount);
            var val = (uint)(uValue & mask);
            var targetMask = mask << bitOffset;
            var shiftedVal = val << bitOffset;
            var current = Unsafe.ReadUnaligned<uint>(ref _buffer[byteIndex]);
            current = (current & ~targetMask) | shiftedVal;
            Unsafe.WriteUnaligned(ref _buffer[byteIndex], current);
        }
        else
        {
            var mask = ulong.MaxValue >> (64 - bitCount);
            uValue &= mask;

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
        }

        _bitPosition += bitCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteULong(ulong value, int bitCount)
    {
        WriteLong((long)value, bitCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat(float value)
    {
        WriteInt(BitConverter.SingleToInt32Bits(value), 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        WriteLong(BitConverter.DoubleToInt64Bits(value), 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateTime(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) value = value.ToUniversalTime();
        WriteLong(value.Ticks, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDecimal(decimal value)
    {
#if NET5_0_OR_GREATER
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
#else
        var bits = decimal.GetBits(value);
#endif
        WriteInt(bits[0], 32);
        WriteInt(bits[1], 32);
        WriteInt(bits[2], 32);
        WriteInt(bits[3], 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(string value, int maxLength)
    {
        WriteString(value, maxLength, BitWriter.CalculateBitsNeeded(maxLength * 4));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteString(string value, int maxLength, int lengthBits)
    {
        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "Maximum length must be non-negative.");

        if (value != null && value.Length > maxLength)
            throw new ArgumentOutOfRangeException(nameof(value), value.Length,
                $"String length ({value.Length}) exceeds maximum allowed length ({maxLength}).");

        var text = value ?? string.Empty;
        var maxBytes = maxLength * 4;
        var length = Math.Min(text.Length, maxLength);

        WriteStringLarge(text, maxBytes, length, lengthBits);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteStringLarge(string text, int maxBytes, int length, int lengthBits)
    {
        var pooled = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            var bytesWritten = Encoding.UTF8.GetBytes(text.AsSpan(0, length), pooled);
            WriteInt(bytesWritten, lengthBits);
            WriteBytes(pooled.AsSpan(0, bytesWritten));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooled);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytes(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty) return;

        var byteIndex = _bitPosition >> 3;
        var bitOffset = _bitPosition & 7;

        if (bitOffset == 0)
        {
            source.CopyTo(_buffer.Slice(byteIndex, source.Length));
            _bitPosition += source.Length * 8;
        }
        else
        {
            var invShift = 8 - bitOffset;
            var lowMask = (byte)((1 << bitOffset) - 1);
            var highMask = (byte)~lowMask;
            var srcLen = source.Length;
            var i = 0;
            if (srcLen >= 2)
            {
                var srcWords = MemoryMarshal.Cast<byte, ushort>(source);
                foreach (var word in srcWords)
                {
                    var s0 = (byte)word;
                    var s1 = (byte)(word >> 8);
                    _buffer[byteIndex] = (byte)((_buffer[byteIndex] & lowMask) | (s0 << bitOffset));
                    _buffer[byteIndex + 1] = (byte)((s0 >> invShift) | (s1 << bitOffset));
                    _buffer[byteIndex + 2] = (byte)((_buffer[byteIndex + 2] & highMask) | (s1 >> invShift));
                    byteIndex += 2;
                    i += 2;
                }
            }

            for (; i < srcLen; i++)
            {
                var b = source[i];
                _buffer[byteIndex] = (byte)((_buffer[byteIndex] & lowMask) | (b << bitOffset));
                _buffer[byteIndex + 1] = (byte)((_buffer[byteIndex + 1] & highMask) | (b >> invShift));
                byteIndex++;
            }

            _bitPosition += srcLen * 8;
        }
    }

    public void Reset()
    {
        _bitPosition = 0;
    }
}