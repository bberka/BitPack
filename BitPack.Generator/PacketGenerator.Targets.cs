// Member discovery, accessibility, validation and constructor matching.
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
    private static bool HasIgnoreAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonIgnoreAttribute" ||
            a.AttributeClass?.ToDisplayString() == "System.Runtime.Serialization.IgnoreDataMemberAttribute");
    }

    private static bool HasIncludeAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "System.Runtime.Serialization.DataMemberAttribute");
    }

    private static List<SerializationTarget> GetSerializationTargets(ITypeSymbol typeSymbol)
    {
        var targets = new List<SerializationTarget>();

        foreach (var currentType in GetSerializableTypeHierarchy(typeSymbol))
        foreach (var member in currentType.GetMembers())
        {
            if (member.IsStatic) continue;
            if (!IsAccessibleFromGeneratedType(member, typeSymbol)) continue;

            if (member is IPropertySymbol p)
            {
                var isPublic = p.DeclaredAccessibility == Accessibility.Public;
                var hasInclude = HasIncludeAttribute(p);
                var hasIgnore = HasIgnoreAttribute(p);

                if ((isPublic && !hasIgnore) || (!isPublic && hasInclude))
                    AddOrReplaceTarget(targets, new SerializationTarget(p.Name, p.Type, p, GetBitFieldKey(p)));
            }
            else if (member is IFieldSymbol f && !f.IsImplicitlyDeclared)
            {
                var hasInclude = HasIncludeAttribute(f);
                if (hasInclude) AddOrReplaceTarget(targets, new SerializationTarget(f.Name, f.Type, f, GetBitFieldKey(f)));
            }
        }

        return targets.Any(t => t.FieldKey.HasValue)
            ? targets.OrderBy(t => t.FieldKey.GetValueOrDefault()).ToList()
            : targets;
    }

    private static IEnumerable<INamedTypeSymbol> GetSerializableTypeHierarchy(ITypeSymbol typeSymbol)
    {
        var stack = new Stack<INamedTypeSymbol>();
        var current = typeSymbol as INamedTypeSymbol;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            stack.Push(current);
            current = current.BaseType;
        }

        return stack;
    }

    private static bool IsAccessibleFromGeneratedType(ISymbol member, ITypeSymbol generatedType)
    {
        if (SymbolEqualityComparer.Default.Equals(member.ContainingType, generatedType)) return true;

        return member.DeclaredAccessibility != Accessibility.Private;
    }

    private static void AddOrReplaceTarget(List<SerializationTarget> targets, SerializationTarget target)
    {
        targets.RemoveAll(t => t.Name == target.Name);
        targets.Add(target);
    }

    private static int? GetBitFieldKey(ISymbol symbol)
    {
        var attr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "BitPack.BitFieldKeyAttribute");
        if (attr != null && attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int key)
            return key;

        return null;
    }

    private static void ValidateTargetSafety(SourceProductionContext context, ITypeSymbol typeSymbol,
        List<SerializationTarget> targets)
    {
        var namedType = typeSymbol as INamedTypeSymbol;

        foreach (var target in targets)
            if (target.Symbol is IPropertySymbol p)
            {
                // 1. Check getter accessor
                if (p.GetMethod == null)
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedTypeError,
                        p.Locations.FirstOrDefault(),
                        p.Name,
                        $"Property '{p.Name}' must have a getter accessor to be serialized."));

                // 2. Check setter/init or constructor match
                if (p.SetMethod == null)
                {
                    var hasMatchingCtorParam = false;
                    if (namedType != null)
                        foreach (var ctor in namedType.InstanceConstructors)
                            if (ctor.Parameters.Any(param =>
                                    string.Equals(param.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                hasMatchingCtorParam = true;
                                break;
                            }

                    if (!hasMatchingCtorParam)
                        context.ReportDiagnostic(Diagnostic.Create(
                            UnsupportedTypeError,
                            p.Locations.FirstOrDefault(),
                            p.Name,
                        $"Property '{p.Name}' must have a setter or init accessor, or match a constructor parameter to be deserialized."));
                }
            }

        foreach (var target in targets) ValidateMathCodecSafety(context, target);

        var hasAnyFieldKey = targets.Any(t => t.FieldKey.HasValue);
        if (!hasAnyFieldKey) return;

        foreach (var target in targets)
        {
            if (!target.FieldKey.HasValue)
                context.ReportDiagnostic(Diagnostic.Create(
                    BitFieldKeyError,
                    target.Symbol.Locations.FirstOrDefault(),
                    target.Name,
                    "all serializable members must declare [BitFieldKey] when any member declares one."));
            else if (target.FieldKey.Value < 0)
                context.ReportDiagnostic(Diagnostic.Create(
                    BitFieldKeyError,
                    target.Symbol.Locations.FirstOrDefault(),
                    target.Name,
                    "keys must be zero or greater."));
        }

        foreach (var duplicateGroup in targets
                     .Where(t => t.FieldKey.HasValue)
                     .GroupBy(t => t.FieldKey!.Value)
                     .Where(g => g.Count() > 1))
        {
            foreach (var target in duplicateGroup)
                context.ReportDiagnostic(Diagnostic.Create(
                    BitFieldKeyError,
                    target.Symbol.Locations.FirstOrDefault(),
                    target.Name,
                $"key {duplicateGroup.Key} is already used by another serializable member."));
        }
    }

    private static void ValidateMathCodecSafety(SourceProductionContext context, SerializationTarget target)
    {
        var codecCount = 0;
        if (HasAngleAttribute(target.Symbol)) codecCount++;
        if (HasVectorRangeAttribute(target.Symbol)) codecCount++;
        if (HasQuaternionSmallestThreeAttribute(target.Symbol)) codecCount++;

        if (codecCount == 0) return;
        if (codecCount > 1)
        {
            ReportMathCodecError(context, target, "only one game math codec attribute can be applied to a member.");
            return;
        }

        var effectiveType = target.Type is IArrayTypeSymbol arrayType ? arrayType.ElementType : target.Type;

        if (HasAngleAttribute(target.Symbol) &&
            !TryGetAngleConfig(effectiveType, target.Symbol, out _, out var angleError))
            ReportMathCodecError(context, target, angleError);

        if (HasVectorRangeAttribute(target.Symbol) &&
            !TryGetVectorRangeConfig(effectiveType, target.Symbol, out _, out var vectorError))
            ReportMathCodecError(context, target, vectorError);

        if (HasQuaternionSmallestThreeAttribute(target.Symbol) &&
            !TryGetQuaternionSmallestThreeConfig(effectiveType, target.Symbol, out _, out var quaternionError))
            ReportMathCodecError(context, target, quaternionError);
    }

    private static void ReportMathCodecError(SourceProductionContext context, SerializationTarget target, string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            MathCodecError,
            target.Symbol.Locations.FirstOrDefault(),
            target.Name,
            message));
    }

    private static bool IsSpanCompatibleTarget(SerializationTarget target)
    {
        if (HasDiscriminatorAttribute(target.Symbol)) return true;
        return IsSpanCompatibleType(target.Type, target.Symbol);
    }

    private static bool IsDeltaCompatibleTarget(SerializationTarget target)
    {
        if (HasDiscriminatorAttribute(target.Symbol)) return true;
        return IsDeltaCompatibleType(target.Type);
    }

    private static bool IsDeltaCompatibleType(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface) return false;
        if (type is IArrayTypeSymbol arrayType) return IsDeltaCompatibleType(arrayType.ElementType);

        return true;
    }

    private static bool IsSpanCompatibleType(ITypeSymbol type, ISymbol symbol)
    {
        if (type is IArrayTypeSymbol arrayType)
            return TryGetArrayConfig(symbol, arrayType, out _, out _) &&
                   IsSpanCompatibleType(arrayType.ElementType, symbol);

        if (TryGetAngleConfig(type, symbol, out _, out _) ||
            TryGetVectorRangeConfig(type, symbol, out _, out _) ||
            TryGetQuaternionSmallestThreeConfig(type, symbol, out _, out _))
            return true;

        if (type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_String
            or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal
            or SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_SByte
            or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_Int64)
            return true;

        if (type.ToDisplayString() is "System.DateTime" or "nint" or "System.IntPtr" or "nuint" or "System.UIntPtr")
            return true;

        if (type.TypeKind == TypeKind.Enum) return true;

        return false;
    }

    private static IMethodSymbol? FindMatchingConstructor(INamedTypeSymbol? typeSymbol,
        List<SerializationTarget> targets, out List<SerializationTarget> ctorArgs)
    {
        ctorArgs = new List<SerializationTarget>();
        if (typeSymbol == null) return null;
        var ctors = typeSymbol.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public ||
                        c.DeclaredAccessibility == Accessibility.Internal)
            .OrderByDescending(c => c.Parameters.Length);

        foreach (var ctor in ctors)
        {
            if (ctor.Parameters.Length == 0) continue;

            var matchedArgs = new List<SerializationTarget>();
            var allMatched = true;

            foreach (var param in ctor.Parameters)
            {
                var target = targets.FirstOrDefault(t =>
                    string.Equals(t.Name, param.Name, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    matchedArgs.Add(target);
                }
                else
                {
                    allMatched = false;
                    break;
                }
            }

            if (allMatched)
            {
                ctorArgs = matchedArgs;
                return ctor;
            }
        }

        return null;
    }

    private static bool IsWritableProperty(ISymbol symbol)
    {
        if (symbol is IFieldSymbol) return true;
        if (symbol is IPropertySymbol p) return p.SetMethod != null && !p.SetMethod.IsInitOnly;

        return false;
    }

    private static bool IsWritablePropertyForInitializer(ISymbol symbol)
    {
        if (symbol is IFieldSymbol) return true;
        if (symbol is IPropertySymbol p) return p.SetMethod != null;

        return false;
    }
}
