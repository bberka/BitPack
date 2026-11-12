// Discriminated-union ([BitVariant]) config and emitter.
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
    private static bool HasDiscriminatorAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "BitPack.BitDiscriminatorAttribute");
    }

    private static List<BitVariantEntry> CollectVariants(ISymbol symbol)
    {
        var variants = new List<BitVariantEntry>();
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != "BitPack.BitVariantAttribute") continue;
            if (attr.ConstructorArguments.Length < 2) continue;

            var variantTypeArg = attr.ConstructorArguments[0].Value as ITypeSymbol;
            if (variantTypeArg == null) continue;

            if (attr.ConstructorArguments[1].Value is not int index) continue;

            variants.Add(new BitVariantEntry(variantTypeArg, index));
        }

        return variants;
    }

    private static bool TryGetUnionConfig(SourceProductionContext context, SerializationTarget target,
        out UnionConfig config)
    {
        config = default;
        if (!HasDiscriminatorAttribute(target.Symbol)) return false;

        var variants = CollectVariants(target.Symbol);
        if (variants.Count == 0)
        {
            ReportUnionError(context, target.Symbol,
                "declares [BitDiscriminator] but has no [BitVariant] entries.");
            return false;
        }

        foreach (var variant in variants)
        {
            if (!IsBitPacketType(variant.VariantType))
            {
                ReportUnionError(context, target.Symbol,
                    $"variant '{variant.VariantType.ToDisplayString()}' must be marked with [BitPacket].");
                return false;
            }
        }

        var indexSet = new HashSet<int>();
        foreach (var variant in variants)
        {
            if (variant.Index < 0)
            {
                ReportUnionError(context, target.Symbol,
                    $"variant '{variant.VariantType.ToDisplayString()}' has a negative discriminator index.");
                return false;
            }

            if (!indexSet.Add(variant.Index))
            {
                ReportUnionError(context, target.Symbol,
                    $"duplicate discriminator index {variant.Index} for variant '{variant.VariantType.ToDisplayString()}'.");
                return false;
            }
        }

        var maxIndex = indexSet.Max();
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

        if (maxIndex >= (1L << discriminatorBits))
        {
            ReportUnionError(context, target.Symbol,
                $"discriminator has {discriminatorBits} bits but variant index {maxIndex} requires more.");
            return false;
        }

        if (defaultVariant < 0 || defaultVariant >= (1L << discriminatorBits))
        {
            ReportUnionError(context, target.Symbol,
                $"defaultVariant index {defaultVariant} is outside the declared discriminator range.");
            return false;
        }

        config = new UnionConfig(discriminatorBits, defaultVariant, variants);
        return true;
    }

    private static void ReportUnionError(SourceProductionContext context, ISymbol symbol, string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            UnionError,
            symbol.Locations.FirstOrDefault(),
            symbol.Name,
            message));
    }

    private static void GenerateUnionSerialization(StringBuilder sb, SerializationTarget target, UnionConfig config,
        bool isWriting, string targetVarName, bool useSpan)
    {
        var slotVar = $"union_{SanitizeIdentifier(target.Name)}";
        var suffix = SanitizeIdentifier(target.Name);

        if (isWriting)
        {
            sb.AppendLine($"        if ({targetVarName} == null)");
            sb.AppendLine("        {");
            sb.AppendLine($"            writer.WriteInt({config.DefaultVariant}, {config.DiscriminatorBits});");
            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");
            sb.AppendLine($"            var {slotVar} = {config.DefaultVariant};");
            foreach (var variant in config.Variants)
                sb.AppendLine(
                    $"            if ({targetVarName} is {variant.VariantType.ToDisplayString()} __pick_{suffix}_{variant.Index}) {slotVar} = {variant.Index};");
            sb.AppendLine($"            writer.WriteInt({slotVar}, {config.DiscriminatorBits});");
            foreach (var variant in config.Variants)
            {
                sb.AppendLine($"            if ({targetVarName} is {variant.VariantType.ToDisplayString()} __typed_{suffix}_{variant.Index})");
                sb.AppendLine("            {");
                if (useSpan)
                    sb.AppendLine($"                __typed_{suffix}_{variant.Index}.Serialize(ref writer);");
                else
                    sb.AppendLine($"                __typed_{suffix}_{variant.Index}.Serialize(writer);");
                sb.AppendLine("            }");
            }

            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine($"        var discriminator_{suffix} = reader.ReadInt({config.DiscriminatorBits});");
            sb.AppendLine(
                $"        if (discriminator_{suffix} < 0 || discriminator_{suffix} >= {1L << config.DiscriminatorBits}) discriminator_{suffix} = {config.DefaultVariant};");
            for (var i = 0; i < config.Variants.Count; i++)
            {
                var variant = config.Variants[i];
                var keyword = i == 0 ? "if" : "else if";
                sb.AppendLine($"        {keyword} (discriminator_{suffix} == {variant.Index})");
                sb.AppendLine("        {");
                if (useSpan)
                    sb.AppendLine($"            {targetVarName} = {variant.VariantType.ToDisplayString()}.Read(ref reader);");
                else
                    sb.AppendLine($"            {targetVarName} = {variant.VariantType.ToDisplayString()}.Read(reader);");
                sb.AppendLine("        }");
            }
        }
    }
}
