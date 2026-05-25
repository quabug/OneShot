using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OneShot.Generator;

[Generator]
public sealed class OneShotGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Trigger 1: zero-arg Register<T>() / Instantiate<T>() call sites on Container.
        // requireAttributeTrigger:false because the call site itself is the opt-in.
        var callSiteTypes = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => IsCallSiteCandidate(node),
            transform: static (ctx, ct) =>
            {
                var invocation = (InvocationExpressionSyntax)ctx.Node;
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return null;
                if (memberAccess.Name is not GenericNameSyntax) return null;

                var symbolInfo = ctx.SemanticModel.GetSymbolInfo(invocation, ct);
                if (symbolInfo.Symbol is not IMethodSymbol method) return null;
                if (method.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    != "global::OneShot.Container") return null;
                if (method.Parameters.Length != 0) return null;

                var typeArg = method.TypeArguments[0] as INamedTypeSymbol;
                if (typeArg is null) return null;

                return TypeModelExtractor.Extract(typeArg, ctx.SemanticModel.Compilation,
                    requireAttributeTrigger: false);
            }
        ).Where(static m => m is not null);

        // Trigger 2: types carrying [Inject] on any member or parameter.
        var injectMemberTypes = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => IsCandidateType(node),
            transform: static (ctx, ct) =>
            {
                if (ctx.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)ctx.Node, ct)
                    is not INamedTypeSymbol symbol) return null;
                return TypeModelExtractor.Extract(symbol, ctx.SemanticModel.Compilation);
            }
        ).Where(static m => m is not null);

        // Deduplicate by fully-qualified name: a type discovered through both
        // triggers (e.g. registered AND carrying [Inject]) emits one file.
        var combined = callSiteTypes.Collect()
            .Combine(injectMemberTypes.Collect())
            .SelectMany(static (pair, _) =>
            {
                var dict = new Dictionary<string, TypeModel>();
                foreach (var m in pair.Left)
                    if (m is not null) dict[m.FullyQualifiedName] = m;
                foreach (var m in pair.Right)
                    if (m is not null) dict[m.FullyQualifiedName] = m;
                return dict.Values.ToImmutableArray();
            });

        context.RegisterSourceOutput(combined, static (spc, model) =>
        {
            spc.AddSource(model.SafeName + ".g.cs", Emitter.Emit(model));
        });
    }

    private static bool IsCallSiteCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.ArgumentList.Arguments.Count != 0) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;
        if (memberAccess.Name is not GenericNameSyntax genericName) return false;
        if (genericName.TypeArgumentList.Arguments.Count != 1) return false;
        var name = genericName.Identifier.ValueText;
        return name is "Register" or "Instantiate";
    }

    private static bool IsCandidateType(SyntaxNode node)
    {
        if (node is not TypeDeclarationSyntax typeDecl) return false;

        foreach (var member in typeDecl.Members)
        {
            if (member is TypeDeclarationSyntax) continue;

            // Member attributes include `[field: Inject]` on auto-properties, so
            // scanning the member's own attribute lists is sufficient — no need
            // to walk the property's backing field separately.
            if (AnyInjectAttribute(member.AttributeLists)) return true;

            if (member is BaseMethodDeclarationSyntax method)
            {
                foreach (var param in method.ParameterList.Parameters)
                    if (AnyInjectAttribute(param.AttributeLists)) return true;
            }
        }

        return false;
    }

    private static bool AnyInjectAttribute(SyntaxList<AttributeListSyntax> lists)
    {
        foreach (var list in lists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = GetSimpleName(attr.Name);
                if (name is "Inject" or "InjectAttribute") return true;
            }
        }
        return false;
    }

    private static string? GetSimpleName(NameSyntax name)
    {
        return name switch
        {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => null
        };
    }
}
