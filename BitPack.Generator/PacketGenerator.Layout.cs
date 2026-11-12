// Bit-width math, layout manifest/hash, diagnostic reporters and versioning.
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
    private static bool GetQuantizationConfig(ISymbol symbol, out double minVal, out double maxVal, out int decimals,
        out double scale, out int bits)
    {
        minVal = 0;
        maxVal = 0;
        decimals = 0;
        scale = 1;
        bits = 32;

        var rangeAttr = symbol.GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.RangeAttribute");

        var precisionAttr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "BitPack.PrecisionAttribute");

        if (rangeAttr != null && precisionAttr != null && rangeAttr.ConstructorArguments.Length >= 2 &&
            precisionAttr.ConstructorArguments.Length > 0)
            try
            {
                minVal = Convert.ToDouble(rangeAttr.ConstructorArguments[0].Value);
                maxVal = Convert.ToDouble(rangeAttr.ConstructorArguments[1].Value);
                decimals = Convert.ToInt32(precisionAttr.ConstructorArguments[0].Value);

                scale = Math.Pow(10, decimals);
                var rangeSize = (maxVal - minVal) * scale;
                bits = (int)Math.Ceiling(Math.Log(rangeSize + 1, 2));
                return true;
            }
            catch
            {
            }

        return false;
    }

    private static int GetEnumBits(ITypeSymbol enumType, ISymbol symbol)
    {
        var rangeAttr = symbol.GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.RangeAttribute");

        if (rangeAttr != null && rangeAttr.ConstructorArguments.Length >= 2)
            try
            {
                var maxValRange = Convert.ToInt32(rangeAttr.ConstructorArguments[1].Value);
                return CalculateBitsNeeded(maxValRange);
            }
            catch
            {
            }

        var maxEnumVal = 1;
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
            if (member.HasConstantValue)
                try
                {
                    var val = Convert.ToInt32(member.ConstantValue);
                    if (val > maxEnumVal) maxEnumVal = val;
                }
                catch
                {
                }

        return CalculateBitsNeeded(maxEnumVal);
    }

    private static int GetNumericBits(ITypeSymbol propType, ISymbol symbol, out bool hasConstraint, out long minVal,
        out long maxVal)
    {
        hasConstraint = false;
        minVal = 0;
        maxVal = 0;

        var rangeAttr = symbol.GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.RangeAttribute");

        if (rangeAttr != null && rangeAttr.ConstructorArguments.Length >= 2)
            try
            {
                hasConstraint = true;
                minVal = Convert.ToInt64(rangeAttr.ConstructorArguments[0].Value);
                maxVal = Convert.ToInt64(rangeAttr.ConstructorArguments[1].Value);

                var size = maxVal - minVal;
                return CalculateBitsNeeded((int)size);
            }
            catch
            {
            }

        return propType.SpecialType switch
        {
            SpecialType.System_Byte => 8,
            SpecialType.System_SByte => 8,
            SpecialType.System_Int16 => 16,
            SpecialType.System_UInt16 => 16,
            SpecialType.System_Int32 => 32,
            SpecialType.System_UInt32 => 32,
            SpecialType.System_Int64 => 64,
            SpecialType.System_UInt64 => 64,
            _ => 64
        };
    }

    private static int GetMaxLen(ISymbol symbol, out bool hasConstraint)
    {
        hasConstraint = false;

        var maxLengthAttr = symbol.GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.MaxLengthAttribute");

        if (maxLengthAttr != null && maxLengthAttr.ConstructorArguments.Length > 0 &&
            maxLengthAttr.ConstructorArguments[0].Value is int maxLenVal)
        {
            hasConstraint = true;
            return maxLenVal;
        }

        var stringLengthAttr = symbol.GetAttributes()
            .FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.StringLengthAttribute");

        if (stringLengthAttr != null && stringLengthAttr.ConstructorArguments.Length > 0 &&
            stringLengthAttr.ConstructorArguments[0].Value is int strLenVal)
        {
            hasConstraint = true;
            return strLenVal;
        }

        return 256; // default fallback string length
    }

    private static int CalculateBitsNeeded(int maxValue)
    {
        if (maxValue <= 0) return 1;
        return (int)Math.Ceiling(Math.Log(maxValue + 1, 2));
    }

    private static int GetPacketMaxBits(ITypeSymbol typeSymbol, List<SerializationTarget> targets, int packetMaxVersion)
    {
        return GetPacketMaxBits(typeSymbol, targets, packetMaxVersion, new HashSet<string>());
    }

    private static int GetPacketMaxBits(ITypeSymbol typeSymbol, List<SerializationTarget> targets, int packetMaxVersion,
        HashSet<string> seenTypes)
    {
        var typeKey = typeSymbol.ToDisplayString();
        if (!seenTypes.Add(typeKey)) return -1;

        var totalBits = packetMaxVersion > 1 ? 4 : 0;
        foreach (var target in targets)
        {
            if (GetPropertyVersion(target.Symbol) > packetMaxVersion) continue;

            var targetBits = GetTargetMaxBits(target, seenTypes);
            if (targetBits < 0)
            {
                seenTypes.Remove(typeKey);
                return -1;
            }

            totalBits += targetBits;
        }

        seenTypes.Remove(typeKey);
        return totalBits;
    }

    private static int GetTargetMaxBits(SerializationTarget target, HashSet<string> seenTypes)
    {
        var propType = target.Type;

        if (TryGetUnionConfigForBitMath(target, out var unionConfig)) return GetUnionMaxBits(unionConfig, seenTypes);

        if (propType is IArrayTypeSymbol arrayType) return GetArrayMaxBits(target, arrayType, seenTypes);
        if (TryGetAngleConfig(propType, target.Symbol, out var angleConfig, out _)) return angleConfig.Bits;
        if (TryGetVectorRangeConfig(propType, target.Symbol, out var vectorConfig, out _))
            return (propType.ToDisplayString() == "System.Numerics.Vector3" ? 3 : 2) * vectorConfig.Bits;
        if (TryGetQuaternionSmallestThreeConfig(propType, target.Symbol, out var quaternionConfig, out _))
            return 2 + 3 * quaternionConfig.BitsPerComponent;
        if (propType.SpecialType == SpecialType.System_Boolean) return 1;
        if (propType.SpecialType == SpecialType.System_Char) return 16;
        if (propType.SpecialType == SpecialType.System_String)
        {
            var maxLen = GetMaxLen(target.Symbol, out _);
            var maxBytes = maxLen * 4;
            return CalculateBitsNeeded(maxBytes) + maxBytes * 8;
        }
        if (propType.ToDisplayString() == "System.DateTime") return 64;
        if (propType.TypeKind == TypeKind.Enum) return GetEnumBits(propType, target.Symbol);
        if (propType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
        {
            return GetQuantizationConfig(target.Symbol, out _, out _, out _, out _, out var bits)
                ? bits
                : propType.SpecialType == SpecialType.System_Single ? 32 : 64;
        }
        if (propType.SpecialType == SpecialType.System_Decimal) return 128;
        if (propType.ToDisplayString() == "nint" || propType.ToDisplayString() == "System.IntPtr" ||
            propType.ToDisplayString() == "nuint" || propType.ToDisplayString() == "System.UIntPtr")
            return GetNumericBits(propType, target.Symbol, out _, out _, out _);
        if (propType.TypeKind == TypeKind.Interface) return -1;
        if (IsBitPacketType(propType))
            return GetPacketMaxBits(propType, GetSerializationTargets(propType), GetPacketVersion(propType), seenTypes);
        if (ExposesCustomSerialization(propType, out _)) return -1;
        if (propType.SpecialType is SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_SByte
            or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32
            or SpecialType.System_Int64)
            return GetNumericBits(propType, target.Symbol, out _, out _, out _);

        return -1;
    }

    private static int GetArrayMaxBits(SerializationTarget target, IArrayTypeSymbol arrayType, HashSet<string> seenTypes)
    {
        if (!TryGetArrayConfig(target.Symbol, arrayType, out var config, out _)) return -1;

        var elementTarget = new SerializationTarget($"{target.Name}Element", arrayType.ElementType, target.Symbol, null);
        var elementBits = GetTargetMaxBits(elementTarget, seenTypes);
        if (elementBits < 0) return -1;

        return config.IsFixed
            ? config.Count * elementBits
            : config.LengthBits + config.Count * elementBits;
    }

    private static bool TryGetUnionConfigForBitMath(SerializationTarget target, out UnionConfig config)
    {
        config = default;
        if (!HasDiscriminatorAttribute(target.Symbol)) return false;

        var variants = CollectVariants(target.Symbol);
        if (variants.Count == 0) return false;

        var maxIndex = 0;
        foreach (var variant in variants)
            if (variant.Index > maxIndex) maxIndex = variant.Index;

        var discriminatorAttr = target.Symbol.GetAttributes()
            .First(a => a.AttributeClass?.ToDisplayString() == "BitPack.BitDiscriminatorAttribute");
        var declaredBits = discriminatorAttr.ConstructorArguments.Length > 0 &&
                           discriminatorAttr.ConstructorArguments[0].Value is int declared
            ? declared
            : 0;
        var defaultVariant = discriminatorAttr.ConstructorArguments.Length > 1 &&
                             discriminatorAttr.ConstructorArguments[1].Value is int def
            ? def
            : 0;

        var discriminatorBits = declaredBits == 0
            ? Math.Max(1, CalculateBitsNeeded(maxIndex))
            : declaredBits;

        config = new UnionConfig(discriminatorBits, defaultVariant, variants);
        return true;
    }

    private static int GetUnionMaxBits(UnionConfig config, HashSet<string> seenTypes)
    {
        var maxVariantBits = 0;
        foreach (var variant in config.Variants)
        {
            var variantTarget = new SerializationTarget(variant.VariantType.Name, variant.VariantType,
                variant.VariantType, null);
            var variantBits = GetPacketMaxBits(variant.VariantType, GetSerializationTargets(variant.VariantType),
                GetPacketVersion(variant.VariantType), seenTypes);
            if (variantBits < 0) return -1;
            if (variantBits > maxVariantBits) maxVariantBits = variantBits;
        }

        return config.DiscriminatorBits + maxVariantBits;
    }

    private static string BuildLayoutManifest(ITypeSymbol typeSymbol, List<SerializationTarget> targets, int packetMaxVersion,
        int maxBits, int maxBytes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"type={typeSymbol.ToDisplayString()}");
        sb.AppendLine($"version={packetMaxVersion}");
        sb.AppendLine($"maxBits={maxBits}");
        sb.AppendLine($"maxBytes={maxBytes}");
        foreach (var target in targets)
        {
            var targetBits = GetTargetMaxBits(target, new HashSet<string> { typeSymbol.ToDisplayString() });
            sb.AppendLine(BuildLayoutManifestLine(target, targetBits));
        }

        return sb.ToString();
    }

    private static string BuildLayoutManifestLine(SerializationTarget target, int targetBits)
    {
        var line =
            $"field={target.Name};type={target.Type.ToDisplayString()};declaringType={target.Symbol.ContainingType.ToDisplayString()};key={target.FieldKey?.ToString() ?? ""};since={GetPropertyVersion(target.Symbol)}";

        line += GetCodecManifest(target.Type, target.Symbol);

        if (target.Type is IArrayTypeSymbol arrayType && TryGetArrayConfig(target.Symbol, arrayType, out var config, out _))
        {
            var elementTarget = new SerializationTarget($"{target.Name}Element", arrayType.ElementType, target.Symbol, null);
            var elementBits = GetTargetMaxBits(elementTarget, new HashSet<string>());
            line +=
                $";array={(config.IsFixed ? "fixed" : "max")};count={config.Count};elementType={arrayType.ElementType.ToDisplayString()};elementBits={elementBits}";
        }

        if (TryGetUnionConfigForBitMath(target, out var unionConfig))
            line +=
                $";union=discriminatorBits={unionConfig.DiscriminatorBits};defaultVariant={unionConfig.DefaultVariant};variants={string.Join(",", unionConfig.Variants.Select(v => v.VariantType.ToDisplayString() + "@" + v.Index))}";

        return $"{line};bits={targetBits}";
    }

    private static string GetCodecManifest(ITypeSymbol type, ISymbol symbol)
    {
        var effectiveType = type is IArrayTypeSymbol arrayType ? arrayType.ElementType : type;
        if (TryGetAngleConfig(effectiveType, symbol, out var angleConfig, out _))
            return $";codec=angle;codecBits={angleConfig.Bits}";
        if (TryGetVectorRangeConfig(effectiveType, symbol, out var vectorConfig, out _))
            return $";codec=vectorRange;min={vectorConfig.Min};max={vectorConfig.Max};decimals={vectorConfig.Decimals};componentBits={vectorConfig.Bits}";
        if (TryGetQuaternionSmallestThreeConfig(effectiveType, symbol, out var quaternionConfig, out _))
            return $";codec=quaternionSmallestThree;componentBits={quaternionConfig.BitsPerComponent}";

        return string.Empty;
    }

    private static uint ComputeFnv1A32(string value)
    {
        unchecked
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;
            var hash = offsetBasis;
            foreach (var b in Encoding.UTF8.GetBytes(value))
            {
                hash ^= b;
                hash *= prime;
            }

            return hash;
        }
    }

    private static string EscapeStringLiteral(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static void ReportError(SourceProductionContext context, ISymbol prop)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            UnsupportedTypeError,
            prop.Locations.FirstOrDefault(),
            prop.Name,
            prop is IPropertySymbol p
                ? p.Type.ToDisplayString()
                : prop is IFieldSymbol f
                    ? f.Type.ToDisplayString()
                    : ""));
    }

    private static void ReportWarning(SourceProductionContext context, ISymbol prop)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            UnconstrainedPropertyWarning,
            prop.Locations.FirstOrDefault(),
            prop.Name,
            prop is IPropertySymbol p
                ? p.Type.ToDisplayString()
                : prop is IFieldSymbol f
                    ? f.Type.ToDisplayString()
                    : ""));
    }

    private static int GetPacketVersion(ITypeSymbol typeSymbol)
    {
        var attr = typeSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "BitPack.BitPacketAttribute");
        if (attr != null && attr.ConstructorArguments.Length > 0 &&
            attr.ConstructorArguments[0].Value is int v) return v;

        return 1;
    }

    private static int GetPropertyVersion(ISymbol symbol)
    {
        var attr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "BitPack.SinceVersionAttribute");
        if (attr != null && attr.ConstructorArguments.Length > 0 &&
            attr.ConstructorArguments[0].Value is int v) return v;

        return 1;
    }
}
