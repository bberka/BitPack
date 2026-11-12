// Source emitters for serialize/deserialize/delta, including math codecs and arrays.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BitPack.Generator;

public partial class PacketGenerator
{
    private static void GenerateSerializationLoop(SourceProductionContext context, StringBuilder sb,
        List<SerializationTarget> targets, bool isWriting, int packetMaxVersion,
        bool skipValidation, bool useSpan = false)
    {
        foreach (var target in targets)
        {
            var propVersion = GetPropertyVersion(target.Symbol);
            if (propVersion > packetMaxVersion)
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedTypeError,
                    target.Symbol.Locations.FirstOrDefault(),
                    target.Name,
                    $"Property version {propVersion} exceeds packet version {packetMaxVersion}."));

            var propSb = new StringBuilder();

            if (isWriting)
            {
                GeneratePropertySerialization(context, propSb, target, true, $"this.{target.Name}", skipValidation, useSpan);
            }
            else
            {
                if (IsWritableProperty(target.Symbol))
                {
                    GeneratePropertySerialization(context, propSb, target, false, $"this.{target.Name}", false, useSpan);
                }
                else
                {
                    propSb.AppendLine(
                        $"        // Read and discard init-only / read-only property '{target.Name}' during instance Deserialize");
                    propSb.AppendLine($"        {target.Type.ToDisplayString()} discard_{target.Name};");
                    GeneratePropertySerialization(context, propSb, target, false, $"discard_{target.Name}", false, useSpan);
                }
            }

