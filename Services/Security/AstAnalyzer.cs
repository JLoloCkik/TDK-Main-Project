using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace Kreta.Services.Security;

/// <summary>
/// Roslyn AST elemző a generált C# kód biztonsági átvizsgálásához.
/// </summary>
public class AstAnalyzer
{
    public bool IsCodeSafe(string sourceCode, out string violation)
    {
        violation = string.Empty;

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            var invocationExpressions = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocationExpressions)
            {
                var methodCall = invocation.ToString();

                if (methodCall.Contains("Process.Start") || 
                    methodCall.Contains("Assembly.Load") || 
                    methodCall.Contains("Environment.Exit"))
                {
                    violation = $"Tiltott metódushívás észlelve: {methodCall}";
                    return false;
                }
            }

            return true;
        }
        catch (System.Exception ex)
        {
            violation = $"AST Elemzési hiba: {ex.Message}";
            return false;
        }
    }
}