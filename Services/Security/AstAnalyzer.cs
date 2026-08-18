using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kreta.Services.Security;

public class AstAnalyzer
{
    // ONLY THESE NAMESPACES AND TYPES ARE ALLOWED IN THE GENERATED CODE
    private static readonly HashSet<string> AllowedNamespaces = new(StringComparer.Ordinal)
    {
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text",
        "System.Globalization",
        "Avalonia",
        "Avalonia.Controls",
        "Avalonia.Controls.Primitives",
        "Avalonia.Controls.Templates",
        "Avalonia.Layout",
        "Avalonia.Media",
        "Avalonia.Interactivity",
        "Kreta.Core",
        "Kreta.Contexts",
        "Kreta.Dynamic"
    };

    // EXPLICITLY BANNED METHODS AND TYPES (EVEN IF ATTEMPTED VIA REFLECTION OR TRICKS)
    private static readonly HashSet<string> BannedTypesAndMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Process", "Assembly", "MethodInfo", "MemberInfo", "FieldInfo", "PropertyInfo",
        "Type", "Activator", "Environment", "File", "Directory", "Path", "Stream",
        "StreamReader", "StreamWriter", "HttpClient", "WebClient", "Socket", "GC",
        "Marshal", "Unsafe", "Task", "Thread", "ThreadPool"
    };

    public bool IsCodeSafe(string sourceCode, out string violationMessage)
    {
        violationMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            violationMessage = "A kapott forráskód üres.";
            return false;
        }

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            if (root.DescendantNodes().OfType<UnsafeStatementSyntax>().Any() ||
                root.DescendantTokens().Any(t => t.IsKind(SyntaxKind.UnsafeKeyword)))
            {
                violationMessage = "Biztonsági hiba: 'unsafe' kódblokk használata szigorúan tiltott!";
                return false;
            }

            var usingDirectives = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
            foreach (var usingDir in usingDirectives)
            {
                var ns = usingDir.Name?.ToString() ?? string.Empty;
                if (!IsNamespaceAllowed(ns))
                {
                    violationMessage = $"Biztonsági hiba: Nem engedélyezett névtér használata: 'using {ns};'";
                    return false;
                }
            }

            var qualifiedNames = root.DescendantNodes().OfType<QualifiedNameSyntax>();
            foreach (var qn in qualifiedNames)
            {
                var fullName = qn.ToString();
                if (IsBannedName(fullName))
                {
                    violationMessage = $"Biztonsági hiba: Tiltott típus/névtér hivatkozás észlelve: '{fullName}'";
                    return false;
                }
            }

            var typeOfExpressions = root.DescendantNodes().OfType<TypeOfExpressionSyntax>();
            if (typeOfExpressions.Any())
            {
                violationMessage = "Biztonsági hiba: 'typeof()' reflection használata tiltott!";
                return false;
            }

            var identifierNames = root.DescendantNodes().OfType<IdentifierNameSyntax>();
            foreach (var id in identifierNames)
            {
                if (id.Identifier.Text == "dynamic")
                {
                    violationMessage = "Biztonsági hiba: 'dynamic' típus használata tiltott!";
                    return false;
                }

                if (BannedTypesAndMethods.Contains(id.Identifier.Text))
                {
                    violationMessage =
                        $"Biztonsági hiba: Tiltott típus/osztály használata észlelve: '{id.Identifier.Text}'";
                    return false;
                }
            }

            var invocationExpressions = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocationExpressions)
            {
                var callText = invocation.Expression.ToString();

                // If the invoked element includes GetType, Invoke, Start, etc.
                if (callText.EndsWith(".GetType") ||
                    callText.EndsWith(".Invoke") ||
                    callText.Contains("GetMethod") ||
                    callText.Contains("GetProperty") ||
                    callText.Contains("GetField"))
                {
                    violationMessage = $"Biztonsági hiba: Reflection vagy dinamikus hívás észlelve: '{callText}'";
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            violationMessage = $"A kód elemzése során szintaktikai/AST hiba történt: {ex.Message}";
            return false;
        }
    }

    private bool IsNamespaceAllowed(string ns)
    {
        if (AllowedNamespaces.Contains(ns))
            return true;


        if (ns.StartsWith("Avalonia.", StringComparison.Ordinal) &&
            (ns.StartsWith("Avalonia.Controls", StringComparison.Ordinal) ||
             ns.StartsWith("Avalonia.Layout", StringComparison.Ordinal) ||
             ns.StartsWith("Avalonia.Media", StringComparison.Ordinal)))
            return true;

        return false;
    }

    private bool IsBannedName(string fullName)
    {
        foreach (var banned in BannedTypesAndMethods)
            if (fullName.Contains(banned, StringComparison.OrdinalIgnoreCase) ||
                fullName.StartsWith("System.Diagnostics", StringComparison.OrdinalIgnoreCase) ||
                fullName.StartsWith("System.Reflection", StringComparison.OrdinalIgnoreCase) ||
                fullName.StartsWith("System.IO", StringComparison.OrdinalIgnoreCase) ||
                fullName.StartsWith("System.Net", StringComparison.OrdinalIgnoreCase) ||
                fullName.StartsWith("System.Runtime", StringComparison.OrdinalIgnoreCase) ||
                fullName.StartsWith("Microsoft.Win32", StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}