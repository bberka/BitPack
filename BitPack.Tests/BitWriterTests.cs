using System;
using Xunit;

namespace BitPack.Tests;

public class BitWriterTests
{
    [Fact]
    public void WriteString_ThrowsWhenStringExceedsMaxLength()
    {
        var buffer = new byte[64];
        var writer = new BitWriter(buffer);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            writer.WriteString("hello world", 5));

        Assert.Equal("value", ex.ParamName);
        Assert.Contains("11", ex.Message);
        Assert.Contains("5", ex.Message);
    }

    [Fact]
    public void WriteString_NullString_DoesNotThrow()
    {
        var buffer = new byte[64];
        var writer = new BitWriter(buffer);

        writer.WriteString(null!, 10);

        var reader = new BitReader(buffer);
        var result = reader.ReadString(10);
        Assert.Empty(result);
    }

    [Fact]
    public void WriteString_ExactMaxLength_DoesNotThrow()
    {
        var buffer = new byte[64];
        var writer = new BitWriter(buffer);

        writer.WriteString("12345", 5);

        var reader = new BitReader(buffer);
        var result = reader.ReadString(5);
        Assert.Equal("12345", result);
    }

    [Fact]
    public void WriteString_NegativeMaxLength_Throws()
    {
        var buffer = new byte[64];
        var writer = new BitWriter(buffer);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            writer.WriteString("x", -1));

        Assert.Equal("maxLength", ex.ParamName);
    }

    [Fact]
    public void ReadBytes_UnalignedRoundTripsEveryOffsetAndLength()
    {
        for (var offset = 1; offset < 8; offset++)
        for (var length = 0; length <= 64; length++)
        {
            var buffer = new byte[128];
            var source = new byte[length];
            for (var i = 0; i < source.Length; i++) source[i] = (byte)(i * 31 + offset + length);

            var writer = new BitWriter(buffer);
            for (var i = 0; i < offset; i++) writer.WriteBool((i & 1) == 0);
            writer.WriteBytes(source);

            var reader = new BitReader(buffer);
            for (var i = 0; i < offset; i++) Assert.Equal((i & 1) == 0, reader.ReadBool());

            var destination = new byte[length];
            reader.ReadBytes(destination);

            Assert.Equal(source, destination);
        }
    }

    [Fact]
    public void MemoryWriterAndReader_RoundTripPrimitiveValues()
    {
        var buffer = new byte[64];
        var writer = new BitWriter(buffer.AsMemory());

        writer.WriteBool(true);
        writer.WriteInt(17, 5);
        writer.WriteString("abc", 8);
        writer.WriteDecimal(123.45m);

        var reader = new BitReader(new ReadOnlyMemory<byte>(buffer, 0, writer.BytesWritten));

        Assert.True(reader.ReadBool());
        Assert.Equal(17, reader.ReadInt(5));
        Assert.Equal("abc", reader.ReadString(8));
        Assert.Equal(123.45m, reader.ReadDecimal());
    }

    [Fact]
    public void SpanWriterAndReader_RoundTripPrimitiveValues()
    {
        Span<byte> buffer = stackalloc byte[64];
        var writer = new BitSpanWriter(buffer);

        writer.WriteBool(false);
        writer.WriteInt(29, 5);
        writer.WriteString("xyz", 8);
        writer.WriteDouble(123.456);

        var reader = new BitSpanReader(buffer.Slice(0, writer.BytesWritten));

        Assert.False(reader.ReadBool());
        Assert.Equal(29, reader.ReadInt(5));
        Assert.Equal("xyz", reader.ReadString(8));
        Assert.Equal(123.456, reader.ReadDouble());
    }

    [Fact]
    public void SpanReader_ReadBytesUnalignedRoundTrips()
    {
        Span<byte> buffer = stackalloc byte[128];
        Span<byte> source = stackalloc byte[17];
        for (var i = 0; i < source.Length; i++) source[i] = (byte)(i * 13 + 7);

        var writer = new BitSpanWriter(buffer);
        writer.WriteBool(true);
        writer.WriteBool(false);
        writer.WriteBool(true);
        writer.WriteBytes(source);

        var reader = new BitSpanReader(buffer.Slice(0, writer.BytesWritten));
        Assert.True(reader.ReadBool());
        Assert.False(reader.ReadBool());
        Assert.True(reader.ReadBool());

        Span<byte> destination = stackalloc byte[17];
        reader.ReadBytes(destination);

        Assert.True(source.SequenceEqual(destination));
    }

    [Fact]
    public void WriteDecimal_DoesNotAllocate()
    {
        var buffer = new byte[32];
        var writer = new BitWriter(buffer);

        writer.WriteDecimal(123456.7890m);
        writer.Reset();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            writer.Reset();
            writer.WriteDecimal(123456.7890m);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
