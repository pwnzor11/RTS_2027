// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Rts.Sim.Tests.Determinism;

/// <summary>
/// Roslyn-based static analyzer that enforces the "Sim rules" of docs/COMMON.md on source text.
/// Rules: DET001 floating point, DET002 clocks and System.Random, DET003 threading,
/// DET004 iteration of unordered collections, DET005 per-process hashes and GUIDs.
/// Pseudo-rules: DET000 (det-allow comment without a reason), DET900 (source does not compile,
/// so the semantic checks could not be trusted).
/// Suppress a rule on one line with a trailing comment: <c>// det-allow: DET004 reason text</c>.
/// </summary>
public static class DeterminismRules
{
    /// <summary>One rule hit. Line is 1-based; Text is the trimmed source line.</summary>
    public sealed record Violation(string Path, int Line, string Rule, string Text);

    private static readonly Regex AllowPattern =
        new(@"^//\s*det-allow:\s*(DET\d{3})\b\s*(.*)$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> BannedTypes = new()
    {
        ["System.Single"] = "DET001",
        ["System.Double"] = "DET001",
        ["System.Decimal"] = "DET001",
        ["System.Random"] = "DET002",
        ["System.DateTime"] = "DET002",
        ["System.DateTimeOffset"] = "DET002",
        ["System.Diagnostics.Stopwatch"] = "DET002",
        ["System.TimeProvider"] = "DET002",
        ["System.Threading.Tasks.Parallel"] = "DET003",
        ["System.Threading.Tasks.Task"] = "DET003",
        ["System.Threading.Tasks.ValueTask"] = "DET003",
        ["System.Threading.Thread"] = "DET003",
        ["System.Threading.ThreadPool"] = "DET003",
        ["System.Threading.Lock"] = "DET003",
    };

    // Unordered collections. Ordered ones (SortedDictionary, SortedSet, ImmutableSorted*) are fine.
    private static readonly HashSet<string> UnorderedCollections = new()
    {
        "System.Collections.Generic.Dictionary",
        "System.Collections.Generic.HashSet",
        "System.Collections.Generic.IDictionary",
        "System.Collections.Generic.ISet",
        "System.Collections.Generic.IReadOnlyDictionary",
        "System.Collections.Generic.IReadOnlySet",
        "System.Collections.IDictionary",
        "System.Collections.Concurrent.ConcurrentDictionary",
        "System.Collections.Immutable.ImmutableDictionary",
        "System.Collections.Immutable.ImmutableHashSet",
        "System.Collections.Immutable.IImmutableDictionary",
        "System.Collections.Immutable.IImmutableSet",
    };

    // What ImplicitUsings provides to the real projects.
    private const string ImplicitUsings = """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.IO;
        global using global::System.Linq;
        global using global::System.Net.Http;
        global using global::System.Threading;
        global using global::System.Threading.Tasks;
        """;

    private static readonly Lazy<IReadOnlyList<MetadataReference>> DefaultRefs = new(() =>
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
        foreach (var p in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) paths.Add(p);
        paths.Add(typeof(System.IO.Hashing.XxHash64).Assembly.Location);
        return paths.Where(File.Exists).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    });

    /// <summary>Metadata references for the running runtime plus the System.IO.Hashing assembly (cached).</summary>
    public static IReadOnlyList<MetadataReference> DefaultReferences() => DefaultRefs.Value;

    /// <summary>Analyzes the given sources as a single compilation and returns every violation.</summary>
    public static IReadOnlyList<Violation> Analyze(
        IEnumerable<(string Path, string Source)> files, IEnumerable<MetadataReference> refs)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = files
            .Select(f => CSharpSyntaxTree.ParseText(SourceText.From(f.Source), parse, f.Path))
            .ToList();
        var usings = CSharpSyntaxTree.ParseText(ImplicitUsings, parse, "<implicit-usings>");

        var compilation = CSharpCompilation.Create(
            "DeterminismScan",
            trees.Append(usings),
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithAllowUnsafe(true));

        var found = new List<Violation>();

        foreach (var d in compilation.GetDiagnostics())
        {
            if (d.Severity != DiagnosticSeverity.Error || d.Location.SourceTree is not { } t || t == usings) continue;
            var line = d.Location.GetLineSpan().StartLinePosition.Line + 1;
            found.Add(new Violation(t.FilePath, line, "DET900", $"{d.Id}: {d.GetMessage()}"));
        }

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var text = tree.GetText();
            var allowed = new HashSet<(int Line, string Rule)>();

