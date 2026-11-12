// Attribute parsing into config structs (arrays, angles, vectors, quaternions).
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
    private static string SanitizeIdentifier(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');

        if (sb.Length == 0 || char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    private static bool TryGetArrayConfig(ISymbol symbol, IArrayTypeSymbol arrayType, out ArrayConfig config,
        out string error)
    {
        config = default;
        error = string.Empty;

        if (arrayType.Rank != 1)
        {
            error = "only one-dimensional arrays are supported.";
            return false;
        }

        if (arrayType.ElementType is IArrayTypeSymbol)
        {
            error = "jagged arrays are not supported.";
            return false;
        }

        if (arrayType.ElementType.SpecialType == SpecialType.System_String)
        {
            error = "string arrays are not supported yet because array length and element string length need separate bounds.";
            return false;
        }

        if (arrayType.ElementType.TypeKind == TypeKind.Interface)
        {
            error = "interface arrays are not supported because elements cannot be preallocated during deserialization.";
            return false;
        }

        var hasMaxLength = TryGetMaxLength(symbol, out var maxLength);
        var hasFixedCount = TryGetFixedCount(symbol, out var fixedCount);

        if (hasMaxLength && hasFixedCount)
        {
            error = "use either [MaxLength] or [FixedCount], not both.";
            return false;
        }

        if (!hasMaxLength && !hasFixedCount)
        {
            error = "arrays must declare [MaxLength] or [FixedCount].";
            return false;
        }

        if (hasMaxLength)
        {
            if (maxLength < 0)
            {
                error = "[MaxLength] must be zero or greater.";
                return false;
            }

            config = new ArrayConfig(false, maxLength, CalculateBitsNeeded(maxLength));
            return true;
        }

        if (fixedCount <= 0)
        {
            error = "[FixedCount] must be greater than zero.";
            return false;
        }

        config = new ArrayConfig(true, fixedCount, 0);
        return true;
    }

    private static bool TryGetFixedCount(ISymbol symbol, out int count)
    {
        count = 0;
        var attr = symbol.GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() is "BitPack.FixedCountAttribute" or "BitPack.FixedCount");
        if (attr != null && attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int value)
        {
            count = value;
            return true;
        }

        return false;
    }

    private static bool HasAngleAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "BitPack.AngleAttribute");
    }

    private static bool HasVectorRangeAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "BitPack.VectorRangeAttribute");
    }

    private static bool HasQuaternionSmallestThreeAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "BitPack.QuaternionSmallestThreeAttribute");
    }

    private static bool TryGetAngleConfig(ITypeSymbol type, ISymbol symbol, out AngleConfig config, out string error)
    {
        config = default;
        error = string.Empty;
        var attr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "BitPack.AngleAttribute");
        if (attr == null) return false;

        if (type.SpecialType is not (SpecialType.System_Single or SpecialType.System_Double))
        {
            error = "[Angle] can only be applied to float or double values.";
            return false;
        }

        var bits = attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int value ? value : 16;
        if (bits is < 1 or > 30)
        {
            error = "[Angle] bits must be between 1 and 30.";
            return false;
        }

        config = new AngleConfig(bits);
        return true;
    }

    private static bool TryGetVectorRangeConfig(ITypeSymbol type, ISymbol symbol, out VectorRangeConfig config,
        out string error)
    {
        config = default;
        error = string.Empty;
        var attr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "BitPack.VectorRangeAttribute");
        if (attr == null) return false;

        var typeName = type.ToDisplayString();
        if (typeName is not ("System.Numerics.Vector2" or "System.Numerics.Vector3"))
        {
            error = "[VectorRange] can only be applied to System.Numerics.Vector2 or Vector3 values.";
            return false;
        }

        if (attr.ConstructorArguments.Length < 3 || attr.ConstructorArguments[0].Value == null ||
            attr.ConstructorArguments[1].Value == null || attr.ConstructorArguments[2].Value is not int decimals)
        {
            error = "[VectorRange] requires minimum, maximum, and decimals constructor arguments.";
            return false;
        }

        var min = Convert.ToDouble(attr.ConstructorArguments[0].Value);
        var max = Convert.ToDouble(attr.ConstructorArguments[1].Value);
        if (max <= min)
        {
            error = "[VectorRange] maximum must be greater than minimum.";
            return false;
        }

        if (decimals < 0)
        {
            error = "[VectorRange] decimals must be zero or greater.";
            return false;
        }

        var scale = Math.Pow(10, decimals);
        var rangeSize = (max - min) * scale;
        var bits = CalculateBitsNeeded((int)Math.Ceiling(rangeSize));
        if (bits > 62)
        {
            error = "[VectorRange] requires too many bits per component.";
            return false;
        }

        config = new VectorRangeConfig(min, max, decimals, scale, bits);
        return true;
    }

    private static bool TryGetQuaternionSmallestThreeConfig(ITypeSymbol type, ISymbol symbol,
        out QuaternionSmallestThreeConfig config, out string error)
    {
        config = default;
        error = string.Empty;
        var attr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "BitPack.QuaternionSmallestThreeAttribute");
        if (attr == null) return false;

        if (type.ToDisplayString() != "System.Numerics.Quaternion")
        {
            error = "[QuaternionSmallestThree] can only be applied to System.Numerics.Quaternion values.";
            return false;
        }

        var bits = attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int value ? value : 16;
        if (bits is < 2 or > 30)
        {
            error = "[QuaternionSmallestThree] bits per component must be between 2 and 30.";
            return false;
        }

        config = new QuaternionSmallestThreeConfig(bits);
        return true;
    }

    private static bool TryGetMaxLength(ISymbol symbol, out int maxLength)
    {
        maxLength = 0;
        var attr = symbol.GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.MaxLengthAttribute");
        if (attr != null && attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int value)
        {
            maxLength = value;
            return true;
        }

        return false;
    }

    private static bool IsBitPacketType(ITypeSymbol type)
    {
        return type.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "BitPack.BitPacketAttribute");
    }

    private static bool ExposesCustomSerialization(ITypeSymbol type, out string? staticMethodName)
    {
        var members = type.GetMembers();
        var hasSerialize = members.OfType<IMethodSymbol>().Any(m =>
            m.Name == "Serialize" &&
            m.Parameters.Length == 1 &&
            m.Parameters[0].Type.ToDisplayString() == "BitPack.BitWriter");

        var staticDeserializer = members.OfType<IMethodSymbol>().FirstOrDefault(m =>
            m.IsStatic &&
            (m.Name == "Deserialize" || m.Name == "Read") &&
            m.Parameters.Length == 1 &&
            m.Parameters[0].Type.ToDisplayString() == "BitPack.BitReader" &&
            SymbolEqualityComparer.Default.Equals(m.ReturnType, type));

        staticMethodName = staticDeserializer?.Name;
        return hasSerialize;
    }
}
