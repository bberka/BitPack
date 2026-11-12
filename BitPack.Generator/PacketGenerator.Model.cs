// Internal model types shared by the generator partials.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace BitPack.Generator;

public partial class PacketGenerator
{
    private class SerializationTarget
    {
        public SerializationTarget(string name, ITypeSymbol type, ISymbol symbol, int? fieldKey)
        {
            Name = name;
            Type = type;
            Symbol = symbol;
            FieldKey = fieldKey;
        }

        public string Name { get; }
        public ITypeSymbol Type { get; }
        public ISymbol Symbol { get; }
        public int? FieldKey { get; }
    }

    private readonly struct BitVariantEntry
    {
        public BitVariantEntry(ITypeSymbol variantType, int index)
        {
            VariantType = variantType;
            Index = index;
        }

        public ITypeSymbol VariantType { get; }
        public int Index { get; }
    }

    private readonly struct UnionConfig
    {
        public UnionConfig(int discriminatorBits, int defaultVariant, List<BitVariantEntry> variants)
        {
            DiscriminatorBits = discriminatorBits;
            DefaultVariant = defaultVariant;
            Variants = variants;
        }

        public int DiscriminatorBits { get; }
        public int DefaultVariant { get; }
        public List<BitVariantEntry> Variants { get; }
    }

    private readonly struct ArrayConfig
    {
        public ArrayConfig(bool isFixed, int count, int lengthBits)
        {
            IsFixed = isFixed;
            Count = count;
            LengthBits = lengthBits;
        }

        public bool IsFixed { get; }
        public int Count { get; }
        public int LengthBits { get; }
    }

    private readonly struct AngleConfig
    {
        public AngleConfig(int bits)
        {
            Bits = bits;
        }

        public int Bits { get; }
    }

    private readonly struct VectorRangeConfig
    {
        public VectorRangeConfig(double min, double max, int decimals, double scale, int bits)
        {
            Min = min;
            Max = max;
            Decimals = decimals;
            Scale = scale;
            Bits = bits;
        }

        public double Min { get; }
        public double Max { get; }
        public int Decimals { get; }
        public double Scale { get; }
        public int Bits { get; }
    }

    private readonly struct QuaternionSmallestThreeConfig
    {
        public QuaternionSmallestThreeConfig(int bitsPerComponent)
        {
            BitsPerComponent = bitsPerComponent;
        }

        public int BitsPerComponent { get; }
    }
}
