using System;

namespace BitPack;

/// <summary>
///     Marks a class, struct, or record as a bit-packed network packet for compile-time serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class BitPacketAttribute : Attribute
{
    public BitPacketAttribute(int version = 1)
    {
        Version = version;
    }

    public int Version { get; }
}

/// <summary>
///     Dictates the decimal precision (number of decimal places) to use when quantizing floats/doubles into fixed-point
///     representations.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PrecisionAttribute : Attribute
{
    public PrecisionAttribute(int decimals)
    {
        Decimals = decimals;
    }

    public int Decimals { get; }
}

/// <summary>
///     Indicates the protocol version when this property was introduced.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SinceVersionAttribute : Attribute
{
    public SinceVersionAttribute(int version)
    {
        Version = version;
    }

    public int Version { get; }
}