            foreach (var trivia in root.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)))
            {
                var m = AllowPattern.Match(trivia.ToString().TrimEnd());
                if (!m.Success) continue;
                var line = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                if (m.Groups[2].Value.Trim().Length == 0)
                    found.Add(new Violation(tree.FilePath, line, "DET000",
                        "det-allow comment needs a reason: " + trivia.ToString().Trim()));
                else
                    allowed.Add((line, m.Groups[1].Value));
            }

            var hits = new List<(int Line, string Rule)>();
            void Hit(SyntaxNode n, string rule) =>
                hits.Add((n.GetLocation().GetLineSpan().StartLinePosition.Line + 1, rule));

            foreach (var node in root.DescendantNodes())
            {
                // Type-based rules (DET001-003): any expression or type syntax whose type mentions a banned type.
                if (node is ExpressionSyntax expr)
                {
                    foreach (var ty in Flatten(model.GetTypeInfo(expr).Type))
                        if (ty is INamedTypeSymbol nt && Classify(nt) is { } rule)
                            Hit(expr, rule);
                }

                switch (node)
                {
                    case LockStatementSyntax:
                        Hit(node, "DET003");
                        break;
                    case MethodDeclarationSyntax md when md.Modifiers.Any(SyntaxKind.AsyncKeyword):
                    case LocalFunctionStatementSyntax lf when lf.Modifiers.Any(SyntaxKind.AsyncKeyword):
                    case AnonymousFunctionExpressionSyntax af when af.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword):
                        Hit(node, "DET003");
                        break;

                    case SimpleNameSyntax name:
                        CheckName(name, model, Hit);
                        break;

                    case ForEachStatementSyntax fe:
                        if (IsUnordered(model.GetTypeInfo(fe.Expression).Type)) Hit(fe, "DET004");
                        break;
                    case ForEachVariableStatementSyntax fev:
                        if (IsUnordered(model.GetTypeInfo(fev.Expression).Type)) Hit(fev, "DET004");
                        break;
                    case FromClauseSyntax fc:
                        if (IsUnordered(model.GetTypeInfo(fc.Expression).Type)) Hit(fc, "DET004");
                        break;
                    case JoinClauseSyntax jc:
                        if (IsUnordered(model.GetTypeInfo(jc.InExpression).Type)) Hit(jc, "DET004");
                        break;
                    case InvocationExpressionSyntax inv:
                        CheckInvocation(inv, model, Hit);
                        break;
                }
            }

            foreach (var (line, rule) in hits.Distinct())
            {
                if (allowed.Contains((line, rule))) continue;
                found.Add(new Violation(tree.FilePath, line, rule, text.Lines[line - 1].ToString().Trim()));
            }
        }

        return found
            .DistinctBy(v => (v.Path, v.Line, v.Rule))
            .OrderBy(v => v.Path, StringComparer.Ordinal).ThenBy(v => v.Line).ThenBy(v => v.Rule, StringComparer.Ordinal)
            .ToList();
    }

    private static void CheckName(SimpleNameSyntax name, SemanticModel model, Action<SyntaxNode, string> hit)
    {
        var info = model.GetSymbolInfo(name);
        var sym = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        switch (sym)
        {
            case IPropertySymbol { Name: "TickCount" or "TickCount64" } p when IsType(p.ContainingType, "System", "Environment"):
                hit(name, "DET002");
                break;
            case IMethodSymbol { Name: "GetHashCode" } m
                when m.ContainingType.SpecialType is SpecialType.System_String or SpecialType.System_Object:
                hit(name, "DET005");
                break;
            case IMethodSymbol m when (m.Name == "NewGuid" || m.Name == "CreateVersion7")
                                      && IsType(m.ContainingType, "System", "Guid"):
                hit(name, "DET005");
                break;
        }
    }

    private static void CheckInvocation(InvocationExpressionSyntax inv, SemanticModel model, Action<SyntaxNode, string> hit)
    {
        if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol method) return;
        var inLinq = method.ContainingType?.ContainingNamespace?.ToDisplayString() == "System.Linq";

        if (inv.Expression is MemberAccessExpressionSyntax access)
        {
            var receiver = model.GetTypeInfo(access.Expression).Type;
            if (method.Name == "GetEnumerator" && IsUnordered(receiver)) hit(inv, "DET004");
            if (inLinq && method.IsExtensionMethod && IsUnordered(receiver)) hit(inv, "DET004");
        }

        // Static-call form: Enumerable.Select(dict, ...)
        if (inLinq && method.IsExtensionMethod && method.ReducedFrom is null
            && inv.ArgumentList.Arguments.Count > 0
            && IsUnordered(model.GetTypeInfo(inv.ArgumentList.Arguments[0].Expression).Type))
            hit(inv, "DET004");
    }

    private static string? Classify(INamedTypeSymbol t)
    {
        if (t.ContainingType is not null) return null;
        var key = (t.ContainingNamespace?.ToDisplayString() ?? "") + "." + t.Name;
        return BannedTypes.TryGetValue(key, out var rule) ? rule : null;
    }

    private static bool IsType(INamedTypeSymbol? t, string ns, string name) =>
        t is { ContainingType: null } && t.Name == name && t.ContainingNamespace?.ToDisplayString() == ns;

    private static IEnumerable<ITypeSymbol> Flatten(ITypeSymbol? t, int depth = 0)
    {
        if (t is null || depth > 8) yield break;
        yield return t;
        switch (t)
        {
            case IArrayTypeSymbol a:
                foreach (var x in Flatten(a.ElementType, depth + 1)) yield return x;
                break;
            case IPointerTypeSymbol p:
                foreach (var x in Flatten(p.PointedAtType, depth + 1)) yield return x;
                break;
            case INamedTypeSymbol n:
                foreach (var arg in n.TypeArguments)
                    foreach (var x in Flatten(arg, depth + 1)) yield return x;
                break;
        }
    }

    // True for the unordered collection types, their derived classes, and their nested types (Dictionary<,>.KeyCollection).
    private static bool IsUnordered(ITypeSymbol? t)
    {
        for (var cur = t; cur is not null; cur = cur.BaseType)
            for (var n = cur as INamedTypeSymbol; n is not null; n = n.ContainingType)
            {
                var key = (n.ContainingNamespace?.ToDisplayString() ?? "") + "." + n.Name;
                if (UnorderedCollections.Contains(key)) return true;
            }
        return false;
    }
}