            if (!isWriting && propVersion > 1)
            {
                sb.AppendLine($"        if (packetVersion >= {propVersion})");
                sb.AppendLine("        {");
                var lines = propSb.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine("    " + line);

                sb.AppendLine("        }");
            }
            else
            {
                sb.Append(propSb);
            }
        }
    }

    private static void GenerateDeltaSerializationLoop(SourceProductionContext context, StringBuilder sb,
        List<SerializationTarget> targets, int packetMaxVersion, bool skipValidation,
        bool useSpan = false)
    {
        foreach (var target in targets)
        {
            var propVersion = GetPropertyVersion(target.Symbol);
            if (propVersion > packetMaxVersion)
                context.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedTypeError,
                    target.Symbol.Locations.FirstOrDefault(),
                    target.Name,
                    $"Property version {propVersion} exceeds packet version {packetMaxVersion}."));

            var changedVar = $"changed_{SanitizeIdentifier(target.Name)}";
            var currentValue = $"this.{target.Name}";
            var baselineValue = $"baseline.{target.Name}";
            sb.AppendLine($"        var {changedVar} = !({GetEqualityExpression(target, currentValue, baselineValue)});");
            sb.AppendLine($"        writer.WriteBool({changedVar});");
            sb.AppendLine($"        if ({changedVar})");
            sb.AppendLine("        {");

            var propSb = new StringBuilder();
            GeneratePropertySerialization(context, propSb, target, true, currentValue, skipValidation, useSpan);
            AppendIndentedNonBlankLines(sb, propSb, "    ");

            sb.AppendLine("        }");
        }
    }

    private static void GenerateBaselineReadVariables(StringBuilder sb, List<SerializationTarget> targets)
    {
        foreach (var target in targets)
            sb.AppendLine($"        var read_{target.Name} = {GetBaselineReadExpression(target)};");
    }

    private static void GenerateDeltaReadLoop(SourceProductionContext context, StringBuilder sb,
        List<SerializationTarget> targets, int packetMaxVersion, bool useSpan = false)
    {
        foreach (var target in targets)
        {
            var propVersion = GetPropertyVersion(target.Symbol);
            if (propVersion > packetMaxVersion) continue;

            var changedBlock = new StringBuilder();
            changedBlock.AppendLine("        if (reader.ReadBool())");
            changedBlock.AppendLine("        {");

            var propSb = new StringBuilder();
            GeneratePropertySerialization(context, propSb, target, false, $"read_{target.Name}", false, useSpan);
            AppendIndentedNonBlankLines(changedBlock, propSb, "    ");
            changedBlock.AppendLine("        }");

            if (propVersion > 1)
            {
                sb.AppendLine($"        if (packetVersion >= {propVersion})");
                sb.AppendLine("        {");
                AppendIndentedNonBlankLines(sb, changedBlock, "    ");
                sb.AppendLine("        }");
            }
            else
            {
                sb.Append(changedBlock);
            }
        }
    }

    private static string GetEqualityExpression(SerializationTarget target, string left, string right)
    {
        if (target.Type is IArrayTypeSymbol arrayType)
        {
            var elementTypeName = arrayType.ElementType.ToDisplayString();
            return $"global::System.Linq.Enumerable.SequenceEqual({left} ?? global::System.Array.Empty<{elementTypeName}>(), {right} ?? global::System.Array.Empty<{elementTypeName}>())";
        }

        return $"global::System.Collections.Generic.EqualityComparer<{target.Type.ToDisplayString()}>.Default.Equals({left}, {right})";
    }

    private static string GetBaselineReadExpression(SerializationTarget target)
    {
        if (target.Type is IArrayTypeSymbol)
            return $"baseline.{target.Name} == null ? null : ({target.Type.ToDisplayString()})baseline.{target.Name}.Clone()";

        return $"baseline.{target.Name}";
    }

    private static void GeneratePacketConstruction(StringBuilder sourceBuilder, string typeName,
        List<SerializationTarget> targets, IMethodSymbol? ctor, List<SerializationTarget> ctorArgs)
    {
        if (ctor != null)
        {
            sourceBuilder.Append($"        var packet = new {typeName}(");
            for (var i = 0; i < ctorArgs.Count; i++)
            {
                sourceBuilder.Append($"read_{ctorArgs[i].Name}");
                if (i < ctorArgs.Count - 1) sourceBuilder.Append(", ");
            }

            sourceBuilder.AppendLine(")");
            sourceBuilder.AppendLine("        {");
            foreach (var target in targets)
                if (!ctorArgs.Contains(target) && IsWritablePropertyForInitializer(target.Symbol))
                    sourceBuilder.AppendLine($"            {target.Name} = read_{target.Name},");
            sourceBuilder.AppendLine("        };");
        }
        else
        {
            sourceBuilder.AppendLine($"        var packet = new {typeName}");
            sourceBuilder.AppendLine("        {");
            foreach (var target in targets)
                if (IsWritablePropertyForInitializer(target.Symbol))
                    sourceBuilder.AppendLine($"            {target.Name} = read_{target.Name},");
            sourceBuilder.AppendLine("        };");
        }
    }

    private static void AppendIndentedNonBlankLines(StringBuilder target, StringBuilder source, string indent)
    {
        foreach (var line in source.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            if (!string.IsNullOrWhiteSpace(line))
                target.AppendLine(indent + line);
    }

    private static void GeneratePropertySerialization(SourceProductionContext context, StringBuilder sb,
        SerializationTarget target, bool isWriting, string targetVarName, bool skipValidation = false,
        bool useSpan = false)
    {
        var propType = target.Type;

        if (propType is IArrayTypeSymbol arrayType)
        {
            GenerateArraySerialization(context, sb, target, arrayType, isWriting, targetVarName, skipValidation, useSpan);
            return;
        }

        if (TryGetAngleConfig(propType, target.Symbol, out var angleConfig, out _))
        {
            GenerateAngleSerialization(sb, propType, target, angleConfig, isWriting, targetVarName, skipValidation);
            return;
        }

        if (TryGetVectorRangeConfig(propType, target.Symbol, out var vectorConfig, out _))
        {
            GenerateVectorRangeSerialization(sb, propType, target, vectorConfig, isWriting, targetVarName, skipValidation);
            return;
        }

        if (TryGetQuaternionSmallestThreeConfig(propType, target.Symbol, out var quaternionConfig, out _))
        {
            GenerateQuaternionSmallestThreeSerialization(sb, target, quaternionConfig, isWriting, targetVarName);
            return;
        }

        if (TryGetUnionConfig(context, target, out var unionConfig))
        {
            GenerateUnionSerialization(sb, target, unionConfig, isWriting, targetVarName, useSpan);
            return;
        }

        // Check for custom serialization or bit packet nested properties
        var isBitPacket = IsBitPacketType(propType);
        var hasCustom = ExposesCustomSerialization(propType, out var staticMethodName);

        // 1. Boolean
        if (propType.SpecialType == SpecialType.System_Boolean)
        {
            if (isWriting)
                sb.AppendLine($"        writer.WriteBool({targetVarName});");
            else
                sb.AppendLine($"        {targetVarName} = reader.ReadBool();");
        }
        // 2. Char
        else if (propType.SpecialType == SpecialType.System_Char)
        {
            if (isWriting)
                sb.AppendLine($"        writer.WriteInt((int){targetVarName}, 16);");
            else
                sb.AppendLine($"        {targetVarName} = (char)reader.ReadInt(16);");
        }
        // 3. String
        else if (propType.SpecialType == SpecialType.System_String)
        {
            var maxLen = GetMaxLen(target.Symbol, out var hasConstraint);
            if (!hasConstraint && isWriting && !skipValidation) ReportWarning(context, target.Symbol);

            if (isWriting)
                sb.AppendLine(
                    $"        writer.WriteString({targetVarName}, {maxLen}, {CalculateBitsNeeded(maxLen * 4)});");
            else
                sb.AppendLine(
                    $"        {targetVarName} = reader.ReadString({maxLen}, {CalculateBitsNeeded(maxLen * 4)});");
        }
        // 4. DateTime
        else if (propType.ToDisplayString() == "System.DateTime")
        {
            if (isWriting)
                sb.AppendLine($"        writer.WriteDateTime({targetVarName});");
            else
                sb.AppendLine($"        {targetVarName} = reader.ReadDateTime();");
        }
        // 5. Enum
        else if (propType.TypeKind == TypeKind.Enum)
        {
            var bits = GetEnumBits(propType, target.Symbol);
            if (isWriting)
            {
                // Extract range for bounds checking
                long enumMin = 0, enumMax = 0;
                var rangeAttr = target.Symbol.GetAttributes()
                    .FirstOrDefault(a =>
                        a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.RangeAttribute");
                if (rangeAttr != null && rangeAttr.ConstructorArguments.Length >= 2)
                {
                    try
                    {
                        enumMin = Convert.ToInt64(rangeAttr.ConstructorArguments[0].Value);
                    }
                    catch
                    {
                    }

                    try
                    {
                        enumMax = Convert.ToInt64(rangeAttr.ConstructorArguments[1].Value);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    foreach (var field in propType.GetMembers().OfType<IFieldSymbol>())
                        if (field.HasConstantValue)
                        {
                            var val = Convert.ToInt64(field.ConstantValue);
                            if (val > enumMax) enumMax = val;
                            if (val < enumMin) enumMin = val;
                        }
                }

                if (!skipValidation)
                    sb.AppendLine(
                        $"        if ((int){targetVarName} < {enumMin} || (int){targetVarName} > {enumMax}) throw new ArgumentOutOfRangeException(nameof({targetVarName}), (object){targetVarName}, $\"Enum value must be within [{enumMin}, {enumMax}].\");");
                sb.AppendLine($"        writer.WriteInt((int){targetVarName}, {bits});");
            }
            else
            {
                var enumFields = propType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).ToList();
                var enumTypeName = propType.ToDisplayString();
                long maxVal = 0;
                long defaultVal = 0;
                var hasZero = false;
                foreach (var field in enumFields)
                {
                    var val = Convert.ToInt64(field.ConstantValue);
                    if (val > maxVal) maxVal = val;
                    if (val == 0) hasZero = true;
                }

                if (!hasZero && enumFields.Count > 0)
                    defaultVal = Convert.ToInt64(enumFields[0].ConstantValue);
                sb.AppendLine($"        var temp{target.Name} = reader.ReadInt({bits});");
                sb.AppendLine(
                    $"        {targetVarName} = temp{target.Name} <= {maxVal} ? ({enumTypeName})temp{target.Name} : ({enumTypeName}){defaultVal};");
            }
        }
        // 6. Floating-point (float, double) with fixed-point quantization
        else if (propType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
        {
            var isQuantized = GetQuantizationConfig(target.Symbol, out var minVal, out var maxVal, out var decimals,
                out var scale, out var bits);

            if (isQuantized)
            {
                var castStr = propType.SpecialType == SpecialType.System_Single ? "float" : "double";
                if (isWriting)
                {
                    if (!skipValidation)
                        sb.AppendLine(
                            $"        if ({targetVarName} < {minVal} || {targetVarName} > {maxVal}) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {targetVarName}, $\"Value must be within [{minVal}, {maxVal}].\");");
                    sb.AppendLine(
                        $"        writer.WriteLong((long)(({targetVarName} - ({minVal})) * {scale}d + 0.5d), {bits});");
                }
                else
                {
                    sb.AppendLine(
                        $"        {targetVarName} = ({castStr})((double)reader.ReadLong({bits}) / {scale}d + ({minVal}));");
                }
            }
            else
            {
                // Fall back to full float/double representation
                if (isWriting)
                {
                    if (!skipValidation) ReportWarning(context, target.Symbol);
                    if (propType.SpecialType == SpecialType.System_Single)
                        sb.AppendLine($"        writer.WriteFloat({targetVarName});");
                    else
                        sb.AppendLine($"        writer.WriteDouble({targetVarName});");
                }
                else
                {
                    if (propType.SpecialType == SpecialType.System_Single)
                        sb.AppendLine($"        {targetVarName} = reader.ReadFloat();");
                    else
                        sb.AppendLine($"        {targetVarName} = reader.ReadDouble();");
                }
            }
        }
        // 6.5. Decimal
        else if (propType.SpecialType == SpecialType.System_Decimal)
        {
            if (isWriting)
                sb.AppendLine($"        writer.WriteDecimal({targetVarName});");
            else
                sb.AppendLine($"        {targetVarName} = reader.ReadDecimal();");
        }
        // 6.6. Native integers (nint, nuint, IntPtr, UIntPtr)
        else if (propType.ToDisplayString() == "nint" || propType.ToDisplayString() == "System.IntPtr")
        {
            var bits = GetNumericBits(propType, target.Symbol, out var hasConstraint, out var minVal, out var maxVal);
            if (isWriting)
            {
                if (hasConstraint)
                {
                    if (!skipValidation)
                        sb.AppendLine(
                            $"        if ({targetVarName} < {minVal} || {targetVarName} > {maxVal}) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {targetVarName}, $\"Value must be within [{minVal}, {maxVal}].\");");
                    sb.AppendLine($"        writer.WriteLong((long)({targetVarName} - ({minVal})), {bits});");
                }
                else
                {
                    sb.AppendLine($"        writer.WriteLong((long){targetVarName}, {bits});");
                }
            }
            else
            {
                if (hasConstraint)
                    sb.AppendLine($"        {targetVarName} = (nint)(reader.ReadLong({bits}) + ({minVal}));");
                else
                    sb.AppendLine($"        {targetVarName} = (nint)reader.ReadLong({bits});");
            }
        }
        else if (propType.ToDisplayString() == "nuint" || propType.ToDisplayString() == "System.UIntPtr")
        {
            var bits = GetNumericBits(propType, target.Symbol, out var hasConstraint, out var minVal, out var maxVal);
            if (isWriting)
            {
                if (hasConstraint)
                {
                    if (!skipValidation)
                        sb.AppendLine(
                            $"        if ({targetVarName} < {minVal} || {targetVarName} > {maxVal}) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {targetVarName}, $\"Value must be within [{minVal}, {maxVal}].\");");
                    sb.AppendLine($"        writer.WriteULong((ulong)({targetVarName} - ({minVal})), {bits});");
                }
                else
                {
                    sb.AppendLine($"        writer.WriteULong((ulong){targetVarName}, {bits});");
                }
            }
            else
            {
                if (hasConstraint)
                    sb.AppendLine($"        {targetVarName} = (nuint)(reader.ReadULong({bits}) + ({minVal}));");
                else
                    sb.AppendLine($"        {targetVarName} = (nuint)reader.ReadULong({bits});");
            }
        }
        // 6.7. Interface Types
        else if (propType.TypeKind == TypeKind.Interface)
        {
            var implementsBitSerializable =
                propType.AllInterfaces.Any(i => i.ToDisplayString() == "BitPack.IBitSerializable") ||
                propType.ToDisplayString() == "BitPack.IBitSerializable";
            if (implementsBitSerializable)
            {
                if (isWriting)
                    sb.AppendLine($"        {targetVarName}.Serialize(writer);");
                else
                    sb.AppendLine($"        {targetVarName}.Deserialize(reader);");
            }
            else
            {
                if (isWriting && !skipValidation)
                    ReportError(context, target.Symbol);
                sb.AppendLine(
                    $"        // ERROR: Interface property '{targetVarName}' of type '{propType.ToDisplayString()}' must implement IBitSerializable.");
            }
        }
        // 7. Nested Packets or Custom Value Objects
        else if (isBitPacket || hasCustom)
        {
            if (isWriting)
            {
                sb.AppendLine($"        {targetVarName}.Serialize(writer);");
            }
            else
            {
                if (isBitPacket)
                {
                    sb.AppendLine($"        {targetVarName} = {propType.ToDisplayString()}.Read(reader);");
                }
                else if (staticMethodName != null)
                {
                    sb.AppendLine(
                        $"        {targetVarName} = {propType.ToDisplayString()}.{staticMethodName}(reader);");
                }
                else
                {
                    sb.AppendLine($"        {targetVarName} = new {propType.ToDisplayString()}();");
                    sb.AppendLine($"        {targetVarName}.Deserialize(reader);");
                }
            }
        }
        // 8. Unsigned integers (uint, ulong)
        else if (propType.SpecialType is SpecialType.System_UInt32 or SpecialType.System_UInt64)
        {
            var bits = GetNumericBits(propType, target.Symbol, out var hasConstraint, out var minVal, out var maxVal);
            if (!hasConstraint && isWriting && !skipValidation) ReportWarning(context, target.Symbol);

            if (isWriting)
            {
                if (hasConstraint)
                {
                    if (!skipValidation)
                        sb.AppendLine(
                            $"        if ({targetVarName} < {minVal} || {targetVarName} > {maxVal}) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {targetVarName}, $\"Value must be within [{minVal}, {maxVal}].\");");
                    sb.AppendLine($"        writer.WriteULong((ulong)({targetVarName} - ({minVal})), {bits});");
                }
                else
                {
                    sb.AppendLine($"        writer.WriteULong((ulong){targetVarName}, {bits});");
                }
            }
            else
            {
                if (hasConstraint)
                    sb.AppendLine(
                        $"        {targetVarName} = ({propType.ToDisplayString()})(reader.ReadULong({bits}) + ({minVal}));");
                else
                    sb.AppendLine($"        {targetVarName} = ({propType.ToDisplayString()})reader.ReadULong({bits});");
            }
        }
        // 9. Signed or small integers (sbyte, byte, short, ushort, int, long)
        else if (propType.SpecialType is SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
                 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_Int64)
        {
            var bits = GetNumericBits(propType, target.Symbol, out var hasConstraint, out var minVal, out var maxVal);
            if (!hasConstraint && isWriting && !skipValidation) ReportWarning(context, target.Symbol);

            if (isWriting)
            {
                if (hasConstraint)
                {
                    if (!skipValidation)
                        sb.AppendLine(
                            $"        if ({targetVarName} < {minVal} || {targetVarName} > {maxVal}) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {targetVarName}, $\"Value must be within [{minVal}, {maxVal}].\");");
                    sb.AppendLine($"        writer.WriteLong((long)({targetVarName} - ({minVal})), {bits});");
                }
                else
                {
                    sb.AppendLine($"        writer.WriteLong((long){targetVarName}, {bits});");
                }
            }
            else
            {
                if (hasConstraint)
                    sb.AppendLine(
                        $"        {targetVarName} = ({propType.ToDisplayString()})(reader.ReadLong({bits}) + ({minVal}));");
                else
                    sb.AppendLine($"        {targetVarName} = ({propType.ToDisplayString()})reader.ReadLong({bits});");
            }
        }
        // 10. Unsupported complex structures (raise build-time Error BP0001)
        else
        {
            if (isWriting && !skipValidation) ReportError(context, target.Symbol);

            sb.AppendLine(
                $"        // ERROR: Property '{targetVarName}' of type '{propType.ToDisplayString()}' is not packable.");
        }
    }

    private static void GenerateAngleSerialization(StringBuilder sb, ITypeSymbol propType, SerializationTarget target,
        AngleConfig config, bool isWriting, string targetVarName, bool skipValidation)
    {
        var suffix = SanitizeIdentifier(target.Name);
        var scale = 1L << config.Bits;
        var maxEncoded = scale - 1;
        var cast = propType.SpecialType == SpecialType.System_Single ? "float" : "double";

        if (isWriting)
        {
            if (!skipValidation)
                sb.AppendLine(
                    $"        if ({targetVarName} < 0d || {targetVarName} >= 360d) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {targetVarName}, \"Angle must be in the range [0, 360).\");");
            sb.AppendLine(
                $"        var encodedAngle_{suffix} = (long)Math.Floor(((double){targetVarName} * {scale}d / 360d) + 0.5d);");
            sb.AppendLine($"        if (encodedAngle_{suffix} > {maxEncoded}L) encodedAngle_{suffix} = {maxEncoded}L;");
            sb.AppendLine($"        writer.WriteLong(encodedAngle_{suffix}, {config.Bits});");
        }
        else
        {
            sb.AppendLine($"        {targetVarName} = ({cast})((double)reader.ReadLong({config.Bits}) * 360d / {scale}d);");
        }
    }

    private static void GenerateVectorRangeSerialization(StringBuilder sb, ITypeSymbol propType,
        SerializationTarget target, VectorRangeConfig config, bool isWriting, string targetVarName, bool skipValidation)
    {
        var suffix = SanitizeIdentifier(target.Name);
        var isVector3 = propType.ToDisplayString() == "System.Numerics.Vector3";

        if (isWriting)
        {
            GenerateVectorComponentWrite(sb, targetVarName, "X", config, skipValidation);
            GenerateVectorComponentWrite(sb, targetVarName, "Y", config, skipValidation);
            if (isVector3) GenerateVectorComponentWrite(sb, targetVarName, "Z", config, skipValidation);
        }
        else
        {
            GenerateVectorComponentRead(sb, "X", suffix, config);
            GenerateVectorComponentRead(sb, "Y", suffix, config);
            if (isVector3) GenerateVectorComponentRead(sb, "Z", suffix, config);
            sb.AppendLine(isVector3
                ? $"        {targetVarName} = new System.Numerics.Vector3((float)decoded_{suffix}_X, (float)decoded_{suffix}_Y, (float)decoded_{suffix}_Z);"
                : $"        {targetVarName} = new System.Numerics.Vector2((float)decoded_{suffix}_X, (float)decoded_{suffix}_Y);");
        }
    }

    private static void GenerateVectorComponentWrite(StringBuilder sb, string targetVarName, string component,
        VectorRangeConfig config, bool skipValidation)
    {
        var componentAccess = $"{targetVarName}.{component}";
        if (!skipValidation)
            sb.AppendLine(
                $"        if ({componentAccess} < {config.Min}d || {componentAccess} > {config.Max}d) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {componentAccess}, \"Vector component must be within [{config.Min}, {config.Max}].\");");
        sb.AppendLine(
            $"        writer.WriteLong((long)(((double){componentAccess} - ({config.Min}d)) * {config.Scale}d + 0.5d), {config.Bits});");
    }

    private static void GenerateVectorComponentRead(StringBuilder sb, string component, string suffix,
        VectorRangeConfig config)
    {
        sb.AppendLine(
            $"        var decoded_{suffix}_{component} = (double)reader.ReadLong({config.Bits}) / {config.Scale}d + ({config.Min}d);");
    }

    private static void GenerateQuaternionSmallestThreeSerialization(StringBuilder sb, SerializationTarget target,
        QuaternionSmallestThreeConfig config, bool isWriting, string targetVarName)
    {
        var suffix = SanitizeIdentifier(target.Name);
        var maxEncoded = (1L << config.BitsPerComponent) - 1;
        const string limit = "0.7071067811865476d";

        if (isWriting)
        {
            sb.AppendLine($"        var q_{suffix} = {targetVarName};");
            sb.AppendLine(
                $"        var lenSq_{suffix} = (double)q_{suffix}.X * q_{suffix}.X + (double)q_{suffix}.Y * q_{suffix}.Y + (double)q_{suffix}.Z * q_{suffix}.Z + (double)q_{suffix}.W * q_{suffix}.W;");
            sb.AppendLine(
                $"        if (lenSq_{suffix} <= 0d) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {targetVarName}, \"Quaternion length must be greater than zero.\");");
            sb.AppendLine($"        var invLen_{suffix} = 1d / Math.Sqrt(lenSq_{suffix});");
            sb.AppendLine($"        var qx_{suffix} = q_{suffix}.X * invLen_{suffix};");
            sb.AppendLine($"        var qy_{suffix} = q_{suffix}.Y * invLen_{suffix};");
            sb.AppendLine($"        var qz_{suffix} = q_{suffix}.Z * invLen_{suffix};");
            sb.AppendLine($"        var qw_{suffix} = q_{suffix}.W * invLen_{suffix};");
            sb.AppendLine($"        var largestIndex_{suffix} = 0;");
            sb.AppendLine($"        var largestAbs_{suffix} = Math.Abs(qx_{suffix});");
            sb.AppendLine($"        if (Math.Abs(qy_{suffix}) > largestAbs_{suffix}) {{ largestIndex_{suffix} = 1; largestAbs_{suffix} = Math.Abs(qy_{suffix}); }}");
            sb.AppendLine($"        if (Math.Abs(qz_{suffix}) > largestAbs_{suffix}) {{ largestIndex_{suffix} = 2; largestAbs_{suffix} = Math.Abs(qz_{suffix}); }}");
            sb.AppendLine($"        if (Math.Abs(qw_{suffix}) > largestAbs_{suffix}) largestIndex_{suffix} = 3;");
            sb.AppendLine($"        var largestValue_{suffix} = largestIndex_{suffix} == 0 ? qx_{suffix} : largestIndex_{suffix} == 1 ? qy_{suffix} : largestIndex_{suffix} == 2 ? qz_{suffix} : qw_{suffix};");
            sb.AppendLine($"        if (largestValue_{suffix} < 0d) {{ qx_{suffix} = -qx_{suffix}; qy_{suffix} = -qy_{suffix}; qz_{suffix} = -qz_{suffix}; qw_{suffix} = -qw_{suffix}; }}");
            sb.AppendLine($"        writer.WriteInt(largestIndex_{suffix}, 2);");
            GenerateQuaternionComponentWrite(sb, "x", suffix, config, maxEncoded, limit);
            GenerateQuaternionComponentWrite(sb, "y", suffix, config, maxEncoded, limit);
            GenerateQuaternionComponentWrite(sb, "z", suffix, config, maxEncoded, limit);
            GenerateQuaternionComponentWrite(sb, "w", suffix, config, maxEncoded, limit);
        }
        else
        {
            sb.AppendLine($"        var largestIndex_{suffix} = reader.ReadInt(2);");
            sb.AppendLine($"        var qx_{suffix} = 0d;");
            sb.AppendLine($"        var qy_{suffix} = 0d;");
            sb.AppendLine($"        var qz_{suffix} = 0d;");
            sb.AppendLine($"        var qw_{suffix} = 0d;");
            GenerateQuaternionComponentRead(sb, "x", suffix, config, maxEncoded, limit);
            GenerateQuaternionComponentRead(sb, "y", suffix, config, maxEncoded, limit);
            GenerateQuaternionComponentRead(sb, "z", suffix, config, maxEncoded, limit);
            GenerateQuaternionComponentRead(sb, "w", suffix, config, maxEncoded, limit);
            sb.AppendLine($"        var omitted_{suffix} = Math.Sqrt(Math.Max(0d, 1d - (qx_{suffix} * qx_{suffix} + qy_{suffix} * qy_{suffix} + qz_{suffix} * qz_{suffix} + qw_{suffix} * qw_{suffix})));");
            sb.AppendLine($"        if (largestIndex_{suffix} == 0) qx_{suffix} = omitted_{suffix};");
            sb.AppendLine($"        else if (largestIndex_{suffix} == 1) qy_{suffix} = omitted_{suffix};");
            sb.AppendLine($"        else if (largestIndex_{suffix} == 2) qz_{suffix} = omitted_{suffix};");
            sb.AppendLine($"        else qw_{suffix} = omitted_{suffix};");
            sb.AppendLine($"        {targetVarName} = new System.Numerics.Quaternion((float)qx_{suffix}, (float)qy_{suffix}, (float)qz_{suffix}, (float)qw_{suffix});");
        }
    }

    private static void GenerateQuaternionComponentWrite(StringBuilder sb, string component, string suffix,
        QuaternionSmallestThreeConfig config, long maxEncoded, string limit)
    {
        var index = component == "x" ? 0 : component == "y" ? 1 : component == "z" ? 2 : 3;
        sb.AppendLine($"        if (largestIndex_{suffix} != {index})");
        sb.AppendLine("        {");
        sb.AppendLine($"        var encodedQ_{suffix}_{component} = (long)((((q{component}_{suffix} + {limit}) / (2d * {limit})) * {maxEncoded}d) + 0.5d);");
        sb.AppendLine($"        if (encodedQ_{suffix}_{component} < 0L) encodedQ_{suffix}_{component} = 0L;");
        sb.AppendLine($"        else if (encodedQ_{suffix}_{component} > {maxEncoded}L) encodedQ_{suffix}_{component} = {maxEncoded}L;");
        sb.AppendLine($"        writer.WriteLong(encodedQ_{suffix}_{component}, {config.BitsPerComponent});");
        sb.AppendLine("        }");
    }

    private static void GenerateQuaternionComponentRead(StringBuilder sb, string component, string suffix,
        QuaternionSmallestThreeConfig config, long maxEncoded, string limit)
    {
        var index = component == "x" ? 0 : component == "y" ? 1 : component == "z" ? 2 : 3;
        sb.AppendLine($"        if (largestIndex_{suffix} != {index}) q{component}_{suffix} = (double)reader.ReadLong({config.BitsPerComponent}) / {maxEncoded}d * (2d * {limit}) - {limit};");
    }

    private static void GenerateArraySerialization(SourceProductionContext context, StringBuilder sb,
        SerializationTarget target, IArrayTypeSymbol arrayType, bool isWriting, string targetVarName,
        bool skipValidation, bool useSpan)
    {
        if (!TryGetArrayConfig(target.Symbol, arrayType, out var config, out var error))
        {
            if (isWriting && !skipValidation)
                context.ReportDiagnostic(Diagnostic.Create(
                    BoundedArrayError,
                    target.Symbol.Locations.FirstOrDefault(),
                    target.Name,
                    error));

            sb.AppendLine(
                $"        // ERROR: Array property '{targetVarName}' of type '{arrayType.ToDisplayString()}' must be a supported bounded array.");
            return;
        }

        var elementTypeName = arrayType.ElementType.ToDisplayString();
        var elementTarget = new SerializationTarget($"{target.Name}Element", arrayType.ElementType, target.Symbol, null);

        if (isWriting)
        {
            var valueVar = $"value_{SanitizeIdentifier(target.Name)}";
            var indexVar = $"i_{SanitizeIdentifier(target.Name)}";
            var elementVar = $"element_{SanitizeIdentifier(target.Name)}";
            if (config.IsFixed)
            {
                sb.AppendLine(
                    $"        if ({targetVarName} == null || {targetVarName}.Length != {config.Count}) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {targetVarName}?.Length, \"Array length must be exactly {config.Count}.\");");
                sb.AppendLine($"        for (var {indexVar} = 0; {indexVar} < {config.Count}; {indexVar}++)");
                sb.AppendLine("        {");
                sb.AppendLine($"        var {elementVar} = {targetVarName}[{indexVar}];");
                GeneratePropertySerialization(context, sb, elementTarget, true, elementVar, skipValidation, useSpan);
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine($"        var {valueVar} = {targetVarName} ?? Array.Empty<{elementTypeName}>();");
                sb.AppendLine(
                    $"        if ({valueVar}.Length > {config.Count}) throw new ArgumentOutOfRangeException(nameof({targetVarName}), {valueVar}.Length, \"Array length must be less than or equal to {config.Count}.\");");
                sb.AppendLine($"        writer.WriteInt({valueVar}.Length, {config.LengthBits});");
                sb.AppendLine($"        for (var {indexVar} = 0; {indexVar} < {valueVar}.Length; {indexVar}++)");
                sb.AppendLine("        {");
                sb.AppendLine($"        var {elementVar} = {valueVar}[{indexVar}];");
                GeneratePropertySerialization(context, sb, elementTarget, true, elementVar, skipValidation, useSpan);
                sb.AppendLine("        }");
            }
        }
        else
        {
            var lengthVar = config.IsFixed ? config.Count.ToString() : $"length_{SanitizeIdentifier(target.Name)}";
            if (!config.IsFixed) sb.AppendLine($"        var {lengthVar} = reader.ReadInt({config.LengthBits});");

            sb.AppendLine($"        {targetVarName} = new {elementTypeName}[{lengthVar}];");
            sb.AppendLine($"        for (var i_{SanitizeIdentifier(target.Name)} = 0; i_{SanitizeIdentifier(target.Name)} < {targetVarName}.Length; i_{SanitizeIdentifier(target.Name)}++)");
            sb.AppendLine("        {");
            GeneratePropertySerialization(context, sb, elementTarget, false,
                $"{targetVarName}[i_{SanitizeIdentifier(target.Name)}]", skipValidation, useSpan);
            sb.AppendLine("        }");
        }
    }
}
