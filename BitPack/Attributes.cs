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

/// <summary>
///     Assigns a stable wire-layout key to a serializable property or field.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class BitFieldKeyAttribute : Attribute
{
    public BitFieldKeyAttribute(int key)
    {
        Key = key;
    }

    public int Key { get; }
}

/// <summary>
///     Requires an array property or field to contain exactly the specified number of elements on the wire.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class FixedCountAttribute : Attribute
{
    public FixedCountAttribute(int count)
    {
        Count = count;
    }

    public int Count { get; }
}

/// <summary>
///     Quantizes a floating-point angle in degrees into a fixed number of bits.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class AngleAttribute : Attribute
{
    public AngleAttribute(int bits = 16)
    {
        Bits = bits;
    }

    public int Bits { get; }
}

/// <summary>
///     Quantizes System.Numerics.Vector2 and Vector3 components using a shared range and decimal precision.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class VectorRangeAttribute : Attribute
{
    public VectorRangeAttribute(double minimum, double maximum, int decimals)
    {
        Minimum = minimum;
        Maximum = maximum;
        Decimals = decimals;
    }

    public double Minimum { get; }
    public double Maximum { get; }
    public int Decimals { get; }
}

/// <summary>
///     Quantizes System.Numerics.Quaternion using the smallest-three representation.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class QuaternionSmallestThreeAttribute : Attribute
{
    public QuaternionSmallestThreeAttribute(int bitsPerComponent = 16)
    {
        BitsPerComponent = bitsPerComponent;
    }

    public int BitsPerComponent { get; }
}

/// <summary>
///     Marks a property, field, or primary-constructor parameter as a compact union slot whose concrete type is
///     selected by a discriminator written into the bit stream before the variant payload.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class BitDiscriminatorAttribute : Attribute
{
    public BitDiscriminatorAttribute(int discriminatorBits = 0, int defaultVariant = 0)
    {
        DiscriminatorBits = discriminatorBits;
        DefaultVariant = defaultVariant;
    }

    public int DiscriminatorBits { get; }

    public int DefaultVariant { get; }
}

/// <summary>
///     Declares one concrete variant for a slot marked with <see cref="BitDiscriminatorAttribute" />. The
///     <paramref name="index" /> argument is the discriminator value written before the variant payload.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = true, Inherited = false)]
public sealed class BitVariantAttribute : Attribute
{
    public BitVariantAttribute(Type variantType, int index)
    {
        VariantType = variantType;
        Index = index;
    }

    public Type VariantType { get; }

    public int Index { get; }
}