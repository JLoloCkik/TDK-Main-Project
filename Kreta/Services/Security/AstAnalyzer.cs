using System.Collections.Generic;
using System.Linq;
using Kreta.Core;

namespace Kreta.Services.Security;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class AstAnalyzer
{
    private static readonly HashSet<string> ForbiddenNamespaces = new()
    {
        "System.IO", "System.Diagnostics", "System.Reflection", "System.Net", "System.Net.Http"
    };

    private static readonly HashSet<string> ForbiddenIdentifiers = new()
    {
        "File", "Directory", "Process", "Assembly", "HttpClient", "Socket", "Registry"
    };

    // Melyik szerepkör milyen Context-interfészekhez férhet hozzá (a PDF RBAC-modellje szerint).
    private static readonly Dictionary<Role, HashSet<string>> AllowedContexts = new()
    {
        [Role.Student] = new() { "IStudentContext" },
        [Role.Teacher] = new() { "IStudentContext", "ITeacherContext" },
        [Role.Director] = new() { "IStudentContext", "ITeacherContext", "IDirectorContext" },
    };

    private static readonly HashSet<string> AllContextNames = new()
    {
        "IStudentContext", "ITeacherContext", "IDirectorContext"
    };

    /// <summary>Egyszerűsített ellenőrzés — csak a tiltott API-kat vizsgálja, RBAC nélkül.</summary>
    public bool IsSafe(string code) => IsSafe(code, Role.Student, out _);

    /// <summary>
    /// Teljes ellenőrzés: tiltott névterek/API-k + a kérő szerepkör Context-jogosultsága.
    /// Ez a tényleges "tűzfal" — korábban a jogosultsági szabály csak a promptban volt leírva,
    /// amit egy AI elméletileg figyelmen kívül hagyhat vagy elronthat.
    /// </summary>
    public bool IsSafe(string code, Role requestingRole, out string reason)
    {
        reason = string.Empty;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();

        foreach (UsingDirectiveSyntax usingDirective in root.Usings)
        {
            string namespaceName = usingDirective.Name?.ToString() ?? string.Empty;

            if (ForbiddenNamespaces.Any(f => namespaceName == f || namespaceName.StartsWith(f + ".")))
            {
                reason = $"Tiltott névtér használata: '{namespaceName}'.";
                return false;
            }
        }

        var allowedForRole = AllowedContexts.TryGetValue(requestingRole, out var set)
            ? set
            : new HashSet<string>();

        var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
        foreach (var id in identifiers)
        {
            string word = id.Identifier.ValueText;

            if (ForbiddenIdentifiers.Contains(word))
            {
                reason = $"Tiltott API-hívás detektálva: '{word}'.";
                return false;
            }

            if (AllContextNames.Contains(word) && !allowedForRole.Contains(word))
            {
                reason = $"Jogosultsági szabálysértés: a(z) '{requestingRole}' szerepkör nem férhet hozzá a(z) '{word}' kontextushoz!";
                return false;
            }
        }

        return true;
    }
}
