using System;
using Xunit;
using BitPack;

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
}
