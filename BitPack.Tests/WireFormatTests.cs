using System;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace BitPack.Tests;

[BitPacket]
public partial record WireFormatGoldenPacket
{
    [Range(0, 1000)] public int A { get; set; }
    public bool B { get; set; }
    [Range(0, 10)] public byte C { get; set; }
}

/// <summary>
///     Pins the on-the-wire bytes. If one of these fails, the wire format changed and existing peers
///     will no longer understand each other: bump the packet version or update the golden values deliberately.
/// </summary>
public class WireFormatTests
{
    [Fact]
    public void Fields_PackLsbFirstInDeclarationOrder()
    {
        var buffer = new byte[WireFormatGoldenPacket.MaxBytes];
        var writer = new BitWriter(buffer);
        new WireFormatGoldenPacket { A = 789, B = true, C = 8 }.Serialize(writer);

        // A: bits 0-9 (789), B: bit 10, C: bits 11-14 (8) => 789 + (1 << 10) + (8 << 11) = 0x4715
        Assert.Equal(2, writer.BytesWritten);
        Assert.Equal(new byte[] { 0x15, 0x47 }, buffer.AsSpan(0, 2).ToArray());
    }

    [Fact]
    public void LayoutHash_IsStable()
    {
        Assert.Equal(1667317386u, WireFormatGoldenPacket.LayoutHash);
    }
}
