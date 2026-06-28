using System;

namespace BitPack;

/// <summary>
/// Marks a class, struct, or record as a bit-packed network packet for compile-time serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class BitPacketAttribute : Attribute
{
    public int Version { get; }

    public BitPacketAttribute(int version = 1)
    {
        Version = version;
    }
}

/// <summary>
/// Dictates the decimal precision (number of decimal places) to use when quantizing floats/doubles into fixed-point representations.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class PrecisionAttribute : Attribute
{
    public int Decimals { get; }

    public PrecisionAttribute(int decimals)
    {
        Decimals = decimals;
    }
}

/// <summary>
/// Indicates the protocol version when this property was introduced.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class SinceVersionAttribute : Attribute
{
    public int Version { get; }

    public SinceVersionAttribute(int version)
    {
        Version = version;
    }
}
