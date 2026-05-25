using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace OneShot.Generator;

#pragma warning disable RS1024 // Symbols should be compared for equality

// ===== Models =====
//
// Note on ImmutableArray equality: ImmutableArray<T>.Equals uses reference equality,
// not structural. The incremental generator pipeline caches and dedupes results by
// value, so any model carrying ImmutableArray<> overrides Equals/GetHashCode to use
// SequenceEqual. Pure value models (no collections) keep the auto-generated record
// equality.

internal readonly record struct ParameterModel(
    string TypeFqn,
    string Name,
    string? LabelFqn,
    bool HasDefault,
    string? DefaultExpr);

internal readonly record struct InjectFieldModel(
    string Name,
    string TypeFqn,
    string? LabelFqn,
    bool NeedsUnsafeAccessor);

internal readonly record struct InjectPropertyModel(
    string Name,
    string TypeFqn,
    string? LabelFqn,
    bool NeedsUnsafeAccessor);

internal readonly record struct ConstructorModel(ImmutableArray<ParameterModel> Parameters)
{
    public bool Equals(ConstructorModel other) => Parameters.SequenceEqual(other.Parameters);
    public override int GetHashCode() => Parameters.Length;
}

internal readonly record struct InjectMethodModel(
    string Name,
    string ReturnTypeFqn,
    ImmutableArray<ParameterModel> Parameters,
    bool NeedsUnsafeAccessor)
{
    public bool Equals(InjectMethodModel other) =>
        Name == other.Name && ReturnTypeFqn == other.ReturnTypeFqn &&
        NeedsUnsafeAccessor == other.NeedsUnsafeAccessor &&
        Parameters.SequenceEqual(other.Parameters);

    public override int GetHashCode() => Name.GetHashCode();
}

internal sealed record TypeModel(
    string FullyQualifiedName,
    string SafeName,
    bool IsValueType,
    ConstructorModel? Constructor,
    ImmutableArray<InjectFieldModel> Fields,
    ImmutableArray<InjectPropertyModel> Properties,
    ImmutableArray<InjectMethodModel> Methods,
    ImmutableArray<string> Interfaces,
    ImmutableArray<string> BaseTypes)
{
    public bool Equals(TypeModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return FullyQualifiedName == other.FullyQualifiedName
            && SafeName == other.SafeName
            && IsValueType == other.IsValueType
            && Nullable.Equals(Constructor, other.Constructor)
            && Fields.SequenceEqual(other.Fields)
            && Properties.SequenceEqual(other.Properties)
            && Methods.SequenceEqual(other.Methods)
            && Interfaces.SequenceEqual(other.Interfaces)
            && BaseTypes.SequenceEqual(other.BaseTypes);
    }

    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
}

// ===== Extraction =====

internal static class TypeModelExtractor
{
    private static readonly SymbolDisplayFormat s_fqn = SymbolDisplayFormat.FullyQualifiedFormat;

    public static TypeModel? Extract(INamedTypeSymbol type, Compilation compilation,
        bool requireAttributeTrigger = true)
    {
        if (!IsEffectivelyAccessible(type)) return null;
        if (type.IsAbstract || type.TypeKind == TypeKind.Interface) return null;
        if (type.TypeParameters.Length > 0) return null;

        var injectAttr = compilation.GetTypeByMetadataName("OneShot.InjectAttribute");
        if (injectAttr is null) return null;

        if (requireAttributeTrigger)
        {
            bool hasInject = HasAnyInjectAttribute(type, injectAttr);
            if (!hasInject) return null;
        }

        var constructor = ExtractConstructor(type, injectAttr);
        var fields = ExtractFields(type, injectAttr);
        var properties = ExtractProperties(type, injectAttr);
        var methods = ExtractMethods(type, injectAttr);

        var interfaces = type.AllInterfaces
            .Select(i => i.ToDisplayString(s_fqn))
            .ToImmutableArray();

        var baseTypesList = ImmutableArray.CreateBuilder<string>();
        var bt = type.BaseType;
        while (bt != null && bt.SpecialType != SpecialType.System_Object &&
               bt.SpecialType != SpecialType.System_ValueType)
        {
            baseTypesList.Add(bt.ToDisplayString(s_fqn));
            bt = bt.BaseType;
        }

        var fqn = type.ToDisplayString(s_fqn);
        return new TypeModel(fqn, MakeSafeName(fqn), type.IsValueType,
            constructor, fields, properties, methods,
            interfaces, baseTypesList.ToImmutable());
    }

