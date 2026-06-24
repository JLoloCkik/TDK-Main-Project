using System.Collections.Generic;
using System.Linq;

namespace Kreta.Services.Security;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class AstAnalyzer {
    private static readonly List<string> ForbiddenNamespaces =
        new() { "System.IO", "System.Diagnostics", "System.Reflection" };

    public bool IsSafe(string code) {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();

        foreach (UsingDirectiveSyntax usingDirective in root.Usings) {
            string namespaceName = usingDirective.Name?.ToString() ?? string.Empty;

            if (ForbiddenNamespaces.Contains(namespaceName)) {
                return false;
            }
        }
        
        var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
        foreach (var id in identifiers) {
            string word = id.Identifier.ValueText;
            if (word == "File" || word == "Directory" || word == "Process")
            {
                return false; 
            }
        }
        
        return true;
    }
}