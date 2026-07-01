using System;

namespace BitPack.Tests;

/// <summary>
///     Represents a percentage value. 1_000_000 is 100%
///     Can not be a negative value.
/// </summary>
public readonly struct Percent : IEquatable<Percent>
{
    public long Value { get; }

    public Percent(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Percent value cannot be negative.");
        Value = value;
    }

    // Custom BitPack Serialization
    public void Serialize(BitWriter writer)
    {
        // 1_000_000 fits in 20 bits (2^20 = 1,048,576)
        writer.WriteLong(Value, 20);
    }

    public static Percent Deserialize(BitReader reader)
    {
        return new Percent(reader.ReadLong(20));
    }

    public decimal ToDecimal()
    {
        return Value / 1_000_000m;
    }

    public double ToDouble()
    {
        return Value / 1_000_000d;
    }

    public override string ToString()
    {
        return $"{ToDecimal() * 100m:0.####}%";
    }

    public bool Equals(Percent other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is Percent other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static bool operator ==(Percent left, Percent right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Percent left, Percent right)
    {
        return !left.Equals(right);
    }

    public static Percent operator +(Percent left, Percent right)
    {
        return new Percent(left.Value + right.Value);
    }

    public static Percent operator -(Percent left, Percent right)
    {
        if (left.Value < right.Value)
            throw new ArgumentOutOfRangeException(nameof(right), "Resulting percent cannot be negative.");
        return new Percent(left.Value - right.Value);
    }

    public static Percent operator *(Percent left, decimal right)
    {
        return right < 0
            ? throw new ArgumentOutOfRangeException(nameof(right), "Multiplier cannot be negative.")
            : new Percent((long)(left.Value * right));
    }

    public static Percent operator /(Percent left, decimal right)
    {
        if (right <= 0) throw new ArgumentOutOfRangeException(nameof(right), "Divider must be greater than zero.");
        return new Percent((long)(left.Value / right));
    }

    public static Percent From(double value)
    {
        return new Percent((long)value);
    }

    public static Percent From(decimal value)
    {
        return new Percent((long)value);
    }

    public static Percent From(int value)
    {
        return new Percent(value);
    }

    public static Percent From(long value)
    {
        return new Percent(value);
    }
}