    private static bool IsEffectivelyAccessible(INamedTypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            if (current.DeclaredAccessibility is Accessibility.Private
                or Accessibility.Protected or Accessibility.ProtectedAndInternal)
                return false;
            current = current.ContainingType;
        }
        return true;
    }

    private static bool HasAnyInjectAttribute(INamedTypeSymbol type, INamedTypeSymbol injectAttr)
    {
        foreach (var member in type.GetMembers())
        {
            if (HasAttr(member, injectAttr)) return true;
            if (member is IMethodSymbol method)
                foreach (var p in method.Parameters)
                    if (HasAttr(p, injectAttr)) return true;
        }
        return false;
    }

    private static bool HasAttr(ISymbol symbol, INamedTypeSymbol attrType)
    {
        return symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrType));
    }

    private static ConstructorModel? ExtractConstructor(INamedTypeSymbol type, INamedTypeSymbol injectAttr)
    {
        var ctors = type.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared).ToArray();
        if (ctors.Length == 0)
            return new ConstructorModel(ImmutableArray<ParameterModel>.Empty);

        IMethodSymbol? chosen;
        if (ctors.Length == 1)
            chosen = ctors[0];
        else
            chosen = ctors.FirstOrDefault(c => HasAttr(c, injectAttr));

        if (chosen is null)
            return null; // multiple ctors, none marked

        return new ConstructorModel(chosen.Parameters.Select(p => ExtractParam(p, injectAttr)).ToImmutableArray());
    }

    private static ParameterModel ExtractParam(IParameterSymbol param, INamedTypeSymbol injectAttr)
    {
        return new ParameterModel(
            param.Type.ToDisplayString(s_fqn),
            param.Name,
            GetLabelFqn(param, injectAttr),
            param.HasExplicitDefaultValue,
            param.HasExplicitDefaultValue ? FormatDefault(param) : null);
    }

    private static string? GetLabelFqn(ISymbol symbol, INamedTypeSymbol injectAttr)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, injectAttr)) continue;
            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol labelType)
                return labelType.ToDisplayString(s_fqn);
            return null;
        }
        return null;
    }

    private static ImmutableArray<InjectFieldModel> ExtractFields(INamedTypeSymbol type, INamedTypeSymbol injectAttr)
    {
        var result = ImmutableArray.CreateBuilder<InjectFieldModel>();
        var current = type;
        bool isDeclaring = true;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers().OfType<IFieldSymbol>())
            {
                if (!HasAttr(member, injectAttr)) continue;
                if (!isDeclaring && !IsAccessible(member)) continue;

                bool needsUnsafe = !IsAccessible(member) || member.IsImplicitlyDeclared;
                result.Add(new InjectFieldModel(
                    member.Name, member.Type.ToDisplayString(s_fqn),
                    GetLabelFqn(member, injectAttr), needsUnsafe));
            }
            isDeclaring = false;
            current = current.BaseType;
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<InjectPropertyModel> ExtractProperties(INamedTypeSymbol type, INamedTypeSymbol injectAttr)
    {
        var result = ImmutableArray.CreateBuilder<InjectPropertyModel>();
        var current = type;
        bool isDeclaring = true;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!HasAttr(member, injectAttr)) continue;
                if (member.SetMethod is null) continue; // read-only
                if (!isDeclaring && !IsAccessible(member)) continue;

                bool needsUnsafe = member.SetMethod != null && !IsAccessible(member.SetMethod);
                result.Add(new InjectPropertyModel(
                    member.Name, member.Type.ToDisplayString(s_fqn),
                    GetLabelFqn(member, injectAttr), needsUnsafe));
            }
            isDeclaring = false;
            current = current.BaseType;
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<InjectMethodModel> ExtractMethods(INamedTypeSymbol type, INamedTypeSymbol injectAttr)
    {
        var result = ImmutableArray.CreateBuilder<InjectMethodModel>();
        var current = type;
        bool isDeclaring = true;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.MethodKind != MethodKind.Ordinary) continue;
                if (!HasAttr(member, injectAttr)) continue;
                if (!isDeclaring && !IsAccessible(member)) continue;

                var returnFqn = member.ReturnsVoid ? "void" : member.ReturnType.ToDisplayString(s_fqn);
                result.Add(new InjectMethodModel(
                    member.Name, returnFqn,
                    member.Parameters.Select(p => ExtractParam(p, injectAttr)).ToImmutableArray(),
                    !IsAccessible(member)));
            }
            isDeclaring = false;
            current = current.BaseType;
        }
        return result.ToImmutable();
    }

    private static bool IsAccessible(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility is Accessibility.Public
            or Accessibility.Internal or Accessibility.ProtectedOrInternal;
    }

    private static string FormatDefault(IParameterSymbol param)
    {
        var value = param.ExplicitDefaultValue;
        if (value is null)
            return param.Type.IsValueType
                ? string.Format(CultureInfo.InvariantCulture, "default({0})", param.Type.ToDisplayString(s_fqn))
                : "null";

        var literal = value switch
        {
            bool b => b ? "true" : "false",
            char c => string.Format(CultureInfo.InvariantCulture, "'{0}'", EscapeChar(c)),
            string s => string.Format(CultureInfo.InvariantCulture, "\"{0}\"", EscapeString(s)),
            float f => FormatFloat(f),
            double d => FormatDouble(d),
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "M",
            long l => l.ToString(CultureInfo.InvariantCulture) + "L",
            ulong ul => ul.ToString(CultureInfo.InvariantCulture) + "UL",
            int i => i.ToString(CultureInfo.InvariantCulture),
            uint ui => ui.ToString(CultureInfo.InvariantCulture) + "U",
            short s => s.ToString(CultureInfo.InvariantCulture),
            ushort us => us.ToString(CultureInfo.InvariantCulture),
            byte b => b.ToString(CultureInfo.InvariantCulture),
            sbyte sb => sb.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "default"
        };

        // Enum defaults: cast the underlying integral value to the enum type
        if (param.Type.TypeKind == TypeKind.Enum)
            return string.Format(CultureInfo.InvariantCulture, "({0})({1})", param.Type.ToDisplayString(s_fqn), literal);

        return literal;
    }

    private static string FormatFloat(float f)
    {
        if (float.IsPositiveInfinity(f)) return "float.PositiveInfinity";
        if (float.IsNegativeInfinity(f)) return "float.NegativeInfinity";
        if (float.IsNaN(f)) return "float.NaN";
        return f.ToString("R", CultureInfo.InvariantCulture) + "F";
    }

    private static string FormatDouble(double d)
    {
        if (double.IsPositiveInfinity(d)) return "double.PositiveInfinity";
        if (double.IsNegativeInfinity(d)) return "double.NegativeInfinity";
        if (double.IsNaN(d)) return "double.NaN";
        return d.ToString("R", CultureInfo.InvariantCulture) + "D";
    }

    private static string EscapeChar(char c) => c switch
    {
        '\'' => "\\'",
        '\\' => "\\\\",
        '\0' => "\\0",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        _ => c.ToString()
    };

    private static string EscapeString(string s) => s
        .Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    internal static string MakeSafeName(string fqn)
    {
        var result = fqn;
        if (result.StartsWith("global::", System.StringComparison.Ordinal))
            result = result.Substring("global::".Length);
        var sb = new StringBuilder(result.Length);
        foreach (var c in result)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return sb.ToString();
    }
